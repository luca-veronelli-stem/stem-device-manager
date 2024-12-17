using Microsoft.VisualBasic.Logging;
using System.Windows.Forms;
using System.IO.Ports; // used for serial port
using PS_PacketManager;
using static NetworkLayer;

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

        //**********************************
        //  STEM Protocol variables/classes
        //**********************************
        public uint RecipientId;
        public short SelectedCommand;
        public RollingCodeGenerator RollingCodeGen;
        public uint senderId;              // ID del mittente


        //**************************
        //  public Elements instances
        //**************************
        public CANInterfaceTab CanTabPageRef { get; private set; }
        public static Form1 FormRef { get; private set; }

        public Form1()
        {
            InitializeComponent();

            FormRef = this;

            RecipientId = 0;
            SelectedCommand = 0;
            senderId = 8;
            RollingCodeGen = new RollingCodeGenerator();

            CanTabPageRef = new CANInterfaceTab();
            
            //aggiungi tabcan
            tabControl.TabPages.Add(CanTabPageRef);

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
                            label12.Text = ($"Indirizzo\n {item.Indirizzo.ToString()}");
                            RecipientId = Convert.ToUInt32(item.Indirizzo.Substring(2), 16);
                        }

                    }

                    comboBoxCommand.SelectedIndex = 0;
                }

            }
        }

 
        private void buttonSendPS_Click(object sender, EventArgs e)
        {
            //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
            //      SPEDIZIONE PACCHETTO DA APPLICATION LAYER
            //%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%%
            // Parametri del pacchetto

            //AL

            // Creazione del pacchetto a livello applicativo
            byte cmdInit = (byte)(SelectedCommand >> 8);//comando byte alto
            byte cmdOpt = (byte)(SelectedCommand);//comando byte basso 

            // Array di TextBox: sostituisci con i tuoi effettivi TextBox
            TextBox[] textBoxes = { textBox1, textBox2, textBox3, textBox4, textBox5, textBox6, textBox7 };

            // Lista per raccogliere i valori validi
            List<byte> byteList = new List<byte>();

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
            byte[] payload = byteList.ToArray();

            //TL
            byte cryptFlag = 0x00;         // Nessuna crittografia
            
            
            //NL
            string interfaceType = "can";   // Interfaccia CAN
            int version = 1;                // Versione del protocollo
            uint recipientId = RecipientId; // ID del destinatario


            // Crea direttamente il pacchetto di livello Network
            var networkLayer = new NetworkLayer(
                interfaceType,
                version,
                recipientId,
                new byte[] { cryptFlag, (byte)senderId, (byte)(senderId>>8), (byte)(senderId >> 16), (byte)(senderId >> 24), 0, 0, cmdInit, cmdOpt }.Concat(payload).ToArray(),
                true
            );

            // stampa il pacchetto dell'application layer
            richTextBoxTx.AppendText("-- APPLICATION --\n");
            richTextBoxTx.AppendText($"{string.Join(" ", networkLayer.ApplicationPacket.Select(b => b.ToString("X2")))}\n");

            // stampa il pacchetto del transport layer
            richTextBoxTx.AppendText("-- TRANSPORT --\n");
            richTextBoxTx.AppendText($"{string.Join(" ", networkLayer.TransportPacket.Select(b => b.ToString("X2")))}\n");

            // stampa i pacchetti del network layer
            richTextBoxTx.AppendText("-- NETWORK --\n");
            foreach (var item in networkLayer.NetworkPackets)
            {
                // _netInfo, _recipientId, chunk
                richTextBoxTx.AppendText($"NetInfo: {string.Join(" ", item.Item1.Select(b => b.ToString("X2")))} ");
                richTextBoxTx.AppendText($"Id: {item.Item2.ToString("X2")} ");
                richTextBoxTx.AppendText($"Chunk: {string.Join(" ", item.Item3.Select(b => b.ToString("X2")))}\n");
            }

            // Ottieni i pacchetti suddivisi per il CAN
            var networkPackets = networkLayer.NetworkPackets;

            // Invia i pacchetti tramite CAN
            var packetManager = new PacketManager(senderId);

            bool result = packetManager.SendThroughCAN(networkPackets);

 
            //// Simulazione invio di pacchetti tramite CAN
            //List<Tuple<byte[], uint, byte[]>> canPackets = networkLayer.NetworkPackets;
            //bool sentThroughCan = packetManager.SendThroughCAN(canPackets);
            ////Console.WriteLine($"Pacchetti inviati tramite CAN: {sentThroughCan}");
        }

        private void comboBoxCommand_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedCommand = (short) comboBoxCommand.SelectedIndex;
        }

        // Metodo per gestire l'evento
        public void DecodeCommandSP(object sender, PacketReadyEventArgs e)
        {
            // Accesso all'array di byte ricevuto
            byte[] payload = e.Packet;
            uint sourceAddress = e.SourceAddress;
            uint destinationAddress = e.DestinationAddress;

            //ricerca il nome della macchina 
            string MachineName = new string("Non in tabella");
            string MachineNameRecipient = new string("Non in tabella");

            foreach (ExcelHandler.RowData Item in IndirizziProtocollo)
            {
                if(Item.Indirizzo == "0x" + sourceAddress.ToString("X8"))
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

            //find command and decode application layer
            ExcelHandler.CommandData CurrentCommand= new ExcelHandler.CommandData("None","0","0");

            foreach (ExcelHandler.CommandData Item in Comandi)
            {
                byte CmdL = Convert.FromHexString(Item.CmdL.PadLeft(2, '0'))[0];
                byte CmdH = Convert.FromHexString(Item.CmdH.PadLeft(2, '0'))[0];
                if ((payload[0]== CmdH) && (payload[1] == CmdL))
                {
                    CurrentCommand = Item;
                    break;
                }
            }

           if (CurrentCommand.Name != "None")
            {
                //comando riconosciuto
                richTextBoxTx.AppendText($"Comando '{CurrentCommand.Name} ' ricevuto da {MachineName} per {MachineNameRecipient}: ");
            }
            else
            {
                //comando non riconosciuto
                richTextBoxTx.AppendText("Comando non presente in dizionario: ");
            }

           //RAW application layer data
            richTextBoxTx.AppendText("( ");
            for (int i = 0; i < payload.Count(); i++)
            {
                richTextBoxTx.AppendText(payload[i].ToString("X2") + " ");
            }
            richTextBoxTx.AppendText(" )\r\n");
        }

       
    }
}
