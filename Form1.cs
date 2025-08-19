using CanDataLayer;
using DocumentFormat.OpenXml.Drawing.Diagrams;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.VisualBasic.Logging;
using SerialDataLayer;
using SerialPort_Handler;
using Stem_Protocol;
using Stem_Protocol.PacketManager;
using Stem_Protocol.TelemetryManager;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports; // used for serial port
using System.Reflection;
using System.Windows.Forms;
using Windows.Devices.Enumeration;
using static ExcelHandler;
using static Stem_Protocol.NetworkLayer;

namespace StemPC
{
    public partial class Form1 : Form
    {
        public const string Software_Version = "2.12 (pcan a 250k)";

#if TOPLIFT
        public string CommunicationPort = "can";
#elif EDEN
        public string CommunicationPort = "ble";
#else
        public string CommunicationPort = "ble";
#endif


        private UInt16 Prescaler1s = 0;

        //**************************
        //  Terminal variablesc
        //************************** 
        private Terminal _terminal;


        //**********************************
        //   CAN port variables
        //**********************************
        public CANDataLayer _CDL;

        //**********************************
        //   BLE port variables
        //**********************************
        public SDL _BLE_SDL;

        //**********************************
        //   Serial port variables
        //**********************************
        private SerialPortManager _serialPortManager;
        public  SDL _SDL;

        //**************************
        //  Code gen variables
        //**************************
        // Esempio di lista di stringhe popolata tramite interfaccia grafica
        List<string> configurazioni = new List<string>
        {
            "CAN_COMM_CHANNEL_USED",
            "NUM_ACTIVE_CAN_PORTS=2",
            "SER_COMM_CHANNEL_USED",
            "SIZEOF_SERIAL_RX_BUFFER=250",
            "SIZEOF_SERIAL_TX_BUFFER=100",
            "NUM_ACTIVE_SERIAL_PORTS=1",
            "SP_NL_MINIMUM_TIME_BETWEEN_PACKETS=15",
            "SP_NL_TX_QUEUE_SIZE=30",
            "SP_ROUTER_COMMUNICATION_CHANNELS=3",
            "SP_ROUTER_CROSS_TABLE_N_ENTRIES=3",
            "SP_DEVICE_TABLE_N_ENTRIES=2",
            "BUFFER_TX_LEN_MAX=100",
            "BUFFER_RX_LEN_MAX=1100",
            "LOGS_FAT_SIZE=100",
            "MAX_NUM_OF_VARIABLES_IN_LOG=30",
            "CONFIG_TABLE_ELEMENT_SIZE=20"
        };
        string codeFilePath;
        SP_Code_Generator configGenerator;

        //**************************
        //  Excel variables
        //**************************
        string ExcelfilePath = "Dizionari STEM.xlsx";
        // Lista per contenere le righe lette
        List<ExcelHandler.RowData> IndirizziProtocollo;
        List<ExcelHandler.CommandData> Comandi;
        List<ExcelHandler.VariableData> Dizionario;
        ExcelHandler hExcel;

        //**********************************
        //  STEM Protocol variables/classes
        //**********************************
        public uint RecipientId;
        public short SelectedCommand;
        public uint senderId;              // ID del mittente

        // Ricezione globale dei pacchetti stem (pe rora una sola, da far poi diventare dinamica alla ricezione di ogni pacchetto)
        public PacketManager RXpacketManager;

        //******************************
        //  public Elements instances
        //******************************
        public CANInterfaceTab CanTabPageRef { get; private set; }
        public Boot_Interface_Tab BootTabRef { get; private set; }
        public Boot_Smart_Tab BootSmartTabRef { get; private set; }
        public static Form1 FormRef { get; private set; }
        public Telemetry_Tab TelemetryTabRef { get; private set; }
        public BLEInterfaceTab BLETabRef { get; private set; }
        public TopLiftTelemetry_Tab TLTTabRef { get; private set; }

        //**************************
        //  Events
        //**************************
        // Classe per passare i dati di aggiornamneto textbox dopo la decodifica di un comando applayer
        public class AppLayerDecoderEventArgs : EventArgs
        {
            public byte[] Payload { get; }
            public ExcelHandler.CommandData CurrentCommand { get; }
            public string MachineName { get; }
            public string MachineNameRecipient { get; }
            public ExcelHandler.VariableData CurrentVariable { get; }

            public AppLayerDecoderEventArgs(byte[] payload, ExcelHandler.CommandData currentCommand, string machineName, string machineNameRecipient, VariableData currentVariable)
            {
                Payload = payload;
                CurrentCommand = currentCommand;
                MachineName = machineName;
                MachineNameRecipient = machineNameRecipient;
                CurrentVariable = currentVariable;
            }
        }
        public event EventHandler<AppLayerDecoderEventArgs> AppLayerCommandDecoded;

        // Classe per passare i dati di aggiornamneto textbox dopo la decodifica di un comando applayer
        public class AppLayerSendEventArgs : EventArgs
        {
            public NetworkLayer NetLayer { get; }

            public AppLayerSendEventArgs(NetworkLayer netLayer)
            {
                NetLayer = netLayer;
            }
        }

        public event EventHandler<AppLayerSendEventArgs> AppLayerCommandSended;

