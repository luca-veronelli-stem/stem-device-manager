using STEMPM.Core.Enums;
using STEMPM.Core.Models;

namespace STEMPM.GUI.Views
{
    // Interfaccia della vista per la scheda di test delle pulsantiere
    internal interface IButtonPanelTestTab
    {
        event EventHandler OnRunTestsClicked;
        ButtonPanelType GetSelectedPanelType();
        void DisplayResults(List<ButtonPanelTestResult> results);
        void ShowProgress(string message);
        void ShowError(string message);
    }
}
