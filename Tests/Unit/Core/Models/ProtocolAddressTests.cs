using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per il record ProtocolAddress.
/// Verifica value equality e corretta costruzione.
/// </summary>
public class ProtocolAddressTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var addr = new ProtocolAddress("TOPLIFT", "Madre", "0x00080381");

        Assert.Equal("TOPLIFT", addr.DeviceName);
        Assert.Equal("Madre", addr.BoardName);
        Assert.Equal("0x00080381", addr.Address);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new ProtocolAddress("EDEN", "Keyboard", "0x00030101");
        var b = new ProtocolAddress("EDEN", "Keyboard", "0x00030101");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new ProtocolAddress("EDEN", "Keyboard", "0x00030101");
        var b = new ProtocolAddress("EDEN", "Madre", "0x00030141");

        Assert.NotEqual(a, b);
    }
}
