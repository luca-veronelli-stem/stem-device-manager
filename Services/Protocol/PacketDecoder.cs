using System.Collections.Immutable;
using Core.Interfaces;
using Core.Models;

namespace Services.Protocol;

/// <summary>
/// Decoder puro di pacchetti applicativi STEM. Da un <see cref="RawPacket"/>
/// già riassemblato produce un <see cref="AppLayerDecodedEvent"/> basandosi
/// su uno snapshot immutabile del dizionario.
///
/// Struttura attesa del payload (post-riassembly):
/// <code>
/// byte 0    : cryptFlag           (Transport Layer)
/// byte 1..4 : senderId LE         (Transport Layer)
/// byte 5..6 : lPack               (Transport Layer, non letto)
/// byte 7    : cmdInit = codeHigh  (Application Layer)
/// byte 8    : cmdOpt  = codeLow   (Application Layer)
/// byte 9..N : payload applicativo
/// byte N+1..N+2 : CRC16 Modbus    (scartato, non validato)
/// </code>
///
/// Lo snapshot è sostituibile atomicamente tramite <see cref="UpdateDictionary"/>:
/// l'accesso avviene via <see cref="Volatile.Read"/>/<see cref="Volatile.Write"/>
/// per garantire coerenza senza lock.
/// </summary>
public sealed class PacketDecoder : IPacketDecoder
{
    private const int MinPayloadLength = 9;
    private const int CrcTailLength = 2;
    private const int ApplicationPayloadStart = 9;
    private const int CommandHighIndex = 7;
    private const int CommandLowIndex = 8;
    private const int VariableHighIndex = 9;
    private const int VariableLowIndex = 10;

    private DictionarySnapshot _snapshot;

    public PacketDecoder(
        IReadOnlyList<Command> commands,
        IReadOnlyList<Variable> variables,
        IReadOnlyList<ProtocolAddress> addresses)
    {
        _snapshot = Build(commands, variables, addresses);
    }

    /// <summary>
    /// Sostituisce atomicamente lo snapshot del dizionario.
    /// Thread-safe: un <see cref="Decode"/> in corso vede lo snapshot vecchio
    /// o il nuovo, mai uno intermedio.
    /// </summary>
    public void UpdateDictionary(
        IReadOnlyList<Command> commands,
        IReadOnlyList<Variable> variables,
        IReadOnlyList<ProtocolAddress> addresses)
    {
        Volatile.Write(ref _snapshot, Build(commands, variables, addresses));
    }

    /// <inheritdoc/>
    public AppLayerDecodedEvent? Decode(RawPacket packet)
    {
        if (packet.IsEmpty || packet.Length < MinPayloadLength) return null;
        var snapshot = Volatile.Read(ref _snapshot);
        var command = ResolveCommand(packet.Payload, snapshot);
        if (command is null) return null;
        var variable = ResolveVariable(command, packet.Payload, snapshot);
        var senderId = ReadSenderIdLittleEndian(packet.Payload);
        var sender = snapshot.FindSender(senderId);
        var appPayload = ExtractApplicationPayload(packet.Payload);
        return new AppLayerDecodedEvent(
            command,
            variable,
            appPayload,
            sender?.DeviceName ?? "",
            sender?.BoardName ?? "",
            senderId);
    }

    private static DictionarySnapshot Build(
        IReadOnlyList<Command> commands,
        IReadOnlyList<Variable> variables,
        IReadOnlyList<ProtocolAddress> addresses)
    {
        return new DictionarySnapshot(
            commands.ToImmutableArray(),
            variables.ToImmutableArray(),
            addresses.ToImmutableArray());
    }

    private static Command? ResolveCommand(
        ImmutableArray<byte> payload,
        DictionarySnapshot snapshot)
    {
        return snapshot.FindCommand(
            payload[CommandHighIndex],
            payload[CommandLowIndex]);
    }

    private static Variable? ResolveVariable(
        Command command,
        ImmutableArray<byte> payload,
        DictionarySnapshot snapshot)
    {
        if (!IsReadOrWriteVariable(command)) return null;
        if (payload.Length <= VariableLowIndex) return null;
        return snapshot.FindVariable(
            payload[VariableHighIndex],
            payload[VariableLowIndex]);
    }

    private static uint ReadSenderIdLittleEndian(ImmutableArray<byte> payload)
    {
        return payload[1]
             | ((uint)payload[2] << 8)
             | ((uint)payload[3] << 16)
             | ((uint)payload[4] << 24);
    }

    private static ImmutableArray<byte> ExtractApplicationPayload(
        ImmutableArray<byte> payload)
    {
        var end = payload.Length - CrcTailLength;
        if (end <= ApplicationPayloadStart) return ImmutableArray<byte>.Empty;
        var builder = ImmutableArray.CreateBuilder<byte>(end - ApplicationPayloadStart);
        for (int i = ApplicationPayloadStart; i < end; i++)
        {
            builder.Add(payload[i]);
        }
        return builder.MoveToImmutable();
    }

    private static bool IsReadOrWriteVariable(Command command)
    {
        return (command.CodeHigh == "00" || command.CodeHigh == "80")
            && (command.CodeLow == "01" || command.CodeLow == "02");
    }
}
