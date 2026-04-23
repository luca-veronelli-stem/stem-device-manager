using GUI.Windows; // Per CircularProgressBar
using Core.Interfaces;
using Core.Models;
using Services.Cache;
using System.Reflection;

public class DeviceInfo
{
    public int Address { get; set; }
    public string DisplayName { get; set; }
    public bool IsOptional { get; set; }

    public DeviceInfo(int address, string displayName, bool isOptional)
    {
        Address = address;
        DisplayName = displayName;
        IsOptional = isOptional;
    }
}

public class Boot_Smart_Tab : TabPage
{
    private TableLayoutPanel selectionPanel = null!;
    private Button btnStartProcedure = null!;
    private FlowLayoutPanel startPanel = null!;
    private TableLayoutPanel progressBarsPanel = null!;
    private CircularProgressBar[] circProgressBarsLarge = new CircularProgressBar[2];
    private List<BinSelectionControl> binSelections = new List<BinSelectionControl>();
    private int offsetTotalBar = 0;
    private TextBox txtVersions = null!;

    // Servizi di dominio iniettati via ConnectionManager (ricreati ad ogni SwitchToAsync).
    private readonly DictionaryCache _cache;
    private readonly ConnectionManager _connMgr;
    //Lista variabili della macchina (snapshot letto dalla cache)
    private IReadOnlyList<Variable> MachineDictionary = [];

    public Boot_Smart_Tab(DictionaryCache cache, ConnectionManager connMgr)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(connMgr);
        _cache = cache;
        _connMgr = connMgr;
        MachineDictionary = _cache.Variables;
        _cache.DictionaryUpdated += (_, _) => MachineDictionary = _cache.Variables;

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
        string resName = "GUI.Windows.images.ic_fluent_arrow_download_24_filled.png";
        using (Stream? s = asm.GetManifestResourceStream(resName))
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

