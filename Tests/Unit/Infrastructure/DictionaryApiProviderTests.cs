using System.Net;
using Infrastructure.Api;

namespace Tests.Unit.Infrastructure;

/// <summary>
/// Test unitari per DictionaryApiProvider con HttpClient mockato.
/// Verifica il flusso: devices → boards → dictionaries/commands → Core.Models.
/// Cross-platform (net10.0 + Windows).
/// </summary>
public class DictionaryApiProviderTests
{
    private const string BaseUrl = "https://test-api.example.com/";

    // --- JSON fixtures (struttura reale dell'API Dictionaries.Manager) ---

    private const string DevicesJson = """
        [
          {"id":1,"name":"TopLift-A2","machineCode":8,"description":"Sollevatori serie militare"},
          {"id":2,"name":"Eden-XP","machineCode":3,"description":"Supporto barella"}
        ]
        """;

    private const string BoardsDevice1Json = """
        [
          {"id":10,"name":"Azionamento","isPrimary":true,"firmwareType":1,"boardNumber":1,"protocolAddress":"0x00080381","dictionaryId":100,"dictionaryName":"Azionamento TopLift"},
          {"id":11,"name":"Pulsantiera","isPrimary":false,"firmwareType":2,"boardNumber":1,"protocolAddress":"0x00080382"}
        ]
        """;

    private const string BoardsDevice2Json = """
        [{"id":20,"name":"Azionamento","isPrimary":true,"firmwareType":1,"boardNumber":1,"protocolAddress":"0x00090001","dictionaryId":200,"dictionaryName":"Azionamento Eden"}]
        """;

    private const string CommandsJson = """
        [
          {"id":3,"name":"Leggi variabile logica","codeHigh":0,"codeLow":1,"isResponse":false,"parameters":["2|Indirizzo"]},
          {"id":5,"name":"Scrivi variabile logica","codeHigh":0,"codeLow":2,"isResponse":false,"parameters":["2|Indirizzo","N|Valori"]}
        ]
        """;

    private const string ResolvedDict100Json = """
        {
          "id":100,
          "name":"Azionamento TopLift",
          "description":"Dizionario variabili logiche",
          "variables":[
            {"name":"Firmware macchina","addressHigh":0,"addressLow":0,"dataType":"UInt16","access":"ReadOnly","description":"Versione firmware","min":0,"max":255.255,"isStandard":true},
            {"name":"Stato pulsanti","addressHigh":128,"addressLow":0,"dataType":"UInt16","access":"ReadWrite","min":0,"max":3,"isStandard":false}
          ]
        }
        """;

    // --- Helper ---

    private static DictionaryApiProvider CreateProvider(MockHttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        return new DictionaryApiProvider(client);
    }

    private static MockHttpMessageHandler CreateStandardHandler()
    {
        var handler = new MockHttpMessageHandler();
        handler.SetJsonResponse("api/devices/1/boards", BoardsDevice1Json);
        handler.SetJsonResponse("api/devices/2/boards", BoardsDevice2Json);
        handler.SetJsonResponse("api/devices", DevicesJson);
        handler.SetJsonResponse("api/commands", CommandsJson);
        handler.SetJsonResponse("api/dictionaries/100/resolved", ResolvedDict100Json);
        return handler;
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_NullHttpClient_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(() => new DictionaryApiProvider(null!));
    }

    // --- LoadProtocolDataAsync ---

