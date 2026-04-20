using System.Buffers.Binary;
using Core.Models;
using Services.Protocol;

namespace Tests.Unit.Services.Protocol;

/// <summary>
/// Test per <see cref="ProtocolService"/>: pipeline encode (TP + CRC +
/// chunking + framing per canale), pipeline decode (strip canale +
/// reassembly + decoder), pattern request/reply.
/// </summary>
public class ProtocolServiceTests
{
    private static readonly Command ReadVariable = new("ReadVariable", "00", "01");
    private static readonly Command WriteVariable = new("WriteVariable", "00", "02");
    private static readonly Command ReadVariableReply = new("ReadVariableReply", "80", "01");

    // --- Ctor ---

    [Fact]
    public void Ctor_NullPort_Throws()
    {
        var decoder = new PacketDecoder([], [], []);
        Assert.Throws<ArgumentNullException>(
            () => new ProtocolService(null!, decoder, 0));
    }

    [Fact]
    public void Ctor_NullDecoder_Throws()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Can);
        Assert.Throws<ArgumentNullException>(
            () => new ProtocolService(port, null!, 0));
    }

    // --- Send single-chunk CAN ---

    [Fact]
    public async Task SendCommandAsync_CanSingleChunk_BuildsFrameWithArbIdPrefix()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Can);
        var decoder = new PacketDecoder([], [], []);
        using var svc = new ProtocolService(port, decoder, senderId: 0x12345678u);

        await svc.SendCommandAsync(
            recipientId: 0x00080381u,
            command: ReadVariable,
            payload: []);

        // Payload AL = 2 byte (cmdInit+cmdOpt). TP = 1+4+2 + 2 + 2 = 11 byte.
        // CAN chunk size 6 → 2 chunk: 6 byte + 5 byte.
        Assert.Equal(2, port.SentPayloads.Count);

        // Primo frame: [arbId_LE(4) + NetInfo(2) + chunk(6)]
        var frame0 = port.SentPayloads[0];
        Assert.Equal(12, frame0.Length);
        Assert.Equal(
            0x00080381u,
            BinaryPrimitives.ReadUInt32LittleEndian(frame0.AsSpan(0, 4)));
        // NetInfo: remainingChunks=1, setLength=1, packetId=1, version=1
        var netInfo0 = NetInfo.Parse(frame0[4], frame0[5]);
        Assert.Equal(1, netInfo0.RemainingChunks);
        Assert.True(netInfo0.SetLength);
        Assert.Equal(1, netInfo0.PacketId);

        // Secondo frame: remainingChunks=0, setLength=0
        var frame1 = port.SentPayloads[1];
        var netInfo1 = NetInfo.Parse(frame1[4], frame1[5]);
        Assert.Equal(0, netInfo1.RemainingChunks);
        Assert.False(netInfo1.SetLength);
        Assert.Equal(1, netInfo1.PacketId);
    }

    // --- Send single-chunk BLE ---

    [Fact]
    public async Task SendCommandAsync_BleSingleChunk_BuildsFrameWithRecipientInData()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Ble);
        var decoder = new PacketDecoder([], [], []);
        using var svc = new ProtocolService(port, decoder, senderId: 0xDEADBEEFu);

        await svc.SendCommandAsync(
            recipientId: 0xCAFEBABEu,
            command: WriteVariable,
            payload: [0x12, 0x34]);

        // BLE chunk size 98 → 1 chunk singolo per TP di 13 byte.
        Assert.Single(port.SentPayloads);

        var frame = port.SentPayloads[0];
        // [NetInfo(2) + recipientId_LE(4) + chunk(TP)]
        Assert.Equal(2 + 4 + 13, frame.Length);
        var netInfo = NetInfo.Parse(frame[0], frame[1]);
        Assert.Equal(0, netInfo.RemainingChunks); // ultimo chunk
        Assert.True(netInfo.SetLength);
        Assert.Equal(
            0xCAFEBABEu,
            BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(2, 4)));
        // TP inizia a offset 6: cryptFlag + senderId_BE(4) + lPack_BE(2) + AL(4) + CRC(2)
        Assert.Equal(0x00, frame[6]); // cryptFlag
        Assert.Equal(0xDE, frame[7]); // senderId BE (MSB first)
        Assert.Equal(0xAD, frame[8]);
        Assert.Equal(0xBE, frame[9]);
        Assert.Equal(0xEF, frame[10]);
    }

    // --- Rolling packetId ---

    [Fact]
    public async Task SendCommandAsync_MultipleCalls_IncrementPacketIdInRange1To7()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Ble);
        var decoder = new PacketDecoder([], [], []);
        using var svc = new ProtocolService(port, decoder, 0);

        for (int i = 0; i < 10; i++)
        {
            await svc.SendCommandAsync(0, ReadVariable, []);
        }

        // 10 invii, ogni payload BLE sta in 1 chunk → 10 frame
        Assert.Equal(10, port.SentPayloads.Count);

        var packetIds = port.SentPayloads
            .Select(f => NetInfo.Parse(f[0], f[1]).PacketId)
            .ToArray();

        // Rolling code 1..7 poi 1..3
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 1, 2, 3 }, packetIds);
    }

    // --- Receive CAN roundtrip ---

    [Fact]
    public void PacketReceived_CanFrameMultiChunk_StripsArbIdAndReassembles()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Can);
        var decoder = new PacketDecoder([ReadVariable], [], []);
        using var svc = new ProtocolService(port, decoder, 0);
        AppLayerDecodedEvent? received = null;
        svc.AppLayerDecoded += (_, e) => received = e;

        // CAN chunk=6 → TP 11 byte (cryptFlag+senderId+lPack+cmd+CRC) = 2 chunk
        var chunks = BuildCanChunkSequence(
            senderId: 0xABCDEF01,
            recipientId: 0x00080381,
            command: ReadVariable,
            alPayload: []);

        foreach (var chunk in chunks.SkipLast(1))
        {
            port.RaisePacketReceived(chunk, DateTime.UtcNow);
            Assert.Null(received);
        }
        port.RaisePacketReceived(chunks[^1], DateTime.UtcNow);

        Assert.NotNull(received);
        Assert.Same(ReadVariable, received!.Command);
    }

    // --- Receive BLE strip recipientId ---

    [Fact]
    public void PacketReceived_BleFrame_StripsRecipientIdAndFiresEvent()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Ble);
        var decoder = new PacketDecoder([ReadVariable], [], []);
        using var svc = new ProtocolService(port, decoder, 0);
        AppLayerDecodedEvent? received = null;
        svc.AppLayerDecoded += (_, e) => received = e;

        var frame = BuildSerialSingleChunkFrame(
            senderId: 0xABCDEF01,
            recipientId: 0x00080381,
            command: ReadVariable,
            alPayload: []);

        port.RaisePacketReceived(frame, DateTime.UtcNow);

        Assert.NotNull(received);
        Assert.Same(ReadVariable, received!.Command);
    }

    // --- Multi-chunk CAN reassembly ---

    [Fact]
    public void PacketReceived_MultiChunkCan_Reassembles()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Can);
        var decoder = new PacketDecoder([WriteVariable], [], []);
        using var svc = new ProtocolService(port, decoder, 0);
        AppLayerDecodedEvent? received = null;
        svc.AppLayerDecoded += (_, e) => received = e;

        // Costruiamo TP grande che richiede 2 chunk CAN (TP > 6 byte)
        var chunks = BuildCanChunkSequence(
            senderId: 0x01020304,
            recipientId: 0xAA,
            command: WriteVariable,
            alPayload: [0x10, 0x20, 0x30, 0x40, 0x50]); // AL 7 byte → TP 13 byte → 3 chunk

        Assert.True(chunks.Count >= 2);

        foreach (var chunk in chunks.SkipLast(1))
        {
            port.RaisePacketReceived(chunk, DateTime.UtcNow);
            Assert.Null(received); // non ancora completo
        }
        port.RaisePacketReceived(chunks[^1], DateTime.UtcNow);

        Assert.NotNull(received);
        Assert.Same(WriteVariable, received!.Command);
    }

    // --- Request/reply ---

    [Fact]
    public async Task SendCommandAndWaitReplyAsync_ReplyArrivesInTime_ReturnsTrue()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Ble);
        var decoder = new PacketDecoder([ReadVariable, ReadVariableReply], [], []);
        using var svc = new ProtocolService(port, decoder, 0);

        var sendTask = svc.SendCommandAndWaitReplyAsync(
            recipientId: 0xAA,
            command: ReadVariable,
            payload: [],
            replyValidator: evt => evt.Command.Name == "ReadVariableReply",
            timeout: TimeSpan.FromSeconds(5));

        // Il Handler è già subscribed sincronamente prima del primo await dentro
        // SendCommandAndWaitReplyAsync, quindi la reply può arrivare subito senza
        // rischio di race. Nessun Task.Delay artificiale.
        var replyFrame = BuildSerialSingleChunkFrame(
            senderId: 0xAA, recipientId: 0, command: ReadVariableReply, alPayload: []);
        port.RaisePacketReceived(replyFrame, DateTime.UtcNow);

        var result = await sendTask;
        Assert.True(result);
    }

    [Fact]
    public async Task SendCommandAndWaitReplyAsync_NoReplyBeforeTimeout_ReturnsFalse()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Ble);
        var decoder = new PacketDecoder([], [], []);
        using var svc = new ProtocolService(port, decoder, 0);

        var result = await svc.SendCommandAndWaitReplyAsync(
            recipientId: 0xAA,
            command: ReadVariable,
            payload: [],
            replyValidator: _ => true,
            timeout: TimeSpan.FromMilliseconds(100));

        Assert.False(result);
    }

    [Fact]
    public async Task SendCommandAndWaitReplyAsync_CancellationRequested_Throws()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Ble);
        var decoder = new PacketDecoder([], [], []);
        using var svc = new ProtocolService(port, decoder, 0);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => svc.SendCommandAndWaitReplyAsync(
                0xAA, ReadVariable, [], _ => true,
                TimeSpan.FromSeconds(10), cts.Token));
    }

    // --- Dispose ---

    [Fact]
    public void Dispose_UnsubscribesFromPortEvents()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Ble);
        var decoder = new PacketDecoder([ReadVariable], [], []);
        var svc = new ProtocolService(port, decoder, 0);
        AppLayerDecodedEvent? received = null;
        svc.AppLayerDecoded += (_, e) => received = e;

        svc.Dispose();

        // Dopo Dispose, pacchetti ricevuti non devono più fire l'evento
        var frame = BuildSerialSingleChunkFrame(
            senderId: 0, recipientId: 0, command: ReadVariable, alPayload: []);
        port.RaisePacketReceived(frame, DateTime.UtcNow);

        Assert.Null(received);
    }

    [Fact]
    public async Task AfterDispose_SendCommandAsync_Throws()
    {
        using var port = new FakeCommunicationPort(ChannelKind.Can);
        var decoder = new PacketDecoder([], [], []);
        var svc = new ProtocolService(port, decoder, 0);
        svc.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => svc.SendCommandAsync(0, ReadVariable, []));
    }

    // --- Helpers: costruiscono frame wire fedeli alla pipeline di send ---

    private static byte[] BuildSerialSingleChunkFrame(
        uint senderId, uint recipientId, Command command, byte[] alPayload)
    {
        using var senderPort = new FakeCommunicationPort(ChannelKind.Ble);
        var decoder = new PacketDecoder([], [], []);
        using var senderSvc = new ProtocolService(senderPort, decoder, senderId);
        senderSvc.SendCommandAsync(recipientId, command, alPayload).GetAwaiter().GetResult();
        return senderPort.SentPayloads.Single();
    }

    private static List<byte[]> BuildCanChunkSequence(
        uint senderId, uint recipientId, Command command, byte[] alPayload)
    {
        using var senderPort = new FakeCommunicationPort(ChannelKind.Can);
        var decoder = new PacketDecoder([], [], []);
        using var senderSvc = new ProtocolService(senderPort, decoder, senderId);
        senderSvc.SendCommandAsync(recipientId, command, alPayload).GetAwaiter().GetResult();
        return senderPort.SentPayloads;
    }
}
