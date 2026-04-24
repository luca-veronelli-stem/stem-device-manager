using Core.Interfaces;
using Core.Models;
using Infrastructure.Protocol.Hardware;
using Services.Cache;
using Services.Protocol;
using Tests.Unit.Infrastructure.Protocol;

namespace Tests.Unit.Services.Cache;

/// <summary>
/// Test di <see cref="ConnectionManager"/>. Usa real
/// <see cref="CanPort"/>/<see cref="BlePort"/>/<see cref="SerialPort"/> con
/// fake driver (<see cref="FakePcanDriver"/>, <see cref="FakeBleDriver"/>,
/// <see cref="FakeSerialDriver"/>) per verificare:
/// <list type="bullet">
/// <item><description>Aggregazione 3 port + proprietà <c>ActiveChannel</c>/<c>ActiveProtocol</c></description></item>
/// <item><description>Sequenza SwitchTo: dispose protocol vecchio → disconnect port vecchia → connect nuova → crea protocol</description></item>
/// <item><description>Eventi <c>ActiveChannelChanged</c> e <c>StateChanged</c></description></item>
/// </list>
///
/// <para><b>Windows-only:</b> <see cref="Infrastructure.Protocol"/> è
/// referenziato da Tests solo sotto Windows. File escluso dal target
/// <c>net10.0</c> in <c>Tests.csproj</c>.</para>
/// </summary>
public class ConnectionManagerTests
{
    private static readonly DeviceVariantConfig GenericConfig =
        DeviceVariantConfig.Create(DeviceVariant.Generic);

    // --- Ctor ---

