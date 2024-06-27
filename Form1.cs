using Microsoft.VisualBasic.Logging;
using System.Windows.Forms;

namespace StemPC
{
    public partial class Form1 : Form
    {
        public Terminal _terminal;

        public Form1()
        {
            InitializeComponent();
            _terminal = new Terminal(); // Inizializza l'istanza di Terminal
            UpdateTerminal(DateTime.Now + ": Stem Protocol Companion v0.1");
        }

        private void UpdateTerminal(string message)
        {
            terminalOut.Text = _terminal.WriteLog(message);
        }
    }
}
