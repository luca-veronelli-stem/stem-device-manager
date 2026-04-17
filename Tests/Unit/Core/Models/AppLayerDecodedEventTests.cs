using System.Collections.Immutable;
using Core.Models;

namespace Tests.Unit.Core.Models;

/// <summary>
/// Test per AppLayerDecodedEvent. Verifica immutabilità + equality strutturale.
/// </summary>
public class AppLayerDecodedEventTests
{
    private static readonly Command SampleCommand = new("Read", "80", "01");
    private static readonly Variable SampleVariable = new("Temperatura", "80", "01", "uint16_t");

    [Fact]
    public void Constructor_WithVariable_SetsProperties()
    {
        var payload = ImmutableArray.Create<byte>(0xAA, 0xBB);
        var e = new AppLayerDecodedEvent(SampleCommand, SampleVariable, payload, "EDEN", "Madre");

        Assert.Equal(SampleCommand, e.Command);
        Assert.Equal(SampleVariable, e.Variable);
        Assert.Equal(payload, e.Payload);
        Assert.Equal("EDEN", e.SenderDevice);
        Assert.Equal("Madre", e.SenderBoard);
        Assert.True(e.IsVariableEvent);
        Assert.Equal(2, e.PayloadLength);
    }

    [Fact]
    public void Constructor_WithoutVariable_IsNotVariableEvent()
    {
        var e = new AppLayerDecodedEvent(
            SampleCommand, null, ImmutableArray<byte>.Empty, "SPARK", "HMI");

        Assert.Null(e.Variable);
        Assert.False(e.IsVariableEvent);
        Assert.Equal(0, e.PayloadLength);
    }

    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var payload = ImmutableArray.Create<byte>(1, 2, 3);
        var a = new AppLayerDecodedEvent(SampleCommand, SampleVariable, payload, "EDEN", "Madre");
        var b = new AppLayerDecodedEvent(SampleCommand, SampleVariable, payload, "EDEN", "Madre");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentPayloadSameBytes_AreEqual()
    {
        var a = new AppLayerDecodedEvent(
            SampleCommand, SampleVariable,
            ImmutableArray.Create<byte>(1, 2, 3), "EDEN", "Madre");
        var b = new AppLayerDecodedEvent(
            SampleCommand, SampleVariable,
            ImmutableArray.Create<byte>(1, 2, 3), "EDEN", "Madre");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equality_DifferentSender_AreNotEqual()
    {
        var payload = ImmutableArray.Create<byte>(1, 2);
        var a = new AppLayerDecodedEvent(SampleCommand, SampleVariable, payload, "EDEN", "Madre");
        var b = new AppLayerDecodedEvent(SampleCommand, SampleVariable, payload, "SPARK", "HMI");

        Assert.NotEqual(a, b);
    }
}
