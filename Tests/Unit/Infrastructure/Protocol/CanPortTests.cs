using System.Buffers.Binary;
using Core.Models;
using Infrastructure.Protocol.Hardware;

namespace Tests.Unit.Infrastructure.Protocol;

/// <summary>
/// Test per <see cref="CanPort"/>: verifica state machine,
/// convention payload in-band (arbId LE + dati CAN) e forwarding degli
/// eventi del driver sottostante.
/// </summary>
public class CanPortTests
{
    // --- Ctor + stato iniziale ---

    [Fact]
    public void Ctor_DriverDisconnected_InitialStateIsDisconnected()
    {
        var driver = new FakePcanDriver { IsConnected = false };

        using var port = new CanPort(driver);

        Assert.Equal(ConnectionState.Disconnected, port.State);
        Assert.False(port.IsConnected);
    }

    [Fact]
    public void Ctor_DriverConnected_InitialStateIsConnected()
    {
        var driver = new FakePcanDriver { IsConnected = true };

        using var port = new CanPort(driver);

        Assert.Equal(ConnectionState.Connected, port.State);
        Assert.True(port.IsConnected);
    }

    [Fact]
    public void Ctor_NullDriver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new CanPort(null!));
    }

    [Fact]
    public void Kind_IsCan()
    {
        var driver = new FakePcanDriver();
        using var port = new CanPort(driver);
        Assert.Equal(ChannelKind.Can, port.Kind);
    }

    // --- ConnectAsync ---

    [Fact]
    public async Task ConnectAsync_AlreadyConnected_NoStateChange()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        await port.ConnectAsync();

        Assert.Equal(ConnectionState.Connected, port.State);
        Assert.Empty(stateEvents);
    }

    [Fact]
    public async Task ConnectAsync_DriverAlreadyConnected_TransitionsToConnected()
    {
        // Driver già attivo ma port parte da Disconnected (non realistico in prod,
        // utile per testare la transizione esplicita).
        var driver = new FakePcanDriver { IsConnected = false };
        using var port = new CanPort(driver);
        driver.IsConnected = true; // driver connette prima di ConnectAsync
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        await port.ConnectAsync();

        Assert.Equal(ConnectionState.Connected, port.State);
        Assert.Equal(
            new[] { ConnectionState.Connecting, ConnectionState.Connected },
            stateEvents);
    }

    [Fact]
    public async Task ConnectAsync_CancellationRequested_Throws()
    {
        var driver = new FakePcanDriver { IsConnected = false };
        using var port = new CanPort(driver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => port.ConnectAsync(cts.Token));
    }

    // --- DisconnectAsync ---

    [Fact]
    public async Task DisconnectAsync_FromConnected_CallsDriverDisconnect()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        await port.DisconnectAsync();

        Assert.Equal(ConnectionState.Disconnected, port.State);
        Assert.Equal(1, driver.DisconnectCount);
        Assert.Equal(new[] { ConnectionState.Disconnected }, stateEvents);
    }

    [Fact]
    public async Task DisconnectAsync_AlreadyDisconnected_NoOp()
    {
        var driver = new FakePcanDriver { IsConnected = false };
        using var port = new CanPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        await port.DisconnectAsync();

        Assert.Equal(0, driver.DisconnectCount);
        Assert.Empty(stateEvents);
    }

    // --- SendAsync: validazione input ---

    [Fact]
    public async Task SendAsync_NotConnected_ThrowsInvalidOperation()
    {
        var driver = new FakePcanDriver { IsConnected = false };
        using var port = new CanPort(driver);
        var payload = new byte[] { 0x01, 0x02, 0x03, 0x04, 0xAA };

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => port.SendAsync(payload));
        Assert.Empty(driver.SentMessages);
    }

    [Fact]
    public async Task SendAsync_PayloadShorterThanArbIdPrefix_ThrowsArgument()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        var payload = new byte[] { 0x01, 0x02, 0x03 }; // solo 3 byte

        await Assert.ThrowsAsync<ArgumentException>(
            () => port.SendAsync(payload));
    }

    [Fact]
    public async Task SendAsync_DataExceedsCanFrameLimit_ThrowsArgument()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        // 4 byte arbId + 9 byte dati = 13 totali (massimo CAN = 4+8 = 12)
        var payload = new byte[13];

        await Assert.ThrowsAsync<ArgumentException>(
            () => port.SendAsync(payload));
    }

    [Fact]
    public async Task SendAsync_CancellationRequested_Throws()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var payload = new byte[] { 0x01, 0x00, 0x00, 0x00, 0xAA };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => port.SendAsync(payload, cts.Token));
    }

    // --- SendAsync: convention payload ---

    [Fact]
    public async Task SendAsync_ExtractsArbIdLittleEndianAndData()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        // arbId = 0x12345678 LE → 78 56 34 12
        // dati  = DE AD BE EF
        var payload = new byte[] { 0x78, 0x56, 0x34, 0x12, 0xDE, 0xAD, 0xBE, 0xEF };

        await port.SendAsync(payload);

        Assert.Single(driver.SentMessages);
        var sent = driver.SentMessages[0];
        Assert.Equal(0x12345678u, sent.CanId);
        Assert.Equal(new byte[] { 0xDE, 0xAD, 0xBE, 0xEF }, sent.Data);
        Assert.True(sent.IsExtended);
    }

    [Fact]
    public async Task SendAsync_EmptyDataBeyondArbId_SendsZeroLengthData()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        var payload = new byte[] { 0x01, 0x00, 0x00, 0x00 }; // solo arbId

        await port.SendAsync(payload);

        Assert.Single(driver.SentMessages);
        Assert.Equal(1u, driver.SentMessages[0].CanId);
        Assert.Empty(driver.SentMessages[0].Data);
    }

    [Fact]
    public async Task SendAsync_MaxDataLength_IsAccepted()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        // 4 byte arbId + 8 byte dati = 12 (limite CAN extended)
        var payload = new byte[12];
        payload[0] = 0xFF;

        await port.SendAsync(payload);

        Assert.Single(driver.SentMessages);
        Assert.Equal(8, driver.SentMessages[0].Data.Length);
    }

    // --- PacketReceived: convention payload ---

    [Fact]
    public void DriverPacketReceived_WrapsAsRawPacketWithArbIdPrefix()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        RawPacket? received = null;
        port.PacketReceived += (_, p) => received = p;
        var timestamp = new DateTime(2026, 4, 20, 12, 34, 56, DateTimeKind.Utc);

        driver.RaisePacketReceived(
            0xCAFEBABE,
            new byte[] { 0x11, 0x22, 0x33 },
            timestamp);

        Assert.NotNull(received);
        Assert.Equal(timestamp, received!.Timestamp);
        Assert.Equal(4 + 3, received.Length);
        // primi 4 byte = 0xCAFEBABE LE
        Assert.Equal(
            0xCAFEBABEu,
            BinaryPrimitives.ReadUInt32LittleEndian(received.Payload.AsSpan()[..4]));
        // resto = dati originali
        Assert.Equal(0x11, received.Payload[4]);
        Assert.Equal(0x22, received.Payload[5]);
        Assert.Equal(0x33, received.Payload[6]);
    }

    [Fact]
    public void DriverPacketReceived_NoSubscribers_DoesNotThrow()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);

        driver.RaisePacketReceived(1, new byte[] { 0xAA }, DateTime.UtcNow);
        // no assert: solo non deve sollevare
    }

    // --- StateChanged dagli eventi driver ---

    [Fact]
    public void DriverConnectionStatusChanged_ConnectedToDisconnected_FiresStateChanged()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        driver.RaiseConnectionStatusChanged(false);

        Assert.Equal(ConnectionState.Disconnected, port.State);
        Assert.Equal(new[] { ConnectionState.Disconnected }, stateEvents);
    }

    [Fact]
    public void DriverConnectionStatusChanged_SameState_NoEventEmitted()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        using var port = new CanPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        driver.RaiseConnectionStatusChanged(true); // già connesso

        Assert.Empty(stateEvents);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_UnsubscribesFromDriverEvents()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        var port = new CanPort(driver);
        var packetEvents = new List<RawPacket>();
        port.PacketReceived += (_, p) => packetEvents.Add(p);

        port.Dispose();
        driver.RaisePacketReceived(1, new byte[] { 0xAA }, DateTime.UtcNow);

        Assert.Empty(packetEvents);
    }

    [Fact]
    public void Dispose_FromConnected_CallsDriverDisconnect()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        var port = new CanPort(driver);

        port.Dispose();

        Assert.Equal(1, driver.DisconnectCount);
    }

    [Fact]
    public async Task AfterDispose_SendAsync_ThrowsObjectDisposed()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        var port = new CanPort(driver);
        port.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => port.SendAsync(new byte[] { 0x01, 0x00, 0x00, 0x00 }));
    }

    [Fact]
    public async Task AfterDispose_ConnectAsync_ThrowsObjectDisposed()
    {
        var driver = new FakePcanDriver { IsConnected = false };
        var port = new CanPort(driver);
        port.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => port.ConnectAsync());
    }

    [Fact]
    public async Task AfterDispose_DisconnectAsync_ThrowsObjectDisposed()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        var port = new CanPort(driver);
        port.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => port.DisconnectAsync());
    }

    [Fact]
    public void Dispose_CalledTwice_IsIdempotent()
    {
        var driver = new FakePcanDriver { IsConnected = true };
        var port = new CanPort(driver);

        port.Dispose();
        port.Dispose();

        // Primo dispose chiama driver.Disconnect una volta, il secondo è no-op.
        Assert.Equal(1, driver.DisconnectCount);
    }

    [Fact]
    public void Dispose_FromDisconnected_DoesNotCallDriverDisconnect()
    {
        var driver = new FakePcanDriver { IsConnected = false };
        var port = new CanPort(driver);

        port.Dispose();

        Assert.Equal(0, driver.DisconnectCount);
    }

    [Fact]
    public void MultipleStateCycles_FireAllTransitionEvents()
    {
        // Simula: driver connette → disconnette → riconnette → disconnette.
        // CanPort deve rispecchiare ogni transizione con un evento StateChanged.
        var driver = new FakePcanDriver { IsConnected = false };
        using var port = new CanPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        driver.RaiseConnectionStatusChanged(true);
        driver.RaiseConnectionStatusChanged(false);
        driver.RaiseConnectionStatusChanged(true);
        driver.RaiseConnectionStatusChanged(false);

        Assert.Equal(
            new[]
            {
                ConnectionState.Connected,
                ConnectionState.Disconnected,
                ConnectionState.Connected,
                ConnectionState.Disconnected,
            },
            stateEvents);
    }

    [Fact]
    public async Task ConnectAsync_DriverNeverConnects_TransitionsToErrorAndThrows()
    {
        // Copre il percorso di timeout: 20 poll × 100 ms ≈ 2 s.
        // Il driver resta disconnesso → stato finale Error + InvalidOperationException.
        var driver = new FakePcanDriver { IsConnected = false };
        using var port = new CanPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => port.ConnectAsync());

        Assert.Contains("PCAN", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ConnectionState.Error, port.State);
        Assert.Equal(
            new[] { ConnectionState.Connecting, ConnectionState.Error },
            stateEvents);
    }
}
