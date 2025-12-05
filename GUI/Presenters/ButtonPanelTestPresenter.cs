using STEMPM.Core.ButtonPanelEnums;
using STEMPM.Core.Interfaces;
using STEMPM.Core.Models;

namespace STEMPM.GUI.Presenters
{
    // Presenter che gestisce la comunicazione tra la pagina collaudo e il servizio collaudo pulsantiere
    internal class ButtonPanelTestPresenter
    {
        private readonly IButtonPanelTestTab _view;
        private readonly IButtonPanelTestService _service;

        // Costruttore che inizializza la vista e il servizio, e si iscrive all'evento di esecuzione dei test
        public ButtonPanelTestPresenter(IButtonPanelTestTab view, IButtonPanelTestService service)
        {
            _view = view;
            _service = service;
            _view.OnStartTestClicked += HandleStartTestAsync;
            _view.OnStopTestClicked += HandleStopTestAsync;
        }

        // Metodo per gestire l'evento di esecuzione dei test
        private async void HandleStartTestAsync(object? sender, EventArgs e)
        {
            ButtonPanelType panelType = _view.GetSelectedPanelType();
            ButtonPanelTestType testType = _view.GetSelectedTestType();

            var panel = ButtonPanel.GetByType(panelType);

            _view.ShowProgress($"Avvio collaudo {testType} per pulsantiera {panelType}...");
            _view.ResetAllIndicators();

            try
            {
                async Task promptFunc(string msg) => await _view.ShowPromptAsync(msg);
                async Task<bool> confirmFunc(string msg) => await _view.ShowConfirmAsync(msg, testType);

                List<ButtonPanelTestResult> results = new();

                switch (testType)
                {
                    case ButtonPanelTestType.Complete:
                        results = await RunCompleteTest(panelType, panel, confirmFunc, promptFunc);
                        break;

                    case ButtonPanelTestType.Buttons:
                        results.Add(await RunButtonsTest(panelType, panel, promptFunc));
                        break;

                    case ButtonPanelTestType.Led:
                        results.Add(await _service.TestLedAsync(panelType, confirmFunc));
                        break;

                    case ButtonPanelTestType.Buzzer:
                        results.Add(await _service.TestBuzzerAsync(panelType, confirmFunc));
                        break;

                    default:
                        _view.ShowError("Tipo di collaudo non supportato.");
                        return;
                }

                _view.DisplayResults(results);
                _view.ShowProgress($"Collaudo {testType} completato.");
            }
            catch (Exception ex)
            {
                _view.ShowError($"Errore durante il collaudo: {ex.Message}");
                _view.ShowProgress("Collaudo interrotto.");
            }
        }

        private async Task<List<ButtonPanelTestResult>> RunCompleteTest(
            ButtonPanelType panelType,
            ButtonPanel panel,
            Func<string, Task<bool>> confirmFunc,
            Func<string, Task> promptFunc)
        {
            var results = new List<ButtonPanelTestResult>
            {
                await RunButtonsTest(panelType, panel, promptFunc)
            };

            if (panel.HasLed)
                results.Add(await _service.TestLedAsync(panelType, confirmFunc));

            if (panel.HasBuzzer)
                results.Add(await _service.TestBuzzerAsync(panelType, confirmFunc));

            return results;
        }

        private async Task<ButtonPanelTestResult> RunButtonsTest(
            ButtonPanelType panelType,
            ButtonPanel panel,
            Func<string, Task> promptFunc)
        {
            bool allPassed = true;
            string message = "";

            for (int i = 0; i < panel.ButtonCount; i++)
            {
                // Highlight current button in yellow
                _view.SetButtonWaiting(i);

                string buttonName = panel.Buttons[i];
                await promptFunc($"Premi il pulsante: {buttonName}");

                // Expected payload for this button (1 << i)
                byte buttonCode = (byte)(1 << i);
                byte[] expectedPayload = { 0x00, 0x02, 0x80, 0x00, buttonCode };

                bool passed = await _service.AwaitButtonPressEventAsync(expectedPayload, 5000);

                // Update indicator: green = OK, red = failed
                _view.SetButtonResult(i, passed);

                allPassed &= passed;
                message += $"Pulsante {buttonName}: {(passed ? "OK" : "FALLITO")}\n";
            }

            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = ButtonPanelTestType.Buttons,
                Passed = allPassed,
                Message = message.Trim()
            };
        }

        private async void HandleStopTestAsync(object? sender, EventArgs e)
        {
            throw new NotImplementedException();
        }
    }
}
