using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Peak.Can.Basic;
using Peak.Can.Basic.BackwardCompatibility;
using StemPC;
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
    private ListView receivedMessagesView;

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

        // Inizializzazione del ListView
        this.receivedMessagesView = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true
        };
        this.receivedMessagesView.Columns.Add("Messaggi", -2, HorizontalAlignment.Left);

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
        // Controlla se il dispositivo è connesso
        if (!IsConnected)
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
                    ForeColor = Color.Green
                };
                receivedMessagesView.Items.Add(listViewItem);
                receivedMessagesView.EnsureVisible(receivedMessagesView.Items.Count - 1); // Scrolla all'ultimo messaggio
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

    private void UpdateConnectionStatus()
    {
        if (IsConnected == false) { 
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
    }


    public void SendCANMessage(uint CANID, byte[] data)
    {
        // Controlla se il dispositivo è connesso
        if (!IsConnected)
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
                LEN = (byte) data.Length,
                MSGTYPE = TPCANMessageType.PCAN_MESSAGE_STANDARD,
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


                Form1.FormRef.UpdateTerminal($"{timestamp} - TX: ID=0x{canMessage.ID:X} Dati={hexString}");

                //// Aggiungi il messaggio al ListView con colore verde
                //var listViewItem = new ListViewItem($"{timestamp} - TX: ID=0x{canMessage.ID:X} Dati={hexString}")
                //{
                //    ForeColor = Color.Green
                //};
                //receivedMessagesView.Items.Add(listViewItem);
                //receivedMessagesView.EnsureVisible(receivedMessagesView.Items.Count - 1); // Scrolla all'ultimo messaggio
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

    private async Task ReadCANMessages()
    {
        while (true)
        {
            if (!connectionStatusLabel.IsHandleCreated)
                continue;

            try
            {
                // Controlla lo stato della connessione
                var status = PCANBasic.GetStatus(Channel);
                if (status != TPCANStatus.PCAN_ERROR_OK)
                {
                    // La connessione è persa, tentativo di riconnessione
                    Invoke((Action)(() =>
                    {
                        connectionStatusLabel.Text = "Stato: Disconnesso";
                        connectionStatusLabel.ForeColor = Color.Red;

                        //var disconnectionItem = new ListViewItem("Connessione persa. Tentativo di riconnessione...")
                        //{
                        //    ForeColor = Color.Red
                        //};

                        //receivedMessagesView.Items.Add(disconnectionItem);
                        //receivedMessagesView.EnsureVisible(receivedMessagesView.Items.Count - 1);
                    }));

                    IsConnected = false;

                    // Prova a riconnettersi
                    var reconnectResult = PCANBasic.Initialize(Channel, BaudRate);
                    if (reconnectResult == TPCANStatus.PCAN_ERROR_OK)
                    {
                        Invoke((Action)(() =>
                        {
                            connectionStatusLabel.Text = "Stato: Connesso";
                            connectionStatusLabel.ForeColor = Color.Green;

                            var reconnectionItem = new ListViewItem("Riconnessione avvenuta con successo.")
                            {
                                ForeColor = Color.Green
                            };
                            receivedMessagesView.Items.Add(reconnectionItem);
                            receivedMessagesView.EnsureVisible(receivedMessagesView.Items.Count - 1);
                        }));
                        IsConnected = true;
                    }
                    else
                    {
                        // Aspetta un po' prima di riprovare
                        await Task.Delay(1000);
                        continue;
                    }
                }
                else
                {
                    IsConnected = true;
                    // La connessione è attiva
                    Invoke((Action)(() =>
                    {
                        connectionStatusLabel.Text = "Stato: Connesso";
                        connectionStatusLabel.ForeColor = Color.Green;
                    }));
                }

                // Legge il messaggio CAN
                TPCANMsg canMessage;
                TPCANTimestamp canTimestamp;
                var result = PCANBasic.Read(Channel, out canMessage, out canTimestamp);

                if (result == TPCANStatus.PCAN_ERROR_OK)
                {
                    // Ottieni il timestamp come data e ora completi
                    string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                    // Estrai i dati dal messaggio
                    string dataString = string.Join(" ", canMessage.DATA.Take(canMessage.LEN).Select(b => b.ToString("X2")));

                    // Costruisci il contenuto del messaggio
                    string messageContent = $"{timestamp} - RX: ID=0x{canMessage.ID:X} Dati={dataString}";

                    // Aggiungi il messaggio alla lista con colore blu
                    var listViewItem = new ListViewItem(messageContent)
                    {
                        ForeColor = Color.Blue
                    };
                    Invoke((Action)(() =>
                    {
                        receivedMessagesView.Items.Add(listViewItem);
                        receivedMessagesView.EnsureVisible(receivedMessagesView.Items.Count - 1);
                    }));
                }
                else if (result != TPCANStatus.PCAN_ERROR_QRCVEMPTY)
                {
                    // Log degli errori diversi dalla coda vuota
                    StringBuilder errorTextB = new StringBuilder(256);
                    PCANBasic.GetErrorText(result, 0, errorTextB);
                    string errorText = errorTextB.ToString();

                    Invoke((Action)(() =>
                    {
                        var errorItem = new ListViewItem($"Errore CAN: {errorText}")
                        {
                            ForeColor = Color.Red
                        };
                        receivedMessagesView.Items.Add(errorItem);
                        receivedMessagesView.EnsureVisible(receivedMessagesView.Items.Count - 1);
                    }));
                }
            }
            catch (Exception ex)
            {
                Invoke((Action)(() =>
                {
                    var errorItem = new ListViewItem($"Errore durante la lettura: {ex.Message}")
                    {
                        ForeColor = Color.Red
                    };
                    receivedMessagesView.Items.Add(errorItem);
                    receivedMessagesView.EnsureVisible(receivedMessagesView.Items.Count - 1);
                }));
            }

            await Task.Delay(50); // Ritardo per ridurre il carico del loop
        }
    }
}

