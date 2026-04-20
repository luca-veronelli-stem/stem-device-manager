using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services;

namespace Tests.Unit.Services.Wiring;

/// <summary>
/// Test del wiring DI di <see cref="Services.DependencyInjection.AddServices"/>:
/// verifica che il container risolva <see cref="IDeviceVariantConfig"/> e
/// <see cref="IPacketDecoder"/> coerenti con la <see cref="IConfiguration"/>
/// fornita. Sono test cross-platform (girano su net10.0 in CI) perché la
/// risoluzione non tocca driver HW.
/// </summary>
public class AddServicesTests
{
    [Fact]
    public void AddServices_NullServices_Throws()
    {
        var config = BuildConfiguration();

        Assert.Throws<ArgumentNullException>(
            () => global::Services.DependencyInjection.AddServices(null!, config));
    }

    [Fact]
    public void AddServices_NullConfiguration_Throws()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentNullException>(
            () => services.AddServices(null!));
    }

    [Fact]
    public void AddServices_DefaultConfig_ResolvesGenericVariantWithDefaultSenderId()
    {
        using var sp = BuildProvider(BuildConfiguration());

        var cfg = sp.GetRequiredService<IDeviceVariantConfig>();

        Assert.Equal(DeviceVariant.Generic, cfg.Variant);
        Assert.Equal(DeviceVariantConfig.DefaultSenderId, cfg.SenderId);
    }

    [Theory]
    [InlineData("TopLift", DeviceVariant.TopLift)]
    [InlineData("Eden", DeviceVariant.Eden)]
    [InlineData("Egicon", DeviceVariant.Egicon)]
    [InlineData("Generic", DeviceVariant.Generic)]
    public void AddServices_VariantFromConfig_ResolvesCorrectVariant(string raw, DeviceVariant expected)
    {
        using var sp = BuildProvider(BuildConfiguration(variant: raw));

        var cfg = sp.GetRequiredService<IDeviceVariantConfig>();

        Assert.Equal(expected, cfg.Variant);
    }

    [Fact]
    public void AddServices_SenderIdFromConfig_OverridesDefault()
    {
        using var sp = BuildProvider(BuildConfiguration(senderId: "42"));

        var cfg = sp.GetRequiredService<IDeviceVariantConfig>();

        Assert.Equal(42u, cfg.SenderId);
    }

    [Fact]
    public void AddServices_InvalidSenderId_FallsBackToDefault()
    {
        using var sp = BuildProvider(BuildConfiguration(senderId: "non-numerico"));

        var cfg = sp.GetRequiredService<IDeviceVariantConfig>();

        Assert.Equal(DeviceVariantConfig.DefaultSenderId, cfg.SenderId);
    }

    [Fact]
    public void AddServices_PacketDecoder_ResolvesAsSingleton()
    {
        using var sp = BuildProvider(BuildConfiguration());

        var first = sp.GetRequiredService<IPacketDecoder>();
        var second = sp.GetRequiredService<IPacketDecoder>();

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    [Fact]
    public void AddServices_DeviceVariantConfig_ResolvesAsSingleton()
    {
        using var sp = BuildProvider(BuildConfiguration(variant: "TopLift"));

        var first = sp.GetRequiredService<IDeviceVariantConfig>();
        var second = sp.GetRequiredService<IDeviceVariantConfig>();

        Assert.Same(first, second);
    }

    private static IConfiguration BuildConfiguration(
        string? variant = null, string? senderId = null)
    {
        var dict = new Dictionary<string, string?>();
        if (variant is not null) dict["Device:Variant"] = variant;
        if (senderId is not null) dict["Device:SenderId"] = senderId;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static ServiceProvider BuildProvider(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddServices(config);
        return services.BuildServiceProvider();
    }
}
