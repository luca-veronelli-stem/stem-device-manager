using Infrastructure.Protocol.Hardware;

namespace Tests.Unit.Infrastructure.Protocol;

/// <summary>
/// Mock manuale di <see cref="IBleDriver"/> per i test di <c>BlePort</c>.
/// Registra le chiamate a <see cref="SendMessageAsync"/> e consente di
/// simulare pacchetti ricevuti e cambi di stato.
/// </summary>
internal sealed class FakeBleDriver : IBleDriver
{
    public bool IsConnected { get; set; }

    public event EventHandler<BlePacketEventArgs>? PacketReceived;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public List<byte[]> SentMessages { get; } = [];
    public Func<byte[], bool> SendResult { get; set; } = _ => true;
    public int DisconnectCount { get; private set; }

    public Task<bool> SendMessageAsync(byte[] data)
    {
        SentMessages.Add(data);
        return Task.FromResult(SendResult(data));
    }

    public Task DisconnectAsync()
    {
        DisconnectCount++;
        IsConnected = false;
        return Task.CompletedTask;
    }

    /// <summary>Simula la ricezione di un pacchetto BLE dal driver.</summary>
    public void RaisePacketReceived(byte[] data, DateTime timestamp)
    {
        PacketReceived?.Invoke(this, new BlePacketEventArgs(data, timestamp));
    }

    /// <summary>Simula un cambio di stato della connessione dal driver.</summary>
    public void RaiseConnectionStatusChanged(bool isConnected)
    {
        IsConnected = isConnected;
        ConnectionStatusChanged?.Invoke(this, isConnected);
    }
}
