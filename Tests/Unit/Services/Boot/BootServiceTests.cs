using System.Buffers.Binary;
using Core.Interfaces;
using Core.Models;
using Services.Boot;
using Services.Protocol;
using Tests.Unit.Services.Protocol;

namespace Tests.Unit.Services.Boot;

/// <summary>
/// Test per <see cref="BootService"/>. Usa <see cref="ProtocolService"/> reale +
/// <see cref="FakeCommunicationPort"/> con auto-reply: ogni comando inviato che
/// attende reply riceve automaticamente un pacchetto risposta con CodeHigh="80",
/// permettendo di verificare la sequenza completa senza driver HW reale.
/// </summary>
public class BootServiceTests
{
    private const uint OwnRecipientId = 0xDEADBEEFu;
    private const uint TargetRecipientId = 0x00080381u;
    private const int FirmwareBlockSize = 1024;
    private const string ReplyCodeHigh = "80";

    private static readonly TimeSpan FastResponseTimeout = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan FastRestartInterval = TimeSpan.FromMilliseconds(10);

    // Comandi reply che il decoder del receiver deve conoscere
    private static readonly Command[] ReplyCommands =
    [
        new("StartProcedureReply", "80", "05"),
        new("EndProcedureReply",   "80", "06"),
        new("ProgramBlockReply",   "80", "07"),
    ];

    // --- Ctor ---

