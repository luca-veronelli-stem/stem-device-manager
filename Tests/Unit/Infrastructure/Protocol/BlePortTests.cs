using Core.Models;
using Infrastructure.Protocol.Hardware;

namespace Tests.Unit.Infrastructure.Protocol;

/// <summary>
/// Test per <see cref="BlePort"/>: verifica state machine, convention
/// pass-through (no prefisso arbId) e forwarding degli eventi del driver
/// sottostante.
/// </summary>
public class BlePortTests
{
    // --- Ctor + stato iniziale ---

    [Fact]
    public void Ctor_DriverDisconnected_InitialStateIsDisconnected()
    {
        var driver = new FakeBleDriver { IsConnected = false };

        using var port = new BlePort(driver);

        Assert.Equal(ConnectionState.Disconnected, port.State);
        Assert.False(port.IsConnected);
    }

    [Fact]
    public void Ctor_DriverConnected_InitialStateIsConnected()
    {
        var driver = new FakeBleDriver { IsConnected = true };

        using var port = new BlePort(driver);

        Assert.Equal(ConnectionState.Connected, port.State);
        Assert.True(port.IsConnected);
    }

    [Fact]
    public void Ctor_NullDriver_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BlePort(null!));
    }

    // --- ConnectAsync ---

    [Fact]
    public async Task ConnectAsync_AlreadyConnected_NoStateChange()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        await port.ConnectAsync();

        Assert.Equal(ConnectionState.Connected, port.State);
        Assert.Empty(stateEvents);
    }

    [Fact]
    public async Task ConnectAsync_DriverBecomesConnected_TransitionsViaConnecting()
    {
        var driver = new FakeBleDriver { IsConnected = false };
        using var port = new BlePort(driver);
        driver.IsConnected = true;
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
        var driver = new FakeBleDriver { IsConnected = false };
        using var port = new BlePort(driver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => port.ConnectAsync(cts.Token));
    }

    // --- DisconnectAsync ---

    [Fact]
    public async Task DisconnectAsync_FromConnected_CallsDriverDisconnect()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);
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
        var driver = new FakeBleDriver { IsConnected = false };
        using var port = new BlePort(driver);

        await port.DisconnectAsync();

        Assert.Equal(0, driver.DisconnectCount);
    }

    // --- SendAsync: validazione input ---

    [Fact]
    public async Task SendAsync_NotConnected_ThrowsInvalidOperation()
    {
        var driver = new FakeBleDriver { IsConnected = false };
        using var port = new BlePort(driver);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => port.SendAsync(new byte[] { 0xAA }));
        Assert.Empty(driver.SentMessages);
    }

    [Fact]
    public async Task SendAsync_EmptyPayload_ThrowsArgument()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);

        await Assert.ThrowsAsync<ArgumentException>(
            () => port.SendAsync(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public async Task SendAsync_CancellationRequested_Throws()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => port.SendAsync(new byte[] { 0xAA }, cts.Token));
    }

    [Fact]
    public async Task SendAsync_DriverReportsFailure_ThrowsInvalidOperation()
    {
        var driver = new FakeBleDriver
        {
            IsConnected = true,
            SendResult = _ => false
        };
        using var port = new BlePort(driver);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => port.SendAsync(new byte[] { 0xAA }));
    }

    // --- SendAsync: convention pass-through ---

    [Fact]
    public async Task SendAsync_PassThroughBytesWithoutInterpretation()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);
        var payload = new byte[] { 0x00, 0x04, 0x01, 0x02, 0x03, 0x04, 0xAA, 0xBB, 0xCC };

        await port.SendAsync(payload);

        Assert.Single(driver.SentMessages);
        Assert.Equal(payload, driver.SentMessages[0]);
    }

    [Fact]
    public async Task SendAsync_LargeFrame_AcceptedAsIs()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);
        // BLE chunk max = 98 byte dati + 6 header = 104 byte tipici
        var payload = new byte[104];
        for (int i = 0; i < payload.Length; i++) payload[i] = (byte)(i & 0xFF);

        await port.SendAsync(payload);

        Assert.Equal(payload, driver.SentMessages[0]);
    }

    // --- PacketReceived: convention pass-through ---

    [Fact]
    public void DriverPacketReceived_WrapsAsRawPacketPassThrough()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);
        RawPacket? received = null;
        port.PacketReceived += (_, p) => received = p;
        var timestamp = new DateTime(2026, 4, 20, 12, 34, 56, DateTimeKind.Utc);
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
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);

        driver.RaisePacketReceived(new byte[] { 0xAA }, DateTime.UtcNow);
    }

    // --- StateChanged dagli eventi driver ---

    [Fact]
    public void DriverConnectionStatusChanged_ConnectedToDisconnected_FiresStateChanged()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        driver.RaiseConnectionStatusChanged(false);

        Assert.Equal(ConnectionState.Disconnected, port.State);
        Assert.Equal(new[] { ConnectionState.Disconnected }, stateEvents);
    }

    [Fact]
    public void DriverConnectionStatusChanged_SameState_NoEventEmitted()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        using var port = new BlePort(driver);
        var stateEvents = new List<ConnectionState>();
        port.StateChanged += (_, s) => stateEvents.Add(s);

        driver.RaiseConnectionStatusChanged(true);

        Assert.Empty(stateEvents);
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_UnsubscribesFromDriverEvents()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        var port = new BlePort(driver);
        var packetEvents = new List<RawPacket>();
        port.PacketReceived += (_, p) => packetEvents.Add(p);

        port.Dispose();
        driver.RaisePacketReceived(new byte[] { 0xAA }, DateTime.UtcNow);

        Assert.Empty(packetEvents);
    }

    [Fact]
    public void Dispose_FromConnected_CallsDriverDisconnect()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        var port = new BlePort(driver);

        port.Dispose();

        Assert.Equal(1, driver.DisconnectCount);
    }

    [Fact]
    public async Task AfterDispose_SendAsync_ThrowsObjectDisposed()
    {
        var driver = new FakeBleDriver { IsConnected = true };
        var port = new BlePort(driver);
        port.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => port.SendAsync(new byte[] { 0xAA }));
    }
}
