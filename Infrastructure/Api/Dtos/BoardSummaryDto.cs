namespace Infrastructure.Api.Dtos;

/// <summary>
/// Riepilogo board restituito da GET /api/devices/{id}/boards.
/// ProtocolAddress è la stringa hex (es. "0x00080381") usata per il matching con RecipientId.
/// </summary>
public record BoardSummaryDto(
    int Id,
    string Name,
    bool IsPrimary,
    int FirmwareType,
    int BoardNumber,
    string ProtocolAddress,
    int? DictionaryId,
    string? DictionaryName);
