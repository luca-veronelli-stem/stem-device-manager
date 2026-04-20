using System.Collections.Immutable;
using Core.Models;
using Services.Protocol;

namespace Tests.Unit.Services.Protocol;

public class PacketDecoderTests
{
    private static readonly Command ReadVariable = new("ReadVariable", "00", "01");
    private static readonly Command WriteVariable = new("WriteVariable", "00", "02");
    private static readonly Command ReadVariableReply = new("ReadVariableReply", "80", "01");
    private static readonly Command ConfigureTelemetry = new("ConfigureTelemetry", "00", "15");
    private static readonly Variable Speed = new("Speed", "12", "34", "uint16_t");
    private static readonly ProtocolAddress EdenAddr = new("EDEN", "Madre", "00080381");

    [Fact]
    public void Decode_PacchettoVuoto_ReturnsNull()
    {
        var decoder = new PacketDecoder([ReadVariable], [], []);
        var empty = new RawPacket(ImmutableArray<byte>.Empty, DateTime.UtcNow);

        Assert.Null(decoder.Decode(empty));
    }

    [Fact]
    public void Decode_PayloadTroppoCorto_ReturnsNull()
    {
        var decoder = new PacketDecoder([ReadVariable], [], []);
        var shortPacket = new RawPacket(
            ImmutableArray.Create<byte>(0, 0, 0, 0, 0, 0, 0, 0), // 8 byte, min = 9
            DateTime.UtcNow);

        Assert.Null(decoder.Decode(shortPacket));
    }

    [Fact]
    public void Decode_ComandoSconosciuto_ReturnsNull()
    {
        var decoder = new PacketDecoder([ReadVariable], [], []);
        var packet = MakePacket(cmdHigh: 0xAA, cmdLow: 0xBB);

        Assert.Null(decoder.Decode(packet));
    }

    [Fact]
    public void Decode_ComandoNoto_PopolaCommand()
    {
        var decoder = new PacketDecoder([ConfigureTelemetry], [], []);
        var packet = MakePacket(cmdHigh: 0x00, cmdLow: 0x15);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Equal("ConfigureTelemetry", evt.Command.Name);
        Assert.Null(evt.Variable);
    }

    [Fact]
    public void Decode_ComandoReadVariable_RisolveVariable()
    {
        var decoder = new PacketDecoder([ReadVariable], [Speed], []);
        var packet = MakePacket(
            cmdHigh: 0x00, cmdLow: 0x01,
            applicationPayload: [0x12, 0x34]);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Same(ReadVariable, evt.Command);
        Assert.NotNull(evt.Variable);
        Assert.Equal("Speed", evt.Variable!.Name);
    }

    [Fact]
    public void Decode_ComandoWriteVariable_RisolveVariable()
    {
        var decoder = new PacketDecoder([WriteVariable], [Speed], []);
        var packet = MakePacket(
            cmdHigh: 0x00, cmdLow: 0x02,
            applicationPayload: [0x12, 0x34, 0x00, 0x64]);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Same(Speed, evt.Variable);
    }

    [Fact]
    public void Decode_RispostaReadVariable_RisolveVariable()
    {
        var decoder = new PacketDecoder([ReadVariableReply], [Speed], []);
        var packet = MakePacket(
            cmdHigh: 0x80, cmdLow: 0x01,
            applicationPayload: [0x12, 0x34, 0x00, 0x64]);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Same(Speed, evt.Variable);
    }

    [Fact]
    public void Decode_ComandoNonDiVariabile_LasciaVariableNull()
    {
        var decoder = new PacketDecoder([ConfigureTelemetry], [Speed], []);
        var packet = MakePacket(
            cmdHigh: 0x00, cmdLow: 0x15,
            applicationPayload: [0x12, 0x34]);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Null(evt.Variable);
    }

    [Fact]
    public void Decode_SenderIdRisolto_PopolaDeviceEBoard()
    {
        var decoder = new PacketDecoder([ReadVariable], [], [EdenAddr]);
        var packet = MakePacket(
            senderId: 0x00080381u,
            cmdHigh: 0x00, cmdLow: 0x01);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Equal("EDEN", evt.SenderDevice);
        Assert.Equal("Madre", evt.SenderBoard);
    }

