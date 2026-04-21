using Core.Interfaces;
using Core.Models;
using Services.Cache;

namespace Tests.Unit.Services.Cache;

/// <summary>
/// Test di <see cref="DictionaryCache"/>: state machine (load → select),
/// propagazione al <see cref="IPacketDecoder"/>, emissione
/// <see cref="DictionaryCache.DictionaryUpdated"/>, risoluzione
/// device+board → recipientId.
///
/// Test cross-platform (girano su net10.0 in CI). Usano fake manuali per
/// <see cref="IDictionaryProvider"/> e <see cref="IPacketDecoder"/> per
/// evitare qualsiasi I/O reale.
/// </summary>
public class DictionaryCacheTests
{
    private static readonly Command CmdRead = new("ReadVariable", "00", "01");
    private static readonly Command CmdWrite = new("WriteVariable", "00", "02");
    private static readonly ProtocolAddress AddrEden =
        new("EDEN", "Madre", "0x00080381");
    private static readonly ProtocolAddress AddrSpark =
        new("SPARK", "HMI", "0x000803AA");
    private static readonly Variable VarX = new("X", "01", "00", "uint16_t");

    // --- Ctor ---

    [Fact]
    public void Ctor_NullProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DictionaryCache(null!, new FakePacketDecoder()));
    }

    [Fact]
    public void Ctor_NullDecoder_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new DictionaryCache(new FakeDictionaryProvider(), null!));
    }

    // --- Stato iniziale ---

    [Fact]
    public void InitialState_IsEmpty()
    {
        var cache = new DictionaryCache(new FakeDictionaryProvider(), new FakePacketDecoder());

        Assert.Empty(cache.Commands);
        Assert.Empty(cache.Addresses);
        Assert.Empty(cache.Variables);
        Assert.Equal(0u, cache.CurrentRecipientId);
    }

    // --- LoadAsync ---

    [Fact]
    public async Task LoadAsync_PopulatesCommandsAndAddresses_VariablesRemainEmpty()
    {
        var provider = new FakeDictionaryProvider
        {
            ProtocolData = new DictionaryData([AddrEden, AddrSpark], [CmdRead, CmdWrite])
        };
        var decoder = new FakePacketDecoder();
        var cache = new DictionaryCache(provider, decoder);

        await cache.LoadAsync();

        Assert.Equal(2, cache.Commands.Count);
        Assert.Equal(2, cache.Addresses.Count);
        Assert.Empty(cache.Variables);
        Assert.Equal(0u, cache.CurrentRecipientId);
    }

    [Fact]
    public async Task LoadAsync_UpdatesPacketDecoderSnapshot()
    {
        var provider = new FakeDictionaryProvider
        {
            ProtocolData = new DictionaryData([AddrEden], [CmdRead])
        };
        var decoder = new FakePacketDecoder();
        var cache = new DictionaryCache(provider, decoder);

        await cache.LoadAsync();

        Assert.Single(decoder.UpdateCalls);
        var (cmds, vars, addrs) = decoder.UpdateCalls[0];
        Assert.Single(cmds);
        Assert.Empty(vars);
        Assert.Single(addrs);
    }

    [Fact]
    public async Task LoadAsync_EmitsDictionaryUpdated()
    {
        var cache = new DictionaryCache(
            new FakeDictionaryProvider { ProtocolData = new DictionaryData([], []) },
            new FakePacketDecoder());
        int count = 0;
        cache.DictionaryUpdated += (_, _) => count++;

        await cache.LoadAsync();

        Assert.Equal(1, count);
    }

    // --- SelectByRecipientAsync ---

    [Fact]
    public async Task SelectByRecipientAsync_LoadsVariablesAndUpdatesState()
    {
        var provider = new FakeDictionaryProvider
        {
            ProtocolData = new DictionaryData([AddrEden], [CmdRead]),
            VariablesByRecipient =
            {
                [0x00080381u] = [VarX]
            }
        };
        var cache = new DictionaryCache(provider, new FakePacketDecoder());
        await cache.LoadAsync();

        await cache.SelectByRecipientAsync(0x00080381u);

        Assert.Single(cache.Variables);
        Assert.Equal(0x00080381u, cache.CurrentRecipientId);
    }

    [Fact]
    public async Task SelectByRecipientAsync_UpdatesPacketDecoderWithFullSnapshot()
    {
        var provider = new FakeDictionaryProvider
        {
            ProtocolData = new DictionaryData([AddrEden], [CmdRead]),
            VariablesByRecipient = { [0x00080381u] = [VarX] }
        };
        var decoder = new FakePacketDecoder();
        var cache = new DictionaryCache(provider, decoder);
        await cache.LoadAsync();
        decoder.UpdateCalls.Clear();

        await cache.SelectByRecipientAsync(0x00080381u);

        Assert.Single(decoder.UpdateCalls);
        var (cmds, vars, addrs) = decoder.UpdateCalls[0];
        Assert.Single(cmds);
        Assert.Single(vars);
        Assert.Single(addrs);
    }

    [Fact]
    public async Task SelectByRecipientAsync_EmitsDictionaryUpdated()
    {
        var provider = new FakeDictionaryProvider
        {
            ProtocolData = new DictionaryData([], []),
            VariablesByRecipient = { [42u] = [] }
        };
        var cache = new DictionaryCache(provider, new FakePacketDecoder());
        await cache.LoadAsync();
        int count = 0;
        cache.DictionaryUpdated += (_, _) => count++;

        await cache.SelectByRecipientAsync(42u);

        Assert.Equal(1, count);
    }

    // --- SetCurrentRecipientId (mutazione pura, senza HTTP né event) ---

    [Fact]
    public void SetCurrentRecipientId_UpdatesPropertyOnly_NoProviderCallNoEvent()
    {
        var provider = new FakeDictionaryProvider();
        var decoder = new FakePacketDecoder();
        var cache = new DictionaryCache(provider, decoder);
        int eventCount = 0;
        cache.DictionaryUpdated += (_, _) => eventCount++;

        cache.SetCurrentRecipientId(0xDEADBEEFu);

        Assert.Equal(0xDEADBEEFu, cache.CurrentRecipientId);
        Assert.Equal(0, provider.LoadVariablesCallCount);
        Assert.Equal(0, eventCount);
        Assert.Empty(decoder.UpdateCalls);
    }

    [Fact]
    public async Task SetCurrentRecipientId_DoesNotAlterPreviouslyLoadedVariables()
    {
        var provider = new FakeDictionaryProvider
        {
            ProtocolData = new DictionaryData([], []),
            VariablesByRecipient = { [42u] = [VarX] }
        };
        var cache = new DictionaryCache(provider, new FakePacketDecoder());
        await cache.SelectByRecipientAsync(42u);
        Assert.Single(cache.Variables);

        cache.SetCurrentRecipientId(99u);

        Assert.Equal(99u, cache.CurrentRecipientId);
        Assert.Single(cache.Variables); // variabili invariate
    }

    // --- SelectByDeviceBoardAsync ---

    [Fact]
    public async Task SelectByDeviceBoardAsync_ResolvesRecipientAndLoads()
    {
        var provider = new FakeDictionaryProvider
        {
            ProtocolData = new DictionaryData([AddrEden, AddrSpark], []),
            VariablesByRecipient = { [0x00080381u] = [VarX] }
        };
        var cache = new DictionaryCache(provider, new FakePacketDecoder());
        await cache.LoadAsync();

        await cache.SelectByDeviceBoardAsync("EDEN", "Madre");

        Assert.Equal(0x00080381u, cache.CurrentRecipientId);
        Assert.Single(cache.Variables);
    }

    [Fact]
    public async Task SelectByDeviceBoardAsync_NonExistentPair_Throws()
    {
        var cache = new DictionaryCache(
            new FakeDictionaryProvider
            {
                ProtocolData = new DictionaryData([AddrEden], [])
            },
            new FakePacketDecoder());
        await cache.LoadAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.SelectByDeviceBoardAsync("INESISTENTE", "X"));
    }

    [Fact]
    public async Task SelectByDeviceBoardAsync_NullDevice_Throws()
    {
        var cache = new DictionaryCache(
            new FakeDictionaryProvider(), new FakePacketDecoder());

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => cache.SelectByDeviceBoardAsync(null!, "Madre"));
    }

    [Fact]
    public async Task SelectByDeviceBoardAsync_AddressWithoutPrefix_ParsesHex()
    {
        var addr = new ProtocolAddress("DEV", "BRD", "0080381"); // no "0x"
        var provider = new FakeDictionaryProvider
        {
            ProtocolData = new DictionaryData([addr], []),
            VariablesByRecipient = { [0x0080381u] = [] }
        };
        var cache = new DictionaryCache(provider, new FakePacketDecoder());
        await cache.LoadAsync();

        await cache.SelectByDeviceBoardAsync("DEV", "BRD");

        Assert.Equal(0x0080381u, cache.CurrentRecipientId);
    }

    // --- Cancellation ---

    [Fact]
    public async Task LoadAsync_CancellationToken_ForwardedToProvider()
    {
        var provider = new FakeDictionaryProvider();
        var cache = new DictionaryCache(provider, new FakePacketDecoder());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => cache.LoadAsync(cts.Token));
    }

    // --- Fakes ---

    private sealed class FakeDictionaryProvider : IDictionaryProvider
    {
        public DictionaryData ProtocolData { get; set; } = new DictionaryData([], []);
        public Dictionary<uint, IReadOnlyList<Variable>> VariablesByRecipient { get; } = [];
        public int LoadVariablesCallCount { get; private set; }

        public Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(ProtocolData);
        }

        public Task<IReadOnlyList<Variable>> LoadVariablesAsync(
            uint recipientId, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            LoadVariablesCallCount++;
            if (!VariablesByRecipient.TryGetValue(recipientId, out var vars))
                vars = [];
            return Task.FromResult(vars);
        }
    }

    private sealed class FakePacketDecoder : IPacketDecoder
    {
        public List<(IReadOnlyList<Command>, IReadOnlyList<Variable>, IReadOnlyList<ProtocolAddress>)>
            UpdateCalls { get; } = [];

        public AppLayerDecodedEvent? Decode(RawPacket packet) => null;

        public void UpdateDictionary(
            IReadOnlyList<Command> commands,
            IReadOnlyList<Variable> variables,
            IReadOnlyList<ProtocolAddress> addresses)
        {
            UpdateCalls.Add((commands, variables, addresses));
        }
    }
}
