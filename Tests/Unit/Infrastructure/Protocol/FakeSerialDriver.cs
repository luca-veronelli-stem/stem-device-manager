using Infrastructure.Protocol.Hardware;

namespace Tests.Unit.Infrastructure.Protocol;

/// <summary>
/// Mock manuale di <see cref="ISerialDriver"/> per i test di
/// <c>Infrastructure.Protocol.Hardware.SerialPort</c>.
/// </summary>
internal sealed class FakeSerialDriver : ISerialDriver
{
    public bool IsConnected { get; set; }

    public event EventHandler<SerialPacketEventArgs>? PacketReceived;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public List<byte[]> SentMessages { get; } = [];
    public Func<byte[], bool> SendResult { get; set; } = _ => true;
    public int DisconnectCount { get; private set; }

    public Task<bool> SendMessageAsync(byte[] data)
    {
        SentMessages.Add(data);
        return Task.FromResult(SendResult(data));
    }

    public void Disconnect()
    {
        DisconnectCount++;
        IsConnected = false;
    }

    /// <summary>Simula la ricezione di un pacchetto seriale dal driver.</summary>
    public void RaisePacketReceived(byte[] data, DateTime timestamp)
    {
        PacketReceived?.Invoke(this, new SerialPacketEventArgs(data, timestamp));
    }

    /// <summary>Simula un cambio di stato della connessione dal driver.</summary>
    public void RaiseConnectionStatusChanged(bool isConnected)
    {
        IsConnected = isConnected;
        ConnectionStatusChanged?.Invoke(this, isConnected);
    }
}
