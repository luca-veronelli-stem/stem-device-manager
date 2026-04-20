using Core.Models;
using Services.Configuration;

namespace Tests.Unit.Services.Configuration;

/// <summary>
/// Test per <see cref="DeviceVariantConfigFactory"/>: parsing totale della
/// variante da stringa di configurazione e round-trip con il nome canonico.
/// </summary>
public class DeviceVariantConfigFactoryTests
{
    [Theory]
    [InlineData("TopLift", DeviceVariant.TopLift)]
    [InlineData("Eden", DeviceVariant.Eden)]
    [InlineData("Egicon", DeviceVariant.Egicon)]
    [InlineData("Generic", DeviceVariant.Generic)]
    public void ParseVariant_CanonicalName_ReturnsVariant(string input, DeviceVariant expected)
    {
        Assert.Equal(expected, DeviceVariantConfigFactory.ParseVariant(input));
    }

    [Theory]
    [InlineData("toplift")]
    [InlineData("TOPLIFT")]
    [InlineData("TopLIFT")]
    [InlineData("  TopLift  ")]
    public void ParseVariant_CaseInsensitiveAndTrimmed_ReturnsTopLift(string input)
    {
        Assert.Equal(DeviceVariant.TopLift, DeviceVariantConfigFactory.ParseVariant(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Sconosciuta")]
    [InlineData("TopLift2")]
    public void ParseVariant_NullOrEmptyOrUnknown_ReturnsGeneric(string? input)
    {
        Assert.Equal(DeviceVariant.Generic, DeviceVariantConfigFactory.ParseVariant(input));
    }

    [Fact]
    public void FromString_TopLift_ProducesTopLiftConfig()
    {
        var cfg = DeviceVariantConfigFactory.FromString("TopLift");

        Assert.Equal(DeviceVariant.TopLift, cfg.Variant);
        Assert.Equal(0x00080381u, cfg.DefaultRecipientId);
    }

    [Fact]
    public void FromString_Eden_ProducesEdenConfig()
    {
        var cfg = DeviceVariantConfigFactory.FromString("Eden");

        Assert.Equal(DeviceVariant.Eden, cfg.Variant);
        Assert.Equal("EDEN", cfg.DeviceName);
        Assert.Equal("Madre", cfg.BoardName);
    }

    [Fact]
    public void FromString_Null_ProducesGenericConfig()
    {
        var cfg = DeviceVariantConfigFactory.FromString(null);

        Assert.Equal(DeviceVariant.Generic, cfg.Variant);
        Assert.Equal(0u, cfg.DefaultRecipientId);
    }

    [Theory]
    [InlineData(DeviceVariant.Generic, "Generic")]
    [InlineData(DeviceVariant.TopLift, "TopLift")]
    [InlineData(DeviceVariant.Eden, "Eden")]
    [InlineData(DeviceVariant.Egicon, "Egicon")]
    public void CanonicalName_ReturnsExpectedString(DeviceVariant v, string expected)
    {
        Assert.Equal(expected, DeviceVariantConfigFactory.CanonicalName(v));
    }

    [Theory]
    [InlineData(DeviceVariant.Generic)]
    [InlineData(DeviceVariant.TopLift)]
    [InlineData(DeviceVariant.Eden)]
    [InlineData(DeviceVariant.Egicon)]
    public void CanonicalName_RoundTrip_PreservesVariant(DeviceVariant v)
    {
        var name = DeviceVariantConfigFactory.CanonicalName(v);
        Assert.Equal(v, DeviceVariantConfigFactory.ParseVariant(name));
    }

    [Fact]
    public void CanonicalName_InvalidVariant_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DeviceVariantConfigFactory.CanonicalName((DeviceVariant)999));
    }
}
