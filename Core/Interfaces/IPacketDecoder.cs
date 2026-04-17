using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Decoder puro: da <see cref="RawPacket"/> produce un <see cref="AppLayerDecodedEvent"/>.
/// Non ha stato né side effect, deve essere deterministico.
///
/// Implementazione concreta in Fase 2: <c>Services/Protocol/PacketDecoder</c>.
///
/// Formalizzazione: <c>Specs/Phase1/Interfaces.lean</c> (teorema
/// <c>decoder_is_deterministic</c>).
/// </summary>
public interface IPacketDecoder
{
    /// <summary>
    /// Decodifica un pacchetto raw. Restituisce <c>null</c> se il pacchetto
    /// non è riconoscibile o non appartiene a un comando noto.
    /// </summary>
    AppLayerDecodedEvent? Decode(RawPacket packet);
}
