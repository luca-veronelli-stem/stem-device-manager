using STEMPM.Core.ButtonPanelEnums;
using STEMPM.Core.Models;

namespace STEMPM.Core.Interfaces
{
    // Interfaccia della vista per la pagina di collaudo pulsantiere
    internal interface IButtonPanelTestTab
    {
        // Gestore dell'evento sollevato quando l'utente clicca sul pulsante per avviare il collaudo
        event EventHandler OnStartTestClicked;

        // Gestore dell'evento sollevato quando l'utente clicca sul pulsante per arrestare il collaudo
        event EventHandler OnStopTestClicked;

        // Metodo per ottenere il tipo di pulsantiera selezionato dall'utente
        ButtonPanelType GetSelectedPanelType();

        // Metodo per ottenere il tipo di collaudo selezionato dall'utente
        ButtonPanelTestType GetSelectedTestType();

        public void SetButtonWaiting(int buttonIndex);

        public void SetButtonResult(int buttonIndex, bool success);

        public void ResetAllIndicators();

        // Metodo per mostrare un prompt all'utente
        Task ShowPromptAsync(string message, string title = "Istruzione collaudo pulsanti");

        // Metodo per chiedere una conferma all'utente
        Task<bool> ShowConfirmAsync(string message, ButtonPanelTestType testType);

        // Metodo per visualizzare i risultati del collaudo
        void DisplayResults(List<ButtonPanelTestResult> results);

        // Metodo per modificare lo stato della vista durante l'esecuzione del collaudo
        void ShowProgress(string message);

        // Metodo per visualizzare eventuali messaggi di errore
        void ShowError(string message);
    }
}
