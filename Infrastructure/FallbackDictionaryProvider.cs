using Core.Interfaces;
using Core.Models;

namespace Infrastructure;

/// <summary>
/// Decorator che tenta di usare il provider primario (API) e,
/// in caso di errore di rete, delega al fallback (Excel).
/// </summary>
public class FallbackDictionaryProvider : IDictionaryProvider
{
    private readonly IDictionaryProvider _primary;
    private readonly IDictionaryProvider _fallback;

    public FallbackDictionaryProvider(
        IDictionaryProvider primary,
        IDictionaryProvider fallback)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(fallback);
        _primary = primary;
        _fallback = fallback;
    }

    public async Task<DictionaryData> LoadProtocolDataAsync(
        CancellationToken ct = default)
    {
        try
        {
            return await _primary.LoadProtocolDataAsync(ct);
        }
        catch (HttpRequestException)
        {
            return await _fallback.LoadProtocolDataAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Variable>> LoadVariablesAsync(
        uint recipientId, CancellationToken ct = default)
    {
        try
        {
            return await _primary.LoadVariablesAsync(recipientId, ct);
        }
        catch (HttpRequestException)
        {
            return await _fallback.LoadVariablesAsync(recipientId, ct);
        }
    }
}
