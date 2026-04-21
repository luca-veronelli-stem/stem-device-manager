using GUI.Windows;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Protocol.Hardware;
using Infrastructure.Protocol.Legacy;
using Microsoft.Extensions.DependencyInjection;
using Services.Cache;
using System.Globalization;

namespace StemPC
{
    public partial class Form1 : Form
    {
        // Dependency Injection Service Provider
        private readonly IServiceProvider _serviceProvider;
        private readonly IDictionaryProvider _dictionaryProvider;
        private readonly DictionaryCache _dictionaryCache;
        private readonly ConnectionManager _connMgr;
        private readonly IDeviceVariantConfig _variantConfig;
        // Driver BLE/Serial iniettati via DI (impl in Infrastructure.Protocol.Legacy/).
        // Referenziati come tipo concreto per operazioni UI-side (scan porte seriali,
        // StartScanningAsync BLE) e per passare la STESSA istanza al tab BLE — il
        // BlePort usa lo stesso singleton via IBleDriver, quindi scan+connect sul tab
        // propaga ConnectionStatusChanged al port.
        private readonly SerialPortManager _serialPortManager;
        private readonly BLEManager _bleManager;

        public const string Software_Version = "2.15";

        // Canale hardware corrente selezionato. Le mutazioni vengono propagate a
        // ConnectionManager via SwitchToAsync.
        public ChannelKind CurrentChannel { get; private set; }


        private UInt16 Prescaler1s = 0;

        //**************************
        //  Terminal variablesc
        //**************************
        private Terminal _terminal;

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
        //  Variabili dizionario (popolate da IDictionaryProvider)
        //**************************
        List<ProtocolAddress> IndirizziProtocollo = new();
        List<Command> Comandi = new();
        List<Variable> Dizionario = new();

        //**********************************
        //  STEM Protocol variables/classes
        //**********************************
        public uint RecipientId;
        public short SelectedCommand;
        public uint senderId;              // ID del mittente

        //******************************
        //  public Elements instances
        //******************************
        public Boot_Interface_Tab BootTabRef { get; private set; }
        public Boot_Smart_Tab BootSmartTabRef { get; private set; }
        public Telemetry_Tab TelemetryTabRef { get; private set; }
        public BLEInterfaceTab BLETabRef { get; private set; }
        public TopLiftTelemetry_Tab TLTTabRef { get; private set; } = null!;

