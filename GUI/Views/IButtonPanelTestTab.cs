using STEMPM.Core.Enums;
using STEMPM.Core.Models;

namespace STEMPM.GUI.Views
{
    // Interfaccia della vista per la pagina di collaudo pulsantiere
    internal interface IButtonPanelTestTab
    {
        // Evento scatenato quando l'utente clicca sul pulsante per eseguire il collaudo
        event EventHandler OnRunTestsClicked;

        // Metodo per ottenere il tipo di pulsantiera selezionato dall'utente
        ButtonPanelType GetSelectedPanelType();

        // Metodo per visualizzare i risultati del collaudo
        void DisplayResults(List<ButtonPanelTestResult> results);

        // Metodo per modificare lo stato della vista durante l'esecuzione del collaudo
        void ShowProgress(string message);

        // Metodo per visualizzare eventuali messaggi di errore
        void ShowError(string message);
    }
}
