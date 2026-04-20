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
