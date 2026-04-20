using Core.Interfaces;
using Core.Models;

namespace Tests.Unit.Infrastructure;

/// <summary>
/// Test unitari per FallbackDictionaryProvider.
/// Verifica il pattern: primary OK → usa primary, primary fallisce → usa fallback.
/// </summary>
public class FallbackDictionaryProviderTests
{
    private static readonly DictionaryData SampleData = new(
        [new ProtocolAddress("Dev", "Board", "0x00000001")],
        [new Command("Cmd", "0", "1")]);

    private static readonly IReadOnlyList<Variable> SampleVars =
        [new Variable("Var1", "0", "1", "UInt16")];

    // --- LoadProtocolDataAsync ---

    [Fact]
    public async Task LoadProtocolDataAsync_PrimarySucceeds_ReturnsPrimaryData()
    {
        var primary = new FakeProvider(SampleData, SampleVars);
        var fallback = new FakeProvider(
            new DictionaryData([], []), []);

        var provider = new global::Infrastructure.Persistence.FallbackDictionaryProvider(
            primary, fallback);

        var result = await provider.LoadProtocolDataAsync();

        Assert.Same(SampleData, result);
    }

    [Fact]
    public async Task LoadProtocolDataAsync_PrimaryThrowsHttp_ReturnsFallbackData()
    {
        var fallbackData = new DictionaryData([], []);
        var primary = new FailingProvider();
        var fallback = new FakeProvider(fallbackData, []);

        var provider = new global::Infrastructure.Persistence.FallbackDictionaryProvider(
            primary, fallback);

        var result = await provider.LoadProtocolDataAsync();

        Assert.Same(fallbackData, result);
    }

    // --- LoadVariablesAsync ---

    [Fact]
    public async Task LoadVariablesAsync_PrimarySucceeds_ReturnsPrimaryVars()
    {
        var primary = new FakeProvider(SampleData, SampleVars);
        var fallback = new FakeProvider(SampleData, []);

        var provider = new global::Infrastructure.Persistence.FallbackDictionaryProvider(
            primary, fallback);

        var result = await provider.LoadVariablesAsync(0x00080381);

        Assert.Same(SampleVars, result);
    }

    [Fact]
    public async Task LoadVariablesAsync_PrimaryThrowsHttp_ReturnsFallbackVars()
    {
        IReadOnlyList<Variable> fallbackVars =
            [new Variable("Fallback", "0", "0", "Bool")];
        var primary = new FailingProvider();
        var fallback = new FakeProvider(SampleData, fallbackVars);

        var provider = new global::Infrastructure.Persistence.FallbackDictionaryProvider(
            primary, fallback);

        var result = await provider.LoadVariablesAsync(0x00080381);

        Assert.Same(fallbackVars, result);
    }

    // --- Non-HttpRequestException non viene catturata ---

    [Fact]
    public async Task LoadProtocolDataAsync_PrimaryThrowsOther_Propagates()
    {
        var primary = new FailingProvider(new InvalidOperationException("boom"));
        var fallback = new FakeProvider(SampleData, SampleVars);

        var provider = new global::Infrastructure.Persistence.FallbackDictionaryProvider(
            primary, fallback);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.LoadProtocolDataAsync());
    }

    [Fact]
    public async Task LoadVariablesAsync_PrimaryThrowsOther_Propagates()
    {
        var primary = new FailingProvider(new InvalidOperationException("boom"));
        var fallback = new FakeProvider(SampleData, SampleVars);

        var provider = new global::Infrastructure.Persistence.FallbackDictionaryProvider(
            primary, fallback);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.LoadVariablesAsync(0x00080381));
    }

    // --- CancellationToken propagato al fallback ---

    [Fact]
    public async Task LoadProtocolDataAsync_PrimaryFailsAndTokenCancelled_FallbackThrowsCancelled()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var primary = new FailingProvider();
        var fallback = new CancellationAwareFakeProvider();

        var provider = new global::Infrastructure.Persistence.FallbackDictionaryProvider(
            primary, fallback);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => provider.LoadProtocolDataAsync(cts.Token));
    }

    // --- Constructor ---

    [Fact]
    public void Constructor_NullPrimary_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new global::Infrastructure.Persistence.FallbackDictionaryProvider(
                null!, new FakeProvider(SampleData, SampleVars)));
    }

    [Fact]
    public void Constructor_NullFallback_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new global::Infrastructure.Persistence.FallbackDictionaryProvider(
                new FakeProvider(SampleData, SampleVars), null!));
    }

    // --- Helper: provider fittizi ---

    private class FakeProvider(
        DictionaryData data, IReadOnlyList<Variable> vars) : IDictionaryProvider
    {
        public Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct) =>
            Task.FromResult(data);

        public Task<IReadOnlyList<Variable>> LoadVariablesAsync(
            uint recipientId, CancellationToken ct) =>
            Task.FromResult(vars);
    }

    private class FailingProvider(Exception? ex = null) : IDictionaryProvider
    {
        private readonly Exception _ex = ex ?? new HttpRequestException("API unreachable");

        public Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct) =>
            throw _ex;

        public Task<IReadOnlyList<Variable>> LoadVariablesAsync(
            uint recipientId, CancellationToken ct) =>
            throw _ex;
    }

    private class CancellationAwareFakeProvider : IDictionaryProvider
    {
        public Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new DictionaryData([], []));
        }

        public Task<IReadOnlyList<Variable>> LoadVariablesAsync(
            uint recipientId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<Variable>>([]);
        }
    }
}
