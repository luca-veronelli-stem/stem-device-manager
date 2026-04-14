using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per il record Command.
/// Verifica value equality e corretta costruzione.
/// </summary>
public class CommandTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var cmd = new Command("Leggi variabile logica", "00", "01");

        Assert.Equal("Leggi variabile logica", cmd.Name);
        Assert.Equal("00", cmd.CodeHigh);
        Assert.Equal("01", cmd.CodeLow);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new Command("Scrivi variabile", "00", "02");
        var b = new Command("Scrivi variabile", "00", "02");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new Command("Leggi variabile logica", "00", "01");
        var b = new Command("Scrivi variabile", "00", "02");

        Assert.NotEqual(a, b);
    }
}
