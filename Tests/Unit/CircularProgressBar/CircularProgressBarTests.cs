namespace Tests.Unit.CircularProgressBar;

/// <summary>
/// Test per CircularProgressBar: logica di clamping e validazione proprietà.
/// Non testa rendering (OnPaint).
/// </summary>
public class CircularProgressBarTests
{
    [Fact]
    public void Value_NegativeInput_ClampedToZero()
    {
        var bar = new GUI.Windows.CircularProgressBar();

        bar.Value = -5;

        Assert.Equal(0, bar.Value);
    }

    [Fact]
    public void Value_ExceedsMaximum_ClampedToMaximum()
    {
        var bar = new GUI.Windows.CircularProgressBar { Maximum = 50 };

        bar.Value = 999;

        Assert.Equal(50, bar.Value);
    }

    [Fact]
    public void Maximum_ZeroOrNegative_ThrowsArgumentOutOfRange()
    {
        var bar = new GUI.Windows.CircularProgressBar();

        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Maximum = 0);
        Assert.Throws<ArgumentOutOfRangeException>(() => bar.Maximum = -1);
    }

    [Fact]
    public void Maximum_Reduced_ClampsExistingValue()
    {
        var bar = new GUI.Windows.CircularProgressBar { Maximum = 100 };
        bar.Value = 80;

        bar.Maximum = 50;

        Assert.Equal(50, bar.Value);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(1, 1)]
    [InlineData(10, 10)]
    public void LineWidth_MinimumIsOne(int input, int expected)
    {
        var bar = new GUI.Windows.CircularProgressBar();

        bar.LineWidth = input;

        Assert.Equal(expected, bar.LineWidth);
    }
}
