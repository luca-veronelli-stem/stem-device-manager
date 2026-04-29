using Core.Interfaces;
using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per DeviceVariantConfig e la factory Create.
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
        Assert.Equal(DeviceVariantConfig.DefaultSenderId, cfg.SenderId);
        Assert.Equal(ChannelKind.Ble, cfg.DefaultChannel);
    }

    [Fact]
    public void Create_TopLift_ProducesFixedRecipientId()
    {
        var cfg = DeviceVariantConfig.Create(DeviceVariant.TopLift);

        Assert.Equal(DeviceVariant.TopLift, cfg.Variant);
        Assert.Equal(0x00080381u, cfg.DefaultRecipientId);
        Assert.Equal("", cfg.DeviceName);
        Assert.Equal("", cfg.BoardName);
        Assert.Equal(DeviceVariantConfig.DefaultSenderId, cfg.SenderId);
        Assert.Equal(ChannelKind.Can, cfg.DefaultChannel);
    }

    [Fact]
    public void Create_Eden_ProducesLookupNames()
    {
        var cfg = DeviceVariantConfig.Create(DeviceVariant.Eden);

        Assert.Equal(DeviceVariant.Eden, cfg.Variant);
        Assert.Equal(0u, cfg.DefaultRecipientId);
        Assert.Equal("EDEN", cfg.DeviceName);
        Assert.Equal("Madre", cfg.BoardName);
        Assert.Equal(DeviceVariantConfig.DefaultSenderId, cfg.SenderId);
        Assert.Equal(ChannelKind.Ble, cfg.DefaultChannel);
    }

    [Fact]
    public void Create_Egicon_ProducesLookupNames()
    {
        var cfg = DeviceVariantConfig.Create(DeviceVariant.Egicon);

        Assert.Equal(DeviceVariant.Egicon, cfg.Variant);
        Assert.Equal(0u, cfg.DefaultRecipientId);
        Assert.Equal("SPARK", cfg.DeviceName);
        Assert.Equal("HMI", cfg.BoardName);
        Assert.Equal(DeviceVariantConfig.DefaultSenderId, cfg.SenderId);
        Assert.Equal(ChannelKind.Ble, cfg.DefaultChannel);
    }

    [Theory]
    [InlineData(DeviceVariant.Generic, ChannelKind.Ble)]
    [InlineData(DeviceVariant.TopLift, ChannelKind.Can)]
    [InlineData(DeviceVariant.Eden, ChannelKind.Ble)]
    [InlineData(DeviceVariant.Egicon, ChannelKind.Ble)]
    public void DefaultChannelFor_MatchesLegacyMapping(DeviceVariant v, ChannelKind expected)
    {
        Assert.Equal(expected, DeviceVariantConfig.DefaultChannelFor(v));
    }

    [Fact]
    public void DefaultSenderId_IsEightForLegacyParity()
    {
        Assert.Equal(8u, DeviceVariantConfig.DefaultSenderId);
    }

    [Theory]
    [InlineData(DeviceVariant.Generic)]
    [InlineData(DeviceVariant.TopLift)]
    [InlineData(DeviceVariant.Eden)]
    [InlineData(DeviceVariant.Egicon)]
    public void Create_WithExplicitSenderId_OverridesDefault(DeviceVariant v)
    {
        var cfg = DeviceVariantConfig.Create(v, senderId: 0x12345678u);

        Assert.Equal(0x12345678u, cfg.SenderId);
        // Le altre proprietà devono essere identiche alla factory single-arg
        var defaultCfg = DeviceVariantConfig.Create(v);
        Assert.Equal(defaultCfg.Variant, cfg.Variant);
        Assert.Equal(defaultCfg.DefaultRecipientId, cfg.DefaultRecipientId);
        Assert.Equal(defaultCfg.DeviceName, cfg.DeviceName);
        Assert.Equal(defaultCfg.BoardName, cfg.BoardName);
    }

    [Fact]
    public void Create_WithExplicitSenderId_InvalidVariant_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DeviceVariantConfig.Create((DeviceVariant)999, senderId: 8));
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

    [Theory]
    [InlineData(DeviceVariant.TopLift, "STEM Toplift A2 Manager ")]
    [InlineData(DeviceVariant.Eden,    "STEM Eden XP Manager ")]
    [InlineData(DeviceVariant.Egicon,  "STEM Spark Manager ")]
    [InlineData(DeviceVariant.Generic, "STEM Device Manager ")]
    public void WindowTitle_MatchesVariant(DeviceVariant variant, string expected)
    {
        var cfg = DeviceVariantConfig.Create(variant);

        Assert.Equal(expected, cfg.WindowTitle);
    }

    [Fact]
    public void SmartBootDevices_TopLift_HasThreeKeyboardsPlusMotherboard()
    {
        var cfg = DeviceVariantConfig.Create(DeviceVariant.TopLift);

        Assert.Equal(4, cfg.SmartBootDevices.Count);
        Assert.Equal(3, cfg.SmartBootDevices.Count(d => d.IsKeyboard));
        Assert.Contains(cfg.SmartBootDevices, d => d.Address == 0x00080381u && !d.IsKeyboard);
    }

    [Fact]
    public void SmartBootDevices_Eden_HasTwoKeyboardsPlusMotherboard()
    {
        var cfg = DeviceVariantConfig.Create(DeviceVariant.Eden);

        Assert.Equal(3, cfg.SmartBootDevices.Count);
        Assert.Equal(2, cfg.SmartBootDevices.Count(d => d.IsKeyboard));
        Assert.Contains(cfg.SmartBootDevices, d => d.Address == 0x00030141u && !d.IsKeyboard);
    }

    [Theory]
    [InlineData(DeviceVariant.Generic)]
    [InlineData(DeviceVariant.Egicon)]
    public void SmartBootDevices_GenericAndEgicon_AreEmpty(DeviceVariant variant)
    {
        var cfg = DeviceVariantConfig.Create(variant);

        Assert.Empty(cfg.SmartBootDevices);
    }
}
