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
            comboBoxPanelType.SelectedIndexChanged += ComboBoxPanelType_SelectedIndexChanged;

            // Popola la ComboBox con i tipi di test disponibili
            UpdateTestTypeComboBox((ButtonPanelType)comboBoxPanelType.SelectedItem);
            comboBoxSelectTest.SelectedIndex = 0;

            // Associa l'evento di click del pulsante all'evento pubblico
            buttonRunTests.Click += (s, e) => OnRunTestsClicked?.Invoke(this, EventArgs.Empty);
        }

        // Gestore per aggiornare i test disponibili in base al tipo di panel
        private void ComboBoxPanelType_SelectedIndexChanged(object? sender, EventArgs e)
        {
            var selectedType = (ButtonPanelType)comboBoxPanelType.SelectedItem;
            UpdateTestTypeComboBox(selectedType);
        }

        // Metodo helper per aggiornare comboBoxSelectTest filtrando opzioni non supportate
        private void UpdateTestTypeComboBox(ButtonPanelType panelType)
        {
            var panel = ButtonPanel.GetByType(panelType);
            var allTestTypes = Enum.GetValues(typeof(ButtonPanelTestType)).Cast<ButtonPanelTestType>().ToList();

            // Rimuovi Led se non supportato
            if (!panel.HasLed)
            {
                allTestTypes.Remove(ButtonPanelTestType.Led);
            }

            comboBoxSelectTest.DataSource = allTestTypes;
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

        // Mostra un prompt all'utente
        public async Task ShowPromptAsync(string message, string title = "Istruzione collaudo pulsanti")
        {
            await Task.Run(() => MessageBox.Show(message, title, 
                MessageBoxButtons.OK, MessageBoxIcon.Information));
        }

        // Chiedi una conferma all'utente
        public async Task<bool> ShowConfirmAsync(string message, ButtonPanelTestType testType)
        {
            string title = $"Conferma collaudo {testType}";

            return await Task.Run(() => MessageBox.Show(message, title, 
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes);
        }

        // Aggiorna la lista dei risultati con il risultato del collaudo eseguito
        public void DisplayResults(List<ButtonPanelTestResult> results)
        {
            foreach (var result in results)
            {
                string status = result.Passed ? "PASSATO" : "FALLITO";
                // TODO : tornare a capo se il messaggio è troppo lungo
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
