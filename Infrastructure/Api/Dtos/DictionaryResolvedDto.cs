namespace Infrastructure.Api.Dtos;

/// <summary>
/// Dizionario risolto restituito da GET /api/dictionaries/{id}/resolved.
/// Contiene la lista di variabili standard + specifiche già unite.
/// </summary>
public record DictionaryResolvedDto(
    int Id,
    string Name,
    string? Description,
    List<ResolvedVariableDto> Variables);
