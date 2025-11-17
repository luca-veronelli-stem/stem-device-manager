using STEMPM.Core.Enums;
using STEMPM.Core.Models;

namespace STEMPM.GUI.Views
{
    public partial class ButtonPanelTestTabControl : UserControl, IButtonPanelTestTab
    {
        public event EventHandler? OnRunTestsClicked;

        public ButtonPanelTestTabControl()
        {
            InitializeComponent();

            // Popola la ComboBox con i tipi di pulsantiere disponibili
            comboBoxPanelType.DataSource = Enum.GetValues(typeof(ButtonPanelType));
            comboBoxPanelType.SelectedIndex = -1;
            // Popola la ComboBox con i tipi di test disponibili
            comboBoxSelectTest.DataSource = Enum.GetValues(typeof(ButtonPanelTestType));
            comboBoxSelectTest.SelectedIndex = -1;
        }

        // Restituisce il tipo di pulsantiera selezionato
        public ButtonPanelType GetSelectedPanelType()
        {
            return (ButtonPanelType)comboBoxPanelType.SelectedItem;
        }

        // Aggiorna la lista dei risultati con il risultato del collaudo eseguito
        public void DisplayResults(List<ButtonPanelTestResult> results)
        {
            listBoxResults.Items.Clear();
            foreach (var result in results)
            {
                string status = result.Passed ? "PASSED" : "FAILED";
                listBoxResults.Items.Add($"[{result.PanelType}] {result.TestType}: {status} - {result.Message}");
            }
        }

        // Aggiorna lo stato del collaudo
        public void ShowProgress(string message)
        {
            labelStatus.Text = message;
        }

        // Mostra eventuali messaggi di errore
        public void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
