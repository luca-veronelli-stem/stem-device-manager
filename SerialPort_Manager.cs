using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SerialPort_Handler
{
    /// <summary>
    /// EventArgs personalizzato per la ricezione di dati dalla porta seriale (byte[]).
    /// </summary>
    public class SerialPacketEventArgs : EventArgs
    {
        public byte[] Data { get; }
        public DateTime Timestamp { get; }

        public SerialPacketEventArgs(byte[] data, DateTime timestamp)
        {
            Data = data;
            Timestamp = timestamp;
        }
    }

    /// <summary>
    /// Gestore della comunicazione seriale via COM per applicazioni WinForms (.NET Framework 4.8+).
    /// Consente di scansionare porte, connettersi, inviare e ricevere dati con eventi.
    /// </summary>
    public class SerialPortManager
    {
        // Eventi
        public event Action<string[]> OnScanCompleted;             // Porte trovate
        public event Action<string> OnConnectionEstablished;       // Porta aperta (nome)
        public event Action OnDisconnected;                        // Porta chiusa
        public event EventHandler<SerialPacketEventArgs> PacketReceived; // Dati ricevuti
        public event EventHandler<bool> ConnectionStatusChanged;   // Stato connessione (true=connesso)

        // Porta seriale in uso
        private SerialPort serialPort;

        // Lista delle porte trovate
        public List<string> AvailablePorts { get; private set; } = new List<string>();

        /// <summary>
        /// Scansiona le porte seriali attive sul PC e aggiorna AvailablePorts.
        /// </summary>
        public void ScanPorts()
        {
            try
            {
                // Ottiene l'elenco dei nomi di porta validi (es. COM1, COM2, ...):contentReference[oaicite:4]{index=4}.
                string[] ports = SerialPort.GetPortNames();
                Array.Sort(ports);
                AvailablePorts.Clear();
                AvailablePorts.AddRange(ports);

                Debug.WriteLine($"Porte seriali trovate: {string.Join(", ", ports)}");

                // Solleva evento di fine scansione (con nomi delle porte)
                OnScanCompleted?.Invoke(ports);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Errore durante la scansione delle porte seriali: {ex.Message}",
                                "Errore Porta Seriale", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Connette alla porta seriale specificata, configurando i parametri di comunicazione.
        /// </summary>
        /// <param name="portName">Nome della porta (es. \"COM3\").</param>
        /// <param name="baudRate">Baud rate (es. 9600).</param>
        /// <param name="parity">Parità (None, Even, Odd, ecc.).</param>
        /// <param name="dataBits">Numero di bit di dati (es. 8).</param>
        /// <param name="stopBits">Bit di stop (One, Two, ecc.).</param>
        /// <param name="handshake">Handshaking (None, XOnXOff, ecc.).</param>
        public void Connect(string portName, 
                            int baudRate = 115200,
                            Parity parity = Parity.None, 
                            int dataBits = 8,
                            StopBits stopBits = StopBits.One, 
                            Handshake handshake = Handshake.None)
        {
            try
            {
                // Verifica se la porta è nella lista trovata
                if (!AvailablePorts.Contains(portName))
                {
                    MessageBox.Show($"Porta seriale non trovata: {portName}",
                                    "Errore Connessione", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Se c'era una porta aperta, la chiude prima
                if (serialPort != null && serialPort.IsOpen)
                {
                    serialPort.Close();
                    ConnectionStatusChanged?.Invoke(this, false);
                }

                // Crea e configura la porta seriale
                serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
                serialPort.Handshake = handshake;
                serialPort.DtrEnable = true; // (opzionale, in base all'hardware)

                // Sottoscrive l'evento di ricezione dati
                serialPort.DataReceived += SerialPort_DataReceived;
                serialPort.ErrorReceived += SerialPort_ErrorReceived;

                // Apre la porta seriale
                serialPort.Open();

                // Notifica connessione riuscita
                Debug.WriteLine($"Connesso alla porta seriale {portName}");
                OnConnectionEstablished?.Invoke(portName);
                ConnectionStatusChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Impossibile connettersi alla porta seriale {portName}: {ex.Message}",
                                "Errore Connessione", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ConnectionStatusChanged?.Invoke(this, false);
            }
        }

        /// <summary>
        /// Handler dell'evento DataReceived di SerialPort.
        /// Legge i dati disponibili e solleva PacketReceived.
        /// </summary>
        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // Legge i dati presenti nel buffer
                int bytesToRead = serialPort.BytesToRead;
                byte[] buffer = new byte[bytesToRead];
                int bytesRead = serialPort.Read(buffer, 0, bytesToRead);

                // Crea e solleva l'evento per i dati ricevuti
                var args = new SerialPacketEventArgs(buffer, DateTime.Now);
                Debug.WriteLine($"Dati seriali ricevuti: {bytesRead} byte");
                PacketReceived?.Invoke(this, args);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore nella lettura dei dati seriali: {ex.Message}");
            }
        }

        /// <summary>
        /// Handler dell'evento ErrorReceived di SerialPort.
        /// In caso di errore (frame, overrun, ecc.), disconnette.
        /// </summary>
        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Debug.WriteLine($"Errore seriale ricevuto: {e.EventType}");
            // Opzionalmente gestire errori specifici
            Disconnect();
        }

        /// <summary>
        /// Invia dati (byte[]) alla porta seriale aperta (sincrono).
        /// </summary>
        /// <param name="data">Array di byte da inviare.</param>
        /// <returns>True se inviato correttamente, False altrimenti.</returns>
        public bool SendMessage(byte[] data)
        {
            if (serialPort == null || !serialPort.IsOpen)
            {
                Debug.WriteLine("Impossibile inviare: porta seriale non connessa.");
                return false;
            }

            try
            {
                serialPort.Write(data, 0, data.Length);
                Debug.WriteLine($"Invio dati seriali riuscito: {data.Length} byte");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore durante l'invio seriale: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Invia dati (byte[]) alla porta seriale aperta (asincrono).
        /// </summary>
        /// <param name="data">Array di byte da inviare.</param>
        public Task<bool> SendMessageAsync(byte[] data)
        {
            return Task.Run(() => SendMessage(data));
        }

        /// <summary>
        /// Chiude la connessione seriale attiva.
        /// </summary>
        public void Disconnect()
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                {
                    // Rimuove gestori eventi
                    serialPort.DataReceived -= SerialPort_DataReceived;
                    serialPort.ErrorReceived -= SerialPort_ErrorReceived;

                    serialPort.Close();
                    Debug.WriteLine("Porta seriale chiusa con successo");
                    ConnectionStatusChanged?.Invoke(this, false);
                    OnDisconnected?.Invoke();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Errore durante la disconnessione seriale: {ex.Message}");
            }
        }

        /// <summary>
        /// Proprietà di sola lettura: porta seriale attualmente connessa.
        /// </summary>
        public bool IsConnected
        {
            get
            {
                return (serialPort != null && serialPort.IsOpen);
            }
        }
    }
}
