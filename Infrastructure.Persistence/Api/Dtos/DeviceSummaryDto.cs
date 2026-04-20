namespace Infrastructure.Api.Dtos;

/// <summary>Riepilogo device restituito da GET /api/devices.</summary>
public record DeviceSummaryDto(
    int Id,
    string Name,
    int MachineCode,
    string? Description);
