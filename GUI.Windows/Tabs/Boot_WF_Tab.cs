using Core.Interfaces;
using Core.Models;
using Services.Cache;
using System.Text;


// Classe per l'interfaccia grafica del bootloader
public class Boot_Interface_Tab : TabPage
{
    private readonly DictionaryCache _cache;
    private readonly ConnectionManager _connMgr;
    private readonly IDeviceVariantConfig _variantConfig;
    private System.Windows.Forms.TextBox txtFilePath;
    private System.Windows.Forms.Button btnSelectFile;
    private System.Windows.Forms.Button btnStartProcedure;
    private System.Windows.Forms.Button btnStartBoot;
    private System.Windows.Forms.Button btnEndBoot;
    private System.Windows.Forms.Button btnRestart;
    private System.Windows.Forms.Button btnAuto;
    private DataGridView dgvHexView;
    private CustomProgressBar progressBar;
    //private System.Windows.Forms.ProgressBar progressBar;
    private OpenFileDialog openFileDialog;
    private TableLayoutPanel mainLayout;
    private string filePath = "";
    private byte[] _firmwareBytes = [];

    public Boot_Interface_Tab(DictionaryCache cache, ConnectionManager connMgr, IDeviceVariantConfig variantConfig)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(connMgr);
        ArgumentNullException.ThrowIfNull(variantConfig);
        _cache = cache;
        _connMgr = connMgr;
        _variantConfig = variantConfig;

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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80)); // Pulsanti
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // DataGridView
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // ProgressBar

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

        // Pulsante per avviare il boot
        btnStartBoot = new System.Windows.Forms.Button
        {
            Text = "Start Boot",
            Width = 100
        };
        btnStartBoot.Click += BtnStartBoot_Click;

        // Pulsante per terminare il boot
        btnEndBoot = new System.Windows.Forms.Button
        {
            Text = "End Boot",
            Width = 100
        };
        btnEndBoot.Click += BtnEndBoot_Click;

        // Pulsante per riavviare
        btnRestart = new System.Windows.Forms.Button
        {
            Text = "Restart",
            Width = 100
        };
        btnRestart.Click += BtnRestart_Click;

        // Pulsante per procedura automatica
        btnAuto = new System.Windows.Forms.Button
        {
            Text = "Upload Firmware",
            Width = 100
        };
        btnAuto.Click += BtnAuto_Click;

        // Contenitore per i pulsanti
        FlowLayoutPanel buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill
        };

        buttonPanel.Controls.Add(btnSelectFile);

        // Set di pulsanti specifico per variante: Egicon espone i 4 step separati
        // (StartBoot/StartProcedure/EndBoot/Restart), le altre varianti espongono il
        // solo btnAuto che esegue la sequenza completa.
        if (_variantConfig.Variant == DeviceVariant.Egicon)
        {
            buttonPanel.Controls.Add(btnStartBoot);
            buttonPanel.Controls.Add(btnStartProcedure);
            buttonPanel.Controls.Add(btnEndBoot);
            buttonPanel.Controls.Add(btnRestart);
        }
        else
        {
            buttonPanel.Controls.Add(btnAuto);
        }

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


        // Sottoscrizione progress via ConnectionManager: re-binding automatico su SwitchToAsync.
        _connMgr.BootProgressChanged += UpdateProgressBar;
    }

    private void BtnSelectFile_Click(object? sender, EventArgs e)
    {
        if (openFileDialog.ShowDialog() == DialogResult.OK)
        {
            filePath = openFileDialog.FileName;
            txtFilePath.Text = filePath; // Mostra il percorso nel TextBox
            DisplayFileContent(filePath);
            _firmwareBytes = File.ReadAllBytes(filePath);
        }
    }


    private async void BtnStartBoot_Click(object? sender, EventArgs e)
    {
        var boot = _connMgr.CurrentBoot;
        if (boot is null) { MessageBox.Show("Select communication channel first!"); return; }
        btnStartBoot.Enabled = false;
        bool ok = await boot.StartBootAsync(_cache.CurrentRecipientId);
        MessageBox.Show(ok ? "Bootloader avviato!" : "Risposta al comando non ricevuta!",
            "", MessageBoxButtons.OK, MessageBoxIcon.Information);
        btnStartBoot.Enabled = true;
    }

    private async void BtnEndBoot_Click(object? sender, EventArgs e)
    {
        var boot = _connMgr.CurrentBoot;
        if (boot is null) { MessageBox.Show("Select communication channel first!"); return; }
        btnEndBoot.Enabled = false;
        bool ok = await boot.EndBootAsync(_cache.CurrentRecipientId);
        MessageBox.Show(ok ? "Bootloader terminato!" : "Risposta al comando non ricevuta!",
            "", MessageBoxButtons.OK, MessageBoxIcon.Information);
        btnEndBoot.Enabled = true;
    }

    private async void BtnRestart_Click(object? sender, EventArgs e)
    {
        var boot = _connMgr.CurrentBoot;
        if (boot is null) { MessageBox.Show("Select communication channel first!"); return; }
        btnRestart.Enabled = false;
        await boot.RestartAsync(_cache.CurrentRecipientId);
        btnRestart.Enabled = true;
    }

    private async void BtnStartProcedure_Click(object? sender, EventArgs e)
    {
        if (filePath == "")
        {
            MessageBox.Show("Select Firmware file .bin!");
            return;
        }
        if (_cache.CurrentRecipientId == 0)
        {
            MessageBox.Show("Select destination address!");
            return;
        }
        var boot = _connMgr.CurrentBoot;
        if (boot is null) { MessageBox.Show("Select communication channel first!"); return; }

        btnStartProcedure.Enabled = false;
        await boot.UploadBlocksOnlyAsync(_firmwareBytes, _cache.CurrentRecipientId);
        if (boot.State == BootState.Completed)
            MessageBox.Show("Aggiornamento firmware completato!", "Successo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        else if (boot.State == BootState.Failed)
            MessageBox.Show("Errore: programmazione blocchi fallita.", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        btnStartProcedure.Enabled = true;
    }

    private async void BtnAuto_Click(object? sender, EventArgs e)
    {
        btnAuto.Enabled = false;
        try
        {
            if (filePath == "")
            {
                MessageBox.Show("Select Firmware file .bin!");
                return;
            }
            if (_cache.CurrentRecipientId == 0)
            {
                MessageBox.Show("Select destination address!");
                return;
            }
            var boot = _connMgr.CurrentBoot;
            if (boot is null) { MessageBox.Show("Select communication channel first!"); return; }

            await boot.StartFirmwareUploadAsync(_firmwareBytes, _cache.CurrentRecipientId);
            if (boot.State == BootState.Failed)
                MessageBox.Show("Errore durante l'upload del firmware.", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            btnAuto.Enabled = true;
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
    void UpdateProgressBar(object? sender, BootProgress e)
    {
        int value = e.TotalLength <= 0 ? 0 : (int)((double)e.CurrentOffset / e.TotalLength * 100);
        if (value == 99) value = 100;
        if (progressBar.InvokeRequired)
            progressBar.Invoke(new Action(() => progressBar.Value = value));
        else
            progressBar.Value = value;
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


