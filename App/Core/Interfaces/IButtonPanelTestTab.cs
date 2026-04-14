using Core.Enums;
using Core.Models;

namespace App.Core.Interfaces
{
    // Interfaccia della vista per la pagina di collaudo pulsantiere
    internal interface IButtonPanelTestTab
    {
        // Gestore dell'evento sollevato quando l'utente clicca sul pulsante per avviare il collaudo
        event EventHandler OnStartTestClicked;

        // Gestore dell'evento sollevato quando l'utente clicca sul pulsante per arrestare il collaudo
        event EventHandler OnStopTestClicked;

        // Gestore dell'evento sollevato quando l'utente clicca sul pulsante per scaricare i risultati del collaudo
        event EventHandler OnDownloadTestResultClicked;

        // Metodo per ottenere il testo dei risultati del collaudo
        string GetResultsText();

        // Metodo per mostrare la finestra di dialogo per il salvataggio del file
        string? ShowSaveFileDialog();

        // Metodo per mostrare un messaggio all'utente
        void ShowMessage(string message, string title, MessageBoxButtons buttons, MessageBoxIcon icon);

        // Metodo per ottenere il tipo di pulsantiera selezionato dall'utente
        ButtonPanelType GetSelectedPanelType();

        // Metodo per ottenere il tipo di collaudo selezionato dall'utente
        ButtonPanelTestType GetSelectedTestType();

        // Metodo per impostare lo stato di un indicatore a waiting
        public void SetButtonWaiting(int buttonIndex);

        // Metodo per impostare il risultato di un indicatore
        public void SetButtonResult(int buttonIndex, bool success);

        // Metodo per resettare tutti gli indicatori della vista
        public void ResetAllIndicators();

        // Metodo per mostrare un prompt all'utente
        Task ShowPromptAsync(string message);

        // Metodo per chiedere una conferma all'utente
        Task<bool> ShowConfirmAsync(string message, ButtonPanelTestType testType);

        // Metodo per visualizzare i risultati del collaudo
        void DisplayResults(List<ButtonPanelTestResult> results);

        // Metodo per modificare lo stato della vista durante l'esecuzione del collaudo
        void ShowProgress(string message);

        // Metodo per aggiornare il colore dell'ultimo prompt visualizzato
        void UpdateLastPromptColor(string lastMessage, Color color);

        // Metodo per visualizzare eventuali messaggi di errore
        void ShowError(string message);
    }
}
