using Core.Models;

namespace Tests.Unit.ExcelHandler;

/// <summary>
/// Test per ExcelHandler: guard clauses dei costruttori.
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
        var indirizzi = new List<ProtocolAddress>();
        var comandi = new List<Command>();
        var dizionario = new List<Variable>();

        Assert.Throws<InvalidOperationException>(
            () => handler.EstraiDatiProtocollo(
                indirizzi, comandi, dizionario));
    }

    [Fact]
    public void EstraiDizionario_DefaultConstructor_ThrowsInvalidOperation()
    {
        var handler = new global::ExcelHandler();
        var variabili = new List<Variable>();

        Assert.Throws<InvalidOperationException>(
            () => handler.EstraiDizionario(0x01, variabili));
    }
}
