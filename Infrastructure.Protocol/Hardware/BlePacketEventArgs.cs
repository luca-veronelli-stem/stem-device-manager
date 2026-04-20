namespace Infrastructure.Protocol.Hardware;

/// <summary>
/// Event args per un pacchetto BLE ricevuto. Rimpiazza il tipo omonimo
/// precedentemente definito in <c>App/BLE_Manager.cs</c>.
/// </summary>
public sealed class BlePacketEventArgs : EventArgs
{
    public byte[] Data { get; }
    public DateTime Timestamp { get; }

    public BlePacketEventArgs(byte[] data, DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
        Timestamp = timestamp;
    }
}
