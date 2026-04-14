using Core.Enums;
using App.Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per il modello ButtonIndicator.
/// Verifica stato di default e possibilità di cambio stato.
/// </summary>
public class ButtonIndicatorTests
{
    [Fact]
    public void DefaultState_IsIdle()
    {
        var indicator = new ButtonIndicator();

        Assert.Equal(IndicatorState.Idle, indicator.State);
    }

    [Theory]
    [InlineData(IndicatorState.Idle)]
    [InlineData(IndicatorState.Waiting)]
    [InlineData(IndicatorState.Success)]
    [InlineData(IndicatorState.Failed)]
    public void State_CanBeSetToAllValues(IndicatorState state)
    {
        var indicator = new ButtonIndicator { State = state };

        Assert.Equal(state, indicator.State);
    }
}
