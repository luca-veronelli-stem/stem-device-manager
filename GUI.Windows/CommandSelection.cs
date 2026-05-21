using System.Globalization;
using Core.Models;

namespace StemPC;

internal static class CommandSelection
{
    public static (byte High, byte Low)? ResolveBytes(IReadOnlyList<Command> commands, int selectedIndex)
    {
        if (selectedIndex < 0 || selectedIndex >= commands.Count)
            return null;

        var cmd = commands[selectedIndex];
        var high = byte.Parse(cmd.CodeHigh, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        var low = byte.Parse(cmd.CodeLow, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return (high, low);
    }
}
