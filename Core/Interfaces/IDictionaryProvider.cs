using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Astrazione per l'accesso ai dati dizionario (variabili, comandi, indirizzi protocollo).
/// Implementata da DictionaryApiProvider (API Azure) e ExcelDictionaryProvider (fallback Excel).
/// </summary>
public interface IDictionaryProvider
{
    /// <summary>Carica indirizzi protocollo e comandi globali.</summary>
    Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct = default);

    /// <summary>Carica le variabili per un indirizzo protocollo specifico (RecipientId).</summary>
    Task<IReadOnlyList<Variable>> LoadVariablesAsync(
        uint recipientId, CancellationToken ct = default);
}
