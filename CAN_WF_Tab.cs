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


// Classe per l'interfaccia grafica
public partial class CANInterfaceTab : TabPage
{
    private PCANManager _pcanManager;
    private ListView _receivedMessagesView;
    private System.Windows.Forms.Label _connectionStatusLabel;

    private ComboBox baudRatePicker;
    private Button sendButton;
    private TextBox canIdEntry;
    private TextBox dataEntry;
    private const TPCANHandle Channel = 0x51; // PCAN_USB
    private TPCANBaudrate BaudRate;

  //  private ObservableCollection<CanMessagePCAN> messages = new ObservableCollection<CanMessagePCAN>();

    public PacketManager RXpacketManager;

    public CANInterfaceTab()
    {
        InitializeComponents();
     //   RXpacketManager = new PacketManager(Form1.FormRef.senderId);
        RXpacketManager = new PacketManager(0xFFFFFFFF);
        RXpacketManager.RegisterPacketReadyEvent(Form1.FormRef.DecodeCommandSP);
        InitializePCANManager();
    }

    private void InitializePCANManager()
    {
        _pcanManager = new PCANManager();

        // Sottoscrizione agli eventi
        _pcanManager.PacketReceived += OnPacketReceived;
        _pcanManager.ConnectionStatusChanged += OnConnectionStatusChanged;
        _pcanManager.ErrorOccurred += OnErrorOccurred;

        UpdateConnectionStatus(_pcanManager.IsConnected); //primo update asincrono della label, poi si avvia il pcan
        if (_pcanManager.IsConnected)
        {
            _pcanManager.StartReading();
        }
    }

    private void UpdateConnectionStatus(bool isConnected)
    {
        if (isConnected)
        {
            _connectionStatusLabel.Text = "Stato: Connesso";
            _connectionStatusLabel.ForeColor = System.Drawing.Color.Green;
        }
        else
        {
            _connectionStatusLabel.Text = "Stato: Disconnesso";
            _connectionStatusLabel.ForeColor = System.Drawing.Color.Red;
        }
    }

    private void OnErrorOccurred(object sender, string errorMessage)
    {
        // Metodo thread-safe per gestire gli errori
        if (_receivedMessagesView.InvokeRequired)
        {
            _receivedMessagesView.Invoke(new Action(() => LogError(errorMessage)));
        }
        else
        {
            LogError(errorMessage);
        }
    }

    private void LogError(string errorMessage)
    {
        var errorItem = new ListViewItem(errorMessage)
        {
            ForeColor = System.Drawing.Color.Red
        };
        _receivedMessagesView.Items.Add(errorItem);
        _receivedMessagesView.EnsureVisible(_receivedMessagesView.Items.Count - 1);
    }

    private void OnConnectionStatusChanged(object sender, bool isConnected)
    {
        // Metodo thread-safe per aggiornare lo stato della connessione
        if (_connectionStatusLabel.InvokeRequired)
        {
            _connectionStatusLabel.Invoke(new Action(() => UpdateConnectionStatus(isConnected)));
        }
        else
        {
            UpdateConnectionStatus(isConnected);
        }
    }



    private void OnPacketReceived(object sender, CANPacketEventArgs e)
    {
        // Metodo thread-safe per aggiornare l'interfaccia
        if (_receivedMessagesView.InvokeRequired)
        {
            _receivedMessagesView.Invoke(new Action(() => UpdateMessageList(e)));
        }
        else
        {
            UpdateMessageList(e);
        }
    }

