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
        private CancellationTokenSource? _cts;

        // Costruttore che inizializza la vista e il servizio, e si iscrive all'evento di esecuzione dei test
        public ButtonPanelTestPresenter(IButtonPanelTestTab view, IButtonPanelTestService service)
        {
            _view = view;
            _service = service;
            _view.OnStartTestClicked += HandleStartTestAsync;
            _view.OnStopTestClicked += HandleStopTestAsync;
        }

        // Metodo per gestire l'evento di avvio del collaudo
        private async void HandleStartTestAsync(object? sender, EventArgs e)
        {
            _cts = new CancellationTokenSource();
            ButtonPanelType panelType = _view.GetSelectedPanelType();
            ButtonPanelTestType testType = _view.GetSelectedTestType();

            var panel = ButtonPanel.GetByType(panelType);

            _view.ShowProgress($"Avvio collaudo {testType} per pulsantiera {panelType}...");
            _view.ResetAllIndicators();

            try
            {
                async Task promptFunc(string msg) => await _view.ShowPromptAsync(msg);
                async Task<bool> confirmFunc(string msg) => await _view.ShowConfirmAsync(msg, testType);

                List<ButtonPanelTestResult> results = [];

                switch (testType)
                {
                    case ButtonPanelTestType.Complete:
                        results = await _service.TestAllAsync(panelType, confirmFunc, promptFunc, _cts.Token);
                        break;

                    case ButtonPanelTestType.Buttons:
                        results.Add(await _service.TestButtonsAsync(panelType, promptFunc, _cts.Token));
                        break;

                    case ButtonPanelTestType.Led:
                        results.Add(await _service.TestLedAsync(panelType, confirmFunc, _cts.Token));
                        break;

                    case ButtonPanelTestType.Buzzer:
                        results.Add(await _service.TestBuzzerAsync(panelType, confirmFunc, _cts.Token));
                        break;

                    default:
                        _view.ShowError("Tipo di collaudo non supportato.");
                        return;
                }

                _view.DisplayResults(results);
                _view.ShowProgress($"Collaudo {testType} completato." + Environment.NewLine);
            }
            catch (OperationCanceledException)
            {
                _view.ShowProgress("Collaudo interrotto dall'utente.");
            }
            catch (Exception ex)
            {
                _view.ShowError($"Errore durante il collaudo: {ex.Message}");
                _view.ShowProgress("Collaudo interrotto.");
            }
            finally
            {
                _cts = null;
            }
        }

        // Metodo per gestire l'evento di arresto del collaudo
        private void HandleStopTestAsync(object? sender, EventArgs e)
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _view.ShowProgress("Richiesta di arresto collaudo...");
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
            string resultMessage = "";

            for (int i = 0; i < panel.ButtonCount; i++)
            {
                _view.SetButtonWaiting(i);

                string buttonName = panel.Buttons[i];
                string message = $"Premi il pulsante: {buttonName}";
                await promptFunc(message);

                byte buttonCode = panel.ButtonMasks[i];
                byte[] expectedPayload = { 0x00, 0x02, 0x80, 0x00, buttonCode };

                bool passed = await _service.AwaitButtonPressEventAsync(expectedPayload, 10000);

                _view.SetButtonResult(i, passed);

                Color resultColor = passed ? Color.LimeGreen : Color.Red;
                _view.UpdateLastPromptColor(message, resultColor);

                allPassed &= passed;
                resultMessage += $"- Pulsante {buttonName}: {(passed ? "PASSATO" : "FALLITO")}\n";
            }

            return new ButtonPanelTestResult
            {
                PanelType = panelType,
                TestType = ButtonPanelTestType.Buttons,
                Passed = allPassed,
                Message = resultMessage.Trim()
            };
        }
    }
}
