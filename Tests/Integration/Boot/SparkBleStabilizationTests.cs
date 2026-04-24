using System.Diagnostics;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Protocol.Hardware;
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
}
