//using System.Reflection.Emit;
using GUI.Windows.Properties;
using Core.Models;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using Services.Cache;

public class TopLiftTelemetry_Tab : TabPage
{
    //Cache centralizzata dizionario (sostituisce UpdateDictionary callback)
    private readonly DictionaryCache _cache;
    private readonly ConnectionManager _connMgr;
    //Lista variabili della macchina (snapshot letto dalla cache)
    private IReadOnlyList<Variable> MachineDictionary = [];


    // Dichiarazione dei controlli
    private Panel panel1;
    private Panel panel2;
    private Button startTelemetryButton;
    private Button stopTelemetryButton;
    private PictureBox[] imageContainers;
    private System.Windows.Forms.Label[] imageLabels;
    private Button buttonReadFaults;
    private TextBox textBoxRow3;
    private System.Windows.Forms.Label[] labelsRow4;
    private TextBox[] textBoxesRow4;
    private Button buttonReadSettings;
    private Button buttonWriteSettings;
    private Button buttonLoadSettings;
    private Button buttonSaveSettings;

    //Plotview section
    private LineSeries pointSeries1;
    private PlotView plotView1;
    private LinearAxis xAxis1;
    private LinearAxis yAxis1;

    private LineSeries pointSeries2;
    private PlotView plotView2;
    private LinearAxis xAxis2;
    private LinearAxis yAxis2;

    // Variabili booleane associate ai contenitori di immagini
    private bool[] imageStates;

    //oxyplot variables
    private int time;
    private double windowWidth = 100;     // ampiezza della finestra X (es. 10 secondi)


    public TopLiftTelemetry_Tab(DictionaryCache cache, ConnectionManager connMgr)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(connMgr);
        _cache = cache;
        _connMgr = connMgr;
        MachineDictionary = _cache.Variables;
        _cache.DictionaryUpdated += (_, _) => MachineDictionary = _cache.Variables;

        InitializeComponent();
        InitializeCustomComponents();
        SetupEventHandlers();

        // Sottoscrizione data telemetria via ConnectionManager (re-binding automatico su switch canale).
        _connMgr.TelemetryDataReceived += onDataReady;
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();

        // Impostazioni di base della form
        this.ClientSize = new System.Drawing.Size(800, 500);
        this.Name = "TopLiftTelemetry";
        this.Text = "Top Lift A2 Telemetry";

