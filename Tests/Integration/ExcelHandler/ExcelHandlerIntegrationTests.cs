using System.Reflection;
using static global::ExcelHandler;

namespace Tests.Integration.ExcelHandler;

/// <summary>
/// Test di integrazione per ExcelHandler con il file Excel embedded reale.
/// Verifica che la risorsa sia leggibile e che i dati estratti siano coerenti.
/// </summary>
public class ExcelHandlerIntegrationTests : IDisposable
{
    private const string ResourceName = "App.Resources.Dizionari STEM.xlsx";
    private readonly global::ExcelHandler _handler;
    private readonly Stream _stream;

    public ExcelHandlerIntegrationTests()
    {
        var asm = Assembly.GetAssembly(typeof(global::ExcelHandler))!;
        _stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new FileNotFoundException(
                $"Risorsa embedded non trovata: {ResourceName}");
        _handler = new global::ExcelHandler(_stream);
    }

    public void Dispose()
    {
        _stream.Dispose();
    }

    // --- Caricamento risorsa ---

    [Fact]
    public void EmbeddedResource_CanBeLoaded()
    {
        var asm = Assembly.GetAssembly(typeof(global::ExcelHandler))!;
        using var stream = asm.GetManifestResourceStream(ResourceName);

        Assert.NotNull(stream);
        Assert.True(stream.Length > 0);
    }

    // --- EstraiDatiProtocollo ---

    [Fact]
    public void EstraiDatiProtocollo_FromEmbeddedStream_ReturnsNonEmpty()
    {
        var indirizzi = new List<RowData>();
        var comandi = new List<CommandData>();
        var dizionario = new List<VariableData>();

        _handler.EstraiDatiProtocollo(indirizzi, comandi, dizionario);

        Assert.NotEmpty(indirizzi);
        Assert.NotEmpty(comandi);
    }

    [Fact]
    public void EstraiDatiProtocollo_Addresses_HaveRequiredFields()
    {
        var indirizzi = new List<RowData>();
        var comandi = new List<CommandData>();
        var dizionario = new List<VariableData>();

        _handler.EstraiDatiProtocollo(indirizzi, comandi, dizionario);

        Assert.All(indirizzi, row =>
        {
            Assert.False(string.IsNullOrWhiteSpace(row.Macchina));
            Assert.False(string.IsNullOrWhiteSpace(row.Scheda));
            Assert.False(string.IsNullOrWhiteSpace(row.Indirizzo));
        });
    }

    [Fact]
    public void EstraiDatiProtocollo_Commands_HaveRequiredFields()
    {
        var indirizzi = new List<RowData>();
        var comandi = new List<CommandData>();
        var dizionario = new List<VariableData>();

        _handler.EstraiDatiProtocollo(indirizzi, comandi, dizionario);

        Assert.All(comandi, cmd =>
        {
            Assert.False(string.IsNullOrWhiteSpace(cmd.Name));
            Assert.False(string.IsNullOrWhiteSpace(cmd.CmdH));
            Assert.False(string.IsNullOrWhiteSpace(cmd.CmdL));
        });
    }

    [Fact]
    public void EstraiDatiProtocollo_CommandNames_DoNotContainWhitespaceOnly()
    {
        var indirizzi = new List<RowData>();
        var comandi = new List<CommandData>();
        var dizionario = new List<VariableData>();

        _handler.EstraiDatiProtocollo(indirizzi, comandi, dizionario);

        Assert.All(comandi, cmd =>
            Assert.NotEqual(cmd.Name.Trim(), string.Empty));
    }

    // --- EstraiDizionario ---

    [Fact]
    public void EstraiDizionario_TopLiftRecipientId_ReturnsVariables()
    {
        // RecipientId TopLift A2 scheda madre (usato in produzione)
        uint recipientId = 0x00080381;
        var variabili = new List<VariableData>();

        _handler.EstraiDizionario(recipientId, variabili);

        Assert.NotEmpty(variabili);
    }

    [Fact]
    public void EstraiDizionario_UnknownRecipientId_ReturnsEmpty()
    {
        uint recipientId = 0xDEADBEEF;
        var variabili = new List<VariableData>();

        _handler.EstraiDizionario(recipientId, variabili);

        Assert.Empty(variabili);
    }

    // --- Lettura ripetibile ---

    [Fact]
    public void EstraiDatiProtocollo_CalledTwice_ReturnsSameData()
    {
        var indirizzi1 = new List<RowData>();
        var comandi1 = new List<CommandData>();
        var diz1 = new List<VariableData>();
        _handler.EstraiDatiProtocollo(indirizzi1, comandi1, diz1);

        var indirizzi2 = new List<RowData>();
        var comandi2 = new List<CommandData>();
        var diz2 = new List<VariableData>();
        _handler.EstraiDatiProtocollo(indirizzi2, comandi2, diz2);

        Assert.Equal(indirizzi1.Count, indirizzi2.Count);
        Assert.Equal(comandi1.Count, comandi2.Count);
    }
}
