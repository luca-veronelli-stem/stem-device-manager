using Services.Protocol;

namespace Tests.Unit.Services.Protocol;

/// <summary>
/// Test per <see cref="PacketReassembler"/>: gestione single/multi-chunk,
/// isolamento per packetId, thread-safety.
/// </summary>
public class PacketReassemblerTests
{
    // --- Happy path: single-chunk packet ---

    [Fact]
    public void Accept_SingleChunkWithRemainingZero_ReturnsPayloadImmediately()
    {
        var reassembler = new PacketReassembler();
        // NetInfo: remChunks=0, setLen=1, pktId=1, ver=1
        var chunk = BuildChunk(
            netInfo: new NetInfo(0, true, 1, 1),
            data: [0x11, 0x22, 0x33]);

        var result = reassembler.Accept(chunk);

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, result);
        Assert.Equal(0, reassembler.PendingPacketCount);
    }

    [Fact]
    public void Accept_ChunkShorterThanNetInfo_ReturnsNull()
    {
        var reassembler = new PacketReassembler();
        var tooShort = new byte[] { 0x00 }; // 1 byte < 2

        var result = reassembler.Accept(tooShort);

        Assert.Null(result);
        Assert.Equal(0, reassembler.PendingPacketCount);
    }

    [Fact]
    public void Accept_OnlyNetInfoNoPayload_ReturnsEmptyArray()
    {
        // NetInfo solo (2 byte), nessun payload: remChunks=0 → merge di array vuoto
        var reassembler = new PacketReassembler();
        var chunk = BuildChunk(new NetInfo(0, true, 1, 1), data: []);

        var result = reassembler.Accept(chunk);

        Assert.NotNull(result);
        Assert.Empty(result);
    }

    // --- Multi-chunk ---

    [Fact]
    public void Accept_MultiChunk_AccumulatesAndReturnsMergedOnLast()
    {
        var reassembler = new PacketReassembler();

        // 3 chunk totali → remChunks parte da 2, poi 1, poi 0
        var first = BuildChunk(new NetInfo(2, true, 5, 1), [0xAA, 0xBB]);
        var middle = BuildChunk(new NetInfo(1, false, 5, 1), [0xCC]);
        var last = BuildChunk(new NetInfo(0, false, 5, 1), [0xDD, 0xEE, 0xFF]);

        Assert.Null(reassembler.Accept(first));
        Assert.Equal(1, reassembler.PendingPacketCount);

        Assert.Null(reassembler.Accept(middle));
        Assert.Equal(1, reassembler.PendingPacketCount);

        var result = reassembler.Accept(last);
        Assert.NotNull(result);
        Assert.Equal(
            new byte[] { 0xAA, 0xBB, 0xCC, 0xDD, 0xEE, 0xFF },
            result);
        Assert.Equal(0, reassembler.PendingPacketCount);
    }

    [Fact]
    public void Accept_LastChunkClearsBufferForThatPacketId()
    {
        var reassembler = new PacketReassembler();
        var first = BuildChunk(new NetInfo(1, true, 2, 1), [0x01]);
        var last = BuildChunk(new NetInfo(0, false, 2, 1), [0x02]);

        reassembler.Accept(first);
        reassembler.Accept(last);

        Assert.Equal(0, reassembler.PendingPacketCount);
    }

    // --- Isolamento per packetId ---

    [Fact]
    public void Accept_TwoPacketsInterleaved_DoNotMixPayloads()
    {
        var reassembler = new PacketReassembler();
        // Due pacchetti diversi (packetId=3 e packetId=5) interlacciati
        var p3_first = BuildChunk(new NetInfo(1, true, 3, 1), [0xA0]);
        var p5_first = BuildChunk(new NetInfo(1, true, 5, 1), [0xB0]);
        var p3_last = BuildChunk(new NetInfo(0, false, 3, 1), [0xA1]);
        var p5_last = BuildChunk(new NetInfo(0, false, 5, 1), [0xB1]);

        Assert.Null(reassembler.Accept(p3_first));
        Assert.Null(reassembler.Accept(p5_first));
        Assert.Equal(2, reassembler.PendingPacketCount);

        var r3 = reassembler.Accept(p3_last);
        Assert.Equal(new byte[] { 0xA0, 0xA1 }, r3);
        Assert.Equal(1, reassembler.PendingPacketCount); // p5 ancora in coda

        var r5 = reassembler.Accept(p5_last);
        Assert.Equal(new byte[] { 0xB0, 0xB1 }, r5);
        Assert.Equal(0, reassembler.PendingPacketCount);
    }

    [Fact]
    public void Accept_SamePacketIdReused_RebuffersAfterPreviousCompletion()
    {
        // Rolling code 1..7 → dopo completion, stesso packetId può ricomparire
        var reassembler = new PacketReassembler();
        var first = BuildChunk(new NetInfo(0, true, 4, 1), [0x01]);
        Assert.NotNull(reassembler.Accept(first));

        // Stesso packetId, nuovo pacchetto multi-chunk
        var second = BuildChunk(new NetInfo(1, true, 4, 1), [0x02]);
        var third = BuildChunk(new NetInfo(0, false, 4, 1), [0x03]);

        Assert.Null(reassembler.Accept(second));
        var merged = reassembler.Accept(third);

        Assert.Equal(new byte[] { 0x02, 0x03 }, merged);
    }

    // --- Accept overload con NetInfo separato ---

    [Fact]
    public void Accept_WithSeparateNetInfo_WorksIdenticallyToInline()
    {
        var reassembler = new PacketReassembler();
        var info = new NetInfo(0, true, 1, 1);

        var result = reassembler.Accept(info, [0xDE, 0xAD]);

        Assert.Equal(new byte[] { 0xDE, 0xAD }, result);
    }

    [Fact]
    public void Accept_WithSeparateNetInfo_NullChunkData_Throws()
    {
        var reassembler = new PacketReassembler();

        Assert.Throws<ArgumentNullException>(
            () => reassembler.Accept(new NetInfo(0, true, 1, 1), null!));
    }

    // --- Reset ---

    [Fact]
    public void Reset_ClearsAllPendingBuffers()
    {
        var reassembler = new PacketReassembler();
        reassembler.Accept(BuildChunk(new NetInfo(1, true, 2, 1), [0x01]));
        reassembler.Accept(BuildChunk(new NetInfo(1, true, 3, 1), [0x02]));
        Assert.Equal(2, reassembler.PendingPacketCount);

        reassembler.Reset();

        Assert.Equal(0, reassembler.PendingPacketCount);
    }

    [Fact]
    public void Reset_AfterReset_NewPacketIsAccepted()
    {
        var reassembler = new PacketReassembler();
        reassembler.Accept(BuildChunk(new NetInfo(1, true, 2, 1), [0x99]));
        reassembler.Reset();

        var result = reassembler.Accept(BuildChunk(new NetInfo(0, true, 2, 1), [0xAB]));

        // Il chunk 0x99 è stato scartato, il nuovo pacchetto ha solo 0xAB
        Assert.Equal(new byte[] { 0xAB }, result);
    }

    // --- Thread-safety stress (probabilistico) ---

    [Fact]
    public async Task Accept_ConcurrentFromMultipleThreads_NoExceptions()
    {
        var reassembler = new PacketReassembler();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var exceptions = new List<Exception>();

        var workers = Enumerable.Range(1, 7).Select(packetId => Task.Run(() =>
        {
            try
            {
                int counter = 0;
                while (!cts.IsCancellationRequested)
                {
                    // Ogni worker invia su un packetId distinto (1..7)
                    var first = BuildChunk(
                        new NetInfo(1, true, packetId, 1),
                        [(byte)(counter & 0xFF)]);
                    var last = BuildChunk(
                        new NetInfo(0, false, packetId, 1),
                        [(byte)((counter + 1) & 0xFF)]);
                    reassembler.Accept(first);
                    reassembler.Accept(last);
                    counter++;
                }
            }
            catch (Exception ex) { lock (exceptions) exceptions.Add(ex); }
        })).ToArray();

        await Task.WhenAll(workers);

        Assert.Empty(exceptions);
    }

    private static byte[] BuildChunk(NetInfo netInfo, byte[] data)
    {
        var (lo, hi) = netInfo.ToBytes();
        var chunk = new byte[2 + data.Length];
        chunk[0] = lo;
        chunk[1] = hi;
        Buffer.BlockCopy(data, 0, chunk, 2, data.Length);
        return chunk;
    }
}
