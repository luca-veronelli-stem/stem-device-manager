using App.Core.Enums;
using App.Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per il modello ButtonPanel e il suo metodo factory GetByType.
/// Verifica coerenza tra ButtonCount, Buttons, ButtonMasks e HasLed
/// per ogni tipo di pulsantiera.
/// </summary>
public class ButtonPanelTests
{
    [Fact]
    public void GetByType_DIS0025205_Returns4ButtonsNoLed()
    {
        var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0025205);

        Assert.Equal(4, panel.ButtonCount);
        Assert.False(panel.HasLed);
    }

    [Theory]
    [InlineData(ButtonPanelType.DIS0023789)]
    [InlineData(ButtonPanelType.DIS0026166)]
    [InlineData(ButtonPanelType.DIS0026182)]
    public void GetByType_8ButtonPanels_Returns8ButtonsWithLed(
        ButtonPanelType type)
    {
        var panel = ButtonPanel.GetByType(type);

        Assert.Equal(8, panel.ButtonCount);
        Assert.True(panel.HasLed);
    }

    [Fact]
    public void GetByType_DIS0025205_ReturnsOptimusButtons()
    {
        var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0025205);
        var expected = Enum.GetNames(typeof(OptimusButtons));

        Assert.Equal(expected, panel.Buttons);
    }

    [Fact]
    public void GetByType_DIS0026166_ReturnsR3LXPButtons()
    {
        var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0026166);
        var expected = Enum.GetNames(typeof(R3LXPButtons));

        Assert.Equal(expected, panel.Buttons);
    }

    [Theory]
    [InlineData(ButtonPanelType.DIS0023789)]
    [InlineData(ButtonPanelType.DIS0026182)]
    public void GetByType_EdenPanels_ReturnsEdenButtons(
        ButtonPanelType type)
    {
        var panel = ButtonPanel.GetByType(type);
        var expected = Enum.GetNames(typeof(EdenButtons));

        Assert.Equal(expected, panel.Buttons);
    }

    [Theory]
    [InlineData(ButtonPanelType.DIS0023789)]
    [InlineData(ButtonPanelType.DIS0025205)]
    [InlineData(ButtonPanelType.DIS0026166)]
    [InlineData(ButtonPanelType.DIS0026182)]
    public void GetByType_AllTypes_HaveBuzzer(ButtonPanelType type)
    {
        var panel = ButtonPanel.GetByType(type);

        Assert.True(panel.HasBuzzer);
    }

    [Theory]
    [InlineData(ButtonPanelType.DIS0023789)]
    [InlineData(ButtonPanelType.DIS0025205)]
    [InlineData(ButtonPanelType.DIS0026166)]
    [InlineData(ButtonPanelType.DIS0026182)]
    public void GetByType_AllTypes_ButtonCountMatchesButtonsLength(
        ButtonPanelType type)
    {
        var panel = ButtonPanel.GetByType(type);

        Assert.Equal(panel.ButtonCount, panel.Buttons.Length);
    }

    [Theory]
    [InlineData(ButtonPanelType.DIS0023789)]
    [InlineData(ButtonPanelType.DIS0025205)]
    [InlineData(ButtonPanelType.DIS0026166)]
    [InlineData(ButtonPanelType.DIS0026182)]
    public void GetByType_AllTypes_ButtonCountMatchesMasksCount(
        ButtonPanelType type)
    {
        var panel = ButtonPanel.GetByType(type);

        Assert.Equal(panel.ButtonCount, panel.ButtonMasks.Count);
    }

    [Fact]
    public void GetByType_DIS0025205_HasCorrectMasks()
    {
        var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0025205);

        Assert.Equal(
            new List<byte> { 0x04, 0x10, 0x02, 0x20 },
            panel.ButtonMasks);
    }

    [Fact]
    public void GetByType_Default8Button_HasCorrectMasks()
    {
        var panel = ButtonPanel.GetByType(ButtonPanelType.DIS0023789);

        Assert.Equal(
            new List<byte> { 0x40, 0x04, 0x08, 0x10, 0x80, 0x02, 0x01, 0x20 },
            panel.ButtonMasks);
    }

    [Theory]
    [InlineData(ButtonPanelType.DIS0023789)]
    [InlineData(ButtonPanelType.DIS0025205)]
    [InlineData(ButtonPanelType.DIS0026166)]
    [InlineData(ButtonPanelType.DIS0026182)]
    public void GetByType_AllTypes_SetsCorrectType(
        ButtonPanelType type)
    {
        var panel = ButtonPanel.GetByType(type);

        Assert.Equal(type, panel.Type);
    }
}
