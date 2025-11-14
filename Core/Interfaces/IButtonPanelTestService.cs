using STEMPM.Core.Enums;
using STEMPM.Core.Models;

namespace STEMPM.Core.Interfaces
{
    // Interfaccia per il servizio di test delle pulsantiere
    internal interface IButtonPanelTestService
    {
        // Esegue tutti i test per una pulsantiera specifica e restituisce i risultati
        Task<List<ButtonPanelTestResult>> RunAllTestsAsync(ButtonPanelType panelType);

        // Esegue il test dei pulsanti per una pulsantiera specifica
        Task<ButtonPanelTestResult> TestButtonsAsync(ButtonPanel panel);

        // Esegue il test del LED per una pulsantiera specifica
        Task<ButtonPanelTestResult> TestLedAsync(ButtonPanel panel);

        // Esegue il test del buzzer per una pulsantiera specifica
        Task<ButtonPanelTestResult> TestBuzzerAsync(ButtonPanel panel);
    }
}
