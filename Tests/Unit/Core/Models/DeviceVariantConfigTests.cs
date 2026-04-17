using Core.Interfaces;
using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per DeviceVariantConfig e la factory Create.
/// Verifica la corrispondenza con la formalizzazione Lean in Specs/Phase1/DeviceVariantConfig.lean.
/// </summary>
public class DeviceVariantConfigTests
{
    [Fact]
    public void Create_Generic_ProducesEmptyConfig()
    {
        var cfg = DeviceVariantConfig.Create(DeviceVariant.Generic);

        Assert.Equal(DeviceVariant.Generic, cfg.Variant);
        Assert.Equal(0u, cfg.DefaultRecipientId);
        Assert.Equal("", cfg.DeviceName);
        Assert.Equal("", cfg.BoardName);
    }

    [Fact]
    public void Create_TopLift_ProducesFixedRecipientId()
    {
        var cfg = DeviceVariantConfig.Create(DeviceVariant.TopLift);

        Assert.Equal(DeviceVariant.TopLift, cfg.Variant);
        Assert.Equal(0x00080381u, cfg.DefaultRecipientId);
        Assert.Equal("", cfg.DeviceName);
        Assert.Equal("", cfg.BoardName);
    }

    [Fact]
    public void Create_Eden_ProducesLookupNames()
    {
        var cfg = DeviceVariantConfig.Create(DeviceVariant.Eden);

        Assert.Equal(DeviceVariant.Eden, cfg.Variant);
        Assert.Equal(0u, cfg.DefaultRecipientId);
        Assert.Equal("EDEN", cfg.DeviceName);
        Assert.Equal("Madre", cfg.BoardName);
    }

    [Fact]
    public void Create_Egicon_ProducesLookupNames()
    {
        var cfg = DeviceVariantConfig.Create(DeviceVariant.Egicon);

        Assert.Equal(DeviceVariant.Egicon, cfg.Variant);
        Assert.Equal(0u, cfg.DefaultRecipientId);
        Assert.Equal("SPARK", cfg.DeviceName);
        Assert.Equal("HMI", cfg.BoardName);
    }

    [Theory]
    [InlineData(DeviceVariant.Generic)]
    [InlineData(DeviceVariant.TopLift)]
    [InlineData(DeviceVariant.Eden)]
    [InlineData(DeviceVariant.Egicon)]
    public void Create_VariantMatches(DeviceVariant v)
    {
        var cfg = DeviceVariantConfig.Create(v);
        Assert.Equal(v, cfg.Variant);
    }

    [Fact]
    public void Create_InvalidVariant_Throws()
    {
        var invalid = (DeviceVariant)999;
        Assert.Throws<ArgumentOutOfRangeException>(() => DeviceVariantConfig.Create(invalid));
    }

    [Fact]
    public void Implements_IDeviceVariantConfig()
    {
        IDeviceVariantConfig cfg = DeviceVariantConfig.Create(DeviceVariant.TopLift);

        Assert.Equal(DeviceVariant.TopLift, cfg.Variant);
        Assert.Equal(0x00080381u, cfg.DefaultRecipientId);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = DeviceVariantConfig.Create(DeviceVariant.Eden);
        var b = DeviceVariantConfig.Create(DeviceVariant.Eden);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentVariants_AreNotEqual()
    {
        var a = DeviceVariantConfig.Create(DeviceVariant.Eden);
        var b = DeviceVariantConfig.Create(DeviceVariant.Egicon);

        Assert.NotEqual(a, b);
    }
}
