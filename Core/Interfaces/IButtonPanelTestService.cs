using Core.Enums;
using Core.Models;

namespace Core.Interfaces
{
    // Interfaccia per il servizio di test delle pulsantiere
    internal interface IButtonPanelTestService
    {
        // Esegue tutti i test per una pulsantiera specifica e restituisce i risultati
        Task<List<ButtonPanelTestResult>> TestAllAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            Func<string, Task> userPrompt,
            Action<int>? onButtonStart = null,
            Action<int, bool>? onButtonResult = null,
            CancellationToken cancellationToken = default);

        // Esegue il test dei pulsanti per una pulsantiera specifica
        Task<ButtonPanelTestResult> TestButtonsAsync(
            ButtonPanelType panelType,
            Func<string, Task> userPrompt,
            Action<int>? onButtonStart = null,
            Action<int, bool>? onButtonResult = null,
            CancellationToken cancellationToken = default);

        // Esegue il test del LED per una pulsantiera specifica
        Task<ButtonPanelTestResult> TestLedAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken = default);

        // Esegue il test del buzzer per una pulsantiera specifica
        Task<ButtonPanelTestResult> TestBuzzerAsync(
            ButtonPanelType panelType,
            Func<string, Task<bool>> userConfirm,
            CancellationToken cancellationToken = default);
    }
}
