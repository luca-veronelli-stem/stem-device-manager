using System.Collections.Immutable;
using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per TelemetryDataPoint. Verifica construction + equality strutturale.
/// </summary>
public class TelemetryDataPointTests
{
    private static readonly Variable SampleVariable = new("Temperatura", "80", "01", "uint16_t");

    [Fact]
    public void Constructor_SetsProperties()
    {
        var raw = ImmutableArray.Create<byte>(0x12, 0x34);
        var ts = new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc);
        var p = new TelemetryDataPoint(SampleVariable, raw, ts);

        Assert.Equal(SampleVariable, p.Variable);
        Assert.Equal(raw, p.RawValue);
        Assert.Equal(ts, p.Timestamp);
        Assert.Equal(2, p.RawValueLength);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var ts = new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc);
        var a = new TelemetryDataPoint(
            SampleVariable, ImmutableArray.Create<byte>(1, 2), ts);
        var b = new TelemetryDataPoint(
            SampleVariable, ImmutableArray.Create<byte>(1, 2), ts);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentRawValue_AreNotEqual()
    {
        var ts = new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc);
        var a = new TelemetryDataPoint(
            SampleVariable, ImmutableArray.Create<byte>(1, 2), ts);
        var b = new TelemetryDataPoint(
            SampleVariable, ImmutableArray.Create<byte>(3, 4), ts);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equality_DifferentTimestamp_AreNotEqual()
    {
        var raw = ImmutableArray.Create<byte>(1, 2);
        var a = new TelemetryDataPoint(
            SampleVariable, raw,
            new DateTime(2026, 4, 17, 10, 0, 0, DateTimeKind.Utc));
        var b = new TelemetryDataPoint(
            SampleVariable, raw,
            new DateTime(2026, 4, 17, 10, 0, 1, DateTimeKind.Utc));

        Assert.NotEqual(a, b);
    }
}