        public Form1(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            // Inietta il service provider, il provider dizionari, la cache, il manager connessioni, la variante device, il driver seriale
            _serviceProvider = serviceProvider;
            _dictionaryProvider = serviceProvider.GetRequiredService<IDictionaryProvider>();
            _dictionaryCache = serviceProvider.GetRequiredService<DictionaryCache>();
            _connMgr = serviceProvider.GetRequiredService<ConnectionManager>();
            _variantConfig = serviceProvider.GetRequiredService<IDeviceVariantConfig>();
            _serialPortManager = serviceProvider.GetRequiredService<SerialPortManager>();
            _bleManager = serviceProvider.GetRequiredService<BLEManager>();

            // Canale hardware di default dalla variante.
            CurrentChannel = _variantConfig.DefaultChannel;

            Load += async (_, _) =>
            {
                await LoadDictionaryDataAsync(CancellationToken.None);
                // Attiva il canale di default (connette la port + crea protocol/boot/telemetry).
                await _connMgr.SwitchToAsync(CurrentChannel);
                _connMgr.CurrentTelemetry?.UpdateSourceAddress(RecipientId);
            };

            // Controlla se il TabControl esiste gi  e crealo se non esiste
            if (tabControl == null)
            {
                tabControl = new TabControl() { Dock = DockStyle.Fill };
                Controls.Add(tabControl);
            }

            // Titolo finestra: per variante device-specifiche usa il titolo dedicato,
            // per Generic appende solo la versione al testo di default impostato nel Designer.
            Text = _variantConfig.Variant == DeviceVariant.Generic
                ? Text + Software_Version
                : _variantConfig.WindowTitle + Software_Version;

            RecipientId = 0;
            SelectedCommand = 0;
            senderId = _variantConfig.SenderId;

            //*************************************************************
            //   INIT UI + TAB
            //*************************************************************

            //attiva il check della porta di comunicazione di default
            UpdateChannelMenuChecks();

            //crea e aggiungi il ble manager. Il tab riceve la stessa istanza BLEManager
            //registrata in DI come IBleDriver del BlePort: scan+connect sul tab → BlePort
            //vede ConnectionStatusChanged → port Connected → SendAsync funziona.
            BLETabRef = new BLEInterfaceTab(_bleManager);
            tabControl.TabPages.Add(BLETabRef);

            // Sottoscrivi log driver BLE al terminale UI + errori driver BLE/Serial al MessageBox.
            BLETabRef.bleManager.LogMessageEmitted += UpdateTerminal;
            BLETabRef.bleManager.ErrorOccurred += ShowDriverError;
            _serialPortManager.ErrorOccurred += ShowDriverError;

            // Sottoscrizioni forward da ConnectionManager: stato connessione per canale + app layer decodificato.
            _connMgr.StateChanged += OnConnectionStateChanged;
            _connMgr.AppLayerDecoded += OnAppLayerDecodedFromConnMgr;

            //crea e aggiungi il bootloader manager
            BootTabRef = new Boot_Interface_Tab(_dictionaryCache, _connMgr, _variantConfig);

            // Il tab bootloader classico appare solo su varianti non-TOPLIFT e non-EDEN
            // (blocco #3 in PREPROCESSOR_DIRECTIVES.md): TOPLIFT e EDEN usano il solo
            // bootloader smart; per Egicon e Generic il classico rimane visibile.
            if (_variantConfig.Variant != DeviceVariant.TopLift
                && _variantConfig.Variant != DeviceVariant.Eden)
            {
                tabControl.TabPages.Add(BootTabRef);
            }

            //crea e aggiungi il bootloader manager smart
            BootSmartTabRef = new Boot_Smart_Tab(_dictionaryCache, _connMgr);

            // Lista dispositivi smart-boot letta dalla variante (blocco #4 in
            // PREPROCESSOR_DIRECTIVES.md): TOPLIFT = 3 tastiere + scheda madre,
            // EDEN = 2 tastiere + scheda madre, altre = lista vuota.
            List<DeviceInfo> BootSmartDevices = _variantConfig.SmartBootDevices
                .Select(d => new DeviceInfo((int)d.Address, d.Name, d.IsKeyboard))
                .ToList();

            // Popola la tab con la lista dei dispositivi
            BootSmartTabRef.PopulateDevices(BootSmartDevices);

            //attiva il terminale
            _terminal = new Terminal(); // Inizializza l'istanza di Terminal


            listBoxSerialPorts.Items.Clear();
            _serialPortManager.ScanPorts();
            listBoxSerialPorts.Items.AddRange(_serialPortManager.AvailablePorts.ToArray());

            UpdateTerminal(DateTime.Now + ": Stem Protocol Manager " + Software_Version);

            timerBaseTime.Enabled = true;

            tabControl.TabPages.Remove(tabPageUART);

            //inizializza il code generator
            configGenerator = new SP_Code_Generator();
            codeFilePath = "SP_Config.h";

            tabControl.TabPages.Remove(tabPageCodeGen);

            //crea e aggiungi il telemetry manager
            TelemetryTabRef = new Telemetry_Tab(_dictionaryCache, _connMgr, _variantConfig);

            // Layout tab iniziale (blocco #5 in PREPROCESSOR_DIRECTIVES.md):
            //   TOPLIFT  → UI semplificata: nasconde terminal/protocol/BLE, aggiunge
            //              TopLiftTelemetry + BootSmart.
            //   EGICON   → tab iniziale BLE, nessuna tab aggiuntiva.
            //   Generic  → tab iniziale BLE + aggiunge Telemetry classico.
            //   Eden     → comportamento come Generic (legacy #else: stesso ramo).
            if (_variantConfig.Variant == DeviceVariant.TopLift)
            {
                terminalOut.Visible = false;
                // Rimuove la riga del terminal dal layout principale
                tableLayoutPanel1.RowStyles.RemoveAt(1);
                tableLayoutPanel1.RowCount--;
                tabControl.TabPages.Remove(tabPageProtocol);
                tabControl.TabPages.Remove(BLETabRef);
                BLEStatusLabel.Visible = false;
                toolStripSplitButton3.Visible = false;

                // Telemetry TOPLIFT-specific + smart boot tab
                TLTTabRef = new TopLiftTelemetry_Tab(_dictionaryCache, _connMgr);
                tabControl.TabPages.Add(TLTTabRef);
                tabControl.TabPages.Add(BootSmartTabRef);
            }
            else
            {
                tabControl.SelectedTab = BLETabRef;

                // Il tab Telemetry classico appare solo fuori da Egicon (legacy: Egicon
                // usa solo BLE senza telemetria classica).
                if (_variantConfig.Variant != DeviceVariant.Egicon)
                {
                    tabControl.TabPages.Add(TelemetryTabRef);
                }
            }

            // Nascondi la colonna delle variabili
            tableLayoutPanelProtocol.ColumnStyles[3].SizeType = SizeType.Absolute;
            tableLayoutPanelProtocol.ColumnStyles[3].Width = 0;
        }

