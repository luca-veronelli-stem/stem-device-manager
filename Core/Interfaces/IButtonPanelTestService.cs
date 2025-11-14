using STEMPM.Core.Enums;
using STEMPM.Core.Models;

namespace STEMPM.Core.Interfaces
{
    // Interfaccia per il servizio di test delle pulsantiere
    internal interface IButtonPanelTestService
    {
        // Esegue tutti i test per una pulsantiera specifica e restituisce i risultati
        Task<List<ButtonPanelTestResult>> RunAllTestsAsync(ButtonPanelType panelType);
    }
}
