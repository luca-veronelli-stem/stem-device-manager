using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using DocumentFormat.OpenXml.Wordprocessing;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

using CanDataLayer;
using Peak.Can.Basic.BackwardCompatibility;
using System.Runtime.CompilerServices;

// Classe per l'interfaccia grafica
public partial class CANInterfaceTab : TabPage
{
    private CANDataLayer _canHandler;
    private ListView _receivedMessagesView;
    private System.Windows.Forms.Label _connectionStatusLabel;

    private ComboBox    baudRatePicker;
    private Button      sendButton;
    private TextBox     canIdEntry;
    private TextBox     dataEntry;

  //  private const TPCANHandle   Channel = 0x51; // PCAN_USB
  //  private TPCANBaudrate       BaudRate;

    int BaudRate;

    public static CANInterfaceTab thisRef { get; private set; }

  //  public PacketManager PS_CAN_PacketManager;

    public CANInterfaceTab(CANDataLayer canHandler)
    {
        thisRef = this;

        _canHandler = canHandler;
        BaudRate = canHandler.Bitrate;

        InitializeComponents();

        //_pcanManager.ErrorOccurred += OnErrorOccurred;

        //primo update asincrono della label...
        UpdateConnectionStatus(_canHandler.IsConnected);
    }

    public void ActivateEvents()
    {
        // Sottoscrizione agli eventi
        _canHandler.PacketReceived += OnPacketReceived;
        _canHandler.ConnectionStatusChanged += OnConnectionStatusChanged;
        _canHandler.PacketSended += OnPacketSended;
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

    private void OnPacketReceived(object sender, CANMessage RxPacket)
    {
        // Metodo thread-safe per aggiornare l'interfaccia
        if (_receivedMessagesView.InvokeRequired)
        {
            _receivedMessagesView.Invoke(new Action(() => UpdateMessageList(RxPacket)));
        }
        else
        {
            UpdateMessageList(RxPacket);
        }
    }

    private void UpdateMessageList(CANMessage RxPacket)
    {
        //debug dei dati ricevuti sulla finestra in uscita (blu)
        string dataString = string.Join(" ", RxPacket.Data.Select(b => b.ToString("X2")));
        var listViewItem = new ListViewItem(
            $"{RxPacket.Timestamp:yyyy-MM-dd HH:mm:ss.fff} - RX: ID=0x{RxPacket.ArbitrationId:X} Dati={dataString}")
        {
            ForeColor = System.Drawing.Color.Blue
        };

        _receivedMessagesView.Items.Add(listViewItem);
        _receivedMessagesView.EnsureVisible(_receivedMessagesView.Items.Count - 1);
    }

    //WIN FORMS GRAPICHS SECTION

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
            //case 0: BaudRate = TPCANBaudrate.PCAN_BAUD_100K; break;
            //case 1: BaudRate = TPCANBaudrate.PCAN_BAUD_250K; break;
            //case 2: BaudRate = TPCANBaudrate.PCAN_BAUD_500K; break;
            //case 3: BaudRate = TPCANBaudrate.PCAN_BAUD_1M; break;
            case 0: BaudRate = 100000; break;
            case 1: BaudRate = 250000; break;
            case 2: BaudRate = 500000; break;
            case 3: BaudRate = 1000000; break;
        }
        MessageBox.Show($"Velocità CAN impostata su {baudRatePicker.SelectedItem}");
    }

    private void OnSendClicked(object sender, EventArgs e)
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

        SendCANMessage(canMessage.ID, canMessage.DATA);
    }

    public void SendCANMessage(uint CANID, byte[] data)
    {
        // Controlla se il dispositivo è connesso
        if (!_canHandler.IsConnected)
        {
            MessageBox.Show("PCAN non connesso!", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        try
        {
            CANMessage TxMessage = new CANMessage(CANID, data, false, DateTime.Now);          
            _canHandler.Send(TxMessage);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Errore nel formato dei dati: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnPacketSended(object sender, TX_CAN_Data TX_Can_Data)
    {
    
        TPCANStatus res = (TPCANStatus)TX_Can_Data.Result;
        if (res == TPCANStatus.PCAN_ERROR_OK)
        {
            // Ottieni il timestamp
            string timestamp = TX_Can_Data.Message.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string hexString = string.Join(" ", TX_Can_Data.Message.Data.Select(b => b.ToString("X2")));

            // Aggiungi il messaggio al ListView con colore verde
            var listViewItem = new ListViewItem($"{timestamp} - TX: ID=0x{TX_Can_Data.Message.ArbitrationId:X} Dati={hexString}")
            {
                ForeColor = System.Drawing.Color.Green
            };
            thisRef._receivedMessagesView.Items.Add(listViewItem);
            thisRef._receivedMessagesView.EnsureVisible(thisRef._receivedMessagesView.Items.Count - 1); // Scrolla all'ultimo messaggio
        }
        else
        {
            MessageBox.Show($"Errore durante l'invio: {TX_Can_Data.Result}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
