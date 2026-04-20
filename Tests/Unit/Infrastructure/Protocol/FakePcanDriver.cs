using Infrastructure.Protocol.Hardware;

namespace Tests.Unit.Infrastructure.Protocol;

/// <summary>
/// Mock manuale di <see cref="IPcanDriver"/> per i test di <c>CanPort</c>.
/// Registra le chiamate a <see cref="SendMessageAsync"/> e consente di
/// simulare pacchetti ricevuti e cambi di stato.
/// </summary>
internal sealed class FakePcanDriver : IPcanDriver
{
    public bool IsConnected { get; set; }

    public event EventHandler<CANPacketEventArgs>? PacketReceived;
    public event EventHandler<bool>? ConnectionStatusChanged;

    public List<(uint CanId, byte[] Data, bool IsExtended)> SentMessages { get; } = [];
    public Func<uint, byte[], bool, bool> SendResult { get; set; } = (_, _, _) => true;
    public int DisconnectCount { get; private set; }

    public Task<bool> SendMessageAsync(uint canId, byte[] data, bool isExtended)
    {
        SentMessages.Add((canId, data, isExtended));
        return Task.FromResult(SendResult(canId, data, isExtended));
    }

    public void Disconnect()
    {
        DisconnectCount++;
        IsConnected = false;
    }

    /// <summary>Simula la ricezione di un pacchetto CAN dal driver.</summary>
    public void RaisePacketReceived(uint arbitrationId, byte[] data, DateTime timestamp)
    {
        PacketReceived?.Invoke(
            this,
            new CANPacketEventArgs(arbitrationId, data, timestamp));
    }

    /// <summary>Simula un cambio di stato della connessione dal driver.</summary>
    public void RaiseConnectionStatusChanged(bool isConnected)
    {
        IsConnected = isConnected;
        ConnectionStatusChanged?.Invoke(this, isConnected);
    }
}
