using System;
using System.Drawing;
//using System.Reflection.Emit;
using System.Windows.Forms;

using Stem_Protocol.PacketManager;
using Stem_Protocol.TelemetryManager;
using STEMPM.Properties;

using OxyPlot;
using OxyPlot.Series;
using OxyPlot.WindowsForms;
using DocumentFormat.OpenXml.EMMA;

public class TopLiftTelemetry_Tab : TabPage
{
    //Lista variabili della macchina
    private List<ExcelHandler.VariableData> MachineDictionary;

    // Classe per il backend
    public TelemetryManager telemetryManager;


    // Dichiarazione dei controlli
    private Panel panel1;
    private Panel panel2;
    private Button startTelemetryButton;
    private Button stopTelemetryButton;
    private PictureBox[] imageContainers;
    private Label[] imageLabels;
    private Button buttonReadFaults;
    private TextBox textBoxRow3;
    private Label[] labelsRow4;
    private TextBox[] textBoxesRow4;
    private Button buttonReadSettings;
    private Button buttonWriteSettings;

    //Plotview section
    private LineSeries pointSeries1;
    private PlotView plotView1;

    private LineSeries pointSeries2;
    private PlotView plotView2;

    // Variabili booleane associate ai contenitori di immagini
    private bool[] imageStates;

    public TopLiftTelemetry_Tab(PacketManager packetManagerRX)
    {
        InitializeComponent();
        InitializeCustomComponents();
        SetupEventHandlers();

        // Creazione del gestore per la telemetria
        telemetryManager = new TelemetryManager(packetManagerRX);

        // Aggiunta del gestore per l'evento DataReady
        telemetryManager.DataReady += onDataReady;
    }

    public void UpdateDictionary(List<ExcelHandler.VariableData> Dictionary)
    {
        MachineDictionary = Dictionary;
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
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));  // Quarta riga (pulsante e textbox) - più bassa
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 10F));  // Quinta riga (gruppi e pulsante) - più bassa
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
        startTelemetryButton.Font = new Font(startTelemetryButton.Font.FontFamily, 10, FontStyle.Bold);
        telemetryButtonsRow.Controls.Add(startTelemetryButton, 0, 0);

        // Pulsante Stop Telemetry
        stopTelemetryButton = new Button();
        stopTelemetryButton.Text = "Stop Telemetry";
        stopTelemetryButton.Dock = DockStyle.Fill;
        stopTelemetryButton.Margin = new Padding(5);
        stopTelemetryButton.Font = new Font(stopTelemetryButton.Font.FontFamily, 10, FontStyle.Bold);
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
        pointSeries1 = new LineSeries
        {
            MarkerType = MarkerType.Circle,
            MarkerSize = 3,
            MarkerStroke = OxyColors.Black,
            MarkerFill = OxyColors.Red,
            LineStyle = LineStyle.Solid,
            Points =
            {
                new DataPoint(0, 0),
                new DataPoint(10, 18),
                new DataPoint(20, 12)
            }
        };
        plotModel1.Series.Add(pointSeries1);
        plotView1 = new PlotView
        {
            Dock = DockStyle.Fill, // Occupa tutto il Panel
            Model = plotModel1, // Aggiungi punti iniziali
        };
        // plotView1.Model = 
        panel1.Controls.Add(plotView1);

        panel2 = new Panel();
        panel2.Dock = DockStyle.Fill;
