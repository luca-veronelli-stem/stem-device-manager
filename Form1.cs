using Microsoft.VisualBasic.Logging;
using System.Windows.Forms;
using System.IO.Ports; // used for serial port

namespace StemPC
{
    public partial class Form1 : Form
    {
        private Terminal _terminal;
        private SerialPortManager _serialPortManager;

        public Form1()
        {
            InitializeComponent();
            _terminal = new Terminal(); // Inizializza l'istanza di Terminal
            _serialPortManager = new SerialPortManager("COM3", 19200); ;// Inizializza l'istanza di SerialManager
            UpdateTerminal(DateTime.Now + ": Stem Protocol Companion v0.1");
        }

        private void UpdateTerminal(string message)
        {
            terminalOut.Text = _terminal.WriteLog(message);
        }
    }
}
