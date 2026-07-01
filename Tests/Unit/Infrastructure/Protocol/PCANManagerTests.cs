using Infrastructure.Protocol.Hardware;
using Peak.Can.Basic.BackwardCompatibility;

namespace Tests.Unit.Infrastructure.Protocol;

/// <summary>
/// Tests for <see cref="PCANManager.TryMapBaudRate"/>, the pure kbit/s → enum
/// mapping. The manager itself P/Invokes the native PCAN driver and cannot be
/// constructed on a driver-less host, but the mapping is side-effect-free.
/// </summary>
public class PCANManagerTests
{
    [Theory]
    [InlineData(100, TPCANBaudrate.PCAN_BAUD_100K)]
    [InlineData(125, TPCANBaudrate.PCAN_BAUD_125K)]
    [InlineData(250, TPCANBaudrate.PCAN_BAUD_250K)]
    [InlineData(500, TPCANBaudrate.PCAN_BAUD_500K)]
    public void TryMapBaudRate_SupportedKbps_MapsToEnum(int kbps, TPCANBaudrate expected)
    {
        var ok = PCANManager.TryMapBaudRate(kbps, out var baudRate);

        Assert.True(ok);
        Assert.Equal(expected, baudRate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1000)]
    // 500000 is bps, not kbps — the pre-fix switch wrongly accepted it while
    // rejecting the documented 500. This guards against that regression.
    [InlineData(500000)]
    public void TryMapBaudRate_UnsupportedValue_ReturnsFalse(int value)
    {
        var ok = PCANManager.TryMapBaudRate(value, out _);

        Assert.False(ok);
    }
}
