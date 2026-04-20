using Services.Protocol;

namespace Tests.Unit.Services.Protocol;

/// <summary>
/// Test per <see cref="NetInfo"/>: parsing bit-level, bounds, round-trip
/// encode/decode dei due byte LE del Network Layer STEM.
/// </summary>
public class NetInfoTests
{
    [Fact]
    public void Parse_FirstChunkOfSingleChunkPacket_AllFieldsCorrect()
    {
        // setLength=1, packetId=3, version=1, remainingChunks=0
        // bit layout: remChunks(0000000000) setLen(1) pktId(011) ver(01)
        //           = 0b0000000000_1_011_01 = 0x002D
        // LE bytes: lo=0x2D, hi=0x00
        var info = NetInfo.Parse(lo: 0x2D, hi: 0x00);

        Assert.Equal(0, info.RemainingChunks);
        Assert.True(info.SetLength);
        Assert.Equal(3, info.PacketId);
        Assert.Equal(1, info.Version);
    }

    [Fact]
    public void Parse_MaxRemainingChunks_ReturnsTenBitValue()
    {
        // remainingChunks=1023 (10 bit = 0x3FF), tutto il resto 0
        // raw = 0x3FF << 6 = 0xFFC0
        // LE: lo=0xC0, hi=0xFF
        var info = NetInfo.Parse(lo: 0xC0, hi: 0xFF);

        Assert.Equal(1023, info.RemainingChunks);
        Assert.False(info.SetLength);
        Assert.Equal(0, info.PacketId);
        Assert.Equal(0, info.Version);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void Parse_AllValidPacketIds_ExtractedCorrectly(int packetId)
    {
        // SoloPacketId (bit 4..2) acceso
        int raw = (packetId & 0x07) << 2;
        byte lo = (byte)(raw & 0xFF);
        byte hi = (byte)((raw >> 8) & 0xFF);

        var info = NetInfo.Parse(lo, hi);

        Assert.Equal(packetId, info.PacketId);
    }

    [Fact]
    public void Parse_Version_TwoBitsLsb()
    {
        // version=3 (binario 11), tutto il resto 0
        var info = NetInfo.Parse(lo: 0x03, hi: 0x00);

        Assert.Equal(3, info.Version);
        Assert.Equal(0, info.PacketId);
        Assert.False(info.SetLength);
        Assert.Equal(0, info.RemainingChunks);
    }

    [Theory]
    [InlineData(0, false, 1, 1)]      // primo chunk di pacchetto singolo, packetId=1
    [InlineData(5, true, 7, 1)]       // primo di 6 chunk, packetId=7
    [InlineData(0, false, 3, 1)]      // ultimo chunk arrivato (continuation)
    [InlineData(1023, false, 2, 1)]   // max remainingChunks
    public void RoundTrip_ParseAfterToBytes_RecuperaCampiOriginali(
        int remainingChunks, bool setLength, int packetId, int version)
    {
        var original = new NetInfo(remainingChunks, setLength, packetId, version);

        var (lo, hi) = original.ToBytes();
        var parsed = NetInfo.Parse(lo, hi);

        Assert.Equal(original, parsed);
    }

    [Fact]
    public void ToBytes_ProducesLittleEndianOrder()
    {
        // remainingChunks=1, setLength=1, packetId=2, version=1
        // raw = (1 << 6) | (1 << 5) | (2 << 2) | 1 = 0x40 | 0x20 | 0x08 | 0x01 = 0x69
        var info = new NetInfo(1, true, 2, 1);

        var (lo, hi) = info.ToBytes();

        Assert.Equal(0x69, lo);
        Assert.Equal(0x00, hi);
    }
}
