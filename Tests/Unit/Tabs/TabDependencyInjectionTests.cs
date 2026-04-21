using App.STEMProtocol;
using Core.Interfaces;
using Core.Models;
using Services.Cache;

namespace Tests.Unit.Tabs;

/// <summary>
/// Smoke test dell'injection di <see cref="DictionaryCache"/> nei 4 tab refattorizzati
/// in Branch 2 (<c>refactor/phase-3-tab-decoupling</c>):
/// <see cref="Boot_Interface_Tab"/>, <see cref="Boot_Smart_Tab"/>,
/// <see cref="Telemetry_Tab"/>, <see cref="TopLiftTelemetry_Tab"/>.
///
/// <para><b>Windows-only:</b> i tab referenziano WinForms + risorse embedded nell'assembly
/// App, quindi girano solo sul target <c>net10.0-windows</c>. Tests.csproj esclude
/// <c>Unit/Tabs/**</c> dal target cross-platform.</para>
///
/// <para><b>Scope:</b> verifica che il ctor rifiuti cache null con
/// <see cref="ArgumentNullException"/>. L'istanza completa del tab (con UI real)
/// non è necessaria perché il throw avviene prima dell'inizializzazione WinForms.</para>
/// </summary>
public class TabDependencyInjectionTests
{
    [Fact]
    public void BootInterfaceTab_NullCache_Throws()
    {
        var variant = DeviceVariantConfig.Create(DeviceVariant.Generic);
        Assert.Throws<ArgumentNullException>(() => new Boot_Interface_Tab(null!, variant));
    }

    [Fact]
    public void BootInterfaceTab_NullVariant_Throws()
    {
        var cache = new DictionaryCache(new FakeDictionaryProvider(), new NoopDecoder());
        Assert.Throws<ArgumentNullException>(() => new Boot_Interface_Tab(cache, null!));
    }

    [Fact]
    public void BootSmartTab_NullCache_Throws()
    {
        var rxPacket = new PacketManager(0xFFFFFFFF);
        Assert.Throws<ArgumentNullException>(() => new Boot_Smart_Tab(rxPacket, null!));
    }

    [Fact]
    public void TelemetryTab_NullCache_Throws()
    {
        var rxPacket = new PacketManager(0xFFFFFFFF);
        var variant = DeviceVariantConfig.Create(DeviceVariant.Generic);
        Assert.Throws<ArgumentNullException>(() => new Telemetry_Tab(rxPacket, null!, variant));
    }

    [Fact]
    public void TelemetryTab_NullVariant_Throws()
    {
        var rxPacket = new PacketManager(0xFFFFFFFF);
        var cache = new DictionaryCache(new FakeDictionaryProvider(), new NoopDecoder());
        Assert.Throws<ArgumentNullException>(() => new Telemetry_Tab(rxPacket, cache, null!));
    }

    [Fact]
    public void TopLiftTelemetryTab_NullCache_Throws()
    {
        var rxPacket = new PacketManager(0xFFFFFFFF);
        Assert.Throws<ArgumentNullException>(() => new TopLiftTelemetry_Tab(rxPacket, null!));
    }

    /// <summary>
    /// Verifica che la cache, popolata via <see cref="DictionaryCache.LoadAsync"/>,
    /// emetta <see cref="DictionaryCache.DictionaryUpdated"/>. Questo è il segnale a
    /// cui i tab (Boot_Smart, Telemetry, TopLiftTelemetry) si sottoscrivono per
    /// aggiornare il proprio <c>MachineDictionary</c> locale.
    /// </summary>
    [Fact]
    public async Task DictionaryCache_LoadAsync_FiresDictionaryUpdated()
    {
        var provider = new FakeDictionaryProvider
        {
            ProtocolData = new DictionaryData(
                [new ProtocolAddress("TOPLIFT", "Madre", "0x00080381")],
                [new Command("ReadVariable", "00", "01")])
        };
        var cache = new DictionaryCache(provider, new NoopDecoder());
        int fired = 0;
        cache.DictionaryUpdated += (_, _) => fired++;

        await cache.LoadAsync();

        Assert.Equal(1, fired);
        Assert.Single(cache.Addresses);
        Assert.Single(cache.Commands);
    }

    private sealed class FakeDictionaryProvider : IDictionaryProvider
    {
        public DictionaryData ProtocolData { get; set; } = new DictionaryData([], []);

        public Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct = default)
            => Task.FromResult(ProtocolData);

        public Task<IReadOnlyList<Variable>> LoadVariablesAsync(uint recipientId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Variable>>([]);
    }

    private sealed class NoopDecoder : IPacketDecoder
    {
        public AppLayerDecodedEvent? Decode(RawPacket packet) => null;
        public void UpdateDictionary(
            IReadOnlyList<Command> commands,
            IReadOnlyList<Variable> variables,
            IReadOnlyList<ProtocolAddress> addresses) { }
    }
}
