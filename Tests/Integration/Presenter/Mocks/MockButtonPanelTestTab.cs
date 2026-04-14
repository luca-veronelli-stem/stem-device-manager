using App.Core.Interfaces;
using Core.Enums;
using Core.Models;

namespace Tests.Integration.Presenter.Mocks;

/// <summary>
/// Mock manuale di IButtonPanelTestTab.
/// Cattura le chiamate per verificare il comportamento del Presenter.
/// </summary>
internal class MockButtonPanelTestTab : IButtonPanelTestTab
{
    // --- Eventi ---
    public event EventHandler? OnStartTestClicked;
    public event EventHandler? OnStopTestClicked;
    public event EventHandler? OnDownloadTestResultClicked;

    // --- Configurazione ---
    public ButtonPanelType SelectedPanelType { get; set; }
        = ButtonPanelType.DIS0023789;

    public ButtonPanelTestType SelectedTestType { get; set; }
        = ButtonPanelTestType.Complete;

    public string ResultsText { get; set; } = "test results";
    public string? SaveFilePath { get; set; } = null;
    public bool ConfirmResult { get; set; } = true;

    // --- Cattura chiamate ---
    public List<string> ProgressMessages { get; } = [];
    public List<string> ErrorMessages { get; } = [];
    public List<(string Msg, string Title)> Messages { get; } = [];
    public List<List<ButtonPanelTestResult>> DisplayedResults { get; } = [];
    public int ResetAllIndicatorsCalls { get; private set; }
    public List<int> WaitingButtons { get; } = [];
    public List<(int Index, bool Success)> ButtonResults { get; } = [];

    // --- Implementazione interfaccia ---
    public ButtonPanelType GetSelectedPanelType() => SelectedPanelType;
    public ButtonPanelTestType GetSelectedTestType() => SelectedTestType;
    public string GetResultsText() => ResultsText;
    public string? ShowSaveFileDialog() => SaveFilePath;

    public void ShowMessage(
        string message, string title,
        MessageBoxButtons buttons, MessageBoxIcon icon)
        => Messages.Add((message, title));

    public void SetButtonWaiting(int buttonIndex)
        => WaitingButtons.Add(buttonIndex);

    public void SetButtonResult(int buttonIndex, bool success)
        => ButtonResults.Add((buttonIndex, success));

    public void ResetAllIndicators() => ResetAllIndicatorsCalls++;

    public Task ShowPromptAsync(string message) => Task.CompletedTask;

    public Task<bool> ShowConfirmAsync(
        string message, ButtonPanelTestType testType)
        => Task.FromResult(ConfirmResult);

    public void DisplayResults(List<ButtonPanelTestResult> results)
        => DisplayedResults.Add(new List<ButtonPanelTestResult>(results));

    public void ShowProgress(string message)
        => ProgressMessages.Add(message);

    public void UpdateLastPromptColor(string lastMessage, Color color)
    { }

    public void ShowError(string message)
        => ErrorMessages.Add(message);

    // --- Metodi per simulare eventi utente ---
    public void RaiseStartTest()
        => OnStartTestClicked?.Invoke(this, EventArgs.Empty);

    public void RaiseStopTest()
        => OnStopTestClicked?.Invoke(this, EventArgs.Empty);

    public void RaiseDownloadResult()
        => OnDownloadTestResultClicked?.Invoke(this, EventArgs.Empty);
}
