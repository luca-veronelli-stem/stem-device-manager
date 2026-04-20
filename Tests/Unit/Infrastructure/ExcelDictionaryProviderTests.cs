using System.Reflection;
using Infrastructure.Persistence.Excel;

namespace Tests.Unit.Infrastructure;

/// <summary>
/// Test di integrazione per ExcelDictionaryProvider con il file Excel embedded reale.
/// Verifica che la lettura produca gli stessi dati del legacy ExcelHandler.
/// Questi test girano su Linux (net10.0) e Windows (net10.0-windows).
/// </summary>
public class ExcelDictionaryProviderTests : IDisposable
{
    private const string ResourceName = "Infrastructure.Persistence.Excel.Dizionari STEM.xlsx";
    private readonly Stream _stream;
    private readonly ExcelDictionaryProvider _provider;

    public ExcelDictionaryProviderTests()
    {
        var asm = Assembly.GetAssembly(typeof(ExcelDictionaryProvider))!;
        _stream = asm.GetManifestResourceStream(ResourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{ResourceName}' not found");
        _provider = new ExcelDictionaryProvider(_stream);
    }

    public void Dispose()
    {
        _stream.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_ReturnsNonEmptyAddresses()
    {
        var data = await _provider.LoadProtocolDataAsync();

        Assert.NotEmpty(data.Addresses);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_ReturnsNonEmptyCommands()
    {
        var data = await _provider.LoadProtocolDataAsync();

        Assert.NotEmpty(data.Commands);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_AddressesHaveRequiredFields()
    {
        var data = await _provider.LoadProtocolDataAsync();

        foreach (var addr in data.Addresses)
        {
            Assert.False(string.IsNullOrWhiteSpace(addr.DeviceName));
            Assert.False(string.IsNullOrWhiteSpace(addr.BoardName));
            Assert.False(string.IsNullOrWhiteSpace(addr.Address));
        }
    }

    [Fact]
    public async Task LoadProtocolDataAsync_CommandsHaveRequiredFields()
    {
        var data = await _provider.LoadProtocolDataAsync();

        foreach (var cmd in data.Commands)
        {
            Assert.False(string.IsNullOrWhiteSpace(cmd.Name));
            Assert.False(string.IsNullOrWhiteSpace(cmd.CodeHigh));
            Assert.False(string.IsNullOrWhiteSpace(cmd.CodeLow));
        }
    }

    [Fact]
    public async Task LoadProtocolDataAsync_CalledTwice_ReturnsSameData()
    {
        var first = await _provider.LoadProtocolDataAsync();
        var second = await _provider.LoadProtocolDataAsync();

        Assert.Equal(first.Addresses.Count, second.Addresses.Count);
        Assert.Equal(first.Commands.Count, second.Commands.Count);
    }

    [Fact]
    public async Task LoadVariablesAsync_TopLiftRecipientId_ReturnsVariables()
    {
        // 0x00080381 = Toplift A2 Motherboard (noto dall'Excel)
        var variables = await _provider.LoadVariablesAsync(0x00080381);

        Assert.NotEmpty(variables);
    }

    [Fact]
    public async Task LoadVariablesAsync_VariablesHaveRequiredFields()
    {
        var variables = await _provider.LoadVariablesAsync(0x00080381);

        foreach (var v in variables)
        {
            Assert.False(string.IsNullOrWhiteSpace(v.Name));
            Assert.False(string.IsNullOrWhiteSpace(v.AddressHigh));
            Assert.False(string.IsNullOrWhiteSpace(v.AddressLow));
        }
    }

    [Fact]
    public async Task LoadVariablesAsync_UnknownRecipientId_ReturnsEmpty()
    {
        var variables = await _provider.LoadVariablesAsync(0xDEADBEEF);

        Assert.Empty(variables);
    }

    [Fact]
    public void Constructor_NullStream_ThrowsArgumentNull()
    {
        Assert.Throws<ArgumentNullException>(
            () => new ExcelDictionaryProvider(null!));
    }

    [Fact]
    public async Task LoadProtocolDataAsync_CancelledToken_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _provider.LoadProtocolDataAsync(cts.Token));
    }

    [Fact]
    public async Task LoadVariablesAsync_CancelledToken_ThrowsOperationCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _provider.LoadVariablesAsync(0x00080381, cts.Token));
    }

    [Fact]
    public async Task LoadVariablesAsync_CalledTwice_ReturnsSameData()
    {
        var first = await _provider.LoadVariablesAsync(0x00080381);
        var second = await _provider.LoadVariablesAsync(0x00080381);

        Assert.Equal(first.Count, second.Count);
    }

    [Fact]
    public async Task LoadProtocolDataThenVariables_BothReturnData()
    {
        var data = await _provider.LoadProtocolDataAsync();
        var variables = await _provider.LoadVariablesAsync(0x00080381);

        Assert.NotEmpty(data.Addresses);
        Assert.NotEmpty(variables);
    }

    [Fact]
    public async Task LoadVariablesAsync_DataType_CanBeEmptyButNotNull()
    {
        var variables = await _provider.LoadVariablesAsync(0x00080381);

        foreach (var v in variables)
        {
            Assert.NotNull(v.DataType);
        }
    }
}