        public Form1()
        {
            InitializeComponent();

            labelBytes.Text = "Altri Bytes \r\n (HEX) separati da spazio";

#if TOPLIFT
            Text = "STEM Toplift A2 Manager " + Software_Version;
#elif EDEN
            Text = "STEM Eden XP Manager " + Software_Version;
#elif EGICON
            Text = "STEM Spark Manager " + Software_Version;
#else
            this.Text += Software_Version;
#endif

            FormRef = this;

            RecipientId = 0;
            SelectedCommand = 0;
            senderId = 8;

            //attiva il check della porta di comunicazione di default
            cANToolStripMenuItem.Checked = false;
            bluetoothLEToolStripMenuItem.Checked = true;

            //crea e aggiungi pcan
            var canInterface = "pcan";
            var channel = "PCAN_USBBUS1";
            var bitrate = 250000;
            _CDL = new CANDataLayer(channel, canInterface, bitrate);
            _CDL.ConnectionStatusChanged += OnPCANConnectionStatusChanged;

            //crea e aggiungi il ble manager
            BLETabRef = new BLEInterfaceTab();
            tabControl.TabPages.Add(BLETabRef);
            //crea e aggiungi ble
            _BLE_SDL = new SDL("BLE", "ble", 100000, BLETabRef.bleManager);
            _BLE_SDL.ConnectionStatusChanged += OnBLEConnectionStatusChanged;

            //crea il protocollo stem di ricezione
            RXpacketManager = new PacketManager(0xFFFFFFFF);
            RXpacketManager.OnAppLayerPacketReceived += onAppLayerPacketReady;
            RXpacketManager.Add_CAN_Channel(_CDL);
            RXpacketManager.Add_BLE_Channel(_BLE_SDL);

            //crea e aggiungi il bootloader manager
            BootTabRef = new Boot_Interface_Tab();
            BootTabRef.BootHndlr.SetHardwareChannel(CommunicationPort);

#if TOPLIFT

#elif EDEN

#else
            tabControl.TabPages.Add(BootTabRef);
#endif

            //crea e aggiungi il bootloader manager smart
            BootSmartTabRef = new Boot_Smart_Tab(RXpacketManager);
            BootSmartTabRef.BootHndlr.SetHardwareChannel(CommunicationPort);
            BootSmartTabRef.telemetryManager.SetHardwareChannel(CommunicationPort);

            //Aggiorna il flag di comunicazione
            if (CommunicationPort == "can")
            {
                cANToolStripMenuItem.Checked = true;
                bluetoothLEToolStripMenuItem.Checked = false;
            }

            // Crea la lista dei dispositivi
            List<DeviceInfo> BootSmartDevices = new List<DeviceInfo>
                {
                #if TOPLIFT
                //TOPLIFT devices
                   new DeviceInfo(0x000803C1, "Keyboard 1", true),
                   new DeviceInfo(0x000803C2, "Keyboard 2", true),
                   new DeviceInfo(0x000803C3, "Keyboard 3", true),
                   new DeviceInfo(0x00080381, "Motherboard", false),
                #elif EDEN
                //EDEN devices
                    new DeviceInfo(0x00030101, "Keyboard 1", true),
                    new DeviceInfo(0x00030102, "Keyboard 2", true),
                    new DeviceInfo(0x00030141, "Motherboard", false),
                #else

                #endif
                };

            // Popola la tab con la lista dei dispositivi
            BootSmartTabRef.PopulateDevices(BootSmartDevices);

            //crea e aggiungi tabcan
            // CanTabPageRef = new CANInterfaceTab(_CDL);
            // CanTabPageRef.ActivateEvents();
            // tabControl.TabPages.Add(CanTabPageRef);

            //attiva il terminale
            _terminal = new Terminal(); // Inizializza l'istanza di Terminal

            //attiva la seriale
            _serialPortManager = new SerialPortManager();  // Inizializza l'istanza di SerialManager
                                                           // Ottieni tutte le porte seriali disponibili
                                                           // e aggiungi le porte alla ListBox
            listBoxSerialPorts.Items.Clear();
            _serialPortManager.ScanPorts();
            listBoxSerialPorts.Items.AddRange(_serialPortManager.AvailablePorts.ToArray());

            // Supponiamo che tu abbia un ToolStripMenuItem chiamato "serialToolStripMenuItem"
            // nel tuo MenuStrip o ContextMenuStrip

            //private void UpdateSerialPortsMenu()
            {
                // Pulisce eventuali voci precedenti
                serialToolStripMenuItem.DropDownItems.Clear();

                // Aggiorna la lista delle porte disponibili
                _serialPortManager.ScanPorts();

                // Crea una voce di menu per ogni porta
                foreach (string port in _serialPortManager.AvailablePorts)
                {
                    ToolStripMenuItem portItem = new ToolStripMenuItem(port);

                    // Aggiunge un gestore per il click
                    portItem.Click += (s, e) =>
                    {
                        // Qui puoi fare la connessione alla porta scelta
                        _serialPortManager.Connect(port);
                        MessageBox.Show($"Connesso a {port}", "Info");
                    };

                        serialToolStripMenuItem.DropDownItems.Add(portItem);
                }

                // Se non ci sono porte, mostra una voce disabilitata
                if (_serialPortManager.AvailablePorts.Count == 0)
                {
                    ToolStripMenuItem noPortsItem = new ToolStripMenuItem("Nessuna porta disponibile");
                    noPortsItem.Enabled = false;
                        serialToolStripMenuItem.DropDownItems.Add(noPortsItem);
                }
            }

        UpdateTerminal(DateTime.Now + ": Stem Protocol Manager " + Software_Version);
        
        timerBaseTime.Enabled = true;

        tabControl.TabPages.Remove(tabPageUART);

            //inizializza il code generator
            // Crea un'istanza della classe SP_Config_Generator e chiama il metodo per generare il file
            configGenerator = new SP_Code_Generator();
            codeFilePath = "SP_Config.h";

            tabControl.TabPages.Remove(tabPageCodeGen);

            //primo giro di update connessione can
            OnPCANConnectionStatusChanged(this, _CDL.IsConnected);

            //crea e aggiungi il telemetry manager
            TelemetryTabRef = new Telemetry_Tab(RXpacketManager);
            TelemetryTabRef.telemetryManager.SetHardwareChannel(CommunicationPort);

            //Seleziona il tab iniziale

            //tabControl.SelectedTab = BootTabRef;
            //tabControl.SelectedTab = CanTabPageRef;

#if TOPLIFT
            terminalOut.Visible = false;
            // Rimuovi la riga
            tableLayoutPanel1.RowStyles.RemoveAt(1);
            tableLayoutPanel1.RowCount--;
            tabControl.TabPages.Remove(tabPageProtocol);
            tabControl.TabPages.Remove(BLETabRef);
            BLEStatusLabel.Visible = false;
            toolStripSplitButton2.Visible = false;

            //crea e aggiungi il telemetry per Toplift

            //TLTTabRef = new TopLiftTelemetry_Tab(RXpacketManager);
            //TLTTabRef.telemetryManager.SetHardwareChannel(CommunicationPort);

            TLTTabRef = new TopLiftTelemetry_Tab(RXpacketManager);
            TLTTabRef.telemetryManager.SetHardwareChannel(CommunicationPort);

            tabControl.TabPages.Add(TLTTabRef);
            tabControl.TabPages.Add(BootSmartTabRef);
#elif EGICON
            tabControl.SelectedTab = BLETabRef;
#else
            tabControl.SelectedTab = BLETabRef;

            tabControl.TabPages.Add(TelemetryTabRef);
            tabControl.TabPages.Add(BootSmartTabRef);
#endif

            // Nascondi la colonna delle variabili
            tableLayoutPanelProtocol.ColumnStyles[3].SizeType = SizeType.Absolute;
            tableLayoutPanelProtocol.ColumnStyles[3].Width = 0;

            //// Nascondi le colonne dei byte 1 e 2
            //tableLayoutPanelProtocol.ColumnStyles[4].SizeType = SizeType.Absolute;
            //tableLayoutPanelProtocol.ColumnStyles[4].Width = 0;
            //tableLayoutPanelProtocol.ColumnStyles[5].SizeType = SizeType.Absolute;
            //tableLayoutPanelProtocol.ColumnStyles[5].Width = 0;

            //Estrai i dati dal dizionario stem

            //hExcel = new ExcelHandler();
            //IndirizziProtocollo = new List<ExcelHandler.RowData>();
            //Comandi = new List<ExcelHandler.CommandData>();
            //Dizionario = new List<ExcelHandler.VariableData>();
            //hExcel.EstraiDatiProtocollo(IndirizziProtocollo, Comandi, ExcelfilePath);

#if TOPLIFT
            // Ottieni l’assembly
            var asm = Assembly.GetExecutingAssembly();
            //// Recupera tutti i nomi delle risorse incorporate
            //var resourceNames = asm.GetManifestResourceNames();
            //// Mostrali in un MessageBox o nel Output di Debug
            //string elenco = string.Join("\n", resourceNames);
            //MessageBox.Show("Risorse incorporate trovate:\n" + elenco, "Debug risorse");

            // Caricamento diretto del file Excel dalle risorse (embedded)
            //var asm = Assembly.GetExecutingAssembly();
            const string resourceName = "STEMPM.Resources.Dizionari STEM.xlsx";
            using (var stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new FileNotFoundException("Risorsa non trovata: " + resourceName);
                // Usa un overload di ExcelHandler che accetta uno Stream
                hExcel = new ExcelHandler(stream);
                // Estrai i dati direttamente dal flusso
                IndirizziProtocollo = new List<ExcelHandler.RowData>();
                Comandi            = new List<ExcelHandler.CommandData>();
                Dizionario         = new List<ExcelHandler.VariableData>();
                hExcel.EstraiDatiProtocollo(IndirizziProtocollo, Comandi, Dizionario);
            }
//fissa l'indirizzo toplift ed estrai i dati relativi
            RecipientId = 0x00080381; //indirizzo fisso scheda madre Toplift A2
        //    RecipientId = 0x00030141; //indirizzo fisso scheda madre Eden
            label12.Text = ($"Indirizzo\n 0x{RecipientId.ToString("X8")}");
            hExcel.EstraiDizionario(RecipientId, Dizionario);
            TLTTabRef.UpdateDictionary(Dizionario);
            BootSmartTabRef.UpdateDictionary(Dizionario);
#else
            // Uso del file esterno per configurazioni diverse da client
            ExcelfilePath = Path.Combine(Application.StartupPath, "Dizionari STEM.xlsx");
            hExcel = new ExcelHandler();
            IndirizziProtocollo = new List<ExcelHandler.RowData>();
            Comandi = new List<ExcelHandler.CommandData>();
            Dizionario = new List<ExcelHandler.VariableData>();
            hExcel.EstraiDatiProtocollo(IndirizziProtocollo, Comandi, ExcelfilePath);
            TelemetryTabRef.UpdateDictionary(Dizionario);
#endif

            _terminal.WriteLog("--------------------------------------------------------------------");
            // Stampa i risultati (per verifica)
            foreach (ExcelHandler.RowData item in IndirizziProtocollo)
            {
                UpdateTerminal(item.ToTerminal());
                //popola il combo macchine
                if (!comboBoxMachine.Items.Contains(item.Macchina)) comboBoxMachine.Items.Add(item.Macchina);
            }

            _terminal.WriteLog("--------------------------------------------------------------------");
            comboBoxCommand.Items.Clear();
            // Stampa i risultati (per verifica)
            foreach (ExcelHandler.CommandData item in Comandi)
            {
                UpdateTerminal(item.ToTerminal());
                //popola il combo comandi
                if ((!comboBoxCommand.Items.Contains(item.Name)) && (!item.Name.Contains("risposta")) && (!item.Name.Contains("Risposta"))) comboBoxCommand.Items.Add(item.Name);
            }

#if EDEN
            // Ottieni la stringa correntemente selezionata
            string macchinaSelezionata = "EDEN";
            string schedaSelezionata = "Madre";
            // Cerca i nomi della macchina
            foreach (ExcelHandler.RowData item in IndirizziProtocollo)
            {
                //popola il combo delle schede
                if ((item.Scheda == schedaSelezionata) && (item.Macchina == macchinaSelezionata))
                {
                    label12.Text = ($"Indirizzo\n {item.Indirizzo.ToString()}");
                    RecipientId = Convert.ToUInt32(item.Indirizzo.Substring(2), 16);
                    hExcel.EstraiDizionario(RecipientId, Dizionario, ExcelfilePath);
                    TelemetryTabRef.UpdateDictionary(Dizionario);
                    //aggiorna il combo Macchina
                    int indice = comboBoxMachine.FindStringExact(macchinaSelezionata);

                    if (indice != -1)
                    {
                        comboBoxMachine.SelectedIndex = indice;
                    }
                    else
                    {
                        //    MessageBox.Show($"Elemento \"{elementoDaCercare}\" non trovato nel ComboBox.");
                    }

                    //aggiorna il combo Scheda
                    indice = comboBoxBoard.FindStringExact(schedaSelezionata);

                    if (indice != -1)
                    {
                        comboBoxBoard.SelectedIndex = indice;
                    }
                    else
                    {
                        //    MessageBox.Show($"Elemento \"{elementoDaCercare}\" non trovato nel ComboBox.");
                    }
                }
            }
#elif EGICON
            // Ottieni la stringa correntemente selezionata
            string macchinaSelezionata = "SPARK";
            string schedaSelezionata = "HMI";
            // Cerca i nomi della macchina
            foreach (ExcelHandler.RowData item in IndirizziProtocollo)
            {
                //popola il combo delle schede
                if ((item.Scheda == schedaSelezionata) && (item.Macchina == macchinaSelezionata))
                {
                    label12.Text = ($"Indirizzo\n {item.Indirizzo.ToString()}");
                    RecipientId = Convert.ToUInt32(item.Indirizzo.Substring(2), 16);
                    hExcel.EstraiDizionario(RecipientId, Dizionario, ExcelfilePath);
                    TelemetryTabRef.UpdateDictionary(Dizionario);
                    //aggiorna il combo Macchina
                    int indice = comboBoxMachine.FindStringExact(macchinaSelezionata);

                    if (indice != -1)
                    {
                        comboBoxMachine.SelectedIndex = indice;
                    }
                    else
                    {
                        //    MessageBox.Show($"Elemento \"{elementoDaCercare}\" non trovato nel ComboBox.");
                    }

                    //aggiorna il combo Scheda
                    indice = comboBoxBoard.FindStringExact(schedaSelezionata);

                    if (indice != -1)
                    {
                        comboBoxBoard.SelectedIndex = indice;
                    }
                    else
                    {
                        //    MessageBox.Show($"Elemento \"{elementoDaCercare}\" non trovato nel ComboBox.");
                    }
                }
            }
#endif

            //installa l'evento di aggiornamento textbox applayer
            AppLayerCommandDecoded += onAppLayerDecoded;
            AppLayerCommandSended += onAppLayerSended;
        }