    private void UpdateMessageList(CANPacketEventArgs e)
    {
        string dataString = string.Join(" ", e.Data.Select(b => b.ToString("X2")));
        var listViewItem = new ListViewItem(
            $"{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff} - RX: ID=0x{e.ArbitrationId:X} Dati={dataString}")
        {
            ForeColor = System.Drawing.Color.Blue
        };

        _receivedMessagesView.Items.Add(listViewItem);
        _receivedMessagesView.EnsureVisible(_receivedMessagesView.Items.Count - 1);

        //aggiungi i messaggi alla coda del network layer
        CANMessage RxMessage = new CANMessage(e.ArbitrationId, e.Data, false);
        RXpacketManager.ProcessCANPacket(RxMessage);
        

        //// stampa il pacchetto dell'application layer
        //richTextBoxTx.AppendText("-- APPLICATION --\n");
        //richTextBoxTx.AppendText($"{string.Join(" ", networkLayer.ApplicationPacket.Select(b => b.ToString("X2")))}\n");

        //// stampa il pacchetto del transport layer
        //richTextBoxTx.AppendText("-- TRANSPORT --\n");
        //richTextBoxTx.AppendText($"{string.Join(" ", networkLayer.TransportPacket.Select(b => b.ToString("X2")))}\n");

        //// stampa i pacchetti del network layer
        //richTextBoxTx.AppendText("-- NETWORK --\n");
        //foreach (var item in networkLayer.NetworkPackets)
        //{
        //    // _netInfo, _recipientId, chunk
        //    richTextBoxTx.AppendText($"NetInfo: {string.Join(" ", item.Item1.Select(b => b.ToString("X2")))} ");
        //    richTextBoxTx.AppendText($"Id: {item.Item2.ToString("X2")} ");
        //    richTextBoxTx.AppendText($"Chunk: {string.Join(" ", item.Item3.Select(b => b.ToString("X2")))}\n");
        //}

    }