        /// <summary>
        /// Handler thread-safe per errori driver (BLE/Serial): mostra MessageBox sulla UI.
        /// Sostituisce il vecchio pattern <c>MessageBox.Show</c> dentro i driver (dipendenza
        /// da WinForms eliminata spostando i driver in Infrastructure.Protocol/Legacy/).
        /// </summary>
        private void ShowDriverError(string title, string message)
        {
            if (this.InvokeRequired)
                this.BeginInvoke(new Action(() => ShowDriverError(title, message)));
            else
                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        public void UpdateTerminal(string message)
        {
            terminalOut.Text = _terminal.WriteLog(message);
            // Scorri automaticamente verso l'ultima riga
            terminalOut.SelectionStart = terminalOut.Text.Length; // Posiziona il caret alla fine
            terminalOut.ScrollToCaret(); // Scorri fino all'ultimo
        }

        private void timerBaseTime_Tick(object? sender, EventArgs e)
        {
            if (Prescaler1s > 0) Prescaler1s--;
            else
            {
                Prescaler1s = 10;
                //      UpdateTerminal(DateTime.Now + "");
            }
        }

        private void listBoxSerialPorts_SelectedIndexChanged(object? sender, EventArgs e)
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

        private void button1_Click(object? sender, EventArgs e)
        {
            configGenerator.GeneraFileDiTesto(configurazioni, codeFilePath);
            UpdateTerminal($"File generato con successo: {codeFilePath}");
        }

        private void MaskedTextBox_KeyPress(object? sender, KeyPressEventArgs e)
        {
            // Permetti solo caratteri esadecimali (0-9, A-F, a-f) , Backspace e spazio
            if (!Uri.IsHexDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back && e.KeyChar != (char)Keys.Space)
            {
                e.Handled = true; // Blocca il carattere non valido
            }
        }

        private void comboBoxMachine_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Verifica che il mittente sia effettivamente un ComboBox
            if (sender is ComboBox comboBoxCorrente)
            {
                // Verifica se   stato selezionato un elemento valido
                if (comboBoxCorrente.SelectedIndex != -1)
                {
                    // Ottieni la stringa correntemente selezionata
                    string macchinaSelezionata = comboBoxCorrente.SelectedItem?.ToString() ?? "";

                    comboBoxBoard.Items.Clear(); //azzera i nomi delle schede

                    // Cerca i nomi della macchina
                    foreach (ProtocolAddress item in IndirizziProtocollo)
                    {
                        //popola il combo delle schede
                        if (item.DeviceName == macchinaSelezionata)
                            comboBoxBoard.Items.Add(item.BoardName);
                        // comboBoxCommand.Items.Add(item.)
                    }
                }

            }
        }
        
        private async void comboBoxBoard_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (sender is not ComboBox comboBoxCorrente) return;
            if (comboBoxCorrente.SelectedIndex == -1) return;

            string schedaSelezionata = comboBoxCorrente.SelectedItem!.ToString()!;

