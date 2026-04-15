using System.Reflection;
using Infrastructure.Excel;
using static global::ExcelHandler;

namespace Tests.Integration.Infrastructure;

/// <summary>
/// Test di correttezza: confronta l'output di ExcelDictionaryProvider (nuovo, Core.Models)
/// con quello di ExcelHandler (legacy, tipi inner) sullo stesso file Excel embedded.
/// Se producono gli stessi dati, il nuovo provider è una replica fedele.
/// Windows-only: ExcelHandler dipende da WinForms.
/// </summary>
public class ExcelDictionaryProviderCrossReferenceTests : IDisposable
{
    private const string LegacyResource = "App.Resources.Dizionari STEM.xlsx";
    private const string NewResource = "Infrastructure.Excel.Dizionari STEM.xlsx";

    private readonly global::ExcelHandler _legacy;
    private readonly ExcelDictionaryProvider _provider;
    private readonly Stream _legacyStream;
    private readonly Stream _newStream;

    public ExcelDictionaryProviderCrossReferenceTests()
    {
        var appAsm = Assembly.GetAssembly(typeof(global::ExcelHandler))!;
        _legacyStream = appAsm.GetManifestResourceStream(LegacyResource)
            ?? throw new FileNotFoundException(LegacyResource);
        _legacy = new global::ExcelHandler(_legacyStream);

        var infraAsm = Assembly.GetAssembly(typeof(ExcelDictionaryProvider))!;
        _newStream = infraAsm.GetManifestResourceStream(NewResource)
            ?? throw new FileNotFoundException(NewResource);
        _provider = new ExcelDictionaryProvider(_newStream);
    }

    public void Dispose()
    {
        _legacyStream.Dispose();
        _newStream.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Addresses_Count_MatchesLegacy()
    {
        var legacyAddresses = new List<RowData>();
        var legacyCommands = new List<CommandData>();
        var legacyDiz = new List<VariableData>();
        _legacy.EstraiDatiProtocollo(legacyAddresses, legacyCommands, legacyDiz);

        var newData = await _provider.LoadProtocolDataAsync();

        Assert.Equal(legacyAddresses.Count, newData.Addresses.Count);
    }

    [Fact]
    public async Task Addresses_Values_MatchLegacyFieldByField()
    {
        var legacyAddresses = new List<RowData>();
        var legacyCommands = new List<CommandData>();
        var legacyDiz = new List<VariableData>();
        _legacy.EstraiDatiProtocollo(legacyAddresses, legacyCommands, legacyDiz);

        var newData = await _provider.LoadProtocolDataAsync();

        for (int i = 0; i < legacyAddresses.Count; i++)
        {
            var legacy = legacyAddresses[i];
            var current = newData.Addresses[i];

            Assert.Equal(legacy.Macchina, current.DeviceName);
            Assert.Equal(legacy.Scheda, current.BoardName);
            Assert.Equal(legacy.Indirizzo, current.Address);
        }
    }

    [Fact]
    public async Task Commands_Count_MatchesLegacy()
    {
        var legacyAddresses = new List<RowData>();
        var legacyCommands = new List<CommandData>();
        var legacyDiz = new List<VariableData>();
        _legacy.EstraiDatiProtocollo(legacyAddresses, legacyCommands, legacyDiz);

        var newData = await _provider.LoadProtocolDataAsync();

        Assert.Equal(legacyCommands.Count, newData.Commands.Count);
    }

    [Fact]
    public async Task Commands_Values_MatchLegacyFieldByField()
    {
        var legacyAddresses = new List<RowData>();
        var legacyCommands = new List<CommandData>();
        var legacyDiz = new List<VariableData>();
        _legacy.EstraiDatiProtocollo(legacyAddresses, legacyCommands, legacyDiz);

        var newData = await _provider.LoadProtocolDataAsync();

        for (int i = 0; i < legacyCommands.Count; i++)
        {
            var legacy = legacyCommands[i];
            var current = newData.Commands[i];

            Assert.Equal(legacy.Name, current.Name);
            Assert.Equal(legacy.CmdH, current.CodeHigh);
            Assert.Equal(legacy.CmdL, current.CodeLow);
        }
    }

    [Fact]
    public async Task Variables_TopLift_Count_MatchesLegacy()
    {
        uint recipientId = 0x00080381;

        var legacyVars = new List<VariableData>();
        _legacy.EstraiDizionario(recipientId, legacyVars);

        var newVars = await _provider.LoadVariablesAsync(recipientId);

        Assert.Equal(legacyVars.Count, newVars.Count);
    }

    [Fact]
    public async Task Variables_TopLift_Values_MatchLegacyFieldByField()
    {
        uint recipientId = 0x00080381;

        var legacyVars = new List<VariableData>();
        _legacy.EstraiDizionario(recipientId, legacyVars);

        var newVars = await _provider.LoadVariablesAsync(recipientId);

        for (int i = 0; i < legacyVars.Count; i++)
        {
            var legacy = legacyVars[i];
            var current = newVars[i];

            Assert.Equal(legacy.Name, current.Name);
            Assert.Equal(legacy.AddrH, current.AddressHigh);
            Assert.Equal(legacy.AddrL, current.AddressLow);
            Assert.Equal(legacy.DataType, current.DataType);
        }
    }
}
