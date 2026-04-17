using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per l'enum DeviceVariant.
/// </summary>
public class DeviceVariantTests
{
    [Fact]
    public void Enum_HasExactlyFourVariants()
    {
        var values = Enum.GetValues<DeviceVariant>();
        Assert.Equal(4, values.Length);
    }

    [Theory]
    [InlineData(DeviceVariant.Generic)]
    [InlineData(DeviceVariant.TopLift)]
    [InlineData(DeviceVariant.Eden)]
    [InlineData(DeviceVariant.Egicon)]
    public void Enum_AllValuesAreDefined(DeviceVariant v)
    {
        Assert.True(Enum.IsDefined(v));
    }
}
