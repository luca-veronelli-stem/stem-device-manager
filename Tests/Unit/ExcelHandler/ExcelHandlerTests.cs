using RowData = global::ExcelHandler.RowData;
using CommandData = global::ExcelHandler.CommandData;
using VariableData = global::ExcelHandler.VariableData;

namespace Tests.Unit.ExcelHandler;

/// <summary>
/// Test per ExcelHandler: guard clauses dei costruttori e DTO inner classes.
/// Non testa lettura Excel reale (riservato a Integration/).
/// </summary>
public class ExcelHandlerTests
{
    // --- Costruttori e guard clauses ---

    [Fact]
    public void Constructor_NullStream_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new global::ExcelHandler((Stream)null!));
    }

    [Fact]
    public void Constructor_ValidStream_DoesNotThrow()
    {
        using var stream = new MemoryStream([0x01, 0x02]);

        var handler = new global::ExcelHandler(stream);

        Assert.NotNull(handler);
    }

    [Fact]
    public void EstraiDatiProtocollo_DefaultConstructor_ThrowsInvalidOperation()
    {
        var handler = new global::ExcelHandler();
        var indirizzi = new List<RowData>();
        var comandi = new List<CommandData>();
        var dizionario = new List<VariableData>();

        Assert.Throws<InvalidOperationException>(
            () => handler.EstraiDatiProtocollo(
                indirizzi, comandi, dizionario));
    }

    [Fact]
    public void EstraiDizionario_DefaultConstructor_ThrowsInvalidOperation()
    {
        var handler = new global::ExcelHandler();
        var variabili = new List<VariableData>();

        Assert.Throws<InvalidOperationException>(
            () => handler.EstraiDizionario(0x01, variabili));
    }

    // --- DTO inner classes ---

    [Fact]
    public void RowData_ToTerminal_FormatsCorrectly()
    {
        var row = new RowData("Eden", "MainBoard", "0x10");

        Assert.Equal(
            "Macchina: Eden, Scheda: MainBoard, Indirizzo: 0x10",
            row.ToTerminal());
    }

    [Fact]
    public void CommandData_ToTerminal_FormatsCorrectly()
    {
        var cmd = new CommandData("MotorUp", "0x01", "0x02");

        Assert.Equal(
            "Comando: MotorUp, codeH: 0x01, codeL: 0x02",
            cmd.ToTerminal());
    }

    [Fact]
    public void VariableData_ToTerminal_FormatsCorrectly()
    {
        var v = new VariableData("Speed", "0xA0", "0x01", "uint16");

        Assert.Equal(
            "Variabile logica: Speed, addrH: 0xA0, addrL: 0x01",
            v.ToTerminal());
    }
}