            foreach (ProtocolAddress item in IndirizziProtocollo)
            {
                if (item.BoardName == schedaSelezionata
                    && item.DeviceName == comboBoxMachine.SelectedItem?.ToString())
                {
                    label12.Text = $"Indirizzo\n {item.Address}";
                    RecipientId = Convert.ToUInt32(item.Address.Substring(2), 16);
                    // Carica variabili via cache: fa HTTP una volta sola e notifica i tab via DictionaryUpdated.
                    await _dictionaryCache.SelectByRecipientAsync(RecipientId);
                    Dizionario = _dictionaryCache.Variables.ToList();
                    // Propaga il recipient al servizio di telemetria attivo (condiviso tra tab).
                    _connMgr.CurrentTelemetry?.UpdateSourceAddress(RecipientId);
                }
            }
            comboBoxCommand.SelectedIndex = 0;
        }

        /// <summary>
        /// Carica indirizzi protocollo, comandi e variabili tramite IDictionaryProvider.
        /// Chiamato nell'evento Load (async void è accettabile in WinForms).
        /// </summary>
        private async Task LoadDictionaryDataAsync(CancellationToken ct)
        {
            // Carica commands + addresses via cache (HTTP una volta, notifica tab via DictionaryUpdated).
            await _dictionaryCache.LoadAsync(ct);

            // TEMP: indicatore provider attivo — rimuovere dopo testing
            (string text, Color color) providerTag = _dictionaryProvider switch
            {
                Infrastructure.Persistence.FallbackDictionaryProvider f => f.LastUsedSource switch
                {
                    Infrastructure.Persistence.FallbackDictionaryProvider.ProviderSource.Primary  => ("API", Color.MediumSeaGreen),
                    Infrastructure.Persistence.FallbackDictionaryProvider.ProviderSource.Fallback => ($"Excel (fallback: {f.LastFallbackReason})", Color.Goldenrod),
                    _ => ("API+Excel?", Color.SteelBlue)
                },
                Infrastructure.Persistence.Api.DictionaryApiProvider => ("API", Color.MediumSeaGreen),
                _ => ("Excel", Color.Goldenrod)
            };
            var lblProvider = new ToolStripStatusLabel(providerTag.text)
            {
                BackColor = providerTag.color,
                ForeColor = Color.White,
                Font = new Font(Font.FontFamily, 8.25f, FontStyle.Bold)
            };
            statusStrip1.Items.Add(lblProvider);
            // TEMP END
            IndirizziProtocollo = _dictionaryCache.Addresses.ToList();
            Comandi = _dictionaryCache.Commands.ToList();
            Dizionario = new List<Variable>();

            _terminal.WriteLog("--------------------------------------------------------------------");
            foreach (ProtocolAddress item in IndirizziProtocollo)
            {
                UpdateTerminal($"Macchina: {item.DeviceName}, Scheda: {item.BoardName}, Indirizzo: {item.Address}");
                if (!comboBoxMachine.Items.Contains(item.DeviceName))
                    comboBoxMachine.Items.Add(item.DeviceName);
            }

            _terminal.WriteLog("--------------------------------------------------------------------");
            comboBoxCommand.Items.Clear();
            foreach (Command item in Comandi)
            {
                UpdateTerminal($"Comando: {item.Name}, codeH: {item.CodeHigh}, codeL: {item.CodeLow}");
                if (!comboBoxCommand.Items.Contains(item.Name)
                    && !item.Name.Contains("risposta")
                    && !item.Name.Contains("Risposta"))
                    comboBoxCommand.Items.Add(item.Name);
            }

            // Pre-selezione device/board iniziale (blocco #7 in PREPROCESSOR_DIRECTIVES.md):
            // due strategie in base ai campi di IDeviceVariantConfig popolati dalla factory.
            //   TOPLIFT  → DefaultRecipientId fisso (0x00080381), nessun lookup nel dizionario
            //              (legacy: non selezionava combo device/board perché UI nascoste).
            //   EDEN     → DeviceName="EDEN"+BoardName="Madre", lookup nell'elenco indirizzi
            //              per ricavare l'address reale + aggiorna combo device/board.
            //   EGICON   → DeviceName="SPARK"+BoardName="HMI", stesso pattern di EDEN.
            //   Generic  → tutti i campi vuoti, nessuna pre-selezione (lista variabili vuota).
            if (_variantConfig.DefaultRecipientId != 0)
            {
                RecipientId = _variantConfig.DefaultRecipientId;
                label12.Text = $"Indirizzo\n 0x{RecipientId:X8}";
                await _dictionaryCache.SelectByRecipientAsync(RecipientId, ct);
                Dizionario = _dictionaryCache.Variables.ToList();
            }
            else if (!string.IsNullOrEmpty(_variantConfig.DeviceName)
                     && !string.IsNullOrEmpty(_variantConfig.BoardName))
            {
                foreach (ProtocolAddress item in IndirizziProtocollo)
                {
                    if (item.BoardName == _variantConfig.BoardName
                        && item.DeviceName == _variantConfig.DeviceName)
                    {
                        label12.Text = $"Indirizzo\n {item.Address}";
                        RecipientId = Convert.ToUInt32(item.Address.Substring(2), 16);
                        await _dictionaryCache.SelectByRecipientAsync(RecipientId, ct);
                        Dizionario = _dictionaryCache.Variables.ToList();

                        int indice = comboBoxMachine.FindStringExact(_variantConfig.DeviceName);
                        if (indice != -1) comboBoxMachine.SelectedIndex = indice;

                        indice = comboBoxBoard.FindStringExact(_variantConfig.BoardName);
                        if (indice != -1) comboBoxBoard.SelectedIndex = indice;
                        break;
                    }
                }
            }
        }

