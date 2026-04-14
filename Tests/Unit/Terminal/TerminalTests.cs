namespace Tests.Unit.Terminal;

/// <summary>
/// Test per la classe Terminal.
/// Classe pura con StringBuilder interno — verifica append, get e write.
/// </summary>
public class TerminalTests
{
    [Fact]
    public void Constructor_CreatesEmptyLog()
    {
        var terminal = new global::Terminal();

        Assert.Equal(string.Empty, terminal.GetLog());
    }

    [Fact]
    public void WriteLine_SingleMessage_AppendsWithNewline()
    {
        var terminal = new global::Terminal();

        terminal.WriteLine("test");

        Assert.Equal("test" + Environment.NewLine, terminal.GetLog());
    }

    [Fact]
    public void WriteLine_MultipleMessages_AppendsInOrder()
    {
        var terminal = new global::Terminal();

        terminal.WriteLine("primo");
        terminal.WriteLine("secondo");
        terminal.WriteLine("terzo");

        var expected =
            "primo" + Environment.NewLine +
            "secondo" + Environment.NewLine +
            "terzo" + Environment.NewLine;
        Assert.Equal(expected, terminal.GetLog());
    }

    [Fact]
    public void GetLog_AfterMultipleWrites_ReturnsFullLog()
    {
        var terminal = new global::Terminal();
        terminal.WriteLine("A");
        terminal.WriteLine("B");

        string log = terminal.GetLog();

        Assert.Contains("A", log);
        Assert.Contains("B", log);
    }

    [Fact]
    public void WriteLog_WritesAndReturnsUpdatedLog()
    {
        var terminal = new global::Terminal();

        string result = terminal.WriteLog("messaggio");

        Assert.Contains("messaggio", result);
        Assert.Equal(terminal.GetLog(), result);
    }

    [Fact]
    public void WriteLog_EmptyMessage_AppendsEmptyLine()
    {
        var terminal = new global::Terminal();

        string result = terminal.WriteLog(string.Empty);

        // Deve contenere almeno il newline di una riga vuota
        Assert.Equal(Environment.NewLine, result);
    }
}
 