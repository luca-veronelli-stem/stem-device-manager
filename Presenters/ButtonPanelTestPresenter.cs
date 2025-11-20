using STEMPM.Core.Enums;
using STEMPM.Core.Interfaces;
using STEMPM.Core.Models;
using STEMPM.GUI.Views;

namespace STEMPM.Presenters
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
            _view.OnRunTestsClicked += HandleRunTestsAsync;
        }

        // Metodo per gestire l'evento di esecuzione dei test
        private async void HandleRunTestsAsync(object? sender, EventArgs e)
        {
            ButtonPanelType panelType = _view.GetSelectedPanelType();
            ButtonPanelTestType testType = _view.GetSelectedTestType();

            ButtonPanel panel = ButtonPanel.GetByType(panelType);

            _view.ShowProgress($"Collaudo pulsantiera di tipo {panelType}...");
            try
            {
                List<ButtonPanelTestResult> results = new List<ButtonPanelTestResult>();

                // Chiama il servizio per il tipo di collaudo selezionato
                switch (testType)
                {
                    case ButtonPanelTestType.Complete:
                        results = await _service.TestAllAsync(panelType);
                        break;
                    case ButtonPanelTestType.Buttons:
                        results.Add(await _service.TestButtonsAsync(panelType));
                        break;
                    case ButtonPanelTestType.Led:
                        results.Add(await _service.TestLedAsync(panelType));
                        break;
                    case ButtonPanelTestType.Buzzer:
                        results.Add(await _service.TestBuzzerAsync(panelType));
                        break;
                    default:
                        _view.ShowError("Tipo di collaudo sconosciuto.");
                        return;
                }

                _view.DisplayResults(results);
            }
            catch (Exception ex)
            {
                _view.ShowError(ex.Message);
            }

            _view.ShowProgress("Collaudo completo.");
        }
    }
}