        private async void buttonSendPS_Click(object? sender, EventArgs e)
        {
            // Wrap difensivo: SendPS_Async può lanciare (BLE disconnesso a metà invio,
            // timeout driver, ObjectDisposedException da Plugin.BLE, ecc). Senza try/catch
            // l'eccezione risale al SynchronizationContext di WinForms e crasha l'app.
            // Meglio mostrare MessageBox e lasciare che l'utente ritenti.
            try
            {
                await SendPS_Async(sender, e);
            }
            catch (Exception ex)
            {
                ShowDriverError("Errore invio",
                    $"Invio fallito ({ex.GetType().Name}): {ex.Message}");
            }
        }

        private async Task SendPS_Async(object? sender, EventArgs e)
        {
            var protocol = _connMgr.ActiveProtocol;
            if (protocol is null)
            {
                MessageBox.Show("Select communication channel first!");
                return;
            }

            byte cmdHi = (byte)(this.SelectedCommand >> 8);
            byte cmdLo = (byte)this.SelectedCommand;
            var command = new Command("UserSend", cmdHi.ToString("X2"), cmdLo.ToString("X2"));

            var byteList = new List<byte>();

            // Se la UI è in modalità leggi/scrivi variabile, i primi 2 byte sono l'address dal dizionario.
            if ((tableLayoutPanelProtocol.ColumnStyles[3].Width != 0)
                && (comboBoxVariables.Items.Count != 0)
                && (comboBoxVariables.SelectedIndex >= 0))
            {
                this.textBox1.Text = "";
                this.textBox2.Text = "";
                var variable = Dizionario.ElementAt(comboBoxVariables.SelectedIndex);
                byteList.Add(Convert.FromHexString(variable.AddressHigh.PadLeft(2, '0'))[0]);
                byteList.Add(Convert.FromHexString(variable.AddressLow.PadLeft(2, '0'))[0]);
            }

            // Byte esadecimali dai due TextBox singoli.
            foreach (var textBox in new[] { this.textBox1, this.textBox2 })
            {
                if (string.IsNullOrWhiteSpace(textBox.Text)) continue;
                if (!byte.TryParse(textBox.Text, NumberStyles.HexNumber, null, out var value))
                {
                    MessageBox.Show($"Valore non valido nel campo {textBox.Name}. Inserisci un valore esadecimale valido (0-FF).",
                                    "Errore di input", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                byteList.Add(value);
            }

            // Byte aggiuntivi dal textbox multi-valore (separati da spazio).
            foreach (string hex in textBox3.Text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                byteList.Add(byte.Parse(hex, NumberStyles.HexNumber));
            }

            byte[] payload = byteList.ToArray();
            DisplaySendPacket(command, payload);
            await protocol.SendCommandAsync(this.RecipientId, command, payload);
        }

        private void DisplaySendPacket(Command command, byte[] payload)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string line = $"TX - {timestamp}: {command.CodeHigh} {command.CodeLow} " +
                          string.Join(" ", payload.Select(b => b.ToString("X2"))) + "\n";
            if (richTextBoxTx.InvokeRequired)
                richTextBoxTx.BeginInvoke(new Action(() => AppendLog(line)));
            else
                AppendLog(line);
        }

        private void AppendLog(string line)
        {
            richTextBoxTx.AppendText(line);
            richTextBoxTx.SelectionStart = richTextBoxTx.Text.Length;
            richTextBoxTx.ScrollToCaret();
        }

        private void comboBoxCommand_SelectedIndexChanged(object? sender, EventArgs e)
        {
            SelectedCommand = (short)comboBoxCommand.SelectedIndex;

            if ((SelectedCommand == 1) || (SelectedCommand == 2))
            {
                comboBoxVariables.Items.Clear();

                _terminal.WriteLog("--------------------------------------------------------------------");
                // Stampa i risultati (per verifica)
                foreach (Variable itemtemp in Dizionario)
                {
                    UpdateTerminal($"Variabile logica: {itemtemp.Name}, addrH: {itemtemp.AddressHigh}, addrL: {itemtemp.AddressLow}");

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


        /// <summary>
        /// Handler dell'evento forward <see cref="ConnectionManager.AppLayerDecoded"/>.
        /// Emette log nel richTextBox dell'applicazione (suppressa su TOPLIFT: quella
        /// variante usa solo la tab TopLiftTelemetry per la visualizzazione).
        /// </summary>
        private void OnAppLayerDecodedFromConnMgr(object? sender, AppLayerDecodedEvent evt)
        {
            if (_variantConfig.Variant == DeviceVariant.TopLift) return;
            if (richTextBoxTx.InvokeRequired)
                richTextBoxTx.BeginInvoke(new Action(() => DisplayDecodedPacket(evt)));
            else
                DisplayDecodedPacket(evt);
        }

        private void DisplayDecodedPacket(AppLayerDecodedEvent evt)
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            // Se il sender è nel dizionario mostra "Device->Board", altrimenti mostra l'hex
            // raw del senderId (parità col legacy che mostrava "0x" + sourceAddress.ToString("X8")
            // quando la scheda non era in tabella).
            string senderName = (!string.IsNullOrEmpty(evt.SenderDevice) && !string.IsNullOrEmpty(evt.SenderBoard))
                ? $"{evt.SenderDevice}->{evt.SenderBoard}"
                : $"0x{evt.SenderId:X8}";

            if (evt.Command.Name != "None")
            {
                richTextBoxTx.AppendText($"RX - {timestamp}: Comando '{evt.Command.Name}' da {senderName}: ");
                // Se la decode ha associato una Variable (risposta CMD_READ_VARIABLE), mostra value BE.
                if (evt.Variable is { } variable && variable.Name != "None" && evt.Payload.Length >= 2)
                {
                    richTextBoxTx.AppendText($" {variable.Name}= ");
                    AppendValueFromReadReply(variable.DataType, evt.Payload);
                }
            }
            else
            {
                richTextBoxTx.AppendText($"RX - {timestamp}: Comando non presente in dizionario da {senderName}: ");
            }
            richTextBoxTx.AppendText("[RAW: ");
            foreach (var b in evt.Payload) richTextBoxTx.AppendText(b.ToString("X2") + " ");
            richTextBoxTx.AppendText("]\r\n");
            richTextBoxTx.SelectionStart = richTextBoxTx.Text.Length;
            richTextBoxTx.ScrollToCaret();
        }

        private void AppendValueFromReadReply(string? dataType, System.Collections.Immutable.ImmutableArray<byte> alPayload)
        {
            // alPayload layout: [AddrH, AddrL, value_BE...]. I primi 2 byte sono l'address;
            // la coda è il valore in big-endian (parità col decoder legacy CMD 80 01).
            int start = 2;
            int width = (dataType?.Trim()) switch
            {
                "uint8_t" or "int8_t" => 1,
                "uint16_t" or "int16_t" => 2,
                "uint32_t" or "int32_t" or "float" => 4,
                "bool" => 1,
                _ => alPayload.Length - start
            };
            if (alPayload.Length < start + width) return;

            richTextBoxTx.AppendText("0x");
            for (int i = 0; i < width; i++) richTextBoxTx.AppendText(alPayload[start + i].ToString("X2"));

            uint numericBe = 0;
            for (int i = 0; i < width; i++) numericBe = (numericBe << 8) | alPayload[start + i];
            richTextBoxTx.AppendText($" ({numericBe}) ");
        }

        // Helper thread-safe: aggiorna label di stato canale (testo + colore) con marshal sul thread UI.
        private void UpdateConnectionStatus(ToolStripStatusLabel label, bool isConnected, string connectedText, string disconnectedText)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => UpdateConnectionStatus(label, isConnected, connectedText, disconnectedText)));
                return;
            }

