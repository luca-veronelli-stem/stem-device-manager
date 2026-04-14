using Core.Enums;
using Core.Interfaces;
using Core.Models;

namespace Tests.Integration.Presenter.Mocks;

/// <summary>
/// Mock manuale di IButtonPanelTestService.
/// Permette di configurare il comportamento per ogni metodo di test.
/// </summary>
internal class MockButtonPanelTestService : IButtonPanelTestService
{
    // --- Cattura chiamate ---
    public int TestAllAsyncCalls { get; private set; }
    public int TestButtonsAsyncCalls { get; private set; }
    public int TestLedAsyncCalls { get; private set; }
    public int TestBuzzerAsyncCalls { get; private set; }

    public ButtonPanelType? LastPanelType { get; private set; }

    // --- Configurazione risultati ---
    public List<ButtonPanelTestResult>? TestAllResult { get; set; }
    public ButtonPanelTestResult? TestButtonsResult { get; set; }
    public ButtonPanelTestResult? TestLedResult { get; set; }
    public ButtonPanelTestResult? TestBuzzerResult { get; set; }

    // --- Configurazione eccezioni ---
    public Exception? ExceptionToThrow { get; set; }

    public Task<List<ButtonPanelTestResult>> TestAllAsync(
        ButtonPanelType panelType,
        Func<string, Task<bool>> userConfirm,
        Func<string, Task> userPrompt,
        Action<int>? onButtonStart = null,
        Action<int, bool>? onButtonResult = null,
        CancellationToken cancellationToken = default)
    {
        TestAllAsyncCalls++;
        LastPanelType = panelType;
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        return Task.FromResult(TestAllResult ?? DefaultResults(panelType));
    }

    public Task<ButtonPanelTestResult> TestButtonsAsync(
        ButtonPanelType panelType,
        Func<string, Task> userPrompt,
        Action<int>? onButtonStart = null,
        Action<int, bool>? onButtonResult = null,
        CancellationToken cancellationToken = default)
    {
        TestButtonsAsyncCalls++;
        LastPanelType = panelType;
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        return Task.FromResult(
            TestButtonsResult ?? DefaultResult(panelType, ButtonPanelTestType.Buttons));
    }

    public Task<ButtonPanelTestResult> TestLedAsync(
        ButtonPanelType panelType,
        Func<string, Task<bool>> userConfirm,
        CancellationToken cancellationToken = default)
    {
        TestLedAsyncCalls++;
        LastPanelType = panelType;
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        return Task.FromResult(
            TestLedResult ?? DefaultResult(panelType, ButtonPanelTestType.Led));
    }

    public Task<ButtonPanelTestResult> TestBuzzerAsync(
        ButtonPanelType panelType,
        Func<string, Task<bool>> userConfirm,
        CancellationToken cancellationToken = default)
    {
        TestBuzzerAsyncCalls++;
        LastPanelType = panelType;
        if (ExceptionToThrow != null) throw ExceptionToThrow;
        return Task.FromResult(
            TestBuzzerResult ?? DefaultResult(panelType, ButtonPanelTestType.Buzzer));
    }

    private static ButtonPanelTestResult DefaultResult(
        ButtonPanelType panel, ButtonPanelTestType test) => new()
        {
            PanelType = panel,
            TestType = test,
            Passed = true,
            Message = $"{test} OK"
        };

    private static List<ButtonPanelTestResult> DefaultResults(
        ButtonPanelType panel) =>
    [
        DefaultResult(panel, ButtonPanelTestType.Buttons),
        DefaultResult(panel, ButtonPanelTestType.Led),
        DefaultResult(panel, ButtonPanelTestType.Buzzer)
    ];
}