//        panel2.BackColor = Color.LightGreen;
        panel2.BorderStyle = BorderStyle.FixedSingle;
        topRow.Controls.Add(panel2, 1, 0);

        //plotview init
        var plotModel2 = new PlotModel { Title = "Slope Sensor" };
        pointSeries2 = new LineSeries
        {
            MarkerType = MarkerType.Circle,
            MarkerSize = 3,
            MarkerStroke = OxyColors.Black,
            MarkerFill = OxyColors.Red,
            LineStyle = LineStyle.Solid,
            Points =
            {
                new DataPoint(0, 0),
                new DataPoint(10, 6),
                new DataPoint(20, 18)
            }
        };
        plotModel2.Series.Add(pointSeries2);
        plotView2 = new PlotView
        {
            Dock = DockStyle.Fill, // Occupa tutto il Panel
            Model = plotModel2, // Aggiungi punti iniziali
        };
        // plotView1.Model = 
        panel2.Controls.Add(plotView2);

        // SECONDA RIGA: 7 contenitori di immagini con label sotto (5+2)
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
        imageLabels = new Label[7]; 
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
            //else if (i == 4) imageLabels[i].Text = "PUMP";
            //else if (i == 5) imageLabels[i].Text = "LS1";
            //else if (i == 6) imageLabels[i].Text = "LS2";)
            imageRow.Controls.Add(imageContainers[i], i, 0);

            // Label sotto l'immagine
            imageLabels[i] = new Label();
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
            imageLabels[i].Font = new Font("Poppins", 10, FontStyle.Bold);
            imageRow.Controls.Add(imageLabels[i], i, 1);

            // Stato iniziale (false)
            if (i == 1) imageStates[i] = true;
            else imageStates[i] = false;
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
        buttonReadFaults.Font = new Font("Poppins", 10, FontStyle.Bold);
        buttonReadFaults.Dock = DockStyle.Fill;
        buttonReadFaults.Margin = new Padding(5);
        buttonTextBoxRow.Controls.Add(buttonReadFaults, 1, 0);

        // QUARTA RIGA: 4 gruppi (label + textbox) e un pulsante
        TableLayoutPanel bottomRow = new TableLayoutPanel();
        bottomRow.Dock = DockStyle.Fill;
        bottomRow.ColumnCount = 6;
        bottomRow.RowCount = 2;  // Una riga per le label, una per i textbox
        for (int i = 0; i < 4; i++)
        {
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 22.5F));  // 4 gruppi
        }
        bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 10F));  // Pulsante finale
        bottomRow.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));  // Riga per label
        bottomRow.RowStyles.Add(new RowStyle(SizeType.Percent, 60F));  // Riga per textbox/button
        mainLayout.Controls.Add(bottomRow, 0, 4); // Ora è alla riga 4 invece che 3

        // Creo i 4 gruppi label + textbox
        labelsRow4 = new Label[4];
        textBoxesRow4 = new TextBox[4];

        for (int i = 0; i < 4; i++)
        {
            // Label
            labelsRow4[i] = new Label();
            if (i == 0) labelsRow4[i].Text = "Max Height";
            else if (i == 1) labelsRow4[i].Text = "Min Height";
            else if (i == 2) labelsRow4[i].Text = "Max Slope";
            else if (i == 3) labelsRow4[i].Text = "Min Slope";
            //labelsRow4[i].Text = $"Campo {i + 1}";
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

        // Pulsante read settings
        buttonReadSettings = new Button();
        buttonReadSettings.Text = "READ";
        buttonReadSettings.Font = new Font("Poppins", 10, FontStyle.Bold);
        buttonReadSettings.Dock = DockStyle.Fill;
        buttonReadSettings.Margin = new Padding(5);
        bottomRow.Controls.Add(buttonReadSettings, 4, 1);  // Occupa solo la riga inferiore

        // Pulsante write settings
        buttonWriteSettings = new Button();
        buttonWriteSettings.Text = "WRITE";
        buttonWriteSettings.Font = new Font("Poppins", 10, FontStyle.Bold);
        buttonWriteSettings.Dock = DockStyle.Fill;
        buttonWriteSettings.Margin = new Padding(5);
        bottomRow.Controls.Add(buttonWriteSettings, 5, 1);  // Occupa solo la riga inferiore
    }

    private void SetupEventHandlers()
    {
        // Aggiungi qui i gestori eventi per i pulsanti e altri controlli
        startTelemetryButton.Click += StartTelemetryButton_Click;
        stopTelemetryButton.Click += StopTelemetryButton_Click;
        buttonReadFaults.Click += buttonReadFaults_Click;
        buttonReadSettings.Click += buttonReadSettings_Click;
    }

    private void StartTelemetryButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Avvio telemetria...", "Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Information);
        telemetryManager.ResetDictionary();
        telemetryManager.AddToDictionary(MachineDictionary[1]);
        telemetryManager.TelemetryStart();
        // Qui inserisci la logica per avviare la telemetria
    }

    private void StopTelemetryButton_Click(object sender, EventArgs e)
    {
        MessageBox.Show("Arresto telemetria...", "Telemetria", MessageBoxButtons.OK, MessageBoxIcon.Information);
        telemetryManager.TelemetryStop();
        // Qui inserisci la logica per arrestare la telemetria
    }

    private void buttonReadFaults_Click(object sender, EventArgs e)
    {
        MessageBox.Show($"Hai inserito: {textBoxRow3.Text}", "Messaggio", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void buttonReadSettings_Click(object sender, EventArgs e)
    {
        //string messaggio = "Valori inseriti:\n";
        //for (int i = 0; i < 4; i++)
        //{
        //    messaggio += $"{labelsRow4[i].Text}: {textBoxesRow4[i].Text}\n";
        //}
      //  MessageBox.Show(messaggio, "Dati Letti", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        if (index > 3) return;

        // Cambia l'immagine in base allo stato
        if (imageStates[index])
        {
            imageContainers[index].Image=Resources.ValvolaOn;
            // Qui dovresti caricare l'immagine per lo stato true
            // Per ora utilizziamo colori diversi per simulare il cambio di immagine
           // imageContainers[index].BackColor = Color.LightGreen;
           // imageLabels[index].Text = $"Elemento {index + 1} (ON)";
        }
        else
        {
            imageContainers[index].Image = Resources.ValvolaOff;
            // Qui dovresti caricare l'immagine per lo stato false
         //   imageContainers[index].BackColor = Color.LightGray;
         //   imageLabels[index].Text = $"Elemento {index + 1} (OFF)";
        }
    }

    private async void onDataReady(object sender, DataReadyEventArgs e)
    {
        //var container = activeElements[e.ListIndex];
        //var control = container.Controls[1];
        //if (control is Label label)
        //{
        //    await Task.Run(() => label.Invoke((MethodInvoker)(() => label.Text = telemetryManager.GetVariableName(e.ListIndex) + " " + e.Value.ToString())));
        //}
        //else
        //{
        //    MessageBox.Show("Il controllo non è una Label.", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //}
    }
}