    [Fact]
    public void Ctor_NullProtocol_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new BootService(null!));
    }

    // --- Stato iniziale ---

    [Fact]
    public void InitialState_IsIdleWithZeroProgress()
    {
        using var fixture = new Fixture();

        Assert.Equal(BootState.Idle, fixture.Service.State);
        Assert.Equal(0.0, fixture.Service.Progress);
    }

    // --- Validazione input ---

    [Fact]
    public async Task Start_NullFirmware_Throws()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => fixture.Service.StartFirmwareUploadAsync(null!, TargetRecipientId));
    }

    [Fact]
    public async Task Start_FirmwareTooShort_Throws()
    {
        using var fixture = new Fixture();
        var tinyFirmware = new byte[10];
        await Assert.ThrowsAsync<ArgumentException>(
            () => fixture.Service.StartFirmwareUploadAsync(tinyFirmware, TargetRecipientId));
    }

    // --- Happy path ---

    [Fact]
    public async Task Start_SmallFirmware_CompletesAndEmitsProgress()
    {
        using var fixture = new Fixture();
        var firmware = BuildFirmware(length: 16, fwTypeLow: 0x05, fwTypeHigh: 0x00);
        var progresses = new List<BootProgress>();
        fixture.Service.ProgressChanged += (_, p) => progresses.Add(p);

        await fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId);

        Assert.Equal(BootState.Completed, fixture.Service.State);
        Assert.Equal(1.0, fixture.Service.Progress);
        // ProgressChanged: (0, 16) iniziale, (16, 16) dopo unico blocco (paddato a 1024).
        Assert.Equal(2, progresses.Count);
        Assert.Equal(0, progresses[0].CurrentOffset);
        Assert.Equal(16, progresses[0].TotalLength);
        Assert.Equal(16, progresses[^1].CurrentOffset);
    }

    [Fact]
    public async Task Start_TwoBlocks_EmitsProgressForEachBlock()
    {
        using var fixture = new Fixture();
        // 1500 byte → 2 blocchi (ultimo paddato a 1024 con 0xFF)
        var firmware = BuildFirmware(length: 1500, fwTypeLow: 0x05, fwTypeHigh: 0x00);
        var progresses = new List<BootProgress>();
        fixture.Service.ProgressChanged += (_, p) => progresses.Add(p);

        await fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId);

        Assert.Equal(BootState.Completed, fixture.Service.State);
        // (0, 1500) iniziale, (1024, 1500), (1500, 1500)
        Assert.Equal(3, progresses.Count);
        Assert.Equal(0, progresses[0].CurrentOffset);
        Assert.Equal(1024, progresses[1].CurrentOffset);
        Assert.Equal(1500, progresses[2].CurrentOffset);
    }

    [Fact]
    public async Task Start_SendsCommandsInOrder_StartBlocksEndRestart()
    {
        using var fixture = new Fixture();
        var firmware = BuildFirmware(length: 16, fwTypeLow: 0x05, fwTypeHigh: 0x00);

        await fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId);

        // Filtra solo i "primi chunk" (SetLength=true) per estrarre i cmd code
        var commandCodes = fixture.GetSentCommandCodes();
        // Sequenza: START (00,05), BLOCK (00,07), END (00,06), RESTART (00,0A) x 2
        Assert.Equal(
            new[] { (0x00, 0x05), (0x00, 0x07), (0x00, 0x06), (0x00, 0x0A), (0x00, 0x0A) },
            commandCodes);
    }

    [Fact]
    public async Task Start_ProgramBlockPayload_EncodesFwTypePageNumPageSizeAndBlock()
    {
        using var fixture = new Fixture();
        // fwType bytes 14-15 little-endian = 0x0042
        var firmware = BuildFirmware(length: 20, fwTypeLow: 0x42, fwTypeHigh: 0x00);
        firmware[16] = 0xAB; firmware[17] = 0xCD; firmware[18] = 0xEF; firmware[19] = 0x12;

        await fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId);

        var blockPayload = fixture.GetSentApplicationPayload(commandHi: 0x00, commandLo: 0x07);
        // [fwType_BE(2) + pageNum_BE(4) + pageSize_BE(4) + 0x00000000(4) + block(1024)]
        Assert.Equal(2 + 4 + 4 + 4 + FirmwareBlockSize, blockPayload.Length);
        // fwType_BE = 0x0042
        Assert.Equal(0x00, blockPayload[0]);
        Assert.Equal(0x42, blockPayload[1]);
        // pageNum_BE = 0
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(blockPayload.AsSpan(2, 4)));
        // pageSize_BE = 1024
        Assert.Equal(1024u, BinaryPrimitives.ReadUInt32BigEndian(blockPayload.AsSpan(6, 4)));
        // bytes 10..13 = 0x00000000
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32BigEndian(blockPayload.AsSpan(10, 4)));
        // block: i primi 16 byte sono il firmware originale, i restanti paddati a 0xFF
        for (int i = 0; i < 16; i++) Assert.Equal(firmware[i], blockPayload[14 + i]);
        Assert.Equal(firmware[16], blockPayload[14 + 16]);
        Assert.Equal(0xFF, blockPayload[14 + 20]);
        Assert.Equal(0xFF, blockPayload[^1]);
    }

    // --- Failure paths ---

    [Fact]
    public async Task Start_NoReplyForStart_TransitionsToFailed()
    {
        using var fixture = new Fixture(autoReply: false);
        var firmware = BuildFirmware(length: 16, fwTypeLow: 0x05, fwTypeHigh: 0x00);

        await fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId);

        Assert.Equal(BootState.Failed, fixture.Service.State);
        // Solo lo START è stato tentato (1 retry), mai BLOCK/END/RESTART
        var codes = fixture.GetSentCommandCodes();
        Assert.All(codes, c => Assert.Equal(0x05, c.Item2));
    }

    [Fact]
    public async Task Start_NoReplyForBlock_TransitionsToFailedAfterRetries()
    {
        using var fixture = new Fixture(replyForCommandLo: 0x05); // reply solo a START
        var firmware = BuildFirmware(length: 16, fwTypeLow: 0x05, fwTypeHigh: 0x00);

        await fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId);

        Assert.Equal(BootState.Failed, fixture.Service.State);
        // START 1 volta (riuscito), BLOCK 10 volte (10 retry, falliti), no END, no RESTART
        var codes = fixture.GetSentCommandCodes();
        int blockAttempts = codes.Count(c => c.Item2 == 0x07);
        Assert.Equal(10, blockAttempts);
        Assert.DoesNotContain((0x00, 0x06), codes);
        Assert.DoesNotContain((0x00, 0x0A), codes);
    }

    [Fact]
    public async Task Start_AlreadyUploading_Throws()
    {
        using var fixture = new Fixture();
        var firmware = BuildFirmware(length: 16, fwTypeLow: 0x05, fwTypeHigh: 0x00);
        // Disabilito auto-reply temporaneamente: lo START blocca su WaitReply.
        fixture.AutoReplyEnabled = false;
        var firstUpload = fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId);
        // Diamo tempo al primo upload di entrare in stato Uploading.
        await Task.Delay(50);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId));

        // Cleanup: lascio il primo terminare in Failed (timeout) per non lasciare task pendenti.
        await firstUpload;
    }

    [Fact]
    public async Task Start_CancellationDuringUpload_TransitionsToFailedAndThrows()
    {
        using var fixture = new Fixture();
        // Firmware grande per avere abbastanza blocchi da poter cancellare a metà.
        var firmware = BuildFirmware(length: 5000, fwTypeLow: 0x05, fwTypeHigh: 0x00);
        using var cts = new CancellationTokenSource();
        fixture.OnBlockSent = (count) => { if (count == 2) cts.Cancel(); };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId, cts.Token));

        Assert.Equal(BootState.Failed, fixture.Service.State);
    }

    // --- Dispose ---

    [Fact]
    public async Task AfterDispose_StartUpload_Throws()
    {
        var fixture = new Fixture();
        fixture.Service.Dispose();
        var firmware = BuildFirmware(length: 16, fwTypeLow: 0x05, fwTypeHigh: 0x00);

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => fixture.Service.StartFirmwareUploadAsync(firmware, TargetRecipientId));
        fixture.Dispose();
    }

    // --- Helpers ---

    private static byte[] BuildFirmware(int length, byte fwTypeLow, byte fwTypeHigh)
    {
        var fw = new byte[length];
        for (int i = 0; i < length; i++) fw[i] = (byte)(i & 0xFF);
        fw[14] = fwTypeLow;
        fw[15] = fwTypeHigh;
        return fw;
    }

    /// <summary>
    /// Bundle per i test BootService: <see cref="FakeCommunicationPort"/> con
    /// auto-reply opzionale su BLE, <see cref="ProtocolService"/> reale,
    /// <see cref="BootService"/> con timeout aggressivi.
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        private readonly Dictionary<int, (byte CmdHi, byte CmdLo, uint Recipient)> _packetTracker = new();
        private int _blocksSent;

        public Fixture(bool autoReply = true, byte? replyForCommandLo = null)
        {
            AutoReplyEnabled = autoReply;
            ReplyForCommandLo = replyForCommandLo;
            Port = new FakeCommunicationPort(ChannelKind.Ble);
            var decoder = new PacketDecoder(ReplyCommands, [], []);
            Protocol = new ProtocolService(Port, decoder, OwnRecipientId);
            Service = new BootService(Protocol, FastResponseTimeout, FastRestartInterval);
            Port.OnSent = OnPortSent;
        }

        public bool AutoReplyEnabled { get; set; }
        public byte? ReplyForCommandLo { get; }
        public FakeCommunicationPort Port { get; }
        public ProtocolService Protocol { get; }
        public BootService Service { get; }
        public Action<int>? OnBlockSent { get; set; }

        public IReadOnlyList<(int, int)> GetSentCommandCodes()
        {
            // Per ogni primo chunk (SetLength=true) estrae (cmdHi, cmdLo).
            var result = new List<(int, int)>();
            foreach (var frame in Port.SentPayloads)
            {
                var ni = NetInfo.Parse(frame[0], frame[1]);
                if (ni.SetLength) result.Add((frame[13], frame[14]));
            }
            return result;
        }

        public byte[] GetSentApplicationPayload(byte commandHi, byte commandLo)
        {
            // Trova tutti i chunk di un pacchetto col cmd richiesto, riassembla
            // il TP e ne estrae l'AL payload (post cmdHi/cmdLo, pre CRC).
            int? targetPacketId = null;
            var tpBuilder = new List<byte>();
            foreach (var frame in Port.SentPayloads)
            {
                var ni = NetInfo.Parse(frame[0], frame[1]);
                if (ni.SetLength)
                {
                    if (frame[13] == commandHi && frame[14] == commandLo)
                    {
                        targetPacketId = ni.PacketId;
                        tpBuilder.Clear();
                        tpBuilder.AddRange(frame.AsSpan(6).ToArray());
                    }
                    else
                    {
                        targetPacketId = null;
                    }
                    continue;
                }
                if (targetPacketId.HasValue && ni.PacketId == targetPacketId.Value)
                {
                    tpBuilder.AddRange(frame.AsSpan(6).ToArray());
                    if (ni.RemainingChunks == 0) break;
                }
            }
            // TP = [cryptFlag(1) + senderId_BE(4) + lPack_BE(2) + cmdHi + cmdLo + AL + CRC(2)]
            var tp = tpBuilder.ToArray();
            int alStart = 1 + 4 + 2 + 2;
            int alEnd = tp.Length - 2;
            return tp.AsSpan(alStart, alEnd - alStart).ToArray();
        }

        private void OnPortSent(byte[] frame)
        {
            var ni = NetInfo.Parse(frame[0], frame[1]);
            if (ni.SetLength)
            {
                var recipient = BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(2, 4));
                _packetTracker[ni.PacketId] = (frame[13], frame[14], recipient);
            }
            if (ni.RemainingChunks != 0) return;
            if (!_packetTracker.TryGetValue(ni.PacketId, out var info)) return;
            _packetTracker.Remove(ni.PacketId);

            if (info.CmdLo == 0x07) _blocksSent++;
            OnBlockSent?.Invoke(_blocksSent);

            if (!AutoReplyEnabled) return;
            if (ReplyForCommandLo.HasValue && ReplyForCommandLo.Value != info.CmdLo) return;
            // RESTART (00, 0A) è fire-and-forget — non auto-reply.
            if (info.CmdLo == 0x0A) return;

            FireReply(info.Recipient, info.CmdLo);
        }

        private void FireReply(uint originalRecipient, byte cmdLo)
        {
            var replyCmd = new Command("Reply", ReplyCodeHigh, cmdLo.ToString("X2"));
            // Build reply frame: sender = originalRecipient, recipient = us
            using var replyPort = new FakeCommunicationPort(ChannelKind.Ble);
            var replyDecoder = new PacketDecoder([], [], []);
            using var replySvc = new ProtocolService(replyPort, replyDecoder, originalRecipient);
            replySvc.SendCommandAsync(OwnRecipientId, replyCmd, []).GetAwaiter().GetResult();
            foreach (var chunk in replyPort.SentPayloads)
            {
                Port.RaisePacketReceived(chunk, DateTime.UtcNow);
            }
        }

        public void Dispose()
        {
            Service.Dispose();
            Protocol.Dispose();
            Port.Dispose();
        }
    }
}
