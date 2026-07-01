namespace Infrastructure.Protocol.Hardware;

/// <summary>
/// Astrazione minima sul driver PCAN, estratta per consentire i test
/// di <see cref="CanPort"/> senza dipendere dalle DLL native Peak.
/// Implementata da <see cref="PCANManager"/> in produzione.
/// </summary>
public interface IPcanDriver
{
    /// <summary>True se il canale CAN è aperto e operativo.</summary>
    bool IsConnected { get; }

    /// <summary>Pacchetto CAN ricevuto dal bus.</summary>
    event EventHandler<CANPacketEventArgs>? PacketReceived;

    /// <summary>Cambio di stato della connessione al driver PCAN.</summary>
    event EventHandler<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Invia un messaggio CAN. Ritorna <c>true</c> se PCAN riporta successo.
    /// </summary>
    Task<bool> SendMessageAsync(uint canId, byte[] data, bool isExtended);

    /// <summary>
    /// Reconfigures the CAN channel bitrate at runtime.
    /// </summary>
    /// <param name="baudRateKbps">Bus bitrate in kbit/s (100, 125, 250, or 500).</param>
    /// <returns><c>true</c> if the channel was reinitialized at the new bitrate.</returns>
    bool ChangeBaudRate(int baudRateKbps);

    /// <summary>Chiude il canale PCAN.</summary>
    void Disconnect();
}
