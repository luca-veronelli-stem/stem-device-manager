using System.Diagnostics;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Protocol.Hardware;
using Services.Boot;
using Services.Cache;
using Tests.Unit.Infrastructure.Protocol;

namespace Tests.Integration.Boot;

/// <summary>
/// Spec-001 US1 integration tests. Drive the real service graph
/// (<see cref="ConnectionManager"/> + <see cref="CanPort"/>/<see cref="BlePort"/>/<see cref="SerialPort"/>)
/// against the fake drivers that already cover <see cref="Infrastructure.Protocol.Hardware"/>,
/// to lock in the architectural property the T006 bench run surfaced: across
/// repeated close/relaunch cycles the dispose graph must complete without
/// throwing <see cref="ObjectDisposedException"/>. Hardware-specific behaviour
/// (Plugin.BLE <c>IDevice</c> / <c>ICharacteristic</c> lifecycle) is verified
/// separately on the reference bench per <c>quickstart.md</c> SC-001.
/// </summary>
public class SparkBleStabilizationTests
{
    [Fact]
    public async Task Us1_CloseRelaunch_NoDisposed_Errors()
    {
        var captured = new List<ObjectDisposedException>();
        for (int i = 0; i < 10; i++)
        {
            try { await RunCloseRelaunchCycle(); }
            catch (ObjectDisposedException ode) { captured.Add(ode); }
        }
        Assert.Empty(captured);
    }

    private static async Task RunCloseRelaunchCycle()
    {
        var pcan = new FakePcanDriver();
        var ble = new FakeBleDriver();
        var serial = new FakeSerialDriver();

        using var canPort = new CanPort(pcan);
        using var blePort = new BlePort(ble);
        using var serialPort = new SerialPort(serial);
        using var manager = new ConnectionManager(
            [canPort, blePort, serialPort],
            new NoopDecoder(),
            DeviceVariantConfig.Create(DeviceVariant.Egicon));

        // Session 1: connect, exchange a packet, disconnect.
        ble.IsConnected = true;
        await manager.SwitchToAsync(ChannelKind.Ble);
        ble.RaiseConnectionStatusChanged(true);
        ble.RaisePacketReceived([0x01, 0x02, 0x03], DateTime.UtcNow);
        await manager.DisconnectAsync();
        ble.RaiseConnectionStatusChanged(false);
        ble.IsConnected = false;

        // Re-switch to the same channel — the cycle-2 bench scenario where
        // the user clicked the Bluetooth LE menu item a second time and
        // triggered a swallowed ObjectDisposedException in Plugin.BLE.
        ble.IsConnected = true;
        await manager.SwitchToAsync(ChannelKind.Ble);
        ble.RaiseConnectionStatusChanged(true);
        await manager.DisconnectAsync();
    }

    /// <summary>
    /// Spec-001 US2 / SC-002 / FR-002 / C5: when the BLE stack signals a
    /// physical power-off (DeviceDisconnected surfaced via
    /// <see cref="BlePort.StateChanged"/>), the <see cref="ConnectionManager"/>
    /// must transition to <see cref="ConnectionState.Disconnected"/> within
    /// ≤ 5 seconds. T010 wires this path synchronously on the port's raise,
    /// so the measured latency is effectively zero — the test codifies the
    /// 5 s bound.
    /// </summary>
    [Fact]
    public async Task Us2_PhysicalPowerOff_UiTransitionsWithin_5s()
    {
        const int LatencyBudgetMs = 5_000;

        var pcan = new FakePcanDriver();
        var ble = new FakeBleDriver();
        var serial = new FakeSerialDriver();

        using var canPort = new CanPort(pcan);
        using var blePort = new BlePort(ble);
        using var serialPort = new SerialPort(serial);
        using var manager = new ConnectionManager(
            [canPort, blePort, serialPort],
            new NoopDecoder(),
            DeviceVariantConfig.Create(DeviceVariant.Egicon));

        // Establish Connected via SwitchToAsync, mirroring the US1 harness.
        ble.IsConnected = true;
        await manager.SwitchToAsync(ChannelKind.Ble);
        ble.RaiseConnectionStatusChanged(true);
        Assert.Equal(ConnectionState.Connected, manager.State);

        // Wait for the Disconnected transition with a TaskCompletionSource
        // keyed on the BLE-channel snapshot. The handler fires synchronously
        // inside the driver's event raise, so the measurement is tight.
        var tcs = new TaskCompletionSource<TimeSpan>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stopwatch = new Stopwatch();
        manager.StateChanged += (_, snapshot) =>
        {
            if (snapshot.Channel == ChannelKind.Ble
                && snapshot.State == ConnectionState.Disconnected)
            {
                tcs.TrySetResult(stopwatch.Elapsed);
            }
        };

        // Simulate the physical power-off: the BLE stack reports the device
        // gone (Plugin.BLE DeviceDisconnected → BlePort.StateChanged).
        stopwatch.Start();
        ble.RaiseConnectionStatusChanged(false);
        ble.IsConnected = false;

        var completed = await Task.WhenAny(tcs.Task, Task.Delay(LatencyBudgetMs));
        stopwatch.Stop();

        Assert.Same(tcs.Task, completed);
        var latency = await tcs.Task;
        Assert.True(
            latency.TotalMilliseconds <= LatencyBudgetMs,
            $"UI transition took {latency.TotalMilliseconds} ms, "
            + $"budget is {LatencyBudgetMs} ms.");
        Assert.Equal(ConnectionState.Disconnected, manager.State);
        Assert.Null(manager.ActiveProtocol);
    }

    private sealed class NoopDecoder : IPacketDecoder
    {
        public AppLayerDecodedEvent? Decode(RawPacket packet) => null;
        public void UpdateDictionary(
            IReadOnlyList<Command> commands,
            IReadOnlyList<Variable> variables,
            IReadOnlyList<ProtocolAddress> addresses) { }
    }

    // --- US4 batch orchestration integration test ---------------------------

    private static readonly byte[] Fw = new byte[16];

    // Canonical three-file batch with three distinct recipient IDs so the
    // fake can target each area precisely: HmiApplication (HMI recipient),
    // Motor1 (right motor), Rostrum. BootloaderHmi/HmiApplication/HmiImages
    // share a recipient, so we pick only one of them.
    private static readonly SparkFirmwareArea[] CanonicalBatch =
    [
        SparkFirmwareArea.HmiApplication,
        SparkFirmwareArea.Motor1,
        SparkFirmwareArea.Rostrum,
    ];

    /// <summary>
    /// Spec-001 US4 / SC-006 + SC-007 integration: drive the
    /// <see cref="SparkBatchUpdateService"/> orchestrator across the canonical
    /// three-file batch against a manual fake <see cref="IBootService"/>.
    ///
    /// Two logical halves in one <c>[Fact]</c>:
    /// SC-006 — 10 nominal batches: every area completes (3 areas × 4 boot
    /// steps = 12 calls recorded, 3 <c>AreaCompleted</c> events per run).
    /// SC-007 — 5 fault-injected batches where the second canonical area
    /// fails at <c>UploadBlocks</c>: orchestrator throws
    /// <see cref="SparkBatchUpdateException"/>, aborts before touching the
    /// third area, and the first area completes fully. The "corrupted
    /// WEMOTOR1.corrupt.bin" from the bench runbook maps to the fake
    /// injecting a failure on Motor1's recipient — bytes stay valid because
    /// the orchestrator never inspects payload.
    /// </summary>
    [Fact]
    public async Task Us4_CanonicalBatch_And_FaultInjected_Abort()
    {
        // SC-006: 10 nominal batches, all three areas succeed.
        for (int run = 0; run < 10; run++)
        {
            var fake = new FakeBootService();
            var orchestrator = new SparkBatchUpdateService(fake);
            var completed = new List<SparkFirmwareArea>();
            orchestrator.AreaCompleted += (_, def) => completed.Add(def.Area);

            await orchestrator.ExecuteAsync(MakeBatch());

            Assert.Equal(CanonicalBatch.Length * 4, fake.Calls.Count);
            Assert.Equal(CanonicalBatch, completed.ToArray());
        }

        // SC-007: 5 fault-injected batches, second area fails at UploadBlocks.
        var secondArea = CanonicalBatch[1];
        var secondRecipient = SparkAreas.Get(secondArea).RecipientId;
        var firstArea = CanonicalBatch[0];
        var thirdRecipient = SparkAreas.Get(CanonicalBatch[2]).RecipientId;

        for (int run = 0; run < 5; run++)
        {
            var fake = new FakeBootService
            {
                UploadBlocksFailsForRecipient = secondRecipient,
            };
            var orchestrator = new SparkBatchUpdateService(fake);
            var completed = new List<SparkFirmwareArea>();
            orchestrator.AreaCompleted += (_, def) => completed.Add(def.Area);

            var ex = await Assert.ThrowsAsync<SparkBatchUpdateException>(
                () => orchestrator.ExecuteAsync(MakeBatch()));

            Assert.Equal(secondArea, ex.Area.Area);
            Assert.Equal(SparkBatchPhase.UploadBlocks, ex.Phase);
            // Third area must never have been StartBoot-ed.
            Assert.DoesNotContain(
                fake.Calls,
                c => c.Kind == FakeBootCallKind.StartBoot
                     && c.Recipient == thirdRecipient);
            // First area completed fully (emitted AreaCompleted).
            Assert.Contains(firstArea, completed);
        }
    }

    private static List<SparkBatchItem> MakeBatch()
        => CanonicalBatch.Select(a => new SparkBatchItem(a, Fw)).ToList();

    private enum FakeBootCallKind { StartBoot, UploadBlocksOnly, EndBoot, Restart }
    private sealed record FakeBootCall(FakeBootCallKind Kind, uint Recipient);

    /// <summary>
    /// Manual fake <see cref="IBootService"/>. Mirrors the unit-test fake in
    /// <c>SparkBatchUpdateServiceTests</c>; duplicated here because private
    /// test fakes are not reusable across files and Luca's rules forbid
    /// making them public just for test sharing.
    /// </summary>
    private sealed class FakeBootService : IBootService
    {
        public List<FakeBootCall> Calls { get; } = new();
        public uint? UploadBlocksFailsForRecipient { get; set; }

        public BootState State { get; private set; } = BootState.Idle;
        public double Progress => 0;
        public event EventHandler<BootProgress>? ProgressChanged;

        public Task StartFirmwareUploadAsync(
            byte[] firmware, uint recipientId, CancellationToken ct = default)
            => throw new NotSupportedException(
                "SparkBatchUpdateService should never call StartFirmwareUploadAsync.");

        public Task<bool> StartBootAsync(uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.StartBoot, recipientId));
            return Task.FromResult(true);
        }

        public Task<bool> EndBootAsync(uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.EndBoot, recipientId));
            return Task.FromResult(true);
        }

        public Task RestartAsync(uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.Restart, recipientId));
            return Task.CompletedTask;
        }

        public Task UploadBlocksOnlyAsync(
            byte[] firmware, uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.UploadBlocksOnly, recipientId));
            ProgressChanged?.Invoke(this, new BootProgress(firmware.Length, firmware.Length));
            if (UploadBlocksFailsForRecipient == recipientId)
            {
                State = BootState.Failed;
                return Task.CompletedTask;
            }
            State = BootState.Completed;
            return Task.CompletedTask;
        }
    }
}