        // Pannello due barre grandi + riga aggiuntiva per versioni firmware
        progressBarsPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,               // <-- ora 2 righe
            Margin = new Padding(10)
        };
        // Definisco prima riga per le progress bar e seconda riga AutoSize
        progressBarsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        progressBarsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        progressBarsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        progressBarsPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Costruisco le due barre con label (riga 0)
        string[] labels = { "Single board", "Total" };
        for (int i = 0; i < 2; i++)
        {
            var cellLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2
            };
            cellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 80));
            cellLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20));

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

        // RIGA 1: pannello per versioni firmware con TableLayoutPanel
        var bottomPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(3)
        };
        // Colonna 0: Label auto
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        // Colonna 1: TextBox al 100%
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        // Colonna 2: Button auto
        bottomPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        // Label
        var lblVersions = new Label
        {
            Text = "Motherboard Fimware Version:",
            AutoSize = true,
            Font = new System.Drawing.Font("Poppins", 9, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(3, 8, 3, 3)
        };
        bottomPanel.Controls.Add(lblVersions, 0, 0);

        // TextBox che riempie tutta la colonna centrale
        txtVersions = new TextBox
        {
            ReadOnly = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(3)
        };
        bottomPanel.Controls.Add(txtVersions, 1, 0);

        // Pulsante
        var btnReadVersions = new Button
        {
            Text = "Read Version",
            AutoSize = true,
            Margin = new Padding(3)
        };
        btnReadVersions.Click += btnReadVersions_Click;
        bottomPanel.Controls.Add(btnReadVersions, 2, 0);

        // Aggiungo bottomPanel a progressBarsPanel in riga 1, estendendolo su 2 colonne
        progressBarsPanel.Controls.Add(bottomPanel, 0, 1);
        progressBarsPanel.SetColumnSpan(bottomPanel, 2);

        // Aggiungo controlli a mainLayout
        mainLayout.Controls.Add(selectionPanel, 0, 0);
        mainLayout.Controls.Add(startPanel, 0, 1);
        mainLayout.Controls.Add(progressBarsPanel, 0, 2);
        this.Controls.Add(mainLayout);

        // Sottoscrizioni via ConnectionManager: re-binding automatico su switch canale.
        _connMgr.BootProgressChanged += UpdateProgressBar;
        _connMgr.TelemetryDataReceived += onDataReady;
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
                Margin = new Padding(3),
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

    private async void BtnStartProcedure_Click(object? sender, EventArgs e)
    {
        foreach (var sel in binSelections)
        {
            if ((string.IsNullOrEmpty(sel.FilePath)) && (sel.Device.IsOptional == false))
            {
                MessageBox.Show($"Select firmware file for {sel.Device.DisplayName}");
                return;
            }
        }

        var boot = _connMgr.CurrentBoot;
        if (boot is null) { MessageBox.Show("Select communication channel first!"); return; }

        btnStartProcedure.Enabled = false;

        //reset progress bars
        foreach (var pb in circProgressBarsLarge) pb.Value = 0;
        offsetTotalBar = 0;

        foreach (var sel in binSelections)
        {
            if (!(string.IsNullOrEmpty(sel.FilePath)))
            {
                // Mutazione pura sul cache (nessuna HTTP/event), usata da consumer che leggono CurrentRecipientId.
                _cache.SetCurrentRecipientId((uint)sel.Device.Address);
                byte[] firmware = File.ReadAllBytes(sel.FilePath);
                await boot.StartFirmwareUploadAsync(firmware, (uint)sel.Device.Address);
            }
            else
            {
                int progress = 100;

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

            offsetTotalBar += (100 / binSelections.Count);
        }

        if (InvokeRequired)
        {
            Invoke(new Action(() =>
            {
                circProgressBarsLarge[1].Value = 100; //aggiorna barra progresso totale
            }));
        }
        else
        {
            circProgressBarsLarge[1].Value = 100; //aggiorna barra progresso totale
        }

        btnStartProcedure.Enabled = true;
    }

    private void UpdateProgressBar(object? sender, BootProgress e)
    {
        // This tab is constructed for every variant but only visible/populated
        // for TopLift and Eden. For Egicon/Generic, binSelections is empty and
        // the subscription is dormant — guard against div-by-zero when another
        // tab (e.g. SPARK firmware update) triggers BootProgressChanged.
        if (binSelections.Count == 0) return;
        int value = e.TotalLength <= 0 ? 0 : (int)((double)e.CurrentOffset / e.TotalLength * 100);
        if (value == 99) value = 100;

        int progress = value;

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

    private int GetVariableIndex(String Name)
    {
        for (int i = 0; i < MachineDictionary.Count; i++)
        {
            if (MachineDictionary[i].Name == Name) return i;
        }

        return -1;
    }

    private async void btnReadVersions_Click(object? sender, EventArgs e)
    {
        var tel = _connMgr.CurrentTelemetry;
        if (tel is null) { MessageBox.Show("Select communication channel first!"); return; }

        await tel.StopTelemetryAsync();
        tel.ResetDictionary();

        // Carica in telemetria il firmware della scheda (3 richieste per robustezza verso
        // eventuali pacchetti persi — pattern parità con legacy).
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Firmware scheda")]);
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Firmware scheda")]);
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Firmware scheda")]);
        await tel.ReadOneShotAsync();
    }

    private void onDataReady(object? sender, TelemetryDataPoint dp)
    {
        // Solo read reply: ignora il fast stream che non interessa al tab boot smart.
        if (dp.Source != TelemetrySource.ReadReply) return;

        switch (dp.Variable.Name)
        {
            case "Firmware scheda":
                uint value = dp.NumericValue;
                uint MajorVersion = value >> 8;
                uint MinorVersion = value & 0x000000FF;

                if (this.InvokeRequired)
                {
                    this.Invoke(new Action(() => txtVersions.Text = " " + MajorVersion.ToString() + "." + MinorVersion.ToString()));
                }
                else
                {
                    txtVersions.Text = " " + MajorVersion.ToString() + "." + MinorVersion.ToString();
                }
                break;
            default:
                break;
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
