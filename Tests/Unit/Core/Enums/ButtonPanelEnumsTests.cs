using App.Core.Enums;

namespace Tests.Unit.Core.Enums;

/// <summary>
/// Test per le enum del dominio ButtonPanel.
/// Verifica che il contratto (numero e nomi dei valori) non cambi
/// in modo inatteso, rompendo factory e logica protocollo.
/// </summary>
public class ButtonPanelEnumsTests
{
    [Fact]
    public void ButtonPanelType_Has4Values()
    {
        Assert.Equal(4, Enum.GetValues<ButtonPanelType>().Length);
    }

    [Fact]
    public void ButtonPanelTestType_Has4Values()
    {
        var values = Enum.GetValues<ButtonPanelTestType>();

        Assert.Equal(4, values.Length);
        Assert.Contains(ButtonPanelTestType.Complete, values);
        Assert.Contains(ButtonPanelTestType.Buttons, values);
        Assert.Contains(ButtonPanelTestType.Led, values);
        Assert.Contains(ButtonPanelTestType.Buzzer, values);
    }

    [Fact]
    public void IndicatorState_Has4Values()
    {
        var values = Enum.GetValues<IndicatorState>();

        Assert.Equal(4, values.Length);
        Assert.Contains(IndicatorState.Idle, values);
        Assert.Contains(IndicatorState.Waiting, values);
        Assert.Contains(IndicatorState.Success, values);
        Assert.Contains(IndicatorState.Failed, values);
    }

    [Fact]
    public void EdenButtons_Has8Values()
    {
        Assert.Equal(8, Enum.GetValues<EdenButtons>().Length);
    }

    [Fact]
    public void R3LXPButtons_Has8Values()
    {
        Assert.Equal(8, Enum.GetValues<R3LXPButtons>().Length);
    }

    [Fact]
    public void OptimusButtons_Has4Values()
    {
        Assert.Equal(4, Enum.GetValues<OptimusButtons>().Length);
    }

    [Theory]
    [InlineData(ButtonPanelType.DIS0025205, 4)]
    [InlineData(ButtonPanelType.DIS0023789, 8)]
    [InlineData(ButtonPanelType.DIS0026166, 8)]
    [InlineData(ButtonPanelType.DIS0026182, 8)]
    public void ButtonCounts_MatchPanelFactory(
        ButtonPanelType type, int expectedCount)
    {
        var panel = App.Core.Models.ButtonPanel.GetByType(type);

        Assert.Equal(expectedCount, panel.ButtonCount);
    }
}
