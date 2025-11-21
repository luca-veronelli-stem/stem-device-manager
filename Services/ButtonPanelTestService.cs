using STEMPM.Core.Enums;
using STEMPM.Core.Models;
using STEMPM.Core.Interfaces;


namespace STEMPM.Services
{
    // Implementazione del servizio di test delle pulsantiere
    internal class ButtonPanelTestService : IButtonPanelTestService
    {
        // Esegue tutti i test disponibili per una pulsantiera specifica e restituisce i risultati
        public async Task<List<ButtonPanelTestResult>> TestAllAsync(ButtonPanelType panelType, Func<string, Task<bool>> userConfirm, Func<string, Task> userPrompt)
        {
            ButtonPanel panel = ButtonPanel.GetByType(panelType);

            List<ButtonPanelTestResult> results = new List<ButtonPanelTestResult>
            {
                await TestButtonsAsync(panelType, userPrompt)
            };

            if (panel.HasLed)
            {
                results.Add(await TestLedAsync(panelType, userConfirm));
            }

            if (panel.HasBuzzer)
            {
                results.Add(await TestBuzzerAsync(panelType, userConfirm));
            }

            return results;
        }

        // Esegue il test dei pulsanti della pulsantiera
        public async Task<ButtonPanelTestResult> TestButtonsAsync(ButtonPanelType panelType, Func<string, Task> userPrompt)
        {
            throw new NotImplementedException();
        }

        // Esegue il collaudo dei LED della pulsantiera
        public async Task<ButtonPanelTestResult> TestLedAsync(ButtonPanelType panelType, Func<string, Task<bool>> userConfirm)
        {
            throw new NotImplementedException();
        }

        // Esegue il test del buzzer della pulsantiera
        public async Task<ButtonPanelTestResult> TestBuzzerAsync(ButtonPanelType panelType, Func<string, Task<bool>> userConfirm)
        {
            throw new NotImplementedException();
        }
    }
}