    private void InitializeComponents()
    {
        // Codice simile all'implementazione originale...
        // Implementare l'inizializzazione dei controlli
        this.Text = "CAN Interface";

        // Inizializzazione dei controlli
        this.baudRatePicker = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        this.baudRatePicker.Items.AddRange(new string[] { "100 kbps", "250 kbps", "500 kbps", "1 Mbps" });
        this.baudRatePicker.SelectedIndex = 0;
        this.baudRatePicker.SelectedIndexChanged += BaudRatePicker_SelectedIndexChanged;

        this.canIdEntry = new TextBox { PlaceholderText = "CAN ID (Hex)", Dock = DockStyle.Fill };
        this.dataEntry = new TextBox { PlaceholderText = "Dati (byte separati da spazio)", Dock = DockStyle.Fill };

        this.sendButton = new Button { Text = "Invia", Dock = DockStyle.Fill };
        this.sendButton.Click += OnSendClicked;

        _receivedMessagesView = new ListView
        {
            Dock = DockStyle.Fill,
            View = System.Windows.Forms.View.Details,
            FullRowSelect = true
        };
        _receivedMessagesView.Columns.Add("Messaggi", -2, HorizontalAlignment.Left);

        _connectionStatusLabel = new System.Windows.Forms.Label()
        {
            Text = "Stato: In attesa",
            ForeColor = System.Drawing.Color.Orange,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        // Layout flessibile
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7 // 7 righe totali
        };

        // Configurazione delle righe con proporzioni corrette
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Velocità (Label)
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Velocità (ComboBox)
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // CAN ID (TextBox)
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35)); // Dati (TextBox)
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Pulsante Invia (Button)
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25)); // Stato connessione (Label)
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Lista messaggi (ListBox)

        // Aggiunta dei controlli al layout
        layout.Controls.Add(new System.Windows.Forms.Label
        {
            Text = "Velocità:",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        }, 0, 0);
        layout.Controls.Add(this.baudRatePicker, 0, 1);
        layout.Controls.Add(this.canIdEntry, 0, 2);
        layout.Controls.Add(this.dataEntry, 0, 3);
        layout.Controls.Add(this.sendButton, 0, 4);
        layout.Controls.Add(this._connectionStatusLabel, 0, 5);
        layout.Controls.Add(this._receivedMessagesView, 0, 6);

        this.Controls.Add(layout);
    }

    private void BaudRatePicker_SelectedIndexChanged(object sender, EventArgs e)
    {
        switch (baudRatePicker.SelectedIndex)
        {
            case 0: BaudRate = TPCANBaudrate.PCAN_BAUD_100K; break;
            case 1: BaudRate = TPCANBaudrate.PCAN_BAUD_250K; break;
            case 2: BaudRate = TPCANBaudrate.PCAN_BAUD_500K; break;
            case 3: BaudRate = TPCANBaudrate.PCAN_BAUD_1M; break;
        }
        MessageBox.Show($"Velocità CAN impostata su {baudRatePicker.SelectedItem}");
    }

    private void OnSendClicked(object sender, EventArgs e)
    {
        // Controlla se il dispositivo è connesso
        if (!_pcanManager.IsConnected)
        {
            MessageBox.Show("PCAN non connesso!", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            // Leggi il CAN ID e i dati
            string canIdText = canIdEntry.Text.Trim();
            string dataText = dataEntry.Text.Trim();

            if (string.IsNullOrEmpty(canIdText) || string.IsNullOrEmpty(dataText))
            {
                MessageBox.Show("Inserire un CAN ID e i dati!", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Crea un messaggio CAN
            TPCANMsg canMessage = new TPCANMsg
            {
                ID = Convert.ToUInt32(canIdText, 16),
                LEN = (byte)dataText.Split(' ').Length,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD,
                DATA = new byte[8]
            };

            // Popola i dati nel messaggio
            var dataBytes = dataText.Split(' ').Select(byte.Parse).ToArray();
            for (int i = 0; i < canMessage.LEN && i < 8; i++)
            {
                canMessage.DATA[i] = dataBytes[i];
            }

            // Invia il messaggio
            var result = PCANBasic.Write(Channel, ref canMessage);

            if (result == TPCANStatus.PCAN_ERROR_OK)
            {
                // Ottieni il timestamp
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                // Aggiungi il messaggio al ListView con colore verde
                var listViewItem = new ListViewItem($"{timestamp} - TX: ID=0x{canMessage.ID:X} Dati={dataText}")
                {
                    ForeColor = System.Drawing.Color.Green
                };
                _receivedMessagesView.Items.Add(listViewItem);
                _receivedMessagesView.EnsureVisible(_receivedMessagesView.Items.Count - 1); // Scrolla all'ultimo messaggio
            }
            else
            {
                MessageBox.Show($"Errore durante l'invio: {result}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nel formato dei dati: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public void SendCANMessage(uint CANID, byte[] data)
    {
        // Controlla se il dispositivo è connesso
        if (!_pcanManager.IsConnected)
        {
            MessageBox.Show("PCAN non connesso!", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            //// Leggi il CAN ID e i dati
            //string canIdText = canIdEntry.Text.Trim();
            //string dataText = dataEntry.Text.Trim();

            //if (string.IsNullOrEmpty(canIdText) || string.IsNullOrEmpty(dataText))
            //{
            //    MessageBox.Show("Inserire un CAN ID e i dati!", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    return;
            //}

            // Crea un messaggio CAN
            TPCANMsg canMessage = new TPCANMsg
            {
                ID = CANID,
                LEN = (byte)data.Length,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_EXTENDED,
                DATA = new byte[8]
            };

            // Popola i dati nel messaggio
            //  var dataBytes = dataText.Split(' ').Select(byte.Parse).ToArray();
            for (int i = 0; i < canMessage.LEN && i < 8; i++)
            {
                canMessage.DATA[i] = data[i];
            }

            // Invia il messaggio
            var result = PCANBasic.Write(Channel, ref canMessage);

            if (result == TPCANStatus.PCAN_ERROR_OK)
            {
                // Ottieni il timestamp
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                string hexString = string.Join(" ", canMessage.DATA.Select(b => b.ToString("X2")));


                // Form1.FormRef.UpdateTerminal($"{timestamp} - TX: ID=0x{canMessage.ID:X} Dati={hexString}");

                // Aggiungi il messaggio al ListView con colore verde
                var listViewItem = new ListViewItem($"{timestamp} - TX: ID=0x{canMessage.ID:X} Dati={hexString}")
                {
                    ForeColor = System.Drawing.Color.Green
                };
                _receivedMessagesView.Items.Add(listViewItem);
                _receivedMessagesView.EnsureVisible(_receivedMessagesView.Items.Count - 1); // Scrolla all'ultimo messaggio
            }
            else
            {
                MessageBox.Show($"Errore durante l'invio: {result}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nel formato dei dati: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
