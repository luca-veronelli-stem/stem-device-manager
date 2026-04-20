namespace Infrastructure.Protocol.Hardware;

/// <summary>
/// Astrazione minima sul driver seriale (COM) per consentire i test di
/// <see cref="SerialPort"/> senza aprire una porta hardware reale.
/// In produzione è implementata da <c>App.SerialPortManager</c>.
/// </summary>
public interface ISerialDriver
{
    /// <summary>True se la porta seriale è aperta e operativa.</summary>
    bool IsConnected { get; }

    /// <summary>Pacchetto seriale ricevuto dal buffer della porta.</summary>
    event EventHandler<SerialPacketEventArgs>? PacketReceived;

    /// <summary>
    /// Cambio di stato della connessione (connesso/disconnesso).
    /// Emesso anche in caso di errore di linea (frame, overrun) che causa
    /// una disconnessione automatica.
    /// </summary>
    event EventHandler<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Scrive un frame sulla porta seriale. Ritorna <c>true</c> se tutti i
    /// byte sono stati consegnati al buffer OS.
    /// </summary>
    Task<bool> SendMessageAsync(byte[] data);

    /// <summary>Chiude la porta seriale.</summary>
    void Disconnect();
}