    [Fact]
    public void Ctor_NullPorts_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ConnectionManager(null!, new NoopDecoder(), GenericConfig));
    }

    [Fact]
    public void Ctor_NullDecoder_Throws()
    {
        using var can = new CanPort(new FakePcanDriver());
        Assert.Throws<ArgumentNullException>(
            () => new ConnectionManager([can], null!, GenericConfig));
    }

    [Fact]
    public void Ctor_NullVariantConfig_Throws()
    {
        using var can = new CanPort(new FakePcanDriver());
        Assert.Throws<ArgumentNullException>(
            () => new ConnectionManager([can], new NoopDecoder(), null!));
    }

    [Fact]
    public void Ctor_EmptyPorts_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => new ConnectionManager([], new NoopDecoder(), GenericConfig));
    }

    // --- Stato iniziale ---

    [Fact]
    public void InitialState_ActiveChannelFromVariantConfig_NoActiveProtocol()
    {
        using var fixture = new Fixture(variant: DeviceVariant.TopLift);

        Assert.Equal(ChannelKind.Can, fixture.Manager.ActiveChannel);
        Assert.Null(fixture.Manager.ActiveProtocol);
    }

    [Fact]
    public void InitialState_StateIsDisconnected()
    {
        using var fixture = new Fixture();

        Assert.Equal(ConnectionState.Disconnected, fixture.Manager.State);
        Assert.Null(fixture.Manager.ActiveProtocol);
    }

    [Fact]
    public void InitialState_Generic_DefaultsToBle()
    {
        using var fixture = new Fixture(variant: DeviceVariant.Generic);

        Assert.Equal(ChannelKind.Ble, fixture.Manager.ActiveChannel);
    }

    // --- SwitchToAsync ---

    [Fact]
    public async Task SwitchToAsync_FirstCall_CreatesActiveProtocolAndConnectsPort()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;

        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        Assert.NotNull(fixture.Manager.ActiveProtocol);
        Assert.Equal(ChannelKind.Ble, fixture.Manager.ActiveChannel);
        Assert.Equal(ConnectionState.Connected, fixture.BlePort.State);
    }

    [Fact]
    public void InitialState_CurrentBootAndTelemetryAreNull()
    {
        using var fixture = new Fixture();

        Assert.Null(fixture.Manager.CurrentBoot);
        Assert.Null(fixture.Manager.CurrentTelemetry);
    }

    [Fact]
    public async Task SwitchToAsync_FirstCall_CreatesCurrentBootAndTelemetry()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;

        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        Assert.NotNull(fixture.Manager.CurrentBoot);
        Assert.NotNull(fixture.Manager.CurrentTelemetry);
    }

    [Fact]
    public async Task SwitchToAsync_SecondCall_RecreatesBootAndTelemetry()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        fixture.PcanDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);
        var firstBoot = fixture.Manager.CurrentBoot!;
        var firstTelemetry = fixture.Manager.CurrentTelemetry!;

        await fixture.Manager.SwitchToAsync(ChannelKind.Can);

        Assert.NotSame(firstBoot, fixture.Manager.CurrentBoot);
        Assert.NotSame(firstTelemetry, fixture.Manager.CurrentTelemetry);
    }

    [Fact]
    public async Task SwitchToAsync_FromBleToCan_DisconnectsBleAndConnectsCan()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        fixture.PcanDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);
        var firstProtocol = fixture.Manager.ActiveProtocol!;

        await fixture.Manager.SwitchToAsync(ChannelKind.Can);

        Assert.Equal(ChannelKind.Can, fixture.Manager.ActiveChannel);
        Assert.NotSame(firstProtocol, fixture.Manager.ActiveProtocol);
        Assert.Equal(ConnectionState.Connected, fixture.CanPort.State);
        Assert.Equal(ConnectionState.Disconnected, fixture.BlePort.State);
        Assert.Equal(1, fixture.BleDriver.DisconnectCount);
    }

    [Fact]
    public async Task SwitchToAsync_SameChannelTwice_DoesNotDisconnect()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;

        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        Assert.Equal(0, fixture.BleDriver.DisconnectCount);
        Assert.Equal(ConnectionState.Connected, fixture.BlePort.State);
    }

    [Fact]
    public async Task SwitchToAsync_InvalidChannel_Throws()
    {
        using var fixture = new Fixture();

        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Manager.SwitchToAsync((ChannelKind)999));
    }

    [Fact]
    public async Task SwitchToAsync_FirstCall_StateBecomesConnected()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;

        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        Assert.Equal(ConnectionState.Connected, fixture.Manager.State);
        Assert.NotNull(fixture.Manager.ActiveProtocol);
    }

    [Fact]
    public async Task SwitchToAsync_Cancelled_StateRollsBackToDisconnected()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.Manager.SwitchToAsync(ChannelKind.Ble, cts.Token));

        Assert.Equal(ConnectionState.Disconnected, fixture.Manager.State);
        Assert.Null(fixture.Manager.ActiveProtocol);
        Assert.Null(fixture.Manager.CurrentBoot);
        Assert.Null(fixture.Manager.CurrentTelemetry);
    }

    [Fact]
    public async Task SwitchToAsync_EmitsActiveChannelChanged()
    {
        using var fixture = new Fixture();
        fixture.PcanDriver.IsConnected = true;
        var fired = new List<ChannelKind>();
        fixture.Manager.ActiveChannelChanged += (_, kind) => fired.Add(kind);

        await fixture.Manager.SwitchToAsync(ChannelKind.Can);

        Assert.Single(fired);
        Assert.Equal(ChannelKind.Can, fired[0]);
    }

    // --- DisconnectAsync ---

    [Fact]
    public async Task DisconnectAsync_ClearsActiveProtocolAndDisconnectsPort()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        await fixture.Manager.DisconnectAsync();

        Assert.Null(fixture.Manager.ActiveProtocol);
        Assert.Equal(1, fixture.BleDriver.DisconnectCount);
    }

    [Fact]
    public async Task DisconnectAsync_StateBecomesDisconnected()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        await fixture.Manager.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, fixture.Manager.State);
        Assert.Null(fixture.Manager.ActiveProtocol);
    }

    [Fact]
    public async Task DisconnectAsync_ClearsCurrentBootAndTelemetry()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        await fixture.Manager.DisconnectAsync();

        Assert.Null(fixture.Manager.CurrentBoot);
        Assert.Null(fixture.Manager.CurrentTelemetry);
    }

    // --- Port → Manager disconnect propagation (spec-001 T010, C3 item 3) ---

    [Fact]
    public async Task PortDisconnected_WhileConnected_ManagerTransitionsToDisconnected()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        fixture.BleDriver.RaiseConnectionStatusChanged(false);

        Assert.Equal(ConnectionState.Disconnected, fixture.Manager.State);
        Assert.Null(fixture.Manager.ActiveProtocol);
        Assert.Null(fixture.Manager.CurrentBoot);
        Assert.Null(fixture.Manager.CurrentTelemetry);
    }

    [Fact]
    public async Task PortDisconnected_WhileConnected_DisposesActiveProtocol()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);
        var protocol = fixture.Manager.ActiveProtocol!;

        fixture.BleDriver.RaiseConnectionStatusChanged(false);

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => protocol.SendCommandAsync(0, new Command("x", "00", "01"), []));
    }

    [Fact]
    public async Task PortDisconnected_NonActiveChannel_ManagerStateUnchanged()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        fixture.PcanDriver.RaiseConnectionStatusChanged(true);
        fixture.PcanDriver.RaiseConnectionStatusChanged(false);

        Assert.Equal(ConnectionState.Connected, fixture.Manager.State);
        Assert.NotNull(fixture.Manager.ActiveProtocol);
    }

    [Fact]
    public async Task SwitchToAsync_AfterPortDropAndReconnect_RebuildsActiveProtocol()
    {
        // spec-001 #55: BLE device re-selection after a drop must rebuild the
        // protocol/boot/telemetry trio. The tab calls SwitchToAsync(Ble) after
        // the driver reconnects; the manager should produce a fresh trio even
        // though the port is already Connected.
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);
        var firstProtocol = fixture.Manager.ActiveProtocol!;
        var firstBoot = fixture.Manager.CurrentBoot!;
        var firstTelemetry = fixture.Manager.CurrentTelemetry!;

        // Drop: T010 pulls the manager back to Disconnected + null trio.
        fixture.BleDriver.RaiseConnectionStatusChanged(false);
        Assert.Equal(ConnectionState.Disconnected, fixture.Manager.State);
        Assert.Null(fixture.Manager.ActiveProtocol);

        // Reconnect: driver + port back to Connected (would be the tab's
        // ConnectToAsync path in prod).
        fixture.BleDriver.IsConnected = true;
        fixture.BleDriver.RaiseConnectionStatusChanged(true);
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        Assert.Equal(ConnectionState.Connected, fixture.Manager.State);
        Assert.NotNull(fixture.Manager.ActiveProtocol);
        Assert.NotNull(fixture.Manager.CurrentBoot);
        Assert.NotNull(fixture.Manager.CurrentTelemetry);
        Assert.NotSame(firstProtocol, fixture.Manager.ActiveProtocol);
        Assert.NotSame(firstBoot, fixture.Manager.CurrentBoot);
        Assert.NotSame(firstTelemetry, fixture.Manager.CurrentTelemetry);
    }

    [Fact]
    public async Task PortDisconnected_StillForwardsStateChangedEvent()
    {
        using var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);
        var captured = new List<ConnectionStateSnapshot>();
        fixture.Manager.StateChanged += (_, s) => captured.Add(s);

        fixture.BleDriver.RaiseConnectionStatusChanged(false);

        Assert.Single(captured);
        Assert.Equal(ChannelKind.Ble, captured[0].Channel);
        Assert.Equal(ConnectionState.Disconnected, captured[0].State);
    }

    // --- StateOf ---

    [Fact]
    public void StateOf_ReturnsCurrentStateFromPort()
    {
        using var fixture = new Fixture();

        Assert.Equal(ConnectionState.Disconnected, fixture.Manager.StateOf(ChannelKind.Ble));
        fixture.BleDriver.RaiseConnectionStatusChanged(true);
        Assert.Equal(ConnectionState.Connected, fixture.Manager.StateOf(ChannelKind.Ble));
    }

    [Fact]
    public void StateOf_UnknownChannel_ReturnsDisconnected()
    {
        using var fixture = new Fixture();

        Assert.Equal(ConnectionState.Disconnected, fixture.Manager.StateOf((ChannelKind)999));
    }

    // --- StateChanged event ---

    [Fact]
    public void StateChanged_FiredWhenAnyPortEmits()
    {
        using var fixture = new Fixture();
        var captured = new List<ConnectionStateSnapshot>();
        fixture.Manager.StateChanged += (_, s) => captured.Add(s);

        fixture.BleDriver.RaiseConnectionStatusChanged(true);

        Assert.Single(captured);
        Assert.Equal(ChannelKind.Ble, captured[0].Channel);
        Assert.Equal(ConnectionState.Connected, captured[0].State);
    }

    // --- Dispose ---

    [Fact]
    public async Task Dispose_UnsubscribesFromPortStateChanged()
    {
        var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);
        int eventsAfterDispose = 0;
        fixture.Manager.StateChanged += (_, _) => eventsAfterDispose++;

        fixture.Manager.Dispose();
        fixture.BleDriver.RaiseConnectionStatusChanged(false);

        Assert.Equal(0, eventsAfterDispose);
        fixture.Dispose();
    }

    [Fact]
    public async Task Dispose_DisposesActiveProtocol()
    {
        var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);
        var protocol = fixture.Manager.ActiveProtocol!;

        fixture.Manager.Dispose();

        // Dopo Dispose il protocol è disposed: SendCommandAsync lancia ObjectDisposedException.
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => protocol.SendCommandAsync(0, new Command("x", "00", "01"), []));
        fixture.Dispose();
    }

    [Fact]
    public async Task Dispose_StateBecomesDisconnected()
    {
        var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        fixture.Manager.Dispose();

        Assert.Equal(ConnectionState.Disconnected, fixture.Manager.State);
        Assert.Null(fixture.Manager.ActiveProtocol);
        fixture.Dispose();
    }

    [Fact]
    public async Task Dispose_ClearsCurrentBootAndTelemetry()
    {
        var fixture = new Fixture();
        fixture.BleDriver.IsConnected = true;
        await fixture.Manager.SwitchToAsync(ChannelKind.Ble);

        fixture.Manager.Dispose();

        Assert.Null(fixture.Manager.CurrentBoot);
        Assert.Null(fixture.Manager.CurrentTelemetry);
        fixture.Dispose();
    }

    // --- AppLayerDecoded forwarding ---

    [Fact]
    public async Task AppLayerDecoded_BeforeSwitch_NoSubscriptionRequired()
    {
        using var fixture = new Fixture();
        int fired = 0;
        fixture.Manager.AppLayerDecoded += (_, _) => fired++;

        // Prima dello switch, il manager non è agganciato a nessun protocol.
        // Emettere un pacchetto sulla port BLE non produce l'evento.
        fixture.BleDriver.RaisePacketReceived([0x00, 0x01, 0x02], DateTime.UtcNow);

        Assert.Equal(0, fired);
    }

    [Fact]
    public async Task AfterDispose_SwitchToAsync_Throws()
    {
        var fixture = new Fixture();
        fixture.Manager.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => fixture.Manager.SwitchToAsync(ChannelKind.Can));
        fixture.Dispose();
    }

    // --- Fixture ---

    /// <summary>
    /// Bundle di test: 3 fake driver, 3 real port, ConnectionManager. La
    /// variant config determina il <see cref="ConnectionManager.ActiveChannel"/>
    /// iniziale.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        public Fixture(DeviceVariant variant = DeviceVariant.Generic)
        {
            PcanDriver = new FakePcanDriver();
            BleDriver = new FakeBleDriver();
            SerialDriver = new FakeSerialDriver();
            CanPort = new CanPort(PcanDriver);
            BlePort = new BlePort(BleDriver);
            SerialPort = new SerialPort(SerialDriver);
            Manager = new ConnectionManager(
                [CanPort, BlePort, SerialPort],
                new NoopDecoder(),
                DeviceVariantConfig.Create(variant));
        }

        public FakePcanDriver PcanDriver { get; }
        public FakeBleDriver BleDriver { get; }
        public FakeSerialDriver SerialDriver { get; }
        public CanPort CanPort { get; }
        public BlePort BlePort { get; }
        public SerialPort SerialPort { get; }
        public ConnectionManager Manager { get; }

        public void Dispose()
        {
            Manager.Dispose();
            CanPort.Dispose();
            BlePort.Dispose();
            SerialPort.Dispose();
        }
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
