using System;
using System.Drawing;
using System.Windows.Forms;
using BLE_Handler;

public partial class BLEInterfaceTab : TabPage
{
    private ListBox listBoxDevices;
    private Button btnScan;
    private PictureBox loadingSpinner;
    private BLEManager bleManager;

    public BLEInterfaceTab()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "BLE Scanner";
        this.Width = 800;
        this.Height = 600;

        listBoxDevices = new ListBox();
        listBoxDevices.Location = new System.Drawing.Point(10, 50);
        listBoxDevices.Size = new System.Drawing.Size(760, 450);

        btnScan = new Button();
        btnScan.Text = "Scansiona";
        btnScan.Location = new System.Drawing.Point(10, 10);
        btnScan.Click += BtnScan_Click;

        // Spinner animato vicino al pulsante
        loadingSpinner = new PictureBox();
        loadingSpinner.Image = Image.FromFile("loading.gif"); // Assicurati di avere un file GIF di caricamento
        loadingSpinner.SizeMode = PictureBoxSizeMode.StretchImage;
        loadingSpinner.Size = new System.Drawing.Size(20, 20);
        loadingSpinner.Location = new System.Drawing.Point(120, 13); // Posizione accanto al bottone
        loadingSpinner.Visible = false; // Nasconde lo spinner all'inizio

        Controls.Add(btnScan);
        Controls.Add(listBoxDevices);
        Controls.Add(loadingSpinner);

        bleManager = new BLEManager();
        bleManager.OnDeviceDiscovered += BleManager_OnDeviceDiscovered;
        bleManager.OnScanCompleted += BleManager_OnScanCompleted; // Aggiungi un evento di fine scansione
    }

    private void BtnScan_Click(object sender, EventArgs e)
    {
        listBoxDevices.Items.Clear();
        loadingSpinner.Visible = true;  // Mostra l'animazione
        bleManager.StartScanning();
    }

    private void BleManager_OnDeviceDiscovered(string deviceName)
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(() => AddDeviceToList(deviceName)));
        }
        else
        {
            AddDeviceToList(deviceName);
        }
    }

    private void AddDeviceToList(string deviceName)
    {
        if (!listBoxDevices.Items.Contains(deviceName))
        {
            listBoxDevices.Items.Add(deviceName);
        }
    }

    // Evento che nasconde l'animazione quando la scansione termina
    private void BleManager_OnScanCompleted()
    {
        if (this.InvokeRequired)
        {
            this.Invoke(new Action(() => loadingSpinner.Visible = false));
        }
        else
        {
            loadingSpinner.Visible = false;
        }
    }
}