using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per il record DictionaryData.
/// Verifica costruzione, liste vuote e composizione.
/// </summary>
public class DictionaryDataTests
{
    [Fact]
    public void Constructor_SetsAddressesAndCommands()
    {
        var addresses = new List<ProtocolAddress>
        {
            new("TOPLIFT", "Madre", "0x00080381")
        };
        var commands = new List<Command>
        {
            new("Leggi variabile logica", "00", "01")
        };

        var data = new DictionaryData(addresses, commands);

        Assert.Single(data.Addresses);
        Assert.Single(data.Commands);
    }

    [Fact]
    public void Constructor_EmptyLists_IsValid()
    {
        var data = new DictionaryData([], []);

        Assert.Empty(data.Addresses);
        Assert.Empty(data.Commands);
    }

    [Fact]
    public void Addresses_PreservesInsertionOrder()
    {
        var first = new ProtocolAddress("TOPLIFT", "Madre", "0x00080381");
        var second = new ProtocolAddress("EDEN", "Keyboard", "0x00030101");

        var data = new DictionaryData([first, second], []);

        Assert.Equal(2, data.Addresses.Count);
        Assert.Equal(first, data.Addresses[0]);
        Assert.Equal(second, data.Addresses[1]);
    }
}
