using System;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

using StemPC;
using Stem_Protocol;
using Stem_Protocol.PacketManager;
using Stem_Protocol.BootManager;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;
using DocumentFormat.OpenXml.Wordprocessing;


// Classe per l'interfaccia grafica del bootloader
public class Boot_Interface_Tab : TabPage
{
    private System.Windows.Forms.TextBox txtFilePath;
    private System.Windows.Forms.Button btnSelectFile;
    private System.Windows.Forms.Button btnStartProcedure;
    private DataGridView dgvHexView;
    private CustomProgressBar progressBar;
    //private System.Windows.Forms.ProgressBar progressBar;
    private OpenFileDialog openFileDialog;
    private TableLayoutPanel mainLayout;
    private string filePath= "";

    public Boot_Interface_Tab()
    {
        Name = "tabPageBoot";
        Text = "Boot Interface";

        // Layout principale
        mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };

        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // TextBox
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50)); // Pulsanti
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // DataGridView
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); // ProgressBar

        // TextBox per mostrare il percorso del file selezionato
        txtFilePath = new System.Windows.Forms.TextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            PlaceholderText = "No file selected"
        };

        // Pulsante per selezionare il file
        btnSelectFile = new System.Windows.Forms.Button
        {
            Text = "Select .bin File",
            Width = 100
        };
        btnSelectFile.Click += BtnSelectFile_Click;

        // Pulsante per avviare la procedura
        btnStartProcedure = new System.Windows.Forms.Button
        {
            Text = "Upload Firmware",
            Width = 120
        };
        btnStartProcedure.Click += BtnStartProcedure_Click;

        // Contenitore per i pulsanti
        FlowLayoutPanel buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill
        };
        buttonPanel.Controls.Add(btnSelectFile);
        buttonPanel.Controls.Add(btnStartProcedure);

        // DataGridView per visualizzare il contenuto in esadecimale e ASCII
        dgvHexView = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize
        };

        // Colonna Offset (larghezza fissa)
        DataGridViewColumn offsetColumn = new DataGridViewTextBoxColumn
        {
            Name = "Offset",
            HeaderText = "Offset",
            Width = 100,
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None
        };
        dgvHexView.Columns.Add(offsetColumn);

        // Colonna HexValues (si espande dinamicamente)
        DataGridViewColumn hexColumn = new DataGridViewTextBoxColumn
        {
            Name = "HexValues",
            HeaderText = "Hex Values",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        dgvHexView.Columns.Add(hexColumn);

        // Colonna AsciiValues (si espande dinamicamente)
        DataGridViewColumn asciiColumn = new DataGridViewTextBoxColumn
        {
            Name = "AsciiValues",
            HeaderText = "ASCII",
            ReadOnly = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
        };
        dgvHexView.Columns.Add(asciiColumn);

        // Barra di progresso
        progressBar = new CustomProgressBar
        {
            Dock = DockStyle.Fill
        };

          
        // OpenFileDialog per selezionare i file
        openFileDialog = new OpenFileDialog
        {
            Filter = "Binary Files|*.bin|All Files|*.*",
            Title = "Select a Binary File"
        };

        // Aggiunta dei controlli al layout
        mainLayout.Controls.Add(txtFilePath, 0, 0); // TextBox
        mainLayout.Controls.Add(buttonPanel, 0, 1); // Pulsanti
        mainLayout.Controls.Add(dgvHexView, 0, 2); // DataGridView
        mainLayout.Controls.Add(progressBar, 0, 3); // ProgressBar

        // Aggiunta del layout alla TabPage
        this.Controls.Add(mainLayout);
    }

    private void BtnSelectFile_Click(object sender, EventArgs e)
    {
        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            filePath = openFileDialog.FileName;
            txtFilePath.Text = filePath; // Mostra il percorso nel TextBox
            DisplayFileContent(filePath);
        }
    }

    private async void BtnStartProcedure_Click(object sender, EventArgs e)
    {
        //Test
        //filePath = "C:\\Users\\Michele\\OneDrive\\Dati\\Lavoro\\STEM\\TopLift-A\\Firmware\\Debug_BOOT\\TopLift-A_BOOT_00_01M.bin";
        Form1.FormRef.RecipientId = 0x00030141;

        if (filePath == "")
        {
            MessageBox.Show("Select Firmware file .bin!");
        }
        else if (Form1.FormRef.RecipientId == 0)
        {
            MessageBox.Show("Select destination address!");
        }
        else
        {
            btnStartProcedure.Enabled = false;

            //crea la classe di upload
            BootManager BootHldr = new BootManager(Form1.FormRef.RecipientId, filePath);
            BootHldr.ProgressChanged += UpdateProgressBar;
            // Iscrizione all'evento SendCanCommand
            BootHldr.SendCanCommandRequest += OnSendCanCommand;

            //esegui l'upload
            await BootHldr.UploadFirmware();  

            btnStartProcedure.Enabled = true;
        }
    }

    // Gestore dell'evento send can command
    private static async void OnSendCanCommand(object sender, SendCanCommandEventArgs e)
    {
        await HandleSendCanCommandAsync(sender, e);
    }

    private static async Task HandleSendCanCommandAsync(object sender, SendCanCommandEventArgs e)
    {
        try
        {
            // Crea il pacchetto per l'applicationLayer
            byte[] AppData = { (byte)(e.Command >> 8), (byte)(e.Command) };

            // AL
            byte cmdInit = AppData[0]; // comando byte alto
            byte cmdOpt = AppData[1]; // comando byte basso
            byte[] payload = e.Payload;

            // TL
            byte cryptFlag = 0x00; // Nessuna crittografia

            // NL
            string interfaceType = "can";
            int version = 1;
            uint recipientId = Form1.FormRef.RecipientId;

            // Crea il pacchetto di livello Network
            var networkLayer = new NetworkLayer(
                interfaceType,
                version,
                recipientId,
                new byte[] { cryptFlag, (byte)Form1.FormRef.senderId, (byte)(Form1.FormRef.senderId >> 8), (byte)(Form1.FormRef.senderId >> 16), (byte)(Form1.FormRef.senderId >> 24), 0, 0, cmdInit, cmdOpt }.Concat(payload).ToArray(),
                true
            );

            // Stampa i dettagli           
            Form1.FormRef.UpdateTerminal("Invio Comando Boot Manager:");
            //Form1.FormRef.UpdateTerminal("Comando Boot manager:");
            //Form1.FormRef.UpdateTerminal($"{string.Join(" ", networkLayer.ApplicationPacket.Select(b => b.ToString("X2")))}");

            // Ottieni i chunk da spedire
            var networkPackets = networkLayer.NetworkPackets;
            var packetManager = new PacketManager(Form1.FormRef.senderId, null);
            packetManager.Add_CAN_Channel(Form1.FormRef._CDL);

            // Invia i pacchetti tramite CAN in modo asincrono
            bool result = false;

            if (e.WaitAnswer)
            {
                // Funzione di validazione della risposta
                Func<byte[], bool> responseValidator = (data) =>
                {
                    //// Esempio: Verifica che il primo byte della risposta sia 0xAA
                    //return data.Length > 0 && data[0] == 0xAA;
                    return data.Length > 0;
                };
                result = await packetManager.SendAndWaitForResponseAsync(networkPackets, responseValidator);
            }
            else
            {
                result = await packetManager.SendThroughCANAsync(networkPackets);
            }
            
            // Usa il risultato
            Form1.FormRef.UpdateTerminal(result ? "Pacchetto inviato con successo!" : "Errore durante l'invio del pacchetto.");
        }
        catch (Exception ex)
        {
            // Gestione dell'eccezione
            Form1.FormRef.UpdateTerminal($"Errore durante l'invio del comando CAN: {ex.Message}");
        }
    }

    private void DisplayFileContent(string filePath)
    {
        try
        {
            byte[] fileContent = File.ReadAllBytes(filePath);
            dgvHexView.Rows.Clear();

            int bytesPerRow = 16;
            for (int i = 0; i < fileContent.Length; i += bytesPerRow)
            {
                int rowLength = Math.Min(bytesPerRow, fileContent.Length - i);
                byte[] rowBytes = new byte[rowLength];
                Array.Copy(fileContent, i, rowBytes, 0, rowLength);

                string offset = i.ToString("X8");
                string hexValues = BitConverter.ToString(rowBytes).Replace("-", " ");
                string asciiValues = Encoding.ASCII.GetString(rowBytes)
                    .Replace('\0', '.')
                    .Replace("\r", ".")
                    .Replace("\n", ".");

                dgvHexView.Rows.Add(offset, hexValues, asciiValues);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error reading file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    // Funzione privata per aggiornare la ProgressBar
    void UpdateProgressBar(object sender, ProgressEventArgs e)
    {
        progressBar.Value = (int)((double)e.CurrentOffset / e.TotalLength * 100);
        if (progressBar.Value == 99) progressBar.Value = 100;
    }


}

// c# progress bar with percentage
public class CustomProgressBar : System.Windows.Forms.ProgressBar
{
    public CustomProgressBar()
    {
      //  InitializeComponent();
        // Set default style to owner draw
        this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
    }

    // Override the OnPaint method to custom render the progress bar with percentage text
    protected override void OnPaint(PaintEventArgs pe)
    {
        //Draw percentage
        Rectangle rect = this.ClientRectangle;
        // Create graphics object for drawing custom content
        Graphics g = pe.Graphics;
        ProgressBarRenderer.DrawHorizontalBar(g, rect);
        if (this.Value > 0)
        {
            Rectangle clip = new Rectangle(rect.X, rect.Y, (int)Math.Round(((float)this.Value / this.Maximum) * rect.Width), rect.Height);
            ProgressBarRenderer.DrawHorizontalChunks(g, clip);
        }
        using (var font = new System.Drawing.Font(System.Drawing.FontFamily.GenericMonospace, 14, FontStyle.Bold))
        {
            // Calculate the percentage text to display
            SizeF size = g.MeasureString(string.Format("{0} %", this.Value), font);
            var location = new Point((int)((rect.Width / 2) - (size.Width / 2)), (int)((rect.Height / 2) - (size.Height / 2) + 2));
            // Draw the percentage text on the progress bar
            g.DrawString(string.Format("{0} %", this.Value), font, Brushes.Black, location);
        }
    }
}