        this.ResumeLayout(false);
    }

    private void InitializeCustomComponents()
    {
        // Creo il layout principale con un TableLayoutPanel
        TableLayoutPanel mainLayout = new TableLayoutPanel();
        mainLayout.Dock = DockStyle.Fill;
        mainLayout.RowCount = 5; // Aumentato a 5 per la nuova riga di pulsanti
        mainLayout.ColumnCount = 1;
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F)); // Nuova prima riga (pulsanti telemetria)
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));  // Seconda riga (celle grafiche) - molto più alta
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 25F));  // Terza riga (contenitori immagini)
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 8F));  // Quarta riga (pulsante e textbox) - più bassa
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 12F));  // Quinta riga (gruppi e pulsante) - più bassa
        mainLayout.Padding = new Padding(10);
        this.Controls.Add(mainLayout);

        // NUOVA PRIMA RIGA: Pulsanti "Start Telemetry" e "Stop Telemetry"
        TableLayoutPanel telemetryButtonsRow = new TableLayoutPanel();
        telemetryButtonsRow.Dock = DockStyle.Fill;
        telemetryButtonsRow.ColumnCount = 2;
        telemetryButtonsRow.RowCount = 1;
        telemetryButtonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        telemetryButtonsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.Controls.Add(telemetryButtonsRow, 0, 0);

        // Pulsante Start Telemetry
        startTelemetryButton = new Button();
        startTelemetryButton.Text = "Start Telemetry";
        startTelemetryButton.Dock = DockStyle.Fill;
        startTelemetryButton.Margin = new Padding(5);
        startTelemetryButton.Font = new System.Drawing.Font(startTelemetryButton.Font.FontFamily, 10, FontStyle.Bold);
        telemetryButtonsRow.Controls.Add(startTelemetryButton, 0, 0);

        // Pulsante Stop Telemetry
        stopTelemetryButton = new Button();
        stopTelemetryButton.Text = "Stop Telemetry";
        stopTelemetryButton.Dock = DockStyle.Fill;
        stopTelemetryButton.Margin = new Padding(5);
        stopTelemetryButton.Font = new System.Drawing.Font(stopTelemetryButton.Font.FontFamily, 10, FontStyle.Bold);
        telemetryButtonsRow.Controls.Add(stopTelemetryButton, 1, 0);

        // SECONDA RIGA (precedentemente prima): Due celle affiancate per elementi grafici
        TableLayoutPanel topRow = new TableLayoutPanel();
        topRow.Dock = DockStyle.Fill;
        topRow.ColumnCount = 2;
        topRow.RowCount = 1;
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        topRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        mainLayout.Controls.Add(topRow, 0, 1); // Ora è alla riga 1 invece che 0

        // Aggiungo i due pannelli per gli elementi grafici
        panel1 = new Panel();
        panel1.Dock = DockStyle.Fill;
        //  panel1.BackColor = Color.LightBlue;
        panel1.BorderStyle = BorderStyle.FixedSingle;
        topRow.Controls.Add(panel1, 0, 0);

        //plotview init
        var plotModel1 = new PlotModel { Title = "Height Sensor" };

        plotModel1.DefaultFont = "Poppins";

        // Asse X in secondi, con range iniziale [0, windowWidth]
        xAxis1 = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            Maximum = windowWidth,
            Title = "Sample num"
        };
        plotModel1.Axes.Add(xAxis1);

        // Asse Y in valore assoluto 0-32767
        yAxis1 = new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = 0,
            Maximum = 32767,
        };
        plotModel1.Axes.Add(yAxis1);

        pointSeries1 = new LineSeries
        {
            MarkerType = OxyPlot.MarkerType.Cross,
            MarkerSize = 2,
            MarkerStroke = OxyColors.Black,
            MarkerFill = OxyColors.Red,
            LineStyle = LineStyle.Solid,
            Color = OxyColor.FromRgb(8, 72, 133)
        };

        plotModel1.Series.Add(pointSeries1);

        plotView1 = new PlotView
        {
            Dock = DockStyle.Fill, // Occupa tutto il Panel
            Model = plotModel1, // Aggiungi punti iniziali
        };

        panel1.Controls.Add(plotView1);

        panel2 = new Panel();
        panel2.Dock = DockStyle.Fill;
        //        panel2.BackColor = Color.LightGreen;
        panel2.BorderStyle = BorderStyle.FixedSingle;
        topRow.Controls.Add(panel2, 1, 0);

        //plotview init
        var plotModel2 = new PlotModel { Title = "Slope Sensor" };
        plotModel2.DefaultFont = "Poppins";

        // Asse X in secondi, con range iniziale [0, windowWidth]
        xAxis2 = new LinearAxis
        {
            Position = AxisPosition.Bottom,
            Minimum = 0,
            Maximum = windowWidth,
            Title = "Sample num"
        };

        plotModel2.Axes.Add(xAxis2);

        // Asse Y in valore assoluto 0-32767
        yAxis2 = new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = 0,
            Maximum = 32767,
        };
        plotModel2.Axes.Add(yAxis2);

        pointSeries2 = new LineSeries
        {
            MarkerType = OxyPlot.MarkerType.Cross,
            MarkerSize = 2,
            MarkerStroke = OxyColors.Black,
            MarkerFill = OxyColors.Red,
            LineStyle = LineStyle.Solid,
            Color = OxyColor.FromRgb(8, 72, 133)
        };

        plotModel2.Series.Add(pointSeries2);

        plotView2 = new PlotView
        {
            Dock = DockStyle.Fill, // Occupa tutto il Panel
            Model = plotModel2, // Aggiungi punti iniziali
        };
        panel2.Controls.Add(plotView2);

        //// 1. Crea un controller personalizzato
        //var controller = new PlotController();

        //// 2. Rimuovi il binding di default della rotellina
        //controller.UnbindMouseWheel();

        //// 3. Associa la rotellina solo allo zoom sull’asse X


        //// Crea un nuovo manipolatore personalizzato
        //var xAxisZoomer = new XAxisMouseWheelZoomManipulator(plotView1);
        //controller.BindMouseWheel(xAxisZoomer.HandleMouseWheel);
        ////// Aggiunge il nuovo manipolatore personalizzato
        ////controller.BindMouseWheel(OxyModifierKeys.None, xAxisZoomer.HandleMouseWheel);
        ////     controller.BindMouseWheel(PlotCommands.z);



        //// 4. Assegna il controller al tuo PlotView
        //plotView1.Controller = controller;
        //plotView2.Controller = controller;

        // SECONDA RIGA: 7 contenitori di immagini con label sotto
        TableLayoutPanel imageRow = new TableLayoutPanel();
        imageRow.Dock = DockStyle.Fill;
        imageRow.ColumnCount = 7;
        imageRow.RowCount = 2;
        for (int i = 0; i < 7; i++)
        {
            imageRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / 7F)); // Distribuiti equamente
        }
        imageRow.RowStyles.Add(new RowStyle(SizeType.Percent, 80F));  // Spazio per le immagini
        imageRow.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));  // Spazio per le label
        mainLayout.Controls.Add(imageRow, 0, 2); // Ora è alla riga 2 invece che 1

        // Creo i contenitori di immagini e le relative label
        imageContainers = new PictureBox[7];
        imageLabels = new System.Windows.Forms.Label[7];
        imageStates = new bool[7];

        for (int i = 0; i < 7; i++)
        {
            // Contenitore immagine
            imageContainers[i] = new PictureBox();
            imageContainers[i].Dock = DockStyle.Fill;
            imageContainers[i].SizeMode = PictureBoxSizeMode.Zoom;
            //         imageContainers[i].BorderStyle = BorderStyle.FixedSingle;
            imageContainers[i].BorderStyle = BorderStyle.None;
            //   imageContainers[i].BackColor = Color.White;
            imageContainers[i].Margin = new Padding(5);
            imageContainers[i].Tag = i;  // Salva l'indice per identificarlo
            if (i == 0) imageContainers[i].Image = Resources.ValvolaOff;
            else if (i == 1) imageContainers[i].Image = Resources.ValvolaOff;
            else if (i == 2) imageContainers[i].Image = Resources.ValvolaOff;
            else if (i == 3) imageContainers[i].Image = Resources.ValvolaOff;
            imageRow.Controls.Add(imageContainers[i], i, 0);

            // Label sotto l'immagine
            imageLabels[i] = new System.Windows.Forms.Label();
            if (i == 0) imageLabels[i].Text = "EVA";
            else if (i == 1) imageLabels[i].Text = "EVB";
            else if (i == 2) imageLabels[i].Text = "EVC";
            else if (i == 3) imageLabels[i].Text = "P2A";
            else if (i == 4) imageLabels[i].Text = "PUMP";
            else if (i == 5) imageLabels[i].Text = "LS1";
            else if (i == 6) imageLabels[i].Text = "LS2";
            //  imageLabels[i].Text = $"Elemento {i + 1}";
            imageLabels[i].Dock = DockStyle.Fill;
            imageLabels[i].TextAlign = ContentAlignment.MiddleCenter;
            imageLabels[i].Font = new System.Drawing.Font("Poppins", 10, FontStyle.Bold);
            imageRow.Controls.Add(imageLabels[i], i, 1);

            // Stato iniziale (false)
            imageStates[i] = false;
            UpdateImageDisplay(i);
        }

        // TERZA RIGA: TextBox (85%) e pulsante
        TableLayoutPanel buttonTextBoxRow = new TableLayoutPanel();
        buttonTextBoxRow.Dock = DockStyle.Fill;
        buttonTextBoxRow.ColumnCount = 2;
        buttonTextBoxRow.RowCount = 1;
        buttonTextBoxRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 85F));  // TextBox
        buttonTextBoxRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));  // Pulsante
        mainLayout.Controls.Add(buttonTextBoxRow, 0, 3); // Ora è alla riga 3 invece che 2

        // TextBox
        textBoxRow3 = new TextBox();
        textBoxRow3.Dock = DockStyle.Fill;
        textBoxRow3.Margin = new Padding(5);
        textBoxRow3.Multiline = true;
        buttonTextBoxRow.Controls.Add(textBoxRow3, 0, 0);

        // Pulsante
        buttonReadFaults = new Button();
        buttonReadFaults.Text = "Read Faults";
        buttonReadFaults.Font = new System.Drawing.Font("Poppins", 10, FontStyle.Bold);
        buttonReadFaults.Dock = DockStyle.Fill;
        buttonReadFaults.Margin = new Padding(5);
        buttonTextBoxRow.Controls.Add(buttonReadFaults, 1, 0);

        // QUARTA RIGA: 9 gruppi (label + textbox) e 2 pulsanti
        TableLayoutPanel bottomRow = new TableLayoutPanel();
        bottomRow.Dock = DockStyle.Fill;
        bottomRow.ColumnCount = 11;
        bottomRow.RowCount = 2;  // Una riga per le label, una per i textbox
        for (int i = 0; i < 9; i++)
        {
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, (8.8F)));  // 9 gruppi
        }
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));  // Pulsante read
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));  // Pulsante write
        bottomRow.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));  // Riga per label
        bottomRow.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));  // Riga per textbox/button
        mainLayout.Controls.Add(bottomRow, 0, 4); // Ora è alla riga 4 

        // Creo i 9 gruppi label + textbox
        labelsRow4 = new System.Windows.Forms.Label[9];
        textBoxesRow4 = new TextBox[9];

        for (int i = 0; i < 9; i++)
        {
            // Label
            labelsRow4[i] = new System.Windows.Forms.Label();
            if (i == 0) labelsRow4[i].Text = "Max Height";
            else if (i == 1) labelsRow4[i].Text = "Min Height";
            else if (i == 2) labelsRow4[i].Text = "Max Slope";
            else if (i == 3) labelsRow4[i].Text = "Min Slope";
            else if (i == 4) labelsRow4[i].Text = "Height 1";
            else if (i == 5) labelsRow4[i].Text = "Height 2";
            else if (i == 6) labelsRow4[i].Text = "Height 3";
            else if (i == 7) labelsRow4[i].Text = "Loading height";
            else labelsRow4[i].Text = "Initial Delay ms";

            labelsRow4[i].Dock = DockStyle.Fill;
            labelsRow4[i].TextAlign = ContentAlignment.BottomLeft;
            labelsRow4[i].Padding = new Padding(5, 0, 0, 5);
            bottomRow.Controls.Add(labelsRow4[i], i, 0);

            // TextBox
            textBoxesRow4[i] = new TextBox();
            textBoxesRow4[i].Dock = DockStyle.Fill;
            textBoxesRow4[i].Margin = new Padding(5);
            textBoxesRow4[i].Multiline = true;
            bottomRow.Controls.Add(textBoxesRow4[i], i, 1);
        }

        // Pulsante read settings from file
        buttonLoadSettings = new Button();
        buttonLoadSettings.Text = "LOAD FROM FILE";
        buttonLoadSettings.Font = new System.Drawing.Font("Poppins", 9, FontStyle.Bold);
        buttonLoadSettings.Dock = DockStyle.Fill;
        buttonLoadSettings.Margin = new Padding(5);
        bottomRow.Controls.Add(buttonLoadSettings, 9, 0);  // Occupa solo la riga inferiore

        // Pulsante write settings to file
        buttonSaveSettings = new Button();
        buttonSaveSettings.Text = "SAVE TO FILE";
        buttonSaveSettings.Font = new System.Drawing.Font("Poppins", 9, FontStyle.Bold);
        buttonSaveSettings.Dock = DockStyle.Fill;
        buttonSaveSettings.Margin = new Padding(5);
        bottomRow.Controls.Add(buttonSaveSettings, 10, 0);  // Occupa solo la riga inferiore

        // Pulsante read settings
        buttonReadSettings = new Button();
        buttonReadSettings.Text = "READ FROM BOARD";
        buttonReadSettings.Font = new System.Drawing.Font("Poppins", 9, FontStyle.Bold);
        buttonReadSettings.Dock = DockStyle.Fill;
        buttonReadSettings.Margin = new Padding(5);
        bottomRow.Controls.Add(buttonReadSettings, 9, 1);  // Occupa solo la riga inferiore

        // Pulsante write settings
        buttonWriteSettings = new Button();
        buttonWriteSettings.Text = "WRITE TO BOARD";
        buttonWriteSettings.Font = new System.Drawing.Font("Poppins", 9, FontStyle.Bold);
        buttonWriteSettings.Dock = DockStyle.Fill;
        buttonWriteSettings.Margin = new Padding(5);
        bottomRow.Controls.Add(buttonWriteSettings, 10, 1);  // Occupa solo la riga inferiore
    }

    private void SetupEventHandlers()
    {
        // Aggiungi qui i gestori eventi per i pulsanti e altri controlli
        startTelemetryButton.Click += StartTelemetryButton_Click;
        stopTelemetryButton.Click += StopTelemetryButton_Click;
        buttonReadFaults.Click += buttonReadFaults_Click;
        buttonReadSettings.Click += buttonReadSettings_Click;
        buttonWriteSettings.Click += buttonWriteSettings_Click;
        buttonSaveSettings.Click += BtnSave_Click;
        buttonLoadSettings.Click += BtnLoad_Click;
    }

    private void ResetPlot(LineSeries pointSeries, PlotView plotView)
    {
        // 1) Pulisci i dati esistenti
        pointSeries.Points.Clear();

        // 2) Ripristini la finestra X originale
        xAxis1.Minimum = 0;
        xAxis1.Maximum = windowWidth;

        // 3) Ridisegni (importante per aggiornare subito la griglia vuota)
        plotView.InvalidatePlot(true);
    }

    private async void StartTelemetryButton_Click(object sender, EventArgs e)
    {
        var tel = _connMgr.CurrentTelemetry;
        if (tel is null) { MessageBox.Show("Select communication channel first!"); return; }

        await tel.StopTelemetryAsync();
        tel.ResetDictionary();

        //Azzera il grafico oxyplot
        time = 0;
        ResetPlot(pointSeries1, plotView1);
        ResetPlot(pointSeries2, plotView2);

        //Carica in telemetria i dati valvole e pompa
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Stato EVA")]);
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Stato EVB")]);
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Stato EVC")]);
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Valore RAW del potenzio altezza")]);
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Stato P2A")]);
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Stato pompa")]);
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Stato finecorsa ")]);
        tel.AddToDictionary(MachineDictionary[GetVariableIndex("Valore RAW del potenzio inclinazione")]);

        await tel.StartFastTelemetryAsync();
    }

    private int GetVariableIndex(String Name)
    {
        for (int i = 0; i < MachineDictionary.Count; i++)
        {
            if (MachineDictionary[i].Name == Name) return i;
        }

        return -1;
    }

    private async void StopTelemetryButton_Click(object sender, EventArgs e)
    {
        var tel = _connMgr.CurrentTelemetry;
        if (tel is null) return;
        await tel.StopTelemetryAsync();
    }

    private async void buttonReadFaults_Click(object sender, EventArgs e)
    {
        var tel = _connMgr.CurrentTelemetry;
        if (tel is null) { MessageBox.Show("Select communication channel first!"); return; }

        await tel.StopTelemetryAsync();
        tel.ResetDictionary();

        //Carica in telemetria i fault (5 richieste per robustezza verso pacchetti persi, parità legacy).
        for (int i = 0; i < 5; i++)
            tel.AddToDictionary(MachineDictionary[GetVariableIndex("Allarmi")]);
        await tel.ReadOneShotAsync();
    }

    private async void buttonReadSettings_Click(object sender, EventArgs e)
    {
        var tel = _connMgr.CurrentTelemetry;
        if (tel is null) { MessageBox.Show("Select communication channel first!"); return; }

        await tel.StopTelemetryAsync();
        tel.ResetDictionary();

        //Carica in telemetria i parametri (lista duplicata per robustezza pacchetti persi, parità legacy).
        string[] settings =
        {
            "Potenzio inclinazione giu'", "Potenzio inclinazione orizz.",
            "Potenzio altezza max", "Potenzio altezza min", "Potenzio altezza min",
            "Altezza di carico", "Altezza 1 da chiuso", "Altezza 2 da chiuso",
            "Altezza 3 da chiuso", "Tempo di ritardo all'accensione"
        };
        for (int rep = 0; rep < 2; rep++)
            foreach (var name in settings)
                tel.AddToDictionary(MachineDictionary[GetVariableIndex(name)]);
        await tel.ReadOneShotAsync();
    }

    private async void buttonWriteSettings_Click(object sender, EventArgs e)
    {
        var tel = _connMgr.CurrentTelemetry;
        if (tel is null) { MessageBox.Show("Select communication channel first!"); return; }

        await tel.StopTelemetryAsync();
        tel.ResetDictionary();

        //Carica in telemetria i parametri da scrivere (lista duplicata, parità legacy).
        (string varName, int textBoxIndex)[] writes =
        {
            ("Potenzio inclinazione giu'", 3),
            ("Potenzio inclinazione orizz.", 2),
            ("Potenzio altezza max", 0),
            ("Potenzio altezza min", 1),
            ("Altezza di carico", 4),
            ("Altezza 1 da chiuso", 5),
            ("Altezza 2 da chiuso", 6),
            ("Altezza 3 da chiuso", 7),
            ("Tempo di ritardo all'accensione", 8)
        };
        for (int rep = 0; rep < 2; rep++)
            foreach (var (varName, idx) in writes)
                tel.AddToDictionaryForWrite(MachineDictionary[GetVariableIndex(varName)], textBoxesRow4[idx].Text);

        await tel.WriteOneShotAsync();

        buttonReadSettings_Click(this, EventArgs.Empty);
    }

    /// <summary>
    /// Metodo pubblico per aggiornare lo stato e la visualizzazione di un'immagine
    /// </summary>
    /// <param name="index">Indice del contenitore immagine (0-4)</param>
    /// <param name="state">Nuovo stato booleano</param>
    public void UpdateImageState(int index, bool state)
    {
        if (index >= 0 && index < imageContainers.Length)
        {
            imageStates[index] = state;
            UpdateImageDisplay(index);
        }
    }

    private void UpdateImageDisplay(int index)
    {
        if (index > 6) return;

        if (index < 4)
        {
            // Cambia l'immagine in base allo stato
            if (imageStates[index])
            {
                imageContainers[index].Image = Resources.ValvolaOn;
            }
            else
            {
                imageContainers[index].Image = Resources.ValvolaOff;
            }
        }
        else if (index == 4)
        {
            // Cambia l'immagine in base allo stato
            if (imageStates[index])
            {
                imageContainers[index].Image = Resources.PompaOn;
            }
            else
            {
                imageContainers[index].Image = Resources.PompaOff;
            }
        }
        else if (index == 5)
        {
            // Cambia l'immagine in base allo stato
            if (imageStates[index])
            {
                imageContainers[index].Image = Resources.LSOn;
            }
            else
            {
                imageContainers[index].Image = Resources.LSOff;
            }
        }
        else if (index == 6)
        {
            // Cambia l'immagine in base allo stato
            if (imageStates[index])
            {
                imageContainers[index].Image = Resources.LSOn;
            }
            else
            {
                imageContainers[index].Image = Resources.LSOff;
            }
        }
    }

    private void UpdatePlot(uint newValue, LineSeries pointSeries, LinearAxis xAxis, PlotView plotView)
    {
        // return;
        // Simulo un segnale: qui puoi inserire il tuo dato in tempo reale
        //   double newValue = Math.Sin(2 * Math.PI * 0.5 * time);

        // Aggiungo il nuovo punto
        pointSeries.Points.Add(new OxyPlot.DataPoint(time, newValue));

        // Scorro la finestra sull'asse X
        if (time > windowWidth)
        {
            xAxis.Minimum = time - windowWidth;
            xAxis.Maximum = time;
        }

        // Evita che la serie cresca indefinitamente (opzionale)
        if (pointSeries.Points.Count > 1000)
            pointSeries.Points.RemoveAt(0);

        // Ridisegno il plot
        plotView.InvalidatePlot(true);
    }

    private void onDataReady(object sender, TelemetryDataPoint dp)
    {
        //Verifica quale variabile arriva e completa i campi di conseguenza
        String VarName = dp.Variable.Name;
        uint value = dp.NumericValue;
        switch (VarName)
        {
            case "Stato EVA":
                if (value != 0) imageStates[0] = true;
                else imageStates[0] = false;
                UpdateImageDisplay(0);
                break;
            case "Stato EVB":
                if (value != 0) imageStates[1] = true;
                else imageStates[1] = false;
                UpdateImageDisplay(1);
                break;
            case "Stato EVC":
                if (value != 0) imageStates[2] = true;
                else imageStates[2] = false;
                UpdateImageDisplay(2);
                break;
            case "Stato P2A":
                if (value != 0) imageStates[3] = true;
                else imageStates[3] = false;
                UpdateImageDisplay(3);
                break;
            case "Stato pompa":
                if (value != 0) imageStates[4] = true;
                else imageStates[4] = false;
                UpdateImageDisplay(4);
                break;
            case "Stato finecorsa ":
                if ((value & 0x01) != 0) imageStates[5] = true;
                else imageStates[5] = false;
                UpdateImageDisplay(5);

                if ((value & 0x02) != 0) imageStates[6] = true;
                else imageStates[6] = false;
                UpdateImageDisplay(6);
                break;
            case "Valore RAW del potenzio altezza":
                {
                    time += 1;
                    if (value != 0)
                        UpdatePlot(value, pointSeries1, xAxis1, plotView1);
                }
                break;
            case "Valore RAW del potenzio inclinazione":
                {
                    time += 1;
                    if (value != 0)
                        UpdatePlot(value, pointSeries2, xAxis2, plotView2);
                }
                break;
            case "Allarmi":
                {
                    string FaultString = null;
                    uint FaultValue = value;

                    // FaultValue = 0xFFFFFFFF;

                    if (FaultValue != 0)
                    {
                        if ((FaultValue & 0x00000001) != 0) // Controlla se il bit 0 è 1
                        {
                            FaultString += " Pump Overcurrent ";
                        }
                        if ((FaultValue & 0x00000002) != 0)
                        {
                            FaultString += " Pump Open Circuit ";
                        }
                        if ((FaultValue & 0x00000004) != 0)
                        {
                            FaultString += " EVA Overcurrent ";
                        }
                        if ((FaultValue & 0x00000008) != 0)
                        {
                            FaultString += " EVA Open Circuit ";
                        }
                        if ((FaultValue & 0x00000010) != 0)
                        {
                            FaultString += " EVB Overcurrent ";
                        }
                        if ((FaultValue & 0x00000020) != 0)
                        {
                            FaultString += " EVB Open Circuit ";
                        }
                        if ((FaultValue & 0x00000040) != 0)
                        {
                            FaultString += " EVC Overcurrent ";
                        }
                        if ((FaultValue & 0x00000080) != 0)
                        {
                            FaultString += " EVC Open Circuit ";
                        }
                        if ((FaultValue & 0x00000100) != 0)
                        {
                            FaultString += " P2A Overcurrent ";
                        }
                        if ((FaultValue & 0x00000200) != 0)
                        {
                            FaultString += " P2A Open Circuit ";
                        }
                        if ((FaultValue & 0x00040000) != 0)
                        {
                            FaultString += " Low battery ";
                        }
                        if ((FaultValue & 0x00100000) != 0)
                        {
                            FaultString += " Software error ";
                        }
                        if ((FaultValue & 0x00200000) != 0)
                        {
                            FaultString += " EEPROM error ";
                        }
                    }
                    else FaultString = "No active Faults";

                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(() => textBoxRow3.Text = FaultString));
                    }
                    else
                    {
                        textBoxRow3.Text = FaultString;
                    }
                }
                break;
            case "Potenzio inclinazione giu'":
                SetTextBoxSafe(textBoxesRow4[3], value.ToString());
                break;
            case "Potenzio inclinazione orizz.":
                SetTextBoxSafe(textBoxesRow4[2], value.ToString());
                break;
            case "Potenzio altezza max":
                SetTextBoxSafe(textBoxesRow4[0], value.ToString());
                break;
            case "Potenzio altezza min":
                SetTextBoxSafe(textBoxesRow4[1], value.ToString());
                break;
            case "Altezza di carico":
                SetTextBoxSafe(textBoxesRow4[4], value.ToString());
                break;
            case "Altezza 1 da chiuso":
                SetTextBoxSafe(textBoxesRow4[5], value.ToString());
                break;
            case "Altezza 2 da chiuso":
                SetTextBoxSafe(textBoxesRow4[6], value.ToString());
                break;
            case "Altezza 3 da chiuso":
                SetTextBoxSafe(textBoxesRow4[7], value.ToString());
                break;
            case "Tempo di ritardo all'accensione":
                SetTextBoxSafe(textBoxesRow4[8], value.ToString());
                break;
            default:
                break;
        }
    }

    private void SetTextBoxSafe(TextBox tb, string text)
    {
        if (tb.InvokeRequired)
            tb.BeginInvoke(new Action(() => tb.Text = text));
        else
            tb.Text = text;
    }

    private void BtnSave_Click(object sender, EventArgs e)
    {
        using (SaveFileDialog sfd = new SaveFileDialog())
        {
            sfd.Title = "Save Parameters";
            sfd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            sfd.DefaultExt = "txt";
            if (sfd.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                var lines = new string[]
                {
                        $"{labelsRow4[0].Text}: {textBoxesRow4[0].Text}",
                        $"{labelsRow4[1].Text}: {textBoxesRow4[1].Text}",
                        $"{labelsRow4[2].Text}: {textBoxesRow4[2].Text}",
                        $"{labelsRow4[3].Text}: {textBoxesRow4[3].Text}",
                        $"{labelsRow4[4].Text}: {textBoxesRow4[4].Text}",
                        $"{labelsRow4[5].Text}: {textBoxesRow4[5].Text}",
                        $"{labelsRow4[6].Text}: {textBoxesRow4[6].Text}",
                        $"{labelsRow4[7].Text}: {textBoxesRow4[7].Text}",
                        $"{labelsRow4[8].Text}: {textBoxesRow4[8].Text}"
                };

                File.WriteAllLines(sfd.FileName, lines);
                MessageBox.Show("Parameters saved succesfully:\n" + sfd.FileName,
                                "Save Parameters", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Errore in saving parameters:\n" + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private void BtnLoad_Click(object sender, EventArgs e)
    {
        using (OpenFileDialog ofd = new OpenFileDialog())
        {
            ofd.Title = "Load Parameters";
            ofd.Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*";
            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            try
            {
                string[] lines = File.ReadAllLines(ofd.FileName);
                if (lines.Length < 9)
                {
                    MessageBox.Show($"File corrupted or incomplete : {lines.Length} parameters found.",
                                    "Load Parameters", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Assumi formato "Label: Value"
                string[] parts;
                parts = lines[0].Split(new[] { ':', }, 2);
                textBoxesRow4[0].Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                parts = lines[1].Split(new[] { ':', }, 2);
                textBoxesRow4[1].Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                parts = lines[2].Split(new[] { ':', }, 2);
                textBoxesRow4[2].Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                parts = lines[3].Split(new[] { ':', }, 2);
                textBoxesRow4[3].Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                parts = lines[4].Split(new[] { ':', }, 2);
                textBoxesRow4[4].Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                parts = lines[5].Split(new[] { ':', }, 2);
                textBoxesRow4[5].Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                parts = lines[6].Split(new[] { ':', }, 2);
                textBoxesRow4[6].Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                parts = lines[7].Split(new[] { ':', }, 2);
                textBoxesRow4[7].Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                parts = lines[8].Split(new[] { ':', }, 2);
                textBoxesRow4[8].Text = parts.Length > 1 ? parts[1].Trim() : string.Empty;

                MessageBox.Show("Parameters loaded succesfully from:\n" + ofd.FileName,
                                "Load Parameters", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading parameters:\n" + ex.Message,
                                "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

