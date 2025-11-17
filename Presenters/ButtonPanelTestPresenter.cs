using STEMPM.Core.Enums;
using STEMPM.Core.Interfaces;
using STEMPM.Core.Models;
using STEMPM.GUI.Views;

namespace STEMPM.Presenters
{
    // Presenter che gestisce la comunicazione tra la vista di test delle pulsantiere e il servizio di test
    internal class ButtonPanelTestPresenter
    {
        private readonly IButtonPanelTestTab _view;
        private readonly IButtonPanelTestService _service;

        public ButtonPanelTestPresenter(IButtonPanelTestTab view, IButtonPanelTestService service)
        {
            _view = view;
            _service = service;
            _view.OnRunTestsClicked += HandleRunTests;
        }

        // Metodo per gestire l'evento di esecuzione dei test
        private async void HandleRunTests(object? sender, EventArgs e)
        {
            ButtonPanelType panelType = _view.GetSelectedPanelType();

            _view.ShowProgress("Running tests for type " + panelType + "...");
            try
            {
                List<ButtonPanelTestResult> results = await _service.RunAllTestsAsync(panelType);
                _view.DisplayResults(results);
            }
            catch (Exception ex)
            {
                _view.ShowError(ex.Message);
            }
            _view.ShowProgress("Tests complete.");
        }
    }
}
