using System;
using System.Collections.ObjectModel;
using System.Reflection.Emit;
using System.Threading.Tasks;
using Peak.Can.Basic;
using Peak.Can.Basic.BackwardCompatibility;
using TPCANHandle = System.Byte;

public class CanMessage
{
    public string Content { get; set; }
    public Color Color { get; set; }
}

public partial class CanTabPage : TabPage
{
    private ComboBox baudRatePicker;
    private Button sendButton;
    private TextBox canIdEntry;
    private TextBox dataEntry;
    private System.Windows.Forms.Label connectionStatusLabel;
    private ListBox receivedMessagesView;

    private const TPCANHandle Channel = 0x51; // PCAN_USB
    private TPCANBaudrate BaudRate;
    private bool IsConnected = false;
    private ObservableCollection<CanMessage> messages = new ObservableCollection<CanMessage>();

    public CanTabPage()
    {
        InitializeComponents();
        BaudRate = TPCANBaudrate.PCAN_BAUD_100K;
        UpdateConnectionStatus();
        Task.Run(ReadCANMessages);
    }

    private void InitializeComponents()
    {
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

        this.connectionStatusLabel = new System.Windows.Forms.Label
        {
            Text = "Stato: Disconnesso",
            ForeColor = Color.Red,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        this.receivedMessagesView = new ListBox { Dock = DockStyle.Fill };

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
        layout.Controls.Add(this.connectionStatusLabel, 0, 5);
        layout.Controls.Add(this.receivedMessagesView, 0, 6);

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
        // Codice per l'invio dei messaggi CAN
    }

    private void UpdateConnectionStatus()
    {
        var result = PCANBasic.Initialize(Channel, BaudRate);
        if (result == TPCANStatus.PCAN_ERROR_OK)
        {
            IsConnected = true;
            connectionStatusLabel.Text = "Stato: Connesso";
            connectionStatusLabel.ForeColor = Color.Green;
        }
        else
        {
            IsConnected = false;
            connectionStatusLabel.Text = $"Stato: Disconnesso ({result})";
            connectionStatusLabel.ForeColor = Color.Red;
        }
    }

    private async Task ReadCANMessages()
    {
        // Codice per la lettura dei messaggi CAN
    }
}

