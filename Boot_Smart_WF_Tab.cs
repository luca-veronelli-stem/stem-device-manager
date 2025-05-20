using Stem_Protocol.BootManager;
using StemPC;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using CustomControls; // Per CircularProgressBar

public class DeviceInfo
{
    public int Address { get; set; }
    public string DisplayName { get; set; }

    public DeviceInfo(int address, string displayName)
    {
        Address = address;
        DisplayName = displayName;
    }
}

public class Boot_Smart_Tab : TabPage
{
    private TableLayoutPanel selectionPanel;
    private Button btnStartProcedure;
    private FlowLayoutPanel startPanel;
    private TableLayoutPanel progressBarsPanel;
    private CircularProgressBar circProgressBarSmall;
    private CircularProgressBar[] circProgressBarsLarge = new CircularProgressBar[2];
    private List<BinSelectionControl> binSelections = new List<BinSelectionControl>();
    private int offsetTotalBar = 0;
    public BootManager BootHndlr;

    public Boot_Smart_Tab()
    {
        Name = "tabPageBootSmart";
        Text = "Firmware Update";

        // Layout principale: selezione, start, progress bar
        TableLayoutPanel mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Pannello selezione
        selectionPanel = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 3,
            Dock = DockStyle.Top,
            Margin = new Padding(0)
        };
        selectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        selectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        selectionPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        // Pannello start
        startPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(10),
            AutoSize = true
        };

        // Crea bottone start
        Assembly asm = Assembly.GetExecutingAssembly();
        string resName = "STEMPM.images.ic_fluent_arrow_download_24_filled.png";
        using (Stream s = asm.GetManifestResourceStream(resName))
        {
            if (s == null)
            {
                MessageBox.Show($"Risorsa '{resName}' non trovata.", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            Image img = Image.FromStream(s);
            btnStartProcedure = new Button
            {
                Height = 60,
                Width = 60,
                Anchor = AnchorStyles.None,
                BackgroundImage = img,
                BackgroundImageLayout = ImageLayout.Zoom,
                Margin = new Padding(3)
            };
            btnStartProcedure.Click += BtnStartProcedure_Click;
            startPanel.Controls.Add(btnStartProcedure);
        }


        // Pannello due barre grandi
        progressBarsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(10)
        };
        progressBarsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        progressBarsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

        // Costruisci le due barre con label
        string[] labels = { "Single board", "Total" };
        for (int i = 0; i < 2; i++)
        {
            // Layout cella con 2 righe
            var cellLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            cellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 80));
            cellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

            // Progress bar grande
            var pb = new CircularProgressBar
            {
                Size = new Size(240, 240),
                LineWidth = 16,
                ForeColor = Color.FromArgb(8, 72, 133),
                Value = 0,
                Maximum = 100,
                Anchor = AnchorStyles.None,
                Font = new System.Drawing.Font("Poppins", 42, FontStyle.Regular)
            };
            circProgressBarsLarge[i] = pb;
            cellLayout.Controls.Add(pb, 0, 0);

            // Label sotto la barra
            var lbl = new Label
            {
                Text = labels[i],
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new System.Drawing.Font("Poppins", 14, FontStyle.Bold),
                ForeColor = Color.FromArgb(8, 72, 133),
            };
            cellLayout.Controls.Add(lbl, 0, 1);

            progressBarsPanel.Controls.Add(cellLayout, i, 0);
        }

        // Aggiungo controlli a mainLayout
        mainLayout.Controls.Add(selectionPanel, 0, 0);
        mainLayout.Controls.Add(startPanel, 0, 1);
        mainLayout.Controls.Add(progressBarsPanel, 0, 2);
        this.Controls.Add(mainLayout);

        // Inizializza BootManager
        BootHndlr = new BootManager();
        BootHndlr.ProgressChanged += UpdateProgressBar;
    }

    public void PopulateDevices(List<DeviceInfo> devices)
    {
        selectionPanel.Controls.Clear();
        selectionPanel.RowStyles.Clear();
        selectionPanel.RowCount = 0;
        binSelections.Clear();

        foreach (var device in devices)
        {
            var lblDevice = new Label
            {
                Text = device.DisplayName,
                Font = new System.Drawing.Font("Poppins", 9, FontStyle.Regular),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(3)
            };
            var txtFilePath = new TextBox
            {
                ReadOnly = true,
                Dock = DockStyle.Fill,
                Margin = new Padding(3)
            };
            var btnSelectFile = new Button
            {
                Text = "Select .bin",
                Font = new System.Drawing.Font("Poppins", 9, FontStyle.Regular),
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Margin = new Padding(3)
            };
            btnSelectFile.Click += (s, e) =>
            {
                var ofd = new OpenFileDialog
                {
                    Filter = "Binary Files|*.bin|All Files|*.*",
                    Title = "Select a Binary File"
                };
                if (ofd.ShowDialog() == DialogResult.OK)
                    txtFilePath.Text = ofd.FileName;
            };
            int currentRow = selectionPanel.RowCount;
            selectionPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            selectionPanel.Controls.Add(lblDevice, 0, currentRow);
            selectionPanel.Controls.Add(txtFilePath, 1, currentRow);
            selectionPanel.Controls.Add(btnSelectFile, 2, currentRow);
            selectionPanel.RowCount++;
            binSelections.Add(new BinSelectionControl(device, txtFilePath));
        }
    }

    private async void BtnStartProcedure_Click(object sender, EventArgs e)
    {
        foreach (var sel in binSelections)
        {
            if (string.IsNullOrEmpty(sel.FilePath))
            {
                MessageBox.Show($"Select firmware file for {sel.Device.DisplayName}");
                return;
            }
        }
        btnStartProcedure.Enabled = false;

        //reset progress bars
        foreach (var pb in circProgressBarsLarge) pb.Value = 0;
        offsetTotalBar = 0;

        foreach (var sel in binSelections)
        {
            BootHndlr.SetFirmwarePath(sel.FilePath);
            Form1.FormRef.RecipientId = (uint)sel.Device.Address;
            await BootHndlr.UploadFirmware();
            offsetTotalBar += circProgressBarsLarge[1].Value;
        }

        btnStartProcedure.Enabled = true;
    }

    private void UpdateProgressBar(object sender, ProgressEventArgs e)
    {

        int Value = (int)((double)e.CurrentOffset / e.TotalLength * 100);
        if (Value == 99) Value = 100;

        int progress = Value;

        if (InvokeRequired)
        {
            Invoke(new Action(() =>
            {
                circProgressBarsLarge[0].Value = progress; //aggiorna barra progresso singolo firmware
                circProgressBarsLarge[1].Value = offsetTotalBar + (progress / binSelections.Count); //aggiorna barra progresso totale
            }));
        }
        else
        {
            circProgressBarsLarge[0].Value = progress; //aggiorna barra progresso singolo firmware
            circProgressBarsLarge[1].Value = offsetTotalBar + (progress / binSelections.Count); //aggiorna barra progresso totale
        }
    }
}

public class BinSelectionControl
{
    public DeviceInfo Device { get; private set; }
    private TextBox FilePathTextBox;
    public string FilePath => FilePathTextBox.Text;

    public BinSelectionControl(DeviceInfo device, TextBox textBox)
    {
        Device = device;
        FilePathTextBox = textBox;
    }
}
