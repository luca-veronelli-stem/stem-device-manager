using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per il record Variable.
/// Verifica value equality e corretta costruzione.
/// </summary>
public class VariableTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var v = new Variable("Temperatura", "80", "01", "uint16_t");

        Assert.Equal("Temperatura", v.Name);
        Assert.Equal("80", v.AddressHigh);
        Assert.Equal("01", v.AddressLow);
        Assert.Equal("uint16_t", v.DataType);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new Variable("Pressione", "80", "02", "float");
        var b = new Variable("Pressione", "80", "02", "float");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new Variable("Temperatura", "80", "01", "uint16_t");
        var b = new Variable("Pressione", "80", "02", "float");

        Assert.NotEqual(a, b);
    }
}
