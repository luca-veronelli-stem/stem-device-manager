namespace Infrastructure.Api.Dtos;

/// <summary>Comando restituito da GET /api/commands.</summary>
public record CommandDto(
    int Id,
    string Name,
    int CodeHigh,
    int CodeLow,
    bool IsResponse,
    List<string>? Parameters);
