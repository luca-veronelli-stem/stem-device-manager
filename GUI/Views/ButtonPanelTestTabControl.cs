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
