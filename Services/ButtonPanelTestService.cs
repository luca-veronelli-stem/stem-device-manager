using STEMPM.Core.Enums;
using STEMPM.Core.Models;
using STEMPM.Core.Interfaces;


namespace STEMPM.Services
{
    // Implementazione del servizio di test delle pulsantiere
    internal class ButtonPanelTestService : IButtonPanelTestService
    {
        // Esegue tutti i test disponibili per una pulsantiera specifica e restituisce i risultati
        public async Task<List<ButtonPanelTestResult>> TestAllAsync(ButtonPanelType panelType)
        {
            ButtonPanel panel = ButtonPanel.GetByType(panelType);

            List<ButtonPanelTestResult> results = [await TestButtonsAsync(panelType)];

            if (panel.HasLed)
            {
                results.Add(await TestLedAsync(panelType));
            }

            if (panel.HasBuzzer)
            {
                results.Add(await TestBuzzerAsync(panelType));
            }

            return results;
        }

        // Esegue il test dei pulsanti della pulsantiera
        public async Task<ButtonPanelTestResult> TestButtonsAsync(ButtonPanelType panelType)
        {
            throw new NotImplementedException();
        }

        // Esegue il test del LED della pulsantiera
        public async Task<ButtonPanelTestResult> TestLedAsync(ButtonPanelType panelType)
        {
            throw new NotImplementedException();
        }

        // Esegue il test del buzzer della pulsantiera
        public async Task<ButtonPanelTestResult> TestBuzzerAsync(ButtonPanelType panelType)
        {
            throw new NotImplementedException();
        }
    }
}
