using Core.Models;
using Tests.Integration.Form1.Mocks;

namespace Tests.Integration.Form1;

/// <summary>
/// Test di integrazione per il caricamento dizionario tramite IDictionaryProvider.
/// Verifica il contratto del mock e i comportamenti attesi della sorgente dati.
/// Nota: Form1 non viene istanziato direttamente — il constructor dipende da hardware
/// (PCAN, BLE, seriale) che non è disponibile in ambiente di test.
/// </summary>
public class Form1DictionaryLoadingTests
{
    // --- MockDictionaryProvider: contratto ---

    [Fact]
    public async Task LoadProtocolDataAsync_Called_RecordsCall()
    {
        var mock = new MockDictionaryProvider();

        await mock.LoadProtocolDataAsync();

        Assert.Equal(1, mock.LoadProtocolDataAsyncCalls);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_CalledTwice_RecordsBothCalls()
    {
        var mock = new MockDictionaryProvider();

        await mock.LoadProtocolDataAsync();
        await mock.LoadProtocolDataAsync();

        Assert.Equal(2, mock.LoadProtocolDataAsyncCalls);
    }

    [Fact]
    public async Task LoadVariablesAsync_WithRecipientId_RecordsId()
    {
        var mock = new MockDictionaryProvider();
        const uint recipientId = 0x00080381u;

        await mock.LoadVariablesAsync(recipientId);

        Assert.Single(mock.LoadVariablesAsyncRecipientIds);
        Assert.Equal(recipientId, mock.LoadVariablesAsyncRecipientIds[0]);
    }

    [Fact]
    public async Task LoadVariablesAsync_MultipleRecipients_RecordsAllIds()
    {
        var mock = new MockDictionaryProvider();

        await mock.LoadVariablesAsync(0x00080381u);
        await mock.LoadVariablesAsync(0x000803C1u);

        Assert.Equal(2, mock.LoadVariablesAsyncRecipientIds.Count);
        Assert.Equal(0x00080381u, mock.LoadVariablesAsyncRecipientIds[0]);
        Assert.Equal(0x000803C1u, mock.LoadVariablesAsyncRecipientIds[1]);
    }

    // --- Dati configurati restituiti correttamente ---

    [Fact]
    public async Task LoadProtocolDataAsync_ConfiguredData_ReturnsExpectedAddresses()
    {
        var mock = new MockDictionaryProvider
        {
            ProtocolDataToReturn = new DictionaryData(
                [ new ProtocolAddress("TOPLIFT A2", "Madre", "0x00080381") ],
                [])
        };

        var result = await mock.LoadProtocolDataAsync();

        Assert.Single(result.Addresses);
        Assert.Equal("TOPLIFT A2", result.Addresses[0].DeviceName);
        Assert.Equal("Madre", result.Addresses[0].BoardName);
        Assert.Equal("0x00080381", result.Addresses[0].Address);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_ConfiguredData_ReturnsExpectedCommands()
    {
        var mock = new MockDictionaryProvider
        {
            ProtocolDataToReturn = new DictionaryData(
                [],
                [ new Command("Leggi variabile logica", "00", "01") ])
        };

        var result = await mock.LoadProtocolDataAsync();

        Assert.Single(result.Commands);
        Assert.Equal("Leggi variabile logica", result.Commands[0].Name);
        Assert.Equal("00", result.Commands[0].CodeHigh);
        Assert.Equal("01", result.Commands[0].CodeLow);
    }

    [Fact]
    public async Task LoadVariablesAsync_ConfiguredVariables_ReturnsExpectedVariables()
    {
        var mock = new MockDictionaryProvider
        {
            VariablesToReturn =
            [
                new Variable("Velocità motore", "80", "01", "uint16_t"),
                new Variable("Stato batteria", "80", "02", "uint8_t")
            ]
        };

        var result = await mock.LoadVariablesAsync(0x00080381u);

        Assert.Equal(2, result.Count);
        Assert.Equal("Velocità motore", result[0].Name);
        Assert.Equal("80", result[0].AddressHigh);
        Assert.Equal("01", result[0].AddressLow);
        Assert.Equal("uint16_t", result[0].DataType);
    }

    // --- Flusso: LoadProtocolData → LoadVariables (ciclo selezione board) ---

    [Fact]
    public async Task DictionaryFlow_LoadProtocolThenVariables_BothCallsRecorded()
    {
        var mock = new MockDictionaryProvider
        {
            ProtocolDataToReturn = new DictionaryData(
                [new ProtocolAddress("EDEN", "Madre", "0x00030141")],
                [])
        };

        // Simula LoadDictionaryDataAsync: prima carica protocollo, poi variabili per board selezionata
        var protocolData = await mock.LoadProtocolDataAsync();
        uint recipientId = Convert.ToUInt32(protocolData.Addresses[0].Address[2..], 16);
        await mock.LoadVariablesAsync(recipientId);

        Assert.Equal(1, mock.LoadProtocolDataAsyncCalls);
        Assert.Single(mock.LoadVariablesAsyncRecipientIds);
        Assert.Equal(0x00030141u, mock.LoadVariablesAsyncRecipientIds[0]);
    }

    [Fact]
    public async Task DictionaryFlow_BoardChange_LoadsVariablesWithNewRecipientId()
    {
        var mock = new MockDictionaryProvider();
        uint firstBoard = 0x00080381u;
        uint secondBoard = 0x000803C1u;

        // Prima selezione board
        await mock.LoadVariablesAsync(firstBoard);
        // Cambio board
        await mock.LoadVariablesAsync(secondBoard);

        Assert.Equal(2, mock.LoadVariablesAsyncRecipientIds.Count);
        Assert.Equal(firstBoard, mock.LoadVariablesAsyncRecipientIds[0]);
        Assert.Equal(secondBoard, mock.LoadVariablesAsyncRecipientIds[1]);
    }
}
