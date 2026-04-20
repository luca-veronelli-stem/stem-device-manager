namespace Infrastructure.Protocol.Hardware;

/// <summary>
/// Astrazione minima sul driver BLE per consentire i test di
/// <see cref="BlePort"/> senza dipendere da <c>Plugin.BLE</c> o hardware reale.
/// In produzione è implementata da <c>App.BLEManager</c>.
/// </summary>
public interface IBleDriver
{
    /// <summary>True se il dispositivo BLE è attualmente connesso.</summary>
    bool IsConnected { get; }

    /// <summary>Pacchetto BLE ricevuto dalla caratteristica Nordic UART TX.</summary>
    event EventHandler<BlePacketEventArgs>? PacketReceived;

    /// <summary>
    /// Cambio di stato della connessione (connesso/disconnesso).
    /// In produzione emesso anche su eventi Bluetooth globali
    /// (bluetooth off, device out of range, ecc.).
    /// </summary>
    event EventHandler<bool>? ConnectionStatusChanged;

    /// <summary>
    /// Scrive un frame sulla caratteristica Nordic UART RX.
    /// Ritorna <c>true</c> in caso di successo.
    /// </summary>
    Task<bool> SendMessageAsync(byte[] data);

    /// <summary>Disconnette dal dispositivo BLE corrente.</summary>
    Task DisconnectAsync();
}
