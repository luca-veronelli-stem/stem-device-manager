using Core.Interfaces;
using Infrastructure.Protocol;
using Infrastructure.Protocol.Hardware;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Unit.Infrastructure.Protocol.Wiring;

/// <summary>
/// DI wiring tests for
/// <see cref="Infrastructure.Protocol.DependencyInjection.AddProtocolInfrastructure"/>.
///
/// <para><b>Cross-platform note:</b> these tests run only on
/// <c>net10.0-windows</c> because the <c>Infrastructure.Protocol</c> project is
/// referenced by Tests only on Windows (to avoid pulling the native PCAN
/// dependencies on Linux). For the same reason these tests assert against the
/// registered <see cref="ServiceDescriptor"/> entries and never resolve
/// <c>ICommunicationPort</c> as an enumerable — that would construct
/// <c>PCANManager</c>, which P/Invokes <c>pcanbasic.dll</c> and fails on hosts
/// without the PEAK driver (e.g. GitHub-hosted CI runners).</para>
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

    [Fact]
    public void AddProtocolInfrastructure_RegistersAllThreePortsAsICommunicationPort()
    {
        var services = new ServiceCollection();

        services.AddProtocolInfrastructure();

        // Verify wiring without resolving: PCANManager.ctor would P/Invoke
        // pcanbasic.dll, absent on GitHub-hosted runners. The three ICommunicationPort
        // descriptors must be present, singleton-scoped, and one for each concrete
        // adapter type.
        var portDescriptors = services
            .Where(d => d.ServiceType == typeof(ICommunicationPort))
            .ToList();

        Assert.Equal(3, portDescriptors.Count);
        Assert.All(portDescriptors, d => Assert.Equal(ServiceLifetime.Singleton, d.Lifetime));
        Assert.All(portDescriptors, d => Assert.NotNull(d.ImplementationFactory));
    }

    [Fact]
    public void AddProtocolInfrastructure_ICommunicationPortRegistration_DelegatesToConcreteSingleton()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IBleDriver, FakeBleDriver>();
        services.AddProtocolInfrastructure();

        using var sp = services.BuildServiceProvider();
        var blePortConcrete = sp.GetRequiredService<BlePort>();

        // Probe each ICommunicationPort factory with a typed IServiceProvider that
        // serves only BlePort. The factories registered by AddProtocolInfrastructure
        // are `sp => sp.GetRequiredService<X>()`; the BlePort one returns our
        // concrete singleton, the others throw. This proves the BlePort registration
        // delegates (rather than allocating a new instance) without resolving the
        // CAN port — which would P/Invoke pcanbasic.dll on hosts without PCAN.
        var probe = new SingleTypeServiceProvider<BlePort>(blePortConcrete);
        var resolved = services
            .Where(d => d.ServiceType == typeof(ICommunicationPort))
            .Select(d => TryInvoke(d.ImplementationFactory!, probe))
            .OfType<BlePort>()
            .Single();

        Assert.Same(blePortConcrete, resolved);
    }

    private static object? TryInvoke(Func<IServiceProvider, object> factory, IServiceProvider sp)
    {
        try { return factory(sp); }
        catch (InvalidOperationException) { return null; }
    }

    private sealed class SingleTypeServiceProvider<T>(T value) : IServiceProvider where T : class
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(T)
                ? value
                : throw new InvalidOperationException($"Not served: {serviceType}");
    }
}
