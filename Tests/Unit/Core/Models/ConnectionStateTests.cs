using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per l'enum ConnectionState.
/// </summary>
public class ConnectionStateTests
{
    [Fact]
    public void Enum_HasExpectedValues()
    {
        Assert.Equal(0, (int)ConnectionState.Disconnected);
        Assert.Equal(1, (int)ConnectionState.Connecting);
        Assert.Equal(2, (int)ConnectionState.Connected);
        Assert.Equal(3, (int)ConnectionState.Error);
    }

    [Fact]
    public void Enum_HasExactlyFourValues()
    {
        var values = Enum.GetValues<ConnectionState>();
        Assert.Equal(4, values.Length);
    }
}
