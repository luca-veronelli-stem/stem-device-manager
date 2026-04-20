using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Persistence;

/// <summary>
/// Decorator che tenta di usare il provider primario (API) e,
/// in caso di errore di rete, delega al fallback (Excel).
/// </summary>
public class FallbackDictionaryProvider : IDictionaryProvider
{
    private readonly IDictionaryProvider _primary;
    private readonly IDictionaryProvider _fallback;

    // TEMP: espone quale provider ha risposto per ultimo — rimuovere dopo testing
    public enum ProviderSource { Unknown, Primary, Fallback }
    public ProviderSource LastUsedSource { get; private set; } = ProviderSource.Unknown;
    public string? LastFallbackReason { get; private set; }
    // TEMP END

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
            var result = await _primary.LoadProtocolDataAsync(ct);
            LastUsedSource = ProviderSource.Primary; // TEMP
            return result;
        }
        catch (HttpRequestException ex)
        {
            LastUsedSource = ProviderSource.Fallback; // TEMP
            LastFallbackReason = ex.Message;          // TEMP
            return await _fallback.LoadProtocolDataAsync(ct);
        }
    }

    public async Task<IReadOnlyList<Variable>> LoadVariablesAsync(
        uint recipientId, CancellationToken ct = default)
    {
        try
        {
            var result = await _primary.LoadVariablesAsync(recipientId, ct);
            LastUsedSource = ProviderSource.Primary; // TEMP
            return result;
        }
        catch (HttpRequestException)
        {
            LastUsedSource = ProviderSource.Fallback; // TEMP
            return await _fallback.LoadVariablesAsync(recipientId, ct);
        }
    }
}