        public void UpdateTerminal(string message)
        {
            terminalOut.Text = _terminal.WriteLog(message);
            // Scorri automaticamente verso l'ultima riga
            terminalOut.SelectionStart = terminalOut.Text.Length; // Posiziona il caret alla fine
            terminalOut.ScrollToCaret(); // Scorri fino al caret
        }

        private void timerBaseTime_Tick(object sender, EventArgs e)
        {
            if (Prescaler1s > 0) Prescaler1s--;
            else
            {
                Prescaler1s = 10;
                //      UpdateTerminal(DateTime.Now + "");
            }
        }

        private void listBoxSerialPorts_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxSerialPorts.SelectedItem == null)
            {
                MessageBox.Show("Please select a serial port from the list.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //string selectedPort = listBoxSerialPorts.SelectedItem.ToString();

            //try
            //{
            //    _serialPort = new SerialPort(selectedPort, 9600);
            //    _serialPort.Open();
            //    MessageBox.Show($"Port {selectedPort} is now open.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show($"Failed to open port {selectedPort}. Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //}
        }

        private void button1_Click(object sender, EventArgs e)
        {
            configGenerator.GeneraFileDiTesto(configurazioni, codeFilePath);
            UpdateTerminal($"File generato con successo: {codeFilePath}");
        }

        private void MaskedTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Permetti solo caratteri esadecimali (0-9, A-F, a-f) , Backspace e spazio
            if (!Uri.IsHexDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back && e.KeyChar != (char)Keys.Space)
            {
                e.Handled = true; // Blocca il carattere non valido
            }
        }

