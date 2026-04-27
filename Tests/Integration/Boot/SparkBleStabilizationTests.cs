using System.Buffers.Binary;
using System.Diagnostics;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Protocol.Hardware;
using Services.Boot;
using Services.Cache;
using Services.Protocol;
using Tests.Unit.Infrastructure.Protocol;
using Tests.Unit.Services.Protocol;

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

    // --- US3 single-file upload survives full session -----------------------

    /// <summary>HMI application recipient on SPARK; matches
    /// <c>SparkAreas.Get(SparkFirmwareArea.HmiApplication).RecipientId</c>.</summary>
    private const uint HmiRecipient = 0x000702C1u;

    /// <summary>Reply commands the receiving decoder must know about so the
    /// auto-replier's "80 0X" frames decode into <see cref="AppLayerDecodedEvent"/>
    /// and unblock <see cref="BootService"/>'s wait-for-reply.</summary>
    private static readonly Command[] BootReplyCommands =
    [
        new("StartProcedureReply", "80", "05"),
        new("EndProcedureReply",   "80", "06"),
        new("ProgramBlockReply",   "80", "07"),
    ];

    /// <summary>
    /// Spec-001 US3 / SC-003 + SC-005 integration: drives 10 single-file HMI
    /// uploads through the real <see cref="ConnectionManager"/> +
    /// <see cref="BlePort"/> + <see cref="BootService"/> chain, with a fake
    /// <see cref="IBleDriver"/> that auto-replies to every chunked
    /// <c>StartProcedure</c> / <c>ProgramBlock</c> / <c>EndProcedure</c>. For
    /// each run the test counts <see cref="ConnectionState.Disconnected"/>
    /// transitions emitted on the BLE channel during the upload window.
    ///
    /// SC-003: every run completes with zero mid-upload disconnect events.
    /// SC-005: ≥ 95 % upload-success rate. The auto-reply path is deterministic
    /// so 10/10 = 100 % is the actual measurement; the threshold is asserted
    /// explicitly so the test fails loud if the codepath ever introduces
    /// non-determinism (e.g. real bench wiring).
    ///
    /// Firmware payload is 50 KB of synthetic bytes — large enough that the
    /// upload spends sustained time on the BLE port (50 blocks × auto-reply
    /// round trip) without making the test slow.
    /// </summary>
    [Fact]
    public async Task Us3_Hmi_Upload_SurvivesFullSession()
    {
        const int RunCount = 10;
        const double SuccessRateThreshold = 0.95;
        var firmware = BuildFirmwareMock(length: 50 * 1024);

        var disconnectsByRun = new int[RunCount];
        var completedByRun = new bool[RunCount];

        for (int run = 0; run < RunCount; run++)
        {
            (disconnectsByRun[run], completedByRun[run]) =
                await RunOneHmiUpload(firmware);
        }

        Assert.All(
            disconnectsByRun,
            count => Assert.Equal(0, count));

        int successes = completedByRun.Count(c => c);
        Assert.True(
            successes >= RunCount * SuccessRateThreshold,
            $"Upload success rate {successes}/{RunCount} below SC-005 "
            + $"threshold ({SuccessRateThreshold:P0}).");
    }

    private static async Task<(int Disconnects, bool Completed)> RunOneHmiUpload(
        byte[] firmware)
    {
        var pcan = new FakePcanDriver();
        var ble = new FakeBleDriver();
        var serial = new FakeSerialDriver();

        using var canPort = new CanPort(pcan);
        using var blePort = new BlePort(ble);
        using var serialPort = new SerialPort(serial);
        using var manager = new ConnectionManager(
            [canPort, blePort, serialPort],
            new PacketDecoder(BootReplyCommands, [], []),
            DeviceVariantConfig.Create(DeviceVariant.Egicon));

        ble.IsConnected = true;
        await manager.SwitchToAsync(ChannelKind.Ble);
        ble.RaiseConnectionStatusChanged(true);
        Assert.Equal(ConnectionState.Connected, manager.State);

        ConfigureAutoReply(ble, ownSenderId: DeviceVariantConfig.DefaultSenderId);

        int disconnects = 0;
        EventHandler<ConnectionStateSnapshot> handler = (_, snap) =>
        {
            if (snap.Channel == ChannelKind.Ble
                && snap.State == ConnectionState.Disconnected)
                Interlocked.Increment(ref disconnects);
        };
        manager.StateChanged += handler;

        var boot = manager.CurrentBoot!;
        try
        {
            await boot.StartFirmwareUploadAsync(firmware, HmiRecipient);
        }
        finally
        {
            manager.StateChanged -= handler;
        }

        bool completed = boot.State == BootState.Completed;

        await manager.DisconnectAsync();
        ble.RaiseConnectionStatusChanged(false);
        ble.IsConnected = false;

        return (disconnects, completed);
    }

    private static byte[] BuildFirmwareMock(int length)
    {
        var fw = new byte[length];
        for (int i = 0; i < length; i++) fw[i] = (byte)(i & 0xFF);
        // fwType bytes 14..15 little-endian — mirrors BootServiceTests pattern.
        fw[14] = 0x05;
        fw[15] = 0x00;
        return fw;
    }

    /// <summary>
    /// Hooks <see cref="FakeBleDriver.SendResult"/> so that every chunked
    /// command's last frame triggers a properly-encoded reply packet
    /// (<c>CodeHigh = "80"</c>) injected back via
    /// <see cref="FakeBleDriver.RaisePacketReceived"/>. Mirrors the auto-reply
    /// pattern in <c>BootServiceTests.Fixture</c>; the difference is that the
    /// reply is fed into the driver layer instead of the
    /// <see cref="ICommunicationPort"/> layer, so the full BLE port + protocol
    /// + boot-service stack is exercised end-to-end.
    /// </summary>
    private static void ConfigureAutoReply(FakeBleDriver driver, uint ownSenderId)
    {
        var packetTracker = new Dictionary<int, (byte CmdHi, byte CmdLo, uint Recipient)>();

        driver.SendResult = frame =>
        {
            var ni = NetInfo.Parse(frame[0], frame[1]);
            if (ni.SetLength)
            {
                uint recipient = BinaryPrimitives.ReadUInt32LittleEndian(
                    frame.AsSpan(2, 4));
                packetTracker[ni.PacketId] = (frame[13], frame[14], recipient);
            }
            if (ni.RemainingChunks != 0) return true;
            if (!packetTracker.TryGetValue(ni.PacketId, out var info)) return true;
            packetTracker.Remove(ni.PacketId);

            // RESTART (00, 0A) is fire-and-forget — no reply on the wire.
            if (info.CmdLo == 0x0A) return true;

            FireReply(driver, info.Recipient, info.CmdLo, ownSenderId);
            return true;
        };
    }

    private static void FireReply(
        FakeBleDriver driver, uint originalRecipient, byte cmdLo, uint ownSenderId)
    {
        var replyCmd = new Command("Reply", "80", cmdLo.ToString("X2"));
        using var replyPort = new FakeCommunicationPort(ChannelKind.Ble);
        var replyDecoder = new PacketDecoder([], [], []);
        using var replySvc = new ProtocolService(replyPort, replyDecoder, originalRecipient);
        replySvc.SendCommandAsync(ownSenderId, replyCmd, []).GetAwaiter().GetResult();
        foreach (var chunk in replyPort.SentPayloads)
            driver.RaisePacketReceived(chunk, DateTime.UtcNow);
    }

    // --- US5 single-file upload within v2.15 budget -------------------------

    /// <summary>
    /// Spec-001 US5 / SC-004 / FR-004 regression-guard: a single HMI v8.2
    /// upload through the real <see cref="ConnectionManager"/> +
    /// <see cref="BlePort"/> + <see cref="BootService"/> chain, against the
    /// auto-replying <see cref="FakeBleDriver"/>, completes in ≤ 16 minutes.
    ///
    /// With deterministic auto-reply this assertion is trivially true (the
    /// fake-driven upload completes in seconds — see <see cref="Us3_Hmi_Upload_SurvivesFullSession"/>
    /// which runs 10 of these in ~10 s). The 16-minute budget exists not as
    /// a tight bound on this test but as a tripwire against any future
    /// refactor that introduces an artificial delay (e.g. a stray
    /// <c>Task.Delay</c>, a runaway retry loop, a sync-over-async deadlock
    /// resolved by a long timeout) in the boot orchestration. Real-bench
    /// SC-004 enforcement is the procedure in <c>quickstart.md</c>, run
    /// against SPARK-UC SN 2225998 with <c>WORKCODE_HMI_RSC.bin</c>.
    ///
    /// Five-run mean check (mean ≤ 14 min) is the bench runbook's job per
    /// tasks.md T021 — tests must be deterministic, so single-run only here.
    /// </summary>
    [Fact]
    public async Task Us5_Hmi_Upload_WithinV215_Budget()
    {
        var firmware = BuildFirmwareMock(length: 50 * 1024);
        var stopwatch = Stopwatch.StartNew();

        await RunOneHmiUpload(firmware);

        stopwatch.Stop();

        Assert.True(
            stopwatch.Elapsed <= TimeSpan.FromMinutes(16),
            $"Upload took {stopwatch.Elapsed.TotalMinutes:F2} min, "
            + "exceeds SC-004 single-run budget of 16 min.");
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
