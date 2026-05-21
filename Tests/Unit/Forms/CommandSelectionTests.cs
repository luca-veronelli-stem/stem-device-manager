using Core.Models;
using StemPC;

namespace Tests.Unit.Forms;

public class CommandSelectionTests
{
    private static readonly IReadOnlyList<Command> SampleCommands = new[]
    {
        new Command("Read variable",  "00", "01"),
        new Command("Write variable", "00", "02"),
        new Command("Stop telemetry", "00", "17"),
        new Command("Reply read",     "80", "01"),
    };

    [Fact]
    public void ResolveBytes_ValidIndex_ReturnsCodeOfSelectedCommand()
    {
        var bytes = CommandSelection.ResolveBytes(SampleCommands, 2);

        Assert.NotNull(bytes);
        Assert.Equal(0x00, bytes!.Value.High);
        Assert.Equal(0x17, bytes.Value.Low);
    }

    [Fact]
    public void ResolveBytes_FirstEntry_ReturnsCodeNotIndexZero()
    {
        var bytes = CommandSelection.ResolveBytes(SampleCommands, 0);

        Assert.NotNull(bytes);
        Assert.Equal(0x00, bytes!.Value.High);
        Assert.Equal(0x01, bytes.Value.Low);
    }

    [Fact]
    public void ResolveBytes_HighByteNonZero_ParsesHexNotDecimal()
    {
        var bytes = CommandSelection.ResolveBytes(SampleCommands, 3);

        Assert.NotNull(bytes);
        Assert.Equal(0x80, bytes!.Value.High);
        Assert.Equal(0x01, bytes.Value.Low);
    }

    [Fact]
    public void ResolveBytes_IndexDoesNotEqualCode_RegressionForIssue107()
    {
        // The bug: cmdHi/cmdLo were derived from SelectedIndex, not the command's
        // actual code. Index 2 here would have sent (0x00, 0x02); the real code
        // is (0x00, 0x17). The helper must return the latter.
        var bytes = CommandSelection.ResolveBytes(SampleCommands, 2);

        Assert.NotNull(bytes);
        Assert.NotEqual<byte>(2, bytes!.Value.Low);
        Assert.Equal(0x17, bytes.Value.Low);
    }

    [Fact]
    public void ResolveBytes_NegativeIndex_ReturnsNull()
    {
        Assert.Null(CommandSelection.ResolveBytes(SampleCommands, -1));
    }

    [Fact]
    public void ResolveBytes_IndexOutOfRange_ReturnsNull()
    {
        Assert.Null(CommandSelection.ResolveBytes(SampleCommands, SampleCommands.Count));
    }

    [Fact]
    public void ResolveBytes_EmptyList_ReturnsNull()
    {
        Assert.Null(CommandSelection.ResolveBytes(Array.Empty<Command>(), 0));
    }
}