        private void comboBoxMachine_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Verifica che il mittente sia effettivamente un ComboBox
            if (sender is ComboBox comboBoxCorrente)
            {
                // Verifica se è stato selezionato un elemento valido
                if (comboBoxCorrente.SelectedIndex != -1)
                {
                    // Ottieni la stringa correntemente selezionata
                    string macchinaSelezionata = comboBoxCorrente.SelectedItem.ToString();

                    comboBoxBoard.Items.Clear(); //azzera i nomi delle schede

                    // Cerca i nomi della macchina
                    foreach (ExcelHandler.RowData item in IndirizziProtocollo)
                    {
                        //popola il combo delle schede
                        if (item.Macchina == macchinaSelezionata)
                            comboBoxBoard.Items.Add(item.Scheda);
                        // comboBoxCommand.Items.Add(item.)
                    }
                }

            }
        }

        private void comboBoxBoard_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Verifica che il mittente sia effettivamente un ComboBox
            if (sender is ComboBox comboBoxCorrente)
            {
                // Verifica se è stato selezionato un elemento valido
                if (comboBoxCorrente.SelectedIndex != -1)
                {
                    // Ottieni la stringa correntemente selezionata
                    string schedaSelezionata = comboBoxCorrente.SelectedItem.ToString();

                    // Cerca i nomi della macchina
                    foreach (ExcelHandler.RowData item in IndirizziProtocollo)
                    {
                        //popola il combo delle schede
                        if ((item.Scheda == schedaSelezionata) && (item.Macchina == comboBoxMachine.SelectedItem.ToString()))
                        {
                            label12.Text = ($"Indirizzo\n {item.Indirizzo.ToString()}");
                            RecipientId = Convert.ToUInt32(item.Indirizzo.Substring(2), 16);
#if TOPLIFT
                            hExcel.EstraiDizionario(RecipientId, Dizionario);
                            TLTTabRef.UpdateDictionary(Dizionario);
#else
                            hExcel.EstraiDizionario(RecipientId, Dizionario, ExcelfilePath);
                            TelemetryTabRef.UpdateDictionary(Dizionario);
#endif
                        }
                    }
                    comboBoxCommand.SelectedIndex = 0;
                }
            }
        }

        private async void buttonSendPS_Click(object sender, EventArgs e)
        {
            await SendPS_Async(sender, e);
        }

        private async Task SendPS_Async(object sender, EventArgs e)
        {
            //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
            //      SPEDIZIONE PACCHETTO DA APPLICATION LAYER
            //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
            // Parametri del pacchetto

            //AL

            // Creazione del pacchetto a livello applicativo
            byte cmdInit = (byte)(Form1.FormRef.SelectedCommand >> 8);//comando byte alto
            byte cmdOpt = (byte)(Form1.FormRef.SelectedCommand);//comando byte basso 

            // Array di TextBox: sostituisci con i tuoi effettivi TextBox
            TextBox[] textBoxes = { Form1.FormRef.textBox1, Form1.FormRef.textBox2 };

            // Lista per raccogliere i valori validi
            List<byte> byteList = new List<byte>();

            //Se sei in leggi/scrivi variabili i primi due byte te li da il dizionario
            if (
                (tableLayoutPanelProtocol.ColumnStyles[3].Width != 0)
                && (comboBoxVariables.Items.Count != 0)
                && (comboBoxVariables.SelectedIndex >= 0)
                )
            {
                Form1.FormRef.textBox1.Text = "";
                Form1.FormRef.textBox2.Text = "";

                byte AddrL = Convert.FromHexString(Dizionario.ElementAt(comboBoxVariables.SelectedIndex).AddrL.PadLeft(2, '0'))[0];
                byte AddrH = Convert.FromHexString(Dizionario.ElementAt(comboBoxVariables.SelectedIndex).AddrH.PadLeft(2, '0'))[0];
                if (byteList.Count() < 1)
                {
                    byteList.Add(AddrH);
                }
                else byteList[0] = AddrH;


                if (byteList.Count() < 2)
                {
                    byteList.Add(AddrL);
                }
                else byteList[1] = AddrL;
            }

            // Itera su ogni TextBox
            foreach (var textBox in textBoxes)
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text)) // Ignora TextBox vuoti o spazi bianchi
                {
                    if (byte.TryParse(textBox.Text, System.Globalization.NumberStyles.HexNumber, null, out byte value))
                    {
                        byteList.Add(value); // Aggiungi il valore valido alla lista
                    }
                    else
                    {
                        MessageBox.Show($"Valore non valido nel campo {textBox.Name}. Inserisci un valore esadecimale valido (0-FF).",
                                        "Errore di input", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return; // Esce se c'è un errore di input
                    }
                }
            }

            //Estrai i dati aggiuntivi dall'ultimo textbox

            // Legge il testo dal TextBox
            string input = textBox3.Text;

            // Suddivide la stringa in base allo spazio
            string[] hexValues = input.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (string hex in hexValues)
            {
                // Converte la stringa esadecimale in byte
                byte value = byte.Parse(hex, NumberStyles.HexNumber);
                byteList.Add(value);
            }

            byte[] payload = byteList.ToArray();

            //TL
            byte cryptFlag = 0x00;         // Nessuna crittografia

            //NL
            //string interfaceType = "can";   // Interfaccia CAN
            //string interfaceType = "ble";   // Interfaccia ble
            string interfaceType = CommunicationPort;
            int version = 1;                                // Versione del protocollo
            uint recipientId = Form1.FormRef.RecipientId;   // ID del destinatario


            // Crea direttamente il pacchetto di livello Network
            var networkLayer = new NetworkLayer(
                interfaceType,
                version,
                recipientId,
                new byte[] { cryptFlag, (byte)Form1.FormRef.senderId, (byte)(Form1.FormRef.senderId >> 8), (byte)(Form1.FormRef.senderId >> 16), (byte)(Form1.FormRef.senderId >> 24), 0, 0, cmdInit, cmdOpt }.Concat(payload).ToArray(),
                true
            );

            //stampa cosa stai spedendo
            AppLayerSendEventArgs EventArgs = new AppLayerSendEventArgs(networkLayer);
            AppLayerCommandSended?.Invoke(this, EventArgs);

            // Ottieni i pacchetti suddivisi per il basso livello 
            var networkPackets = networkLayer.NetworkPackets;

            var packetManager = new PacketManager(Form1.FormRef.senderId);
            bool result = false;

            switch (CommunicationPort)
            {
                case "can":
                    // Invia i pacchetti tramite CAN                    
                    packetManager.Add_CAN_Channel(Form1.FormRef._CDL);
                    result = await packetManager.SendThroughCANAsync(networkPackets);
                    break;
                case "ble":
                    //Invia i pacchetti tramite BLE
                    packetManager.Add_BLE_Channel(Form1.FormRef._BLE_SDL);
                    result = await packetManager.SendThroughBLEAsync(networkPackets);
                    break;
            }
            //// Invia i pacchetti tramite CAN
            //var packetManager = new PacketManager(Form1.FormRef.senderId);
            //packetManager.Add_CAN_Channel(Form1.FormRef._CDL);
            //bool result = await packetManager.SendThroughCANAsync(networkPackets);
        }

        private void comboBoxCommand_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedCommand = (short)comboBoxCommand.SelectedIndex;

            if ((SelectedCommand == 1) || (SelectedCommand == 2))
            {
                comboBoxVariables.Items.Clear();

                _terminal.WriteLog("--------------------------------------------------------------------");
                // Stampa i risultati (per verifica)
                foreach (ExcelHandler.VariableData itemtemp in Dizionario)
                {
                    UpdateTerminal(itemtemp.ToTerminal());

                    //popola il combo variabili
                    if ((!comboBoxVariables.Items.Contains(itemtemp.Name)))
                    {
                        comboBoxVariables.Items.Add(itemtemp.Name);
                        comboBoxVariables.SelectedIndex = 0;
                    }
                }

                //visualizza la colonna delle variabili
                tableLayoutPanelProtocol.ColumnStyles[3].SizeType = SizeType.Percent;
                tableLayoutPanelProtocol.ColumnStyles[3].Width = (float)9.01;

                // Nascondi le colonne dei byte 1 e 2
                tableLayoutPanelProtocol.ColumnStyles[4].SizeType = SizeType.Absolute;
                tableLayoutPanelProtocol.ColumnStyles[4].Width = 0;
                tableLayoutPanelProtocol.ColumnStyles[5].SizeType = SizeType.Absolute;
                tableLayoutPanelProtocol.ColumnStyles[5].Width = 0;
            }
            else
            {
                // Nascondi la colonna delle variabili
                tableLayoutPanelProtocol.ColumnStyles[3].SizeType = SizeType.Absolute;
                tableLayoutPanelProtocol.ColumnStyles[3].Width = 0;

                // visualizza le colonne dei byte 1 e 2
                tableLayoutPanelProtocol.ColumnStyles[4].SizeType = SizeType.Percent;
                tableLayoutPanelProtocol.ColumnStyles[4].Width = (float)9.01;
                tableLayoutPanelProtocol.ColumnStyles[5].SizeType = SizeType.Percent;
                tableLayoutPanelProtocol.ColumnStyles[5].Width = (float)9.01;
            }
            //   comboBoxVariables.SelectedIndex = 0;
        }


        public void onAppLayerPacketReady(object sender, PacketReadyEventArgs e)
        {
            if ((IndirizziProtocollo == null) || (Comandi == null)) return;

            // Accesso all'array di byte ricevuto
            byte[] payload = e.Packet;
            if (payload.Length < 2) return;
            uint sourceAddress = e.SourceAddress;
            uint destinationAddress = e.DestinationAddress;

            //ricerca il nome della macchina 
            string MachineName = new string("Non in tabella");
            string MachineNameRecipient = new string("Non in tabella");

            foreach (ExcelHandler.RowData Item in IndirizziProtocollo)
            {
                if (Item.Indirizzo == "0x" + sourceAddress.ToString("X8"))
                {
                    MachineName = Item.Macchina + "->" + Item.Scheda;
                }
            }

            foreach (ExcelHandler.RowData Item in IndirizziProtocollo)
            {
                if (Item.Indirizzo == "0x" + destinationAddress.ToString("X8"))
                {
                    MachineNameRecipient = Item.Macchina + "->" + Item.Scheda;
                }
            }

            if (MachineName == "Non in tabella") MachineName = "0x" + sourceAddress.ToString("X8");
            if (MachineNameRecipient == "Non in tabella") MachineNameRecipient = "0x" + destinationAddress.ToString("X8");

            //Decodifica l'application layer
            ExcelHandler.CommandData CurrentCommand = new ExcelHandler.CommandData("None", "0", "0");

            foreach (ExcelHandler.CommandData Item in Comandi)
            {
                byte CmdL = Convert.FromHexString(Item.CmdL.PadLeft(2, '0'))[0];
                byte CmdH = Convert.FromHexString(Item.CmdH.PadLeft(2, '0'))[0];
                if ((payload[0] == CmdH) && (payload[1] == CmdL))
                {
                    CurrentCommand = Item;
                    break;
                }
            }

            ExcelHandler.VariableData CurrentVariable = new ExcelHandler.VariableData("None", "0", "0", "");

            //se il comando è leggi variabile logica lo mostro come è indicato nel dizionario
            if (CurrentCommand.Name == "Leggi variabile logica risposta")
            {
                // Stampa i risultati (per verifica)
                foreach (ExcelHandler.VariableData itemtemp in Dizionario)
                {
                    byte AddrL = Convert.FromHexString(itemtemp.AddrL.PadLeft(2, '0'))[0];
                    byte AddrH = Convert.FromHexString(itemtemp.AddrH.PadLeft(2, '0'))[0];
                    if ((payload[2] == AddrH) && (payload[3] == AddrL))
                    {
                        CurrentVariable = itemtemp;
                        break;
                    }
                }
            }

            //Aggiorna il textbox
            AppLayerDecoderEventArgs EventArgs = new AppLayerDecoderEventArgs(payload, CurrentCommand, MachineName, MachineNameRecipient, CurrentVariable);
            AppLayerCommandDecoded?.Invoke(this, EventArgs);
        }

        // Evento di aggiornamento del richtextbox dell'application layer

        public void onAppLayerDecoded(object sender, AppLayerDecoderEventArgs e)
        {
            // Metodo thread-safe per aggiornare lo stato della connessione
            if (richTextBoxTx.InvokeRequired)
            {
                richTextBoxTx.Invoke(new Action(() => AppLayerDecoded(this, e)));
            }
            else
            {
                AppLayerDecoded(this, e);
            }
        }

        public void AppLayerDecoded(object sender, AppLayerDecoderEventArgs e)
        {
#if TOPLIFT
#else
            if (e.CurrentCommand.Name != "None")
            {
                // Ottieni il timestamp
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                //comando riconosciuto
                richTextBoxTx.AppendText($"RX - {timestamp}: Comando '{e.CurrentCommand.Name} ' da {e.MachineName} per {e.MachineNameRecipient}: ");

                //se il comando è leggi variabile logica lo mostro come è indicato nel dizionario
                if (e.CurrentCommand.Name == "Leggi variabile logica risposta")
                {
                    richTextBoxTx.AppendText($" {e.CurrentVariable.Name}= ");
                    if (e.CurrentVariable.DataType.Contains("uint8_t"))
                    {
                        //visualizza in esadecimale
                        richTextBoxTx.AppendText("0x");
                        for (int i = 0; i < 1; i++)
                        {
                            richTextBoxTx.AppendText(e.Payload[4 + i].ToString("X2"));
                        }
                        //e in decimale
                        int Val = (e.Payload[4]);
                        richTextBoxTx.AppendText($" ({Val}) ");
                    }
                    else if (e.CurrentVariable.DataType.Contains("uint16_t"))
                    {
                        //visualizza in esadecimale
                        richTextBoxTx.AppendText("0x");
                        for (int i = 0; i < 2; i++)
                        {
                            richTextBoxTx.AppendText(e.Payload[4 + i].ToString("X2"));
                        }
                        //e in decimale
                        int Val = ((e.Payload[4]) << 8) | (e.Payload[5]);
                        richTextBoxTx.AppendText($" ({Val}) ");
                    }
                    else if (e.CurrentVariable.DataType.Contains("uint32_t"))
                    {
                        //visualizza in esadecimale
                        richTextBoxTx.AppendText("0x");
                        for (int i = 0; i < 4; i++)
                        {
                            richTextBoxTx.AppendText(e.Payload[4 + i].ToString("X2"));
                        }
                        //e in decimale
                        int Val = ((e.Payload[4]) << 24) | ((e.Payload[5]) << 16) | ((e.Payload[6]) << 8) | (e.Payload[7]);
                        richTextBoxTx.AppendText($" ({Val}) ");
                    }
                    else if (e.CurrentVariable.DataType.Contains("int8_t"))
                    {
                        //visualizza in esadecimale
                        richTextBoxTx.AppendText("0x");
                        for (int i = 0; i < 1; i++)
                        {
                            richTextBoxTx.AppendText(e.Payload[4 + i].ToString("X2"));
                        }
                        //e in decimale
                        int Val = (e.Payload[4]);
                        richTextBoxTx.AppendText($" ({Val}) ");
                    }
                    else if (e.CurrentVariable.DataType.Contains("int16_t"))
                    {
                        //visualizza in esadecimale
                        richTextBoxTx.AppendText("0x");
                        for (int i = 0; i < 2; i++)
                        {
                            richTextBoxTx.AppendText(e.Payload[4 + i].ToString("X2"));
                        }
                        //e in decimale
                        int Val = ((e.Payload[4]) << 8) | (e.Payload[5]);
                        richTextBoxTx.AppendText($" ({Val}) ");
                    }
                    else if (e.CurrentVariable.DataType.Contains("int32_t"))
                    {
                        //visualizza in esadecimale
                        richTextBoxTx.AppendText("0x");
                        for (int i = 0; i < 4; i++)
                        {
                            richTextBoxTx.AppendText(e.Payload[4 + i].ToString("X2"));
                        }
                        //e in decimale
                        int Val = ((e.Payload[4]) << 24) | ((e.Payload[5]) << 16) | ((e.Payload[6]) << 8) | (e.Payload[7]);
                        richTextBoxTx.AppendText($" ({Val}) ");
                    }
                    else if (e.CurrentVariable.DataType.Contains("float"))
                    {
                        // Estrai i byte 4-7
                        byte[] floatBytes = new byte[4];
                        Array.Copy(e.Payload, 4, floatBytes, 0, 4);

                        // Inverti l'ordine perché BitConverter usa il little-endian
                        Array.Reverse(floatBytes);

                        // Converte in float
                        float Val = BitConverter.ToSingle(floatBytes, 0);
                        richTextBoxTx.AppendText($" ({Val}) ");
                    }
                    else if (e.CurrentVariable.DataType.Contains("bool"))
                    {
                        if (e.Payload[4] == 0) richTextBoxTx.AppendText("false ");
                        else richTextBoxTx.AppendText("true ");
                    }
                }
            }
            else
            {
                //comando non riconosciuto
                richTextBoxTx.AppendText("Comando non presente in dizionario: ");
            }

            //RAW application layer data
            richTextBoxTx.AppendText("[RAW: ");
            for (int i = 0; i < e.Payload.Count(); i++)
            {
                richTextBoxTx.AppendText(e.Payload[i].ToString("X2") + " ");
            }
            richTextBoxTx.AppendText(" ]\r\n");
            // Imposta la posizione del cursore alla fine del testo.
            richTextBoxTx.SelectionStart = richTextBoxTx.Text.Length;
            // Esegue lo scroll fino alla posizione del cursore.
            richTextBoxTx.ScrollToCaret();
#endif
        }

        public void onAppLayerSended(object sender, AppLayerSendEventArgs e)
        {
            // Metodo thread-safe per aggiornare lo stato della connessione
            if (Form1.FormRef.richTextBoxTx.InvokeRequired)
            {
                Form1.FormRef.richTextBoxTx.Invoke(new Action(() => AppLayerSended(this, e)));
            }
            else
            {
                AppLayerSended(this, e);
            }
        }

        public void AppLayerSended(object sender, AppLayerSendEventArgs e)
        {
            // stampa il pacchetto dell'application layer
            //Form1.FormRef.richTextBoxTx.AppendText("-- APPLICATION --\n");
            // Ottieni il timestamp
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Form1.FormRef.richTextBoxTx.AppendText($"TX - {timestamp}: {string.Join(" ", e.NetLayer.ApplicationPacket.Select(b => b.ToString("X2")))}\n");

            //// stampa il pacchetto del transport layer
            //Form1.FormRef.richTextBoxTx.AppendText("-- TRANSPORT --\n");
            //Form1.FormRef.richTextBoxTx.AppendText($"{string.Join(" ", networkLayer.TransportPacket.Select(b => b.ToString("X2")))}\n");

            //// stampa i pacchetti del network layer
            //Form1.FormRef.richTextBoxTx.AppendText("-- NETWORK --\n");
            //foreach (var item in networkLayer.NetworkPackets)
            //{
            //    // _netInfo, _recipientId, chunk
            //    Form1.FormRef.richTextBoxTx.AppendText($"NetInfo: {string.Join(" ", item.Item1.Select(b => b.ToString("X2")))} ");
            //    Form1.FormRef.richTextBoxTx.AppendText($"Id: {item.Item2.ToString("X2")} ");
            //    Form1.FormRef.richTextBoxTx.AppendText($"Chunk: {string.Join(" ", item.Item3.Select(b => b.ToString("X2")))}\n");
            //}

            // Imposta la posizione del cursore alla fine del testo.
            Form1.FormRef.richTextBoxTx.SelectionStart = Form1.FormRef.richTextBoxTx.Text.Length;
            // Esegue lo scroll fino alla posizione del cursore.
            Form1.FormRef.richTextBoxTx.ScrollToCaret();
        }

        private void OnPCANConnectionStatusChanged(object sender, bool isConnected)
        {
            // Metodo thread-safe per aggiornare lo stato della connessione
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdatePCANConnectionStatus(isConnected)));
            }
            else
            {
                UpdatePCANConnectionStatus(isConnected);
            }
        }

        private void UpdatePCANConnectionStatus(bool isConnected)
        {
            if (isConnected)
            {
                PCanLabel.Text = "PCAN: Connected";
                PCanLabel.BackColor = System.Drawing.Color.GreenYellow;
            }
            else
            {
                PCanLabel.Text = "PCAN: Not Connected";
                PCanLabel.BackColor = System.Drawing.Color.Salmon;
            }
        }

        private void OnBLEConnectionStatusChanged(object sender, bool isConnected)
        {
            // Metodo thread-safe per aggiornare lo stato della connessione
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateBLEConnectionStatus(isConnected)));
            }
            else
            {
                UpdateBLEConnectionStatus(isConnected);
            }
        }

        private void UpdateBLEConnectionStatus(bool isConnected)
        {
            if (isConnected)
            {
                BLEStatusLabel.Text = "BLE: Connesso";
                BLEStatusLabel.BackColor = System.Drawing.Color.GreenYellow;
            }
            else
            {
                BLEStatusLabel.Text = "BLE: Non connesso";
                BLEStatusLabel.BackColor = System.Drawing.Color.Salmon;
            }
        }

        private void cANToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CommunicationPort = "can";
            BootTabRef.BootHndlr.SetHardwareChannel(CommunicationPort);
            TelemetryTabRef.telemetryManager.SetHardwareChannel(CommunicationPort);
            cANToolStripMenuItem.Checked = true;
            bluetoothLEToolStripMenuItem.Checked = false;
        }

        private void bluetoothLEToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CommunicationPort = "ble";
            BootTabRef.BootHndlr.SetHardwareChannel(CommunicationPort);
            TelemetryTabRef.telemetryManager.SetHardwareChannel(CommunicationPort);
            cANToolStripMenuItem.Checked = false;
            bluetoothLEToolStripMenuItem.Checked = true;
        }
    }
}
