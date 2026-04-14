using App.Core.Enums;
using App.Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per il modello ButtonPanelTestResult.
/// Verifica valori di default e roundtrip delle proprietà.
/// </summary>
public class ButtonPanelTestResultTests
{
    [Fact]
    public void DefaultInterrupted_IsFalse()
    {
        var result = new ButtonPanelTestResult { Message = "test" };

        Assert.False(result.Interrupted);
    }

    [Fact]
    public void DefaultPassed_IsFalse()
    {
        var result = new ButtonPanelTestResult { Message = "test" };

        Assert.False(result.Passed);
    }

    [Fact]
    public void AllProperties_CanBeSetAndRead()
    {
        var result = new ButtonPanelTestResult
        {
            PanelType = ButtonPanelType.DIS0026166,
            TestType = ButtonPanelTestType.Led,
            Passed = true,
            Message = "LED OK",
            Interrupted = false
        };

        Assert.Equal(ButtonPanelType.DIS0026166, result.PanelType);
        Assert.Equal(ButtonPanelTestType.Led, result.TestType);
        Assert.True(result.Passed);
        Assert.Equal("LED OK", result.Message);
        Assert.False(result.Interrupted);
    }
}