            label.Text = isConnected ? connectedText : disconnectedText;
            label.BackColor = isConnected ? System.Drawing.Color.GreenYellow : System.Drawing.Color.Salmon;
        }

        /// <summary>
        /// Handler forward <see cref="ConnectionManager.StateChanged"/>: aggiorna le 3
        /// label status (PCAN/BLE/COM) in base al canale che ha cambiato stato.
        /// </summary>
        private void OnConnectionStateChanged(object? sender, ConnectionStateSnapshot s)
        {
            bool connected = s.State == ConnectionState.Connected;
            switch (s.Channel)
            {
                case ChannelKind.Can:
                    UpdateConnectionStatus(PCanLabel, connected, "PCAN: Connected", "PCAN: Not Connected");
                    break;
                case ChannelKind.Ble:
                    UpdateConnectionStatus(BLEStatusLabel, connected, "BLE: Connesso", "BLE: Non connesso");
                    break;
                case ChannelKind.Serial:
                    UpdateConnectionStatus(COMStatusLabel, connected, "COM: Connesso", "COM: Non connesso");
                    break;
            }
        }

        private async void bluetoothLEToolStripMenuItem_Click(object? sender, EventArgs e)
            => await SwitchChannelAsync(ChannelKind.Ble);

        private void serialToolStripMenuItem_DropDownOpening(object? sender, EventArgs e)
        {
            serialToolStripMenuItem.DropDownItems.Clear();
            _serialPortManager.ScanPorts();

            foreach (string port in _serialPortManager.AvailablePorts)
            {
                var portItem = new ToolStripMenuItem(port);
                portItem.Click += async (_, _) =>
                {
                    _serialPortManager.Connect(port);
                    await SwitchChannelAsync(ChannelKind.Serial);
                };
                serialToolStripMenuItem.DropDownItems.Add(portItem);
            }

            if (_serialPortManager.AvailablePorts.Count == 0)
            {
                var noPortsItem = new ToolStripMenuItem("Nessuna porta disponibile") { Enabled = false };
                serialToolStripMenuItem.DropDownItems.Add(noPortsItem);
            }
        }

        // I 4 handler baudrate (100/125/250/500 kbps) mantengono il switch a CAN ma non
        // applicano più runtime change del baudrate: col nuovo stack PCAN il bitrate è
        // configurato alla creazione di CanPort. TODO: esporre baudrate runtime via CanPort
        // se serve (ora fix 250kbps).
        private async void toolStripMenuItem2_Click(object? sender, EventArgs e)
            => await SwitchChannelAsync(ChannelKind.Can);

        private async void toolStripMenuItem3_Click(object? sender, EventArgs e)
            => await SwitchChannelAsync(ChannelKind.Can);

        private async void kbpsToolStripMenuItem1_Click(object? sender, EventArgs e)
            => await SwitchChannelAsync(ChannelKind.Can);

        private async void kbpsToolStripMenuItem_Click(object? sender, EventArgs e)
            => await SwitchChannelAsync(ChannelKind.Can);

        /// <summary>
        /// Attiva il canale indicato tramite <see cref="ConnectionManager.SwitchToAsync"/>,
        /// aggiorna lo stato UI e propaga il recipient al servizio telemetria appena creato.
        /// </summary>
        private async Task SwitchChannelAsync(ChannelKind kind)
        {
            try
            {
                CurrentChannel = kind;
                UpdateChannelMenuChecks();
                await _connMgr.SwitchToAsync(kind);
                _connMgr.CurrentTelemetry?.UpdateSourceAddress(RecipientId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Switch canale fallito: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateChannelMenuChecks()
        {
            cANToolStripMenuItem.Checked = CurrentChannel == ChannelKind.Can;
            bluetoothLEToolStripMenuItem.Checked = CurrentChannel == ChannelKind.Ble;
            serialToolStripMenuItem.Checked = CurrentChannel == ChannelKind.Serial;
        }
    }
}