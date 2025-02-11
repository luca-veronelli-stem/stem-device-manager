using System;
using System.Windows.Forms;

using BLE_Handler;

// Classe per l'interfaccia grafica
public partial class BLEInterfaceTab : TabPage
{
        private TabControl tabControl;
        private TabPage tabDevices;
        private ListBox listBoxDevices;
        private Button btnScan;

        // Istanza del backend per la gestione del BLE
        private BLEManager bleManager;

        public BLEInterfaceTab()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            // Impostazioni base della form
            this.Text = "BLE Scanner";
            this.Width = 800;
            this.Height = 600;

          
            // Crea il ListBox che mostrerà i nomi dei dispositivi
            listBoxDevices = new ListBox();
            listBoxDevices.Location = new System.Drawing.Point(10, 50);
            listBoxDevices.Size = new System.Drawing.Size(760, 450);

            // Crea il bottone per avviare la scansione
            btnScan = new Button();
            btnScan.Text = "Scansiona";
            btnScan.Location = new System.Drawing.Point(10, 10);
            btnScan.Click += BtnScan_Click;

            // Aggiunge i controlli alla TabPage
            Controls.Add(btnScan);
            Controls.Add(listBoxDevices);

            // Inizializza il backend per il BLE e sottoscrive l’evento per ricevere i dispositivi trovati
            bleManager = new BLEManager();
            bleManager.OnDeviceDiscovered += BleManager_OnDeviceDiscovered;
        }

        /// <summary>
        /// Gestore del click sul bottone per avviare la scansione
        /// </summary>
        private void BtnScan_Click(object sender, EventArgs e)
        {
            // Pulisce la lista dei dispositivi (se si vuole effettuare una nuova scansione)
            listBoxDevices.Items.Clear();
            bleManager.StartScanning();
        }

        /// <summary>
        /// Gestore dell'evento del backend quando viene scoperto un nuovo dispositivo BLE.
        /// Poiché l'evento potrebbe essere chiamato da un thread diverso, si usa Invoke per aggiornare la UI.
        /// </summary>
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

        /// <summary>
        /// Aggiunge il nome del dispositivo alla ListBox se non è già presente.
        /// </summary>
        private void AddDeviceToList(string deviceName)
        {
            if (!listBoxDevices.Items.Contains(deviceName))
            {
                listBoxDevices.Items.Add(deviceName);
            }
        }
}