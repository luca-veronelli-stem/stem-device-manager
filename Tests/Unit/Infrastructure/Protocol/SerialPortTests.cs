using Core.Models;
using SerialPort = Infrastructure.Protocol.Hardware.SerialPort;

namespace Tests.Unit.Infrastructure.Protocol;

/// <summary>
/// Test per <see cref="SerialPort"/>: verifica state machine, convention
/// pass-through (no prefisso) e forwarding eventi del driver.
/// </summary>
public class SerialPortTests
{
    // --- Ctor + stato iniziale ---

    [Fact]
    public void Ctor_DriverDisconnected_InitialStateIsDisconnected()
    {
        var driver = new FakeSerialDriver { IsConnected = false };

        using var port = new SerialPort(driver);

        Assert.Equal(ConnectionState.Disconnected, port.State);
        Assert.False(port.IsConnected);
    }

    [Fact]
    public void Ctor_DriverConnected_InitialStateIsConnected()
    {
        var driver = new FakeSerialDriver { IsConnected = true };

        using var port = new SerialPort(driver);

        Assert.Equal(ConnectionState.Connected, port.State);
        Assert.True(port.IsConnected);
    }

    [Fact]
    public void Ctor_NullDriver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new SerialPort(null!));
    }

    [Fact]
    public void Kind_IsSerial()
    {
        var driver = new FakeSerialDriver();
        using var port = new SerialPort(driver);
        Assert.Equal(ChannelKind.Serial, port.Kind);
    }

    // --- ConnectAsync ---

    [Fact]
    public async Task ConnectAsync_AlreadyConnected_NoStateChange()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        await port.ConnectAsync();

        Assert.Equal(ConnectionState.Connected, port.State);
        Assert.Empty(stateEvents);
    }

    [Fact]
    public async Task ConnectAsync_DriverIsConnected_TransitionsDirectlyToConnected()
    {
        var driver = new FakeSerialDriver { IsConnected = false };
        using var port = new SerialPort(driver);
        driver.IsConnected = true;
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        await port.ConnectAsync();

        // Nuovo comportamento idempotente: nessun polling/timeout, il port si allinea al
        // driver corrente. Se il driver è Connected, transita direttamente (un solo state
        // event, niente Connecting intermedio).
        Assert.Equal(ConnectionState.Connected, port.State);
        Assert.Equal(new[] { ConnectionState.Connected }, stateEvents);
    }

    [Fact]
    public async Task ConnectAsync_CancellationRequested_Throws()
    {
        var driver = new FakeSerialDriver { IsConnected = false };
        using var port = new SerialPort(driver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => port.ConnectAsync(cts.Token));
    }

    // --- DisconnectAsync ---

    [Fact]
    public async Task DisconnectAsync_FromConnected_CallsDriverDisconnect()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);
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
        var driver = new FakeSerialDriver { IsConnected = false };
        using var port = new SerialPort(driver);

        await port.DisconnectAsync();

        Assert.Equal(0, driver.DisconnectCount);
    }

    // --- SendAsync: validazione input ---

    [Fact]
    public async Task SendAsync_NotConnected_ThrowsInvalidOperation()
    {
        var driver = new FakeSerialDriver { IsConnected = false };
        using var port = new SerialPort(driver);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => port.SendAsync(new byte[] { 0xAA }));
        Assert.Empty(driver.SentMessages);
    }

    [Fact]
    public async Task SendAsync_EmptyPayload_ThrowsArgument()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);

        await Assert.ThrowsAsync<ArgumentException>(
            () => port.SendAsync(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public async Task SendAsync_CancellationRequested_Throws()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => port.SendAsync(new byte[] { 0xAA }, cts.Token));
    }

    [Fact]
    public async Task SendAsync_DriverReportsFailure_ThrowsInvalidOperation()
    {
        var driver = new FakeSerialDriver
        {
            IsConnected = true,
            SendResult = _ => false
        };
        using var port = new SerialPort(driver);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => port.SendAsync(new byte[] { 0xAA }));
    }

    // --- SendAsync: convention pass-through ---

    [Fact]
    public async Task SendAsync_PassThroughBytesWithoutInterpretation()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);
        var payload = new byte[] { 0x00, 0x04, 0x01, 0x02, 0x03, 0x04, 0xAA, 0xBB };

        await port.SendAsync(payload);

        Assert.Single(driver.SentMessages);
        Assert.Equal(payload, driver.SentMessages[0]);
    }

    [Fact]
    public async Task SendAsync_LargeFrame_AcceptedAsIs()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);
        var payload = new byte[104];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i & 0xFF);

        await port.SendAsync(payload);

        Assert.Equal(payload, driver.SentMessages[0]);
    }

    // --- PacketReceived: convention pass-through ---

    [Fact]
    public void DriverPacketReceived_WrapsAsRawPacketPassThrough()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);
        RawPacket? received = null;
        port.PacketReceived += (_, p) => received = p;
        var timestamp = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);
        var data = new byte[] { 0x01, 0x00, 0x81, 0x83, 0x08, 0x00, 0xDE, 0xAD };

        driver.RaisePacketReceived(data, timestamp);

        Assert.NotNull(received);
        Assert.Equal(timestamp, received!.Timestamp);
        Assert.Equal(data.Length, received.Length);
        for (int i = 0; i < data.Length; i++)
            Assert.Equal(data[i], received.Payload[i]);
    }

    [Fact]
    public void DriverPacketReceived_NoSubscribers_DoesNotThrow()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);

        driver.RaisePacketReceived(new byte[] { 0xAA }, DateTime.UtcNow);
    }

    // --- StateChanged dagli eventi driver ---

    [Fact]
    public void DriverConnectionStatusChanged_ConnectedToDisconnected_FiresStateChanged()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        driver.RaiseConnectionStatusChanged(false);

        Assert.Equal(ConnectionState.Disconnected, port.State);
        Assert.Equal(new[] { ConnectionState.Disconnected }, stateEvents);
    }

    [Fact]
    public void DriverConnectionStatusChanged_SameState_NoEventEmitted()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        using var port = new SerialPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        driver.RaiseConnectionStatusChanged(true);

        Assert.Empty(stateEvents);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_UnsubscribesFromDriverEvents()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        var port = new SerialPort(driver);
        var packetEvents = new List<RawPacket>();
        port.PacketReceived += (_, p) => packetEvents.Add(p);

        port.Dispose();
        driver.RaisePacketReceived(new byte[] { 0xAA }, DateTime.UtcNow);

        Assert.Empty(packetEvents);
    }

    [Fact]
    public void Dispose_FromConnected_CallsDriverDisconnect()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        var port = new SerialPort(driver);

        port.Dispose();

        Assert.Equal(1, driver.DisconnectCount);
    }

    [Fact]
    public async Task AfterDispose_SendAsync_ThrowsObjectDisposed()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        var port = new SerialPort(driver);
        port.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => port.SendAsync(new byte[] { 0xAA }));
    }

    [Fact]
    public async Task AfterDispose_ConnectAsync_ThrowsObjectDisposed()
    {
        var driver = new FakeSerialDriver { IsConnected = false };
        var port = new SerialPort(driver);
        port.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => port.ConnectAsync());
    }

    [Fact]
    public async Task AfterDispose_DisconnectAsync_ThrowsObjectDisposed()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        var port = new SerialPort(driver);
        port.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => port.DisconnectAsync());
    }

    [Fact]
    public void Dispose_CalledTwice_IsIdempotent()
    {
        var driver = new FakeSerialDriver { IsConnected = true };
        var port = new SerialPort(driver);

        port.Dispose();
        port.Dispose();

        Assert.Equal(1, driver.DisconnectCount);
    }

    [Fact]
    public void Dispose_FromDisconnected_DoesNotCallDriverDisconnect()
    {
        var driver = new FakeSerialDriver { IsConnected = false };
        var port = new SerialPort(driver);

        port.Dispose();

        Assert.Equal(0, driver.DisconnectCount);
    }

    [Fact]
    public void MultipleStateCycles_FireAllTransitionEvents()
    {
        var driver = new FakeSerialDriver { IsConnected = false };
        using var port = new SerialPort(driver);
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
    public async Task ConnectAsync_DriverNotConnected_NoOpStaysDisconnected()
    {
        // L'apertura della porta COM è pilotata dalla UI (menu seriale in Form1):
        // ConnectAsync non deve lanciare né polling se il driver non è ancora attivo.
        // Il port resta Disconnected e si aggiornerà quando
        // ISerialDriver.ConnectionStatusChanged emetterà Connected.
        var driver = new FakeSerialDriver { IsConnected = false };
        using var port = new SerialPort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        await port.ConnectAsync();

        Assert.Equal(ConnectionState.Disconnected, port.State);
        Assert.Empty(stateEvents);
    }
}
