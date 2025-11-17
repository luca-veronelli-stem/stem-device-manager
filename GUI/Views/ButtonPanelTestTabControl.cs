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

        public ButtonPanelType GetSelectedPanelType()
        {
            throw new NotImplementedException();
        }

        public void DisplayResults(List<ButtonPanelTestResult> results)
        {
            throw new NotImplementedException();
        }

        public void ShowProgress(string message)
        {
            throw new NotImplementedException();
        }

        public void ShowError(string message)
        {
            throw new NotImplementedException();
        }
    }
}
