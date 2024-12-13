using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocumentFormat.OpenXml.Wordprocessing;
using Peak.Can.Basic;
using Peak.Can.Basic.BackwardCompatibility;
using StemPC;
using TPCANHandle = System.Byte;
using PS_PacketManager;

// Classe per la gestione degli eventi di ricezione pacchetti
public class CANPacketEventArgs : EventArgs
{
    public uint ArbitrationId { get; }
    public byte[] Data { get; }
    public DateTime Timestamp { get; }

    public CANPacketEventArgs(uint arbitrationId, byte[] data, DateTime timestamp)
    {
        ArbitrationId = arbitrationId;
        Data = data;
        Timestamp = timestamp;
    }
}

// Classe per la gestione del dispositivo PCAN
public class PCANManager
{
    private const TPCANHandle Channel = 0x51; // PCAN_USB
    private TPCANBaudrate _currentBaudRate;
    private bool _isConnected;

    // Evento per lo stato della connessione
    public event EventHandler<bool> ConnectionStatusChanged;
    public event EventHandler<CANPacketEventArgs> PacketReceived;
    public event EventHandler<string> ErrorOccurred;

    public bool IsConnected
    {
        get => _isConnected;
        private set
        {
            if (_isConnected != value)
            {
                _isConnected = value;
                ConnectionStatusChanged?.Invoke(this, _isConnected);
            }
        }
    }

    public PCANManager(TPCANBaudrate initialBaudRate = TPCANBaudrate.PCAN_BAUD_100K)
    {
        _currentBaudRate = initialBaudRate;
        Initialize();
        StartConnectionMonitoring();
    }

    public bool Initialize(TPCANBaudrate? baudRate = null)
    {
        if (baudRate.HasValue)
            _currentBaudRate = baudRate.Value;

        var result = PCANBasic.Initialize(Channel, _currentBaudRate);
        IsConnected = result == TPCANStatus.PCAN_ERROR_OK;

        return IsConnected;
    }

    private void StartConnectionMonitoring()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    var status = PCANBasic.GetStatus(Channel);

                    if (status != TPCANStatus.PCAN_ERROR_OK)
                    {
                        // Perdita di connessione
                        if (IsConnected)
                        {
                            IsConnected = false;
                            ErrorOccurred?.Invoke(this, "Connessione persa. Tentativo di riconnessione...");
                        }

                        // Tentativo di riconnessione
                        var reconnectResult = PCANBasic.Initialize(Channel, _currentBaudRate);
                        IsConnected = reconnectResult == TPCANStatus.PCAN_ERROR_OK;

                        if (!IsConnected)
                        {
                            await Task.Delay(1000); // Attesa prima del prossimo tentativo
                        }
                    }
                    else
                    {
                        // Assicura che lo stato sia correttamente impostato se la connessione è attiva
                        if (!IsConnected)
                        {
                            IsConnected = true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorOccurred?.Invoke(this, $"Errore nel monitoraggio della connessione: {ex.Message}");
                }

                await Task.Delay(1000); // Controllo dello stato ogni secondo
            }
        });
    }

    public bool SendMessage(uint canId, byte[] data, bool isExtended = false)
    {
        if (!_isConnected)
            return false;

        var canMessage = new TPCANMsg
        {
            ID = canId,
            LEN = (byte)Math.Min(data.Length, 8),
            MSGTYPE = isExtended ? TPCANMessageType.PCAN_MESSAGE_EXTENDED : TPCANMessageType.PCAN_MESSAGE_STANDARD,
            DATA = new byte[8]
        };

        Array.Copy(data, canMessage.DATA, canMessage.LEN);

        var result = PCANBasic.Write(Channel, ref canMessage);
        return result == TPCANStatus.PCAN_ERROR_OK;
    }

    public void StartReading()
    {
        Task.Run(ReadCANMessagesAsync);
    }

    private async Task ReadCANMessagesAsync()
    {
        while (_isConnected)
        {
            try
            {
                TPCANMsg canMessage;
                TPCANTimestamp canTimestamp;
                var result = PCANBasic.Read(Channel, out canMessage, out canTimestamp);

                if (result == TPCANStatus.PCAN_ERROR_OK)
                {
                    var receivedData = canMessage.DATA.Take(canMessage.LEN).ToArray();
                    var packetEvent = new CANPacketEventArgs(
                        canMessage.ID,
                        receivedData,
                        DateTime.Now
                    );

                    PacketReceived?.Invoke(this, packetEvent);
                }
                else if (result != TPCANStatus.PCAN_ERROR_QRCVEMPTY)
                {
                    // Gestione errori
                    StringBuilder errorTextB = new StringBuilder(256);
                    PCANBasic.GetErrorText(result, 0, errorTextB);
                }

                await Task.Delay(50); // Riduzione carico CPU
            }
            catch (Exception ex)
            {
                // Log dell'eccezione
                Console.WriteLine($"Errore durante la lettura: {ex.Message}");
            }
        }
    }

    public void Disconnect()
    {
        PCANBasic.Uninitialize(Channel);
        _isConnected = false;
    }
}