    [Fact]
    public async Task LoadProtocolDataAsync_ReturnsAddressesFromAllDevices()
    {
        var handler = CreateStandardHandler();
        var provider = CreateProvider(handler);

        var data = await provider.LoadProtocolDataAsync();

        // 2 boards da device 1 + 1 board da device 2 = 3
        Assert.Equal(3, data.Addresses.Count);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_AddressesHaveCorrectValues()
    {
        var handler = CreateStandardHandler();
        var provider = CreateProvider(handler);

        var data = await provider.LoadProtocolDataAsync();

        var first = data.Addresses[0];
        Assert.Equal("TopLift-A2", first.DeviceName);
        Assert.Equal("Azionamento", first.BoardName);
        Assert.Equal("0x00080381", first.Address);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_CommandsMappedCorrectly()
    {
        var handler = CreateStandardHandler();
        var provider = CreateProvider(handler);

        var data = await provider.LoadProtocolDataAsync();

        Assert.Equal(2, data.Commands.Count);
        Assert.Equal("Leggi variabile logica", data.Commands[0].Name);
        Assert.Equal("0", data.Commands[0].CodeHigh);
        Assert.Equal("1", data.Commands[0].CodeLow);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_EmptyDevices_ReturnsEmptyData()
    {
        var handler = new MockHttpMessageHandler();
        handler.SetJsonResponse("api/devices", "[]");
        handler.SetJsonResponse("api/commands", "[]");
        var provider = CreateProvider(handler);

        var data = await provider.LoadProtocolDataAsync();

        Assert.Empty(data.Addresses);
        Assert.Empty(data.Commands);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_CancelledToken_Throws()
    {
        var handler = CreateStandardHandler();
        var provider = CreateProvider(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => provider.LoadProtocolDataAsync(cts.Token));
    }

    // --- LoadVariablesAsync ---

    [Fact]
    public async Task LoadVariablesAsync_KnownRecipientId_ReturnsVariables()
    {
        var handler = CreateStandardHandler();
        var provider = CreateProvider(handler);

        var variables = await provider.LoadVariablesAsync(0x00080381);

        Assert.Equal(2, variables.Count);
    }

    [Fact]
    public async Task LoadVariablesAsync_VariablesHaveCorrectValues()
    {
        var handler = CreateStandardHandler();
        var provider = CreateProvider(handler);

        var variables = await provider.LoadVariablesAsync(0x00080381);

        Assert.Equal("Firmware macchina", variables[0].Name);
        Assert.Equal("0", variables[0].AddressHigh);
        Assert.Equal("0", variables[0].AddressLow);
        Assert.Equal("UInt16", variables[0].DataType);
    }

    [Fact]
    public async Task LoadVariablesAsync_UnknownRecipientId_ReturnsEmpty()
    {
        var handler = CreateStandardHandler();
        var provider = CreateProvider(handler);

        var variables = await provider.LoadVariablesAsync(0xDEADBEEF);

        Assert.Empty(variables);
    }

    [Fact]
    public async Task LoadVariablesAsync_BoardWithNoDictionary_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler();
        handler.SetJsonResponse("api/devices",
            """[{"id":1,"name":"TestDevice","machineCode":1,"description":null}]""");
        handler.SetJsonResponse("api/devices/1/boards",
            """[{"id":10,"name":"NoDictBoard","isPrimary":true,"firmwareType":1,"boardNumber":1,"protocolAddress":"0x00080381"}]""");
        var provider = CreateProvider(handler);

        var variables = await provider.LoadVariablesAsync(0x00080381);

        Assert.Empty(variables);
    }

    [Fact]
    public async Task LoadVariablesAsync_CancelledToken_Throws()
    {
        var handler = CreateStandardHandler();
        var provider = CreateProvider(handler);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(
            () => provider.LoadVariablesAsync(0x00080381, cts.Token));
    }

    [Fact]
    public async Task LoadVariablesAsync_RecipientIdMatchesCaseInsensitive()
    {
        // L'API potrebbe restituire "0x00080381" in vari case
        var handler = new MockHttpMessageHandler();
        handler.SetJsonResponse("api/devices",
            """[{"id":1,"name":"Dev","machineCode":1}]""");
        handler.SetJsonResponse("api/devices/1/boards",
            """[{"id":10,"name":"Board","isPrimary":true,"firmwareType":1,"boardNumber":1,"protocolAddress":"0x00080381","dictionaryId":50}]""");
        handler.SetJsonResponse("api/dictionaries/50/resolved",
            """{"id":50,"name":"Test","variables":[{"name":"Var1","addressHigh":1,"addressLow":2,"dataType":"UINT8","isStandard":false}]}""");
        var provider = CreateProvider(handler);

        // RecipientId 0x00080381 deve matchare anche se il case differisce
        var variables = await provider.LoadVariablesAsync(0x00080381);

        Assert.Single(variables);
        Assert.Equal("Var1", variables[0].Name);
        Assert.Equal("1", variables[0].AddressHigh);
        Assert.Equal("2", variables[0].AddressLow);
    }

    // --- LoadProtocolDataAsync: board senza ProtocolAddress viene esclusa ---

    [Fact]
    public async Task LoadProtocolDataAsync_BoardWithEmptyAddress_Excluded()
    {
        var handler = new MockHttpMessageHandler();
        handler.SetJsonResponse("api/devices",
            """[{"id":1,"name":"Dev","machineCode":1}]""");
        handler.SetJsonResponse("api/devices/1/boards",
            """[{"id":10,"name":"NoAddr","isPrimary":true,"firmwareType":1,"boardNumber":1,"protocolAddress":""},{"id":11,"name":"Valid","isPrimary":false,"firmwareType":1,"boardNumber":2,"protocolAddress":"0x00000001","dictionaryId":51}]""");
        handler.SetJsonResponse("api/commands", "[]");
        var provider = CreateProvider(handler);

        var data = await provider.LoadProtocolDataAsync();

        Assert.Single(data.Addresses);
        Assert.Equal("Valid", data.Addresses[0].BoardName);
    }

    // --- Errori HTTP (importante per fallback Branch 5) ---

    [Fact]
    public async Task LoadProtocolDataAsync_ApiReturns500_ThrowsHttpRequestException()
    {
        var handler = new MockHttpMessageHandler();
        handler.SetDefaultResponse(HttpStatusCode.InternalServerError);
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.LoadProtocolDataAsync());
    }

    [Fact]
    public async Task LoadVariablesAsync_ApiReturns401_ThrowsHttpRequestException()
    {
        var handler = new MockHttpMessageHandler();
        handler.SetDefaultResponse(HttpStatusCode.Unauthorized);
        var provider = CreateProvider(handler);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => provider.LoadVariablesAsync(0x00080381));
    }

    // --- Edge case: resolved con variables null ---

    [Fact]
    public async Task LoadVariablesAsync_ResolvedWithNullVariables_ReturnsEmpty()
    {
        var handler = new MockHttpMessageHandler();
        handler.SetJsonResponse("api/devices",
            """[{"id":1,"name":"Dev","machineCode":1}]""");
        handler.SetJsonResponse("api/devices/1/boards",
            """[{"id":10,"name":"Board","isPrimary":true,"firmwareType":1,"boardNumber":1,"protocolAddress":"0x00080381","dictionaryId":50}]""");
        handler.SetJsonResponse("api/dictionaries/50/resolved",
            """{"id":50,"name":"Empty","variables":null}""");
        var provider = CreateProvider(handler);

        var variables = await provider.LoadVariablesAsync(0x00080381);

        Assert.Empty(variables);
    }

    // --- Board senza dictionaryId dalla fixture standard (Pulsantiera) ---

    [Fact]
    public async Task LoadVariablesAsync_PulsantieraRecipientId_ReturnsEmpty()
    {
        // 0x00080382 = Pulsantiera (nella fixture non ha dictionaryId)
        var handler = CreateStandardHandler();
        var provider = CreateProvider(handler);

        var variables = await provider.LoadVariablesAsync(0x00080382);

        Assert.Empty(variables);
    }

    // --- Match su secondo device ---

    [Fact]
    public async Task LoadVariablesAsync_SecondDeviceRecipientId_FindsBoard()
    {
        var handler = CreateStandardHandler();
        handler.SetJsonResponse("api/dictionaries/200/resolved",
            """{"id":200,"name":"Azionamento Eden","variables":[{"name":"EdenVar","addressHigh":0,"addressLow":1,"dataType":"Bool","isStandard":false}]}""");
        var provider = CreateProvider(handler);

        // 0x00090001 = Azionamento Eden-XP (device 2)
        var variables = await provider.LoadVariablesAsync(0x00090001);

        Assert.Single(variables);
        Assert.Equal("EdenVar", variables[0].Name);
    }
}
