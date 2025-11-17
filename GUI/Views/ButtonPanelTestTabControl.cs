using STEMPM.Core.Enums;
using STEMPM.Core.Models;

namespace STEMPM.GUI.Views
{
    public partial class ButtonPanelTestTabControl : UserControl, IButtonPanelTestTab
    {
        // Evento pubblico per notificare quando l'utente fa clic sul pulsante di esecuzione dei test
        public event EventHandler? OnRunTestsClicked;

        // Costruttore del controllo
        public ButtonPanelTestTabControl()
        {
            InitializeComponent();

            // Popola la ComboBox con i tipi di pulsantiere disponibili
            comboBoxPanelType.DataSource = Enum.GetValues(typeof(ButtonPanelType));
            comboBoxPanelType.SelectedIndex = 0;

            // Popola la ComboBox con i tipi di test disponibili
            comboBoxSelectTest.DataSource = Enum.GetValues(typeof(ButtonPanelTestType));
            comboBoxSelectTest.SelectedIndex = 0;

            // Associa l'evento di click del pulsante all'evento pubblico
            buttonRunTests.Click += (s, e) => OnRunTestsClicked?.Invoke(this, EventArgs.Empty);
        }

        // Restituisce il tipo di pulsantiera selezionato
        public ButtonPanelType GetSelectedPanelType()
        {
            return (ButtonPanelType)comboBoxPanelType.SelectedItem;
        }

        // Restituisce il tipo di test selezionato
        public ButtonPanelTestType GetSelectedTestType()
        {
            return (ButtonPanelTestType)comboBoxSelectTest.SelectedItem;
        }

        // Aggiorna la lista dei risultati con il risultato del collaudo eseguito
        public void DisplayResults(List<ButtonPanelTestResult> results)
        {
            listBoxResults.Items.Clear();
            foreach (var result in results)
            {
                string status = result.Passed ? "PASSATO" : "FALLITO";
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
            MessageBox.Show(message, "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
