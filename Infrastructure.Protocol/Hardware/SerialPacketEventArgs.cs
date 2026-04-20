namespace Infrastructure.Protocol.Hardware;

/// <summary>
/// Event args per un pacchetto seriale ricevuto. Rimpiazza il tipo omonimo
/// precedentemente definito in <c>App/SerialPort_Manager.cs</c>.
/// </summary>
public sealed class SerialPacketEventArgs : EventArgs
{
    public byte[] Data { get; }
    public DateTime Timestamp { get; }

    public SerialPacketEventArgs(byte[] data, DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(data);
        Data = data;
        Timestamp = timestamp;
    }
}
