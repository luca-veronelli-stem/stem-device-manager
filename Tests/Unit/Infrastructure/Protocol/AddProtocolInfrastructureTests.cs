using Infrastructure.Protocol;
using Infrastructure.Protocol.Hardware;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Unit.Infrastructure.Protocol.Wiring;

/// <summary>
/// Test del wiring DI di
/// <see cref="Infrastructure.Protocol.DependencyInjection.AddProtocolInfrastructure"/>.
///
/// <para><b>Nota cross-platform:</b> questi test girano solo su
/// <c>net10.0-windows</c> perché il progetto <c>Infrastructure.Protocol</c> è
/// referenziato da Tests solo sotto Windows (per evitare di trascinare le
/// dipendenze native PCAN su Linux). Per la stessa ragione i test verificano
/// solo le <see cref="ServiceDescriptor"/> registrate, **non** istanziano
/// <c>PCANManager</c> (che fallirebbe a runtime in assenza del driver
/// PCAN-USB anche su Windows in CI).</para>
/// </summary>
public class AddProtocolInfrastructureTests
{
    [Fact]
    public void AddProtocolInfrastructure_NullServices_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => global::Infrastructure.Protocol.DependencyInjection
                .AddProtocolInfrastructure(null!));
    }

    [Fact]
    public void AddProtocolInfrastructure_RegistersIPcanDriverAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddProtocolInfrastructure();

        var descriptor = services.Single(d => d.ServiceType == typeof(IPcanDriver));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(PCANManager), descriptor.ImplementationType);
    }

    [Theory]
    [InlineData(typeof(CanPort))]
    [InlineData(typeof(BlePort))]
    [InlineData(typeof(SerialPort))]
    public void AddProtocolInfrastructure_RegistersConcreteAdapterAsSingleton(Type adapterType)
    {
        var services = new ServiceCollection();

        services.AddProtocolInfrastructure();

        var descriptor = services.Single(d => d.ServiceType == adapterType);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void AddProtocolInfrastructure_BlePort_ResolvesWithExternallyRegisteredDriver()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBleDriver, FakeBleDriver>();
        services.AddProtocolInfrastructure();

        using var sp = services.BuildServiceProvider();
        var port = sp.GetRequiredService<BlePort>();

        Assert.NotNull(port);
        Assert.Equal(global::Core.Models.ChannelKind.Ble, port.Kind);
    }

    [Fact]
    public void AddProtocolInfrastructure_SerialPort_ResolvesWithExternallyRegisteredDriver()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISerialDriver, FakeSerialDriver>();
        services.AddProtocolInfrastructure();

        using var sp = services.BuildServiceProvider();
        var port = sp.GetRequiredService<SerialPort>();

        Assert.NotNull(port);
        Assert.Equal(global::Core.Models.ChannelKind.Serial, port.Kind);
    }

    [Fact]
    public void AddProtocolInfrastructure_NoExternalBleDriver_ThrowsAtResolveTime()
    {
        var services = new ServiceCollection();
        services.AddProtocolInfrastructure();

        using var sp = services.BuildServiceProvider();

        // BlePort richiede IBleDriver via ctor; senza registrazione esterna
        // la risoluzione fallisce. Questo conferma il contratto documentato in
        // AddProtocolInfrastructure (l'host deve registrare IBleDriver/ISerialDriver).
        Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<BlePort>());
    }
}
