using System.Collections.Immutable;
using Core.Models;
using Services.Protocol;
using Services.Telemetry;
using Tests.Unit.Services.Protocol;

namespace Tests.Unit.Services.Telemetry;

/// <summary>
/// Test per <see cref="TelemetryService"/>: usa <see cref="ProtocolService"/>
/// reale + <see cref="FakeCommunicationPort"/> per verificare encode + decode
/// end-to-end, parità con <c>App.STEMProtocol.TelemetryManager</c>.
/// </summary>
public class TelemetryServiceTests
{
    private const uint OwnRecipientId = 0xDEADBEEFu;
    private const uint SourceRecipientId = 0x00080381u;

    private static readonly Command CmdTelemetryData =
        new("TelemetryData", "00", "18");

    private static readonly Variable VarUInt8 =
        new("PressureRaw", "01", "00", "uint8_t");
    private static readonly Variable VarUInt16 =
        new("Temperature", "01", "01", "uint16_t");
    private static readonly Variable VarUInt32 =
        new("EncoderTicks", "01", "02", "uint32_t");

    // --- Ctor ---

    [Fact]
    public void Ctor_NullProtocol_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TelemetryService(null!));
    }

    // --- Stato iniziale ---

    [Fact]
    public void InitialState_StoppedAndEmpty()
    {
        using var fixture = new Fixture();
        var svc = fixture.Service;

        Assert.False(svc.IsRunning);
        Assert.Empty(svc.CurrentVariables);
        Assert.Equal(0u, svc.SourceRecipientId);
    }

    // --- UpdateDictionary / UpdateSourceAddress ---

    [Fact]
    public void UpdateDictionary_ReplacesCurrentVariables()
    {
        using var fixture = new Fixture();
        fixture.Service.UpdateDictionary([VarUInt8, VarUInt16]);

        Assert.Equal(2, fixture.Service.CurrentVariables.Count);
        Assert.Same(VarUInt8, fixture.Service.CurrentVariables[0]);
    }

    [Fact]
    public void UpdateDictionary_NullThrows()
    {
        using var fixture = new Fixture();
        Assert.Throws<ArgumentNullException>(() => fixture.Service.UpdateDictionary(null!));
    }

    [Fact]
    public void UpdateSourceAddress_UpdatesProperty()
    {
        using var fixture = new Fixture();
        fixture.Service.UpdateSourceAddress(0xCAFEBABEu);

        Assert.Equal(0xCAFEBABEu, fixture.Service.SourceRecipientId);
    }

    // --- Start / Stop ---

    [Fact]
    public async Task StartFastTelemetryAsync_FromStopped_SendsConfigureAndStart()
    {
        using var fixture = new Fixture();
        fixture.Service.UpdateSourceAddress(SourceRecipientId);
        fixture.Service.UpdateDictionary([VarUInt16]);

        await fixture.Service.StartFastTelemetryAsync();

        Assert.True(fixture.Service.IsRunning);
        // 2 invii: CONFIGURE poi START. BLE chunk size 98, payload piccoli → 1 frame ciascuno.
        Assert.Equal(2, fixture.Port.SentPayloads.Count);
        AssertCommandIs(fixture.Port.SentPayloads[0], cmdHi: 0x00, cmdLo: 0x15); // CONFIGURE
        AssertCommandIs(fixture.Port.SentPayloads[1], cmdHi: 0x00, cmdLo: 0x16); // START
    }

    [Fact]
    public async Task StartFastTelemetryAsync_AlreadyRunning_NoOp()
    {
        using var fixture = new Fixture();
        fixture.Service.UpdateSourceAddress(SourceRecipientId);
        await fixture.Service.StartFastTelemetryAsync();
        int sentBefore = fixture.Port.SentPayloads.Count;

        await fixture.Service.StartFastTelemetryAsync();

        Assert.Equal(sentBefore, fixture.Port.SentPayloads.Count);
    }

    [Fact]
    public async Task StopTelemetryAsync_WhenRunning_SendsStop()
    {
        using var fixture = new Fixture();
        fixture.Service.UpdateSourceAddress(SourceRecipientId);
        await fixture.Service.StartFastTelemetryAsync();
        fixture.Port.SentPayloads.Clear();

        await fixture.Service.StopTelemetryAsync();

        Assert.False(fixture.Service.IsRunning);
        Assert.Single(fixture.Port.SentPayloads);
        AssertCommandIs(fixture.Port.SentPayloads[0], cmdHi: 0x00, cmdLo: 0x17); // STOP
    }

    [Fact]
    public async Task StopTelemetryAsync_WhenStopped_NoOp()
    {
        using var fixture = new Fixture();

        await fixture.Service.StopTelemetryAsync();

        Assert.False(fixture.Service.IsRunning);
        Assert.Empty(fixture.Port.SentPayloads);
    }

    [Fact]
    public async Task StartFastTelemetryAsync_PayloadEncodesMyAddressSourceAddrAndVariables()
    {
        using var fixture = new Fixture();
        fixture.Service.UpdateSourceAddress(SourceRecipientId);
        fixture.Service.UpdateDictionary([VarUInt8, VarUInt32]);

        await fixture.Service.StartFastTelemetryAsync();

        // Estrae il payload applicativo dal frame BLE: [NetInfo(2) + recipient(4) + TP].
        // TP = [cryptFlag(1) + senderId_BE(4) + lPack_BE(2) + cmdHi + cmdLo + AL_payload + CRC(2)].
        var configureFrame = fixture.Port.SentPayloads[0];
        var alPayload = ExtractApplicationPayloadFromBle(configureFrame);
        // AL payload = [tipo(4) + destAddr_BE(4) + instance(1) + period_BE(2) + boardAddr_BE(4) + varAddr(2*2)]
        Assert.Equal(4 + 4 + 1 + 2 + 4 + 4, alPayload.Length);
        // tipo telemetria 0x00000000
        Assert.Equal(0, alPayload[0]); Assert.Equal(0, alPayload[1]);
        Assert.Equal(0, alPayload[2]); Assert.Equal(0, alPayload[3]);
        // destAddr BE = OwnRecipientId
        Assert.Equal(0xDE, alPayload[4]); Assert.Equal(0xAD, alPayload[5]);
        Assert.Equal(0xBE, alPayload[6]); Assert.Equal(0xEF, alPayload[7]);
        // instance
        Assert.Equal(0x00, alPayload[8]);
        // period 200ms = 0x00C8 BE
        Assert.Equal(0x00, alPayload[9]); Assert.Equal(0xC8, alPayload[10]);
        // boardAddr BE = SourceRecipientId
        Assert.Equal(0x00, alPayload[11]); Assert.Equal(0x08, alPayload[12]);
        Assert.Equal(0x03, alPayload[13]); Assert.Equal(0x81, alPayload[14]);
        // varAddrs: VarUInt8 (01,00) + VarUInt32 (01,02)
        Assert.Equal(0x01, alPayload[15]); Assert.Equal(0x00, alPayload[16]);
        Assert.Equal(0x01, alPayload[17]); Assert.Equal(0x02, alPayload[18]);
    }

    // --- Receive CMD_TELEMETRY_DATA ---

    [Fact]
    public async Task OnTelemetryData_WhenRunning_EmitsDataPointsForEachVariable()
    {
        using var fixture = new Fixture(receiverDecoderCommands: [CmdTelemetryData]);
        fixture.Service.UpdateSourceAddress(SourceRecipientId);
        fixture.Service.UpdateDictionary([VarUInt8, VarUInt16, VarUInt32]);
        await fixture.Service.StartFastTelemetryAsync();

        var samples = new List<TelemetryDataPoint>();
        fixture.Service.DataReceived += (_, dp) => samples.Add(dp);

        // Header 4 byte (tipo=0) + uint8 (1) + uint16 LE (2) + uint32 LE (4)
        byte[] alPayload =
        [
            0x00, 0x00, 0x00, 0x00,           // tipo telemetria
            0x42,                              // uint8_t = 0x42
            0xCD, 0xAB,                        // uint16_t LE = 0xABCD
            0xEF, 0xBE, 0xAD, 0xDE             // uint32_t LE = 0xDEADBEEF
        ];
        InjectTelemetryDataPacket(fixture, alPayload);

        Assert.Equal(3, samples.Count);
        Assert.Equal([0x42], samples[0].RawValue);
        Assert.Equal([0xCD, 0xAB], samples[1].RawValue);
        Assert.Equal([0xEF, 0xBE, 0xAD, 0xDE], samples[2].RawValue);
    }

    [Fact]
    public void OnTelemetryData_WhenStopped_Ignored()
    {
        using var fixture = new Fixture(receiverDecoderCommands: [CmdTelemetryData]);
        fixture.Service.UpdateDictionary([VarUInt8]);
        var samples = new List<TelemetryDataPoint>();
        fixture.Service.DataReceived += (_, dp) => samples.Add(dp);

        InjectTelemetryDataPacket(fixture, [0x00, 0x00, 0x00, 0x00, 0x42]);

        Assert.Empty(samples);
    }

    [Fact]
    public async Task OnTelemetryData_NonZeroHeader_Ignored()
    {
        using var fixture = new Fixture(receiverDecoderCommands: [CmdTelemetryData]);
        fixture.Service.UpdateDictionary([VarUInt8]);
        await fixture.Service.StartFastTelemetryAsync();

        var samples = new List<TelemetryDataPoint>();
        fixture.Service.DataReceived += (_, dp) => samples.Add(dp);

        // header[2] = 0x01 → tipo telemetria != 0
        InjectTelemetryDataPacket(fixture, [0x00, 0x00, 0x01, 0x00, 0x42]);

        Assert.Empty(samples);
    }

    [Fact]
    public async Task OnTelemetryData_UnknownDataType_VariableSkipped()
    {
        using var fixture = new Fixture(receiverDecoderCommands: [CmdTelemetryData]);
        var unknown = new Variable("Unknown", "02", "00", "float_t");
        fixture.Service.UpdateDictionary([unknown, VarUInt8]);
        await fixture.Service.StartFastTelemetryAsync();

        var samples = new List<TelemetryDataPoint>();
        fixture.Service.DataReceived += (_, dp) => samples.Add(dp);

        InjectTelemetryDataPacket(fixture, [0x00, 0x00, 0x00, 0x00, 0x42]);

        // unknown saltato (width 0), VarUInt8 emesso con il primo byte.
        Assert.Single(samples);
        Assert.Same(VarUInt8, samples[0].Variable);
        Assert.Equal([0x42], samples[0].RawValue);
    }

    [Fact]
    public async Task OnTelemetryData_PayloadShorterThanExpected_StopsAtBoundary()
    {
        using var fixture = new Fixture(receiverDecoderCommands: [CmdTelemetryData]);
        fixture.Service.UpdateDictionary([VarUInt8, VarUInt32]);
        await fixture.Service.StartFastTelemetryAsync();

        var samples = new List<TelemetryDataPoint>();
        fixture.Service.DataReceived += (_, dp) => samples.Add(dp);

        // Solo 1 byte dati, sufficiente per uint8 ma non per uint32 successivo
        InjectTelemetryDataPacket(fixture, [0x00, 0x00, 0x00, 0x00, 0x42]);

        Assert.Single(samples);
        Assert.Same(VarUInt8, samples[0].Variable);
    }

    [Fact]
    public async Task OnNonTelemetryCommand_Ignored()
    {
        var otherCommand = new Command("ReadVariable", "00", "01");
        using var fixture = new Fixture(receiverDecoderCommands: [otherCommand]);
        fixture.Service.UpdateDictionary([VarUInt8]);
        await fixture.Service.StartFastTelemetryAsync();

        var samples = new List<TelemetryDataPoint>();
        fixture.Service.DataReceived += (_, dp) => samples.Add(dp);

        // Inietta un pacchetto con cmd = 00 01 invece di 00 18
        var frame = BuildBleSingleChunkFrame(
            senderId: 0, recipientId: 0, command: otherCommand,
            alPayload: [0x00, 0x00]);
        fixture.Port.RaisePacketReceived(frame, DateTime.UtcNow);

        Assert.Empty(samples);
    }

    // --- Dispose ---

    [Fact]
    public async Task Dispose_UnsubscribesFromProtocol()
    {
        var fixture = new Fixture(receiverDecoderCommands: [CmdTelemetryData]);
        fixture.Service.UpdateDictionary([VarUInt8]);
        await fixture.Service.StartFastTelemetryAsync();

        var samples = new List<TelemetryDataPoint>();
        fixture.Service.DataReceived += (_, dp) => samples.Add(dp);

        fixture.Service.Dispose();

        InjectTelemetryDataPacket(fixture, [0x00, 0x00, 0x00, 0x00, 0x42]);

        Assert.Empty(samples);
        fixture.Dispose();
    }

    [Fact]
    public async Task AfterDispose_StartTelemetry_Throws()
    {
        var fixture = new Fixture();
        fixture.Service.Dispose();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => fixture.Service.StartFastTelemetryAsync());
        fixture.Dispose();
    }

    // --- Helpers ---

    private static void AssertCommandIs(byte[] bleFrame, byte cmdHi, byte cmdLo)
    {
        // BLE wire frame: [NetInfo(2) + recipient(4) + TP]
        // TP cmd a offset 6 + 7 = 13 (cmdHi), 14 (cmdLo)
        Assert.Equal(cmdHi, bleFrame[2 + 4 + 7]);
        Assert.Equal(cmdLo, bleFrame[2 + 4 + 8]);
    }

    private static byte[] ExtractApplicationPayloadFromBle(byte[] bleFrame)
    {
        // [NetInfo(2) + recipient(4) + cryptFlag(1) + senderId(4) + lPack(2) + cmdHi + cmdLo + AL + CRC(2)]
        const int alStart = 2 + 4 + 1 + 4 + 2 + 2;
        int alEnd = bleFrame.Length - 2;
        var result = new byte[alEnd - alStart];
        Buffer.BlockCopy(bleFrame, alStart, result, 0, result.Length);
        return result;
    }

    private static void InjectTelemetryDataPacket(Fixture fixture, byte[] alPayload)
    {
        var frame = BuildBleSingleChunkFrame(
            senderId: SourceRecipientId,
            recipientId: OwnRecipientId,
            command: CmdTelemetryData,
            alPayload: alPayload);
        fixture.Port.RaisePacketReceived(frame, DateTime.UtcNow);
    }

    private static byte[] BuildBleSingleChunkFrame(
        uint senderId, uint recipientId, Command command, byte[] alPayload)
    {
        using var senderPort = new FakeCommunicationPort(ChannelKind.Ble);
        var senderDecoder = new PacketDecoder([], [], []);
        using var senderSvc = new ProtocolService(senderPort, senderDecoder, senderId);
        // SendCommandAsync su FakeCommunicationPort è sincrona (Task.CompletedTask),
        // quindi attenderla con .Wait() è sicuro: nessun await reale, nessun deadlock.
#pragma warning disable xUnit1031
        senderSvc.SendCommandAsync(recipientId, command, alPayload).GetAwaiter().GetResult();
#pragma warning restore xUnit1031
        return senderPort.SentPayloads.Single();
    }

    /// <summary>
    /// Bundle stack di test: <see cref="ProtocolService"/> reale su BLE +
    /// <see cref="FakeCommunicationPort"/>. Permette di verificare encode (frame
    /// inviati a port.SentPayloads) e decode (RaisePacketReceived → service).
    /// </summary>
    private sealed class Fixture : IDisposable
    {
        public Fixture(IReadOnlyList<Command>? receiverDecoderCommands = null)
        {
            Port = new FakeCommunicationPort(ChannelKind.Ble);
            var decoder = new PacketDecoder(receiverDecoderCommands ?? [], [], []);
            Protocol = new ProtocolService(Port, decoder, OwnRecipientId);
            Service = new TelemetryService(Protocol);
        }

        public FakeCommunicationPort Port { get; }
        public ProtocolService Protocol { get; }
        public TelemetryService Service { get; }

        public void Dispose()
        {
            Service.Dispose();
            Protocol.Dispose();
            Port.Dispose();
        }
    }
}
