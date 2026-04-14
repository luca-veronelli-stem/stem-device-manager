using Core.Enums;
using Core.Interfaces;
using App.Core.Interfaces;
using Core.Models;

namespace App.GUI.Presenters
{
    // Presenter che gestisce la comunicazione tra la pagina collaudo e il servizio collaudo pulsantiere
    internal class ButtonPanelTestPresenter
    {
        private readonly IButtonPanelTestTab _view;
        private readonly IButtonPanelTestService _service; 
        private CancellationTokenSource? _cts;
        private string _lastPromptMessage = string.Empty;
        private List<ButtonPanelTestResult>? _latestResults;

        // Costruttore che inizializza la vista e il servizio, e si iscrive all'evento di esecuzione dei test
        public ButtonPanelTestPresenter(IButtonPanelTestTab view, IButtonPanelTestService service)
        {
            _view = view;
            _service = service;
            _view.OnStartTestClicked += HandleStartTestAsync;
            _view.OnStopTestClicked += HandleStopTestAsync;
            _view.OnDownloadTestResultClicked += HandleDownloadTestResult;
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

            List<ButtonPanelTestResult> results = [];

            try
            {
                async Task promptFunc(string msg)
                {
                    _lastPromptMessage = msg;
                    await _view.ShowPromptAsync(msg);
                }

                Task<bool> confirmFunc(string msg) => _view.ShowConfirmAsync(msg, testType);

                void onButtonStart(int i) => _view.SetButtonWaiting(i);

                void onButtonResult(int i, bool passed)
                {
                    _view.SetButtonResult(i, passed);
                    Color resultColor = passed ? Color.LimeGreen : Color.Red;
                    _view.UpdateLastPromptColor(_lastPromptMessage, resultColor);
                }

                switch (testType)
                {
                    case ButtonPanelTestType.Complete:
                        results = await _service.TestAllAsync(panelType, confirmFunc, promptFunc, onButtonStart, onButtonResult, _cts.Token);
                        break;

                    case ButtonPanelTestType.Buttons:
                        results.Add(await _service.TestButtonsAsync(panelType, promptFunc, onButtonStart, onButtonResult, _cts.Token));
                        break;

                    case ButtonPanelTestType.Led:
                        results.Add(await _service.TestLedAsync(panelType, confirmFunc, _cts.Token));
                        break;

                    case ButtonPanelTestType.Buzzer:
                        results.Add(await _service.TestBuzzerAsync(panelType, confirmFunc, _cts.Token));
                        break;

                    default:
                        throw new NotSupportedException("Tipo di collaudo non supportato.");
                }

                _latestResults = results;
                _view.DisplayResults(results);

                string progressMessage = results.Any(r => r.Interrupted)
                    ? "Collaudo interrotto dall'utente."
                    : $"Collaudo {testType} completato." + Environment.NewLine;
                _view.ShowProgress(progressMessage);
            }
            catch (OperationCanceledException)
            {
                _view.ShowProgress("Collaudo interrotto dall'utente.");
            }
            catch (TimeoutException ex)
            {
                _view.ShowError($"Timeout durante il collaudo: {ex.Message}");
                _view.ShowProgress("Collaudo interrotto a causa di timeout.");
            }
            catch (Exception ex)
            {
                _view.ShowError($"Errore durante il collaudo: {ex.Message}");
                _view.ShowProgress("Collaudo interrotto.");
            }
            finally
            {
                if (_latestResults != null && _latestResults.Any())
                {
                    _view.DisplayResults(_latestResults);
                }

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

        // Metodo per gestire l'evento di download dei risultati del collaudo
        private void HandleDownloadTestResult(object? sender, EventArgs e)
        {
            if (_latestResults == null || _latestResults.Count == 0)
            {
                _view.ShowMessage("Nessun risultato da scaricare.", "Informazione", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string? filePath = _view.ShowSaveFileDialog();
            if (string.IsNullOrEmpty(filePath)) return;

            try
            {
                string content = _view.GetResultsText();
                File.WriteAllText(filePath, content);
                _view.ShowMessage("Risultati salvati con successo.", "Successo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (IOException ex)
            {
                _view.ShowMessage($"Errore I/O durante il salvataggio: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                _view.ShowMessage($"Accesso negato durante il salvataggio: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                _view.ShowMessage($"Errore durante il salvataggio: {ex.Message}", "Errore", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