    [Fact]
    public void Decode_SenderIdSconosciuto_LasciaStringheVuote()
    {
        var decoder = new PacketDecoder([ReadVariable], [], []);
        var packet = MakePacket(
            senderId: 0xDEADBEEFu,
            cmdHigh: 0x00, cmdLow: 0x01);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Equal("", evt.SenderDevice);
        Assert.Equal("", evt.SenderBoard);
    }

    [Fact]
    public void Decode_ApplicationPayloadSenzaCrc_CopiaByteFinoAMeno2()
    {
        var decoder = new PacketDecoder([ConfigureTelemetry], [], []);
        var packet = MakePacket(
            cmdHigh: 0x00, cmdLow: 0x15,
            applicationPayload: [0xAA, 0xBB, 0xCC]);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Equal([0xAA, 0xBB, 0xCC], evt.Payload.ToArray());
    }

    [Fact]
    public void Decode_IgnoraTimestamp_StessoOutputPerTimestampDiversi()
    {
        var decoder = new PacketDecoder([ConfigureTelemetry], [], []);
        var payload = MakePacketBytes(cmdHigh: 0x00, cmdLow: 0x15);
        var packet1 = new RawPacket(payload, new DateTime(2025, 1, 1));
        var packet2 = new RawPacket(payload, new DateTime(2030, 12, 31));

        Assert.Equal(decoder.Decode(packet1), decoder.Decode(packet2));
    }

    [Fact]
    public void Decode_ChiamatoDueVolte_Deterministico()
    {
        var decoder = new PacketDecoder([ConfigureTelemetry], [], []);
        var packet = MakePacket(cmdHigh: 0x00, cmdLow: 0x15);

        Assert.Equal(decoder.Decode(packet), decoder.Decode(packet));
    }

    [Fact]
    public void UpdateDictionary_RiconosceComandoAggiunto()
    {
        var decoder = new PacketDecoder([], [], []);
        var packet = MakePacket(cmdHigh: 0x00, cmdLow: 0x15);

        Assert.Null(decoder.Decode(packet));

        decoder.UpdateDictionary([ConfigureTelemetry], [], []);

        var evt = decoder.Decode(packet);
        Assert.NotNull(evt);
        Assert.Equal("ConfigureTelemetry", evt.Command.Name);
    }

    [Fact]
    public void UpdateDictionary_RimuoveRiconoscimentoPrecedente()
    {
        var decoder = new PacketDecoder([ConfigureTelemetry], [], []);
        var packet = MakePacket(cmdHigh: 0x00, cmdLow: 0x15);
        Assert.NotNull(decoder.Decode(packet));

        decoder.UpdateDictionary([], [], []);

        Assert.Null(decoder.Decode(packet));
    }

    [Fact]
    public void Decode_ReadVariableConPayloadTroppoCortoPerAddress_LasciaVariableNull()
    {
        var decoder = new PacketDecoder([ReadVariable], [Speed], []);
        // 9 byte minimi, nessun byte di address dopo cmd
        var packet = MakePacket(cmdHigh: 0x00, cmdLow: 0x01);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Null(evt.Variable);
    }

    [Fact]
    public void Decode_WriteVariableReply_ResolvesVariable()
    {
        // Comando 0x80 0x02 = risposta a WriteVariable (bit 7 del cmdHigh acceso)
        var writeReply = new Command("WriteVariableReply", "80", "02");
        var decoder = new PacketDecoder([writeReply], [Speed], []);
        var packet = MakePacket(
            cmdHigh: 0x80, cmdLow: 0x02,
            applicationPayload: [0x12, 0x34, 0x00]);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Same(Speed, evt.Variable);
    }

    [Fact]
    public void Decode_ReadVariableWithSingleByteAddress_LeavesVariableNull()
    {
        // Payload length = 10 byte totali = 1 byte dopo cmd (index 9 presente, index 10 NO).
        // Il decoder richiede entrambi i byte per risolvere la variabile.
        var decoder = new PacketDecoder([ReadVariable], [Speed], []);
        var packet = MakePacket(
            cmdHigh: 0x00, cmdLow: 0x01,
            applicationPayload: [0x12]);

        var evt = decoder.Decode(packet);

        Assert.NotNull(evt);
        Assert.Null(evt.Variable);
    }

