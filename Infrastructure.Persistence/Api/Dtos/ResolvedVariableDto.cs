namespace Infrastructure.Persistence.Api.Dtos;

/// <summary>Variabile risolta restituita dentro DictionaryResolvedDto.</summary>
public record ResolvedVariableDto(
    string Name,
    int AddressHigh,
    int AddressLow,
    string DataType,
    string? Access,
    string? Description,
    double? Min,
    double? Max,
    bool IsStandard);
