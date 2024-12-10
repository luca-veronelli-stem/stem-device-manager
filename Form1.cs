using Microsoft.VisualBasic.Logging;
using System.Windows.Forms;
using System.IO.Ports; // used for serial port

namespace StemPC
{
    public partial class Form1 : Form
    {
        private UInt16 Prescaler1s = 0;

        //**************************
        //  Terminal variables
        //************************** 
        private Terminal _terminal;

        //**************************
        //  Serial port variables
        //**************************
        private SerialPortManager _serialPortManager;
        private SerialPortManager _serialPort;

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
        ExcelHandler hExcel;

        public Form1()
        {
            InitializeComponent();

            //aggiungi tabcan
            tabControl.TabPages.Add(new CanTabPage());

            _terminal = new Terminal(); // Inizializza l'istanza di Terminal

            _serialPortManager = new SerialPortManager("COM3", 19200); ;// Inizializza l'istanza di SerialManager
            UpdateTerminal(DateTime.Now + ": Stem Protocol Manager v0.1");
            timerBaseTime.Enabled = true;

            // Ottieni tutte le porte seriali disponibili
            // e aggiungi le porte alla ListBox
            listBoxSerialPorts.Items.Clear();
            listBoxSerialPorts.Items.AddRange(_serialPortManager.GetPorts());

            //inizializza il code generator
            // Crea un'istanza della classe SP_Config_Generator e chiama il metodo per generare il file
            configGenerator = new SP_Code_Generator();
            codeFilePath = "SP_Config.h";

            //Seleziona il tab del protocollo
            tabControl.SelectedTab = tabPageProtocol; // Seleziona la TabPage con nome 'tabPagePrototocol'

            //test excel
            hExcel = new ExcelHandler();
            IndirizziProtocollo = new List<ExcelHandler.RowData>();
            Comandi = new List<ExcelHandler.CommandData>();
            hExcel.EstraiDatiProtocollo(IndirizziProtocollo, Comandi, ExcelfilePath);

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
                //popola il combo macchine
                if ((!comboBoxCommand.Items.Contains(item.Name)) && (!item.Name.Contains("risposta")) && (!item.Name.Contains("Risposta"))) comboBoxCommand.Items.Add(item.Name);
            }
        }

        private void UpdateTerminal(string message)
        {
            terminalOut.Text = _terminal.WriteLog(message);
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
            // Permetti solo caratteri esadecimali (0-9, A-F, a-f) e Backspace
            if (!Uri.IsHexDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back)
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
                            textBoxAddress.Text = item.Indirizzo.ToString();
                        }

                    }
                }

            }
        }

        private void buttonSendPS_Click(object sender, EventArgs e)
        {
            // Esempio di utilizzo del PacketManager per inviare pacchetti tramite CAN e Bluetooth
            int senderId = 8; // ID del mittente
            int recipientId = 2; // ID del destinatario
            byte[] data = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A }; // Dati del pacchetto da inviare

            // Creazione di un pacchetto di livello applicazione con dati
            //ApplicationLayer appLayer = new ApplicationLayer(1, 2, data, true);

            //// Creazione di un pacchetto di livello di rete con dati
            NetworkLayer networkLayer = new NetworkLayer("can", 1, recipientId, data,true);

            // Configurazione del PacketManager
            PacketManager packetManager = new PacketManager(senderId);

            // stampa i pacchetti del network layer
            richTextBoxTx.AppendText("-- NETWORK --\n");
            foreach (var item in networkLayer.NetworkPackets)
            {
                // _netInfo, _recipientId, chunk
                richTextBoxTx.AppendText($"NetInfo: {string.Join(" ", item.Item1)}\n");
                richTextBoxTx.AppendText($"RxId: {item.Item2}\n");
                richTextBoxTx.AppendText($"Chunk: {string.Join(" ", item.Item3)}\n");
            }
            // stampa il pacchetto del transport layer
                richTextBoxTx.AppendText("-- TRANSPORT --\n");
                richTextBoxTx.AppendText($"{string.Join(" ", networkLayer.TransportPacket)}\n");

            // stampa il pacchetto dell'application layer
            richTextBoxTx.AppendText("-- APPLICATION --\n");
            richTextBoxTx.AppendText($"{string.Join(" ", networkLayer.ApplicationPacket)}\n");



            //// Simulazione invio di pacchetti tramite CAN
            //List<Tuple<byte[], int, byte[]>> canPackets = networkLayer.NetworkPackets;
            //bool sentThroughCan = packetManager.SendThroughCAN(canPackets);
            //Console.WriteLine($"Pacchetti inviati tramite CAN: {sentThroughCan}");
        }
    }
}
