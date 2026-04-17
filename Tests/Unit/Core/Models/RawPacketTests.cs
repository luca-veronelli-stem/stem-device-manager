using System.Collections.Immutable;
using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per RawPacket. Verifica immutabilità + equality strutturale.
/// </summary>
public class RawPacketTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var bytes = ImmutableArray.Create<byte>(1, 2, 3);
        var ts = new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc);
        var p = new RawPacket(bytes, ts);

        Assert.Equal(bytes, p.Payload);
        Assert.Equal(ts, p.Timestamp);
        Assert.Equal(3, p.Length);
        Assert.False(p.IsEmpty);
    }

    [Fact]
    public void IsEmpty_EmptyPayload_True()
    {
        var p = new RawPacket(ImmutableArray<byte>.Empty, DateTime.UtcNow);
        Assert.True(p.IsEmpty);
        Assert.Equal(0, p.Length);
    }

    [Fact]
    public void Equality_SamePayloadAndTimestamp_AreEqual()
    {
        var ts = new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc);
        var a = new RawPacket(ImmutableArray.Create<byte>(1, 2, 3), ts);
        var b = new RawPacket(ImmutableArray.Create<byte>(1, 2, 3), ts);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentPayload_AreNotEqual()
    {
        var ts = new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc);
        var a = new RawPacket(ImmutableArray.Create<byte>(1, 2, 3), ts);
        var b = new RawPacket(ImmutableArray.Create<byte>(4, 5, 6), ts);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentTimestamp_AreNotEqual()
    {
        var bytes = ImmutableArray.Create<byte>(1, 2, 3);
        var a = new RawPacket(bytes, new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc));
        var b = new RawPacket(bytes, new DateTime(2026, 4, 17, 10, 0, 1, DateTimeKind.Utc));

        Assert.NotEqual(a, b);
    }
}