    [Fact]
    public void Ctor_NullCommands_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PacketDecoder(null!, [], []));
    }

    [Fact]
    public void Ctor_NullVariables_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PacketDecoder([], null!, []));
    }

    [Fact]
    public void Ctor_NullAddresses_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PacketDecoder([], [], null!));
    }

    [Fact]
    public void UpdateDictionary_NullCommands_ThrowsArgumentNullException()
    {
        var decoder = new PacketDecoder([], [], []);

        Assert.Throws<ArgumentNullException>(
            () => decoder.UpdateDictionary(null!, [], []));
    }

    [Fact]
    public void UpdateDictionary_NullVariables_ThrowsArgumentNullException()
    {
        var decoder = new PacketDecoder([], [], []);

        Assert.Throws<ArgumentNullException>(
            () => decoder.UpdateDictionary([], null!, []));
    }

    [Fact]
    public void UpdateDictionary_NullAddresses_ThrowsArgumentNullException()
    {
        var decoder = new PacketDecoder([], [], []);

        Assert.Throws<ArgumentNullException>(
            () => decoder.UpdateDictionary([], [], null!));
    }

    [Fact]
    public async Task UpdateDictionary_ConcurrentWithDecode_NoExceptions()
    {
        // Stress probabilistico: molte Decode su N thread mentre un altro thread
        // sostituisce continuamente il dizionario. Il decoder deve vedere sempre
        // uno snapshot coerente (o vecchio o nuovo, mai intermedio) — nessuna
        // eccezione attesa.
        var dictA = new[] { new Command("A", "00", "01") };
        var dictB = new[] { new Command("B", "00", "02"), new Command("C", "00", "03") };
        var decoder = new PacketDecoder(dictA, [], []);
        var packetA = MakePacket(cmdHigh: 0x00, cmdLow: 0x01);
        var packetB = MakePacket(cmdHigh: 0x00, cmdLow: 0x02);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        var exceptions = new List<Exception>();
        var readers = Enumerable.Range(0, 4).Select(i => Task.Run(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    _ = decoder.Decode(packetA);
                    _ = decoder.Decode(packetB);
                }
            }
            catch (Exception ex) { lock (exceptions) exceptions.Add(ex); }
        })).ToArray();
        var writer = Task.Run(() =>
        {
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    decoder.UpdateDictionary(dictA, [], []);
                    decoder.UpdateDictionary(dictB, [], []);
                }
            }
            catch (Exception ex) { lock (exceptions) exceptions.Add(ex); }
        });

        await Task.WhenAll(readers.Append(writer));

        Assert.Empty(exceptions);
    }

    private static RawPacket MakePacket(
        uint senderId = 0u,
        byte cmdHigh = 0x00,
        byte cmdLow = 0x00,
        byte[]? applicationPayload = null)
    {
        return new RawPacket(
            MakePacketBytes(senderId, cmdHigh, cmdLow, applicationPayload),
            DateTime.UtcNow);
    }

    private static ImmutableArray<byte> MakePacketBytes(
        uint senderId = 0u,
        byte cmdHigh = 0x00,
        byte cmdLow = 0x00,
        byte[]? applicationPayload = null)
    {
        var app = applicationPayload ?? [];
        var builder = ImmutableArray.CreateBuilder<byte>(9 + app.Length + 2);
        builder.Add(0x00); // cryptFlag
        builder.Add((byte)(senderId & 0xFF));
        builder.Add((byte)((senderId >> 8) & 0xFF));
        builder.Add((byte)((senderId >> 16) & 0xFF));
        builder.Add((byte)((senderId >> 24) & 0xFF));
        builder.Add(0x00); // lPack hi
        builder.Add(0x00); // lPack lo
        builder.Add(cmdHigh);
        builder.Add(cmdLow);
        builder.AddRange(app);
        builder.Add(0xCC); // crc byte 1
        builder.Add(0xCD); // crc byte 2
        return builder.ToImmutable();
    }
}
