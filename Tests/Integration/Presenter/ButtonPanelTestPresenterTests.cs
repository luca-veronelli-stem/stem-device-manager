using App.GUI.Presenters;
using Core.Enums;
using Tests.Integration.Presenter.Mocks;

namespace Tests.Integration.Presenter;

/// <summary>
/// Test di integrazione per ButtonPanelTestPresenter.
/// Verifica l'orchestrazione tra view (mock) e service (mock).
/// </summary>
public class ButtonPanelTestPresenterTests
{
    private readonly MockButtonPanelTestTab _view;
    private readonly MockButtonPanelTestService _service;
    private readonly ButtonPanelTestPresenter _presenter;

    public ButtonPanelTestPresenterTests()
    {
        _view = new MockButtonPanelTestTab();
        _service = new MockButtonPanelTestService();
        _presenter = new ButtonPanelTestPresenter(_view, _service);
    }

    /// <summary>
    /// Attende brevemente per permettere all'async void di completare.
    /// </summary>
    private static async Task WaitForAsyncVoid(int delayMs = 200)
        => await Task.Delay(delayMs);

    // --- Dispatch test type ---

    [Fact]
    public async Task StartTest_Complete_CallsTestAllAsync()
    {
        _view.SelectedTestType = ButtonPanelTestType.Complete;

        _view.RaiseStartTest();
        await WaitForAsyncVoid();

        Assert.Equal(1, _service.TestAllAsyncCalls);
        Assert.Equal(0, _service.TestButtonsAsyncCalls);
    }

    [Fact]
    public async Task StartTest_Buttons_CallsTestButtonsAsync()
    {
        _view.SelectedTestType = ButtonPanelTestType.Buttons;

        _view.RaiseStartTest();
        await WaitForAsyncVoid();

        Assert.Equal(1, _service.TestButtonsAsyncCalls);
        Assert.Equal(0, _service.TestAllAsyncCalls);
    }

    [Fact]
    public async Task StartTest_Led_CallsTestLedAsync()
    {
        _view.SelectedTestType = ButtonPanelTestType.Led;

        _view.RaiseStartTest();
        await WaitForAsyncVoid();

        Assert.Equal(1, _service.TestLedAsyncCalls);
    }

    [Fact]
    public async Task StartTest_Buzzer_CallsTestBuzzerAsync()
    {
        _view.SelectedTestType = ButtonPanelTestType.Buzzer;

        _view.RaiseStartTest();
        await WaitForAsyncVoid();

        Assert.Equal(1, _service.TestBuzzerAsyncCalls);
    }

    // --- Passaggio panel type ---

    [Fact]
    public async Task StartTest_PassesSelectedPanelTypeToService()
    {
        _view.SelectedPanelType = ButtonPanelType.DIS0026166;
        _view.SelectedTestType = ButtonPanelTestType.Led;

        _view.RaiseStartTest();
        await WaitForAsyncVoid();

        Assert.Equal(ButtonPanelType.DIS0026166, _service.LastPanelType);
    }

    // --- Progress e reset ---

    [Fact]
    public async Task StartTest_ShowsProgressAndResetsIndicators()
    {
        _view.SelectedTestType = ButtonPanelTestType.Buttons;

        _view.RaiseStartTest();
        await WaitForAsyncVoid();

        Assert.Equal(1, _view.ResetAllIndicatorsCalls);
        Assert.True(_view.ProgressMessages.Count >= 1);
        Assert.Contains(
            _view.ProgressMessages,
            m => m.Contains("Avvio collaudo"));
    }

    // --- DisplayResults ---

    [Fact]
    public async Task StartTest_DisplaysResults()
    {
        _view.SelectedTestType = ButtonPanelTestType.Buzzer;

        _view.RaiseStartTest();
        await WaitForAsyncVoid();

        Assert.True(_view.DisplayedResults.Count >= 1);
    }

    // --- Timeout ---

    [Fact]
    public async Task StartTest_ServiceThrowsTimeout_ShowsError()
    {
        _view.SelectedTestType = ButtonPanelTestType.Led;
        _service.ExceptionToThrow = new TimeoutException("device offline");

        _view.RaiseStartTest();
        await WaitForAsyncVoid();

        Assert.Contains(
            _view.ErrorMessages,
            m => m.Contains("Timeout"));
    }

    // --- Download senza risultati ---

    [Fact]
    public void Download_NoResults_ShowsInfoMessage()
    {
        _view.RaiseDownloadResult();

        Assert.Single(_view.Messages);
        Assert.Contains("Nessun risultato", _view.Messages[0].Msg);
    }

    // --- Download con risultati ---

    [Fact]
    public async Task Download_WithResults_SavesFile()
    {
        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"presenter_test_{Guid.NewGuid():N}.txt");

        try
        {
            // Esegui un test per popolare _latestResults
            _view.SelectedTestType = ButtonPanelTestType.Buzzer;
            _view.RaiseStartTest();
            await WaitForAsyncVoid();

            // Configura il path di salvataggio e scarica
            _view.SaveFilePath = tempPath;
            _view.ResultsText = "Buzzer: PASSED";
            _view.RaiseDownloadResult();

            Assert.True(File.Exists(tempPath));
            Assert.Equal("Buzzer: PASSED", File.ReadAllText(tempPath));
        }
        finally
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
        }
    }
}
