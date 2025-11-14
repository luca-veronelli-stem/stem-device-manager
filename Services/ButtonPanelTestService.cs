using STEMPM.Core.Enums;
using STEMPM.Core.Models;
using STEMPM.Core.Interfaces;


namespace STEMPM.Services
{
    // Implementazione del servizio di test delle pulsantiere
    internal class ButtonPanelTestService : IButtonPanelTestService
    {
        // Esegue tutti i test per una pulsantiera specifica e restituisce i risultati
        public async Task<List<ButtonPanelTestResult>> RunAllTestsAsync(ButtonPanelType panelType)
        {
            ButtonPanel panel = ButtonPanel.GetByType(panelType);

            // Esegui il test dei pulsanti
            List<ButtonPanelTestResult> results = [await TestButtonsAsync(panel)];

            // Esegui il test del LED se presente
            if (panel.HasLed)
            {
                results.Add(await TestLedAsync(panel));
            }

            // Esegui il test del buzzer se presente
            if (panel.HasBuzzer)
            {
                results.Add(await TestBuzzerAsync(panel));
            }

            return results;
        }

        // Esegue il test dei pulsanti della pulsantiera
        public async Task<ButtonPanelTestResult> TestButtonsAsync(ButtonPanel panel)
        {
            throw new NotImplementedException();
        }

        // Esegue il test del LED della pulsantiera
        public async Task<ButtonPanelTestResult> TestLedAsync(ButtonPanel panel)
        {
            throw new NotImplementedException();
        }

        // Esegue il test del buzzer della pulsantiera
        public async Task<ButtonPanelTestResult> TestBuzzerAsync(ButtonPanel panel)
        {
            throw new NotImplementedException();
        }
    }
}
