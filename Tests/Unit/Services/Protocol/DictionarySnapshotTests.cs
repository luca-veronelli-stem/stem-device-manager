using System.Collections.Immutable;
using Core.Models;
using Services.Protocol;

namespace Tests.Unit.Services.Protocol;

public class DictionarySnapshotTests
{
    [Fact]
    public void Empty_HasZeroEntries()
    {
        var snap = DictionarySnapshot.Empty;

        Assert.Empty(snap.Commands);
        Assert.Empty(snap.Variables);
        Assert.Empty(snap.Addresses);
    }

    [Fact]
    public void FindCommand_ReturnsMatch_WhenCodesCombaciano()
    {
        var cmd = new Command("ReadVariable", "00", "01");
        var snap = BuildSnapshot(commands: [cmd]);

        var found = snap.FindCommand(0x00, 0x01);

        Assert.Same(cmd, found);
    }

    [Fact]
    public void FindCommand_ReturnsNull_QuandoNessunComandoCombacia()
    {
        var snap = BuildSnapshot(commands: [new Command("X", "00", "02")]);

        Assert.Null(snap.FindCommand(0x00, 0x01));
    }

    [Fact]
    public void FindCommand_MatchHexCaseInsensitive()
    {
        var cmd = new Command("Boot", "0A", "FF");
        var snap = BuildSnapshot(commands: [cmd]);

        Assert.Same(cmd, snap.FindCommand(0x0A, 0xFF));
    }

    [Fact]
    public void FindCommand_IgnoraCommandiConCodiciInvalidi()
    {
        var valid = new Command("Valid", "00", "01");
        var invalid = new Command("Invalid", "ZZ", "ZZ");
        var snap = BuildSnapshot(commands: [invalid, valid]);

        Assert.Same(valid, snap.FindCommand(0x00, 0x01));
    }

    [Fact]
    public void FindVariable_ReturnsMatch_QuandoAddressCombacia()
    {
        var variable = new Variable("Speed", "12", "34", "uint16_t");
        var snap = BuildSnapshot(variables: [variable]);

        Assert.Same(variable, snap.FindVariable(0x12, 0x34));
    }

    [Fact]
    public void FindVariable_ReturnsNull_QuandoAddressNonCombacia()
    {
        var snap = BuildSnapshot(variables: [new Variable("X", "12", "34", "uint8_t")]);

        Assert.Null(snap.FindVariable(0x99, 0x99));
    }

    [Fact]
    public void FindSender_ReturnsMatch_QuandoAddressCombaciaUInt32()
    {
        var addr = new ProtocolAddress("EDEN", "Madre", "00080381");
        var snap = BuildSnapshot(addresses: [addr]);

        Assert.Same(addr, snap.FindSender(0x00080381));
    }

    [Fact]
    public void FindSender_ReturnsNull_QuandoNessunIndirizzoCombacia()
    {
        var snap = BuildSnapshot(addresses: [new ProtocolAddress("X", "Y", "DEADBEEF")]);

        Assert.Null(snap.FindSender(0x12345678));
    }

    [Fact]
    public void FindSender_IgnoraIndirizziVuoti()
    {
        var valid = new ProtocolAddress("A", "B", "00080381");
        var empty = new ProtocolAddress("X", "Y", "");
        var snap = BuildSnapshot(addresses: [empty, valid]);

        Assert.Same(valid, snap.FindSender(0x00080381));
    }

    [Fact]
    public void FindCommand_ReturnsFirstMatch_QuandoDuplicati()
    {
        var first = new Command("First", "00", "01");
        var second = new Command("Second", "00", "01");
        var snap = BuildSnapshot(commands: [first, second]);

        Assert.Same(first, snap.FindCommand(0x00, 0x01));
    }

    [Theory]
    [InlineData("a0", "ff")]
    [InlineData("A0", "FF")]
    [InlineData("aB", "cD")]
    public void FindCommand_MatchesHexCaseInsensitive(string high, string low)
    {
        // NumberStyles.HexNumber parsing è case-insensitive per natura.
        var cmd = new Command("Cmd", high, low);
        var snap = BuildSnapshot(commands: [cmd]);

        Assert.Same(
            cmd,
            snap.FindCommand(
                Convert.ToByte(high, 16),
                Convert.ToByte(low, 16)));
    }

    [Fact]
    public void FindVariable_IgnoresEntryWithInvalidHex()
    {
        var invalid = new Variable("Invalid", "ZZ", "ZZ", "uint8_t");
        var valid = new Variable("Valid", "12", "34", "uint16_t");
        var snap = BuildSnapshot(variables: [invalid, valid]);

        Assert.Same(valid, snap.FindVariable(0x12, 0x34));
    }

    [Fact]
    public void FindVariable_IgnoresEntryWithEmptyAddressHigh()
    {
        var empty = new Variable("Empty", "", "34", "uint16_t");
        var snap = BuildSnapshot(variables: [empty]);

        Assert.Null(snap.FindVariable(0x00, 0x34));
    }

    [Fact]
    public void FindVariable_IgnoresEntryWithEmptyAddressLow()
    {
        var empty = new Variable("Empty", "12", "", "uint16_t");
        var snap = BuildSnapshot(variables: [empty]);

        Assert.Null(snap.FindVariable(0x12, 0x00));
    }

    [Fact]
    public void FindSender_IgnoresEntryWithInvalidHex()
    {
        var invalid = new ProtocolAddress("X", "Y", "notahex");
        var valid = new ProtocolAddress("A", "B", "00080381");
        var snap = BuildSnapshot(addresses: [invalid, valid]);

        Assert.Same(valid, snap.FindSender(0x00080381));
    }

    [Theory]
    [InlineData("deadbeef")]
    [InlineData("DEADBEEF")]
    [InlineData("DeAdBeEf")]
    public void FindSender_MatchesHexCaseInsensitive(string addressHex)
    {
        var addr = new ProtocolAddress("Dev", "Board", addressHex);
        var snap = BuildSnapshot(addresses: [addr]);

        Assert.Same(addr, snap.FindSender(0xDEADBEEF));
    }

    [Theory]
    [InlineData("0x00080381")]
    [InlineData("0X00080381")]
    [InlineData("0xDEADBEEF")]
    public void FindSender_MatchesAddressWithHexPrefix(string addressHex)
    {
        // ProtocolAddress produced by DictionaryApiProvider/Excel usa il formato "0x...".
        // uint.TryParse(NumberStyles.HexNumber) non accetta il prefisso: lo snapshot
        // deve strippare "0x"/"0X" prima del parse.
        uint expected = Convert.ToUInt32(addressHex.AsSpan(2).ToString(), 16);
        var addr = new ProtocolAddress("Dev", "Board", addressHex);
        var snap = BuildSnapshot(addresses: [addr]);

        Assert.Same(addr, snap.FindSender(expected));
    }

    private static DictionarySnapshot BuildSnapshot(
        IEnumerable<Command>? commands = null,
        IEnumerable<Variable>? variables = null,
        IEnumerable<ProtocolAddress>? addresses = null)
    {
        return new DictionarySnapshot(
            (commands ?? []).ToImmutableArray(),
            (variables ?? []).ToImmutableArray(),
            (addresses ?? []).ToImmutableArray());
    }
}
