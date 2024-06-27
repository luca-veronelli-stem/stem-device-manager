using Microsoft.VisualBasic.Logging;
using System.Windows.Forms;
using System.IO.Ports; // used for serial port

namespace StemPC
{
    public partial class Form1 : Form
    {
        private UInt16 Prescaler1s = 0;
        private Terminal _terminal;
        private SerialPortManager _serialPortManager;
        private SerialPortManager _serialPort;

        public Form1()
        {
            InitializeComponent();
            _terminal = new Terminal(); // Inizializza l'istanza di Terminal
            _serialPortManager = new SerialPortManager("COM3", 19200); ;// Inizializza l'istanza di SerialManager
            UpdateTerminal(DateTime.Now + ": Stem Protocol Companion v0.1");
            timerBaseTime.Enabled = true;

            // Ottieni tutte le porte seriali disponibili
            // e aggiungi le porte alla ListBox
            listBoxSerialPorts.Items.Clear();
            listBoxSerialPorts.Items.AddRange(_serialPortManager.GetPorts());
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
    }
}
