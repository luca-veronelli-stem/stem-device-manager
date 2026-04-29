using System.Net.Http.Json;
using Core.Interfaces;
using Core.Models;
using Infrastructure.Persistence.Api.Dtos;

namespace Infrastructure.Persistence.Api;

/// <summary>
/// Implementazione di IDictionaryProvider che chiama l'API REST di Stem.Dictionaries.Manager.
/// Autenticazione via header X-Api-Key. Richiede HttpClient configurato esternamente.
/// </summary>
public class DictionaryApiProvider : IDictionaryProvider
{
    private readonly HttpClient _http;

    // Cached device + board catalog. Lives for the lifetime of this provider
    // (registered as DI singleton, i.e. one app session). The catalog is
    // server-side metadata and does not change during a session, so there is
    // no invalidation API — restart the app to pick up server-side changes.
    private readonly SemaphoreSlim _catalogLock = new(1, 1);
    private List<(DeviceSummaryDto Device, BoardSummaryDto Board)>? _catalog;

    public DictionaryApiProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _http = httpClient;
    }

    public async Task<DictionaryData> LoadProtocolDataAsync(
        CancellationToken ct = default)
    {
        var catalog = await GetCatalogAsync(ct).ConfigureAwait(false);

        var addresses = new List<ProtocolAddress>();
        foreach (var (device, board) in catalog)
        {
            if (!string.IsNullOrWhiteSpace(board.ProtocolAddress))
            {
                addresses.Add(new ProtocolAddress(
                    device.Name, board.Name, board.ProtocolAddress));
            }
        }

        // Carica comandi globali
        var commandDtos = await _http.GetFromJsonAsync<List<CommandDto>>(
            "api/commands", ct) ?? [];

        var commands = commandDtos
            .Select(c => new Command(
                c.Name,
                c.CodeHigh.ToString("X2"),
                c.CodeLow.ToString("X2")))
            .ToList();

        return new DictionaryData(addresses, commands);
    }

    public async Task<IReadOnlyList<Variable>> LoadVariablesAsync(
        uint recipientId, CancellationToken ct = default)
    {
        // Trova la board con ProtocolAddress matching
        var board = await FindBoardByRecipientIdAsync(recipientId, ct);
        if (board?.DictionaryId is not int dictionaryId)
            return [];

        // Carica variabili risolte
        var resolved = await _http.GetFromJsonAsync<DictionaryResolvedDto>(
            $"api/dictionaries/{dictionaryId}/resolved", ct);

        if (resolved?.Variables is null)
            return [];

        return [.. resolved.Variables
            .Select(v => new Variable(
                v.Name,
                v.AddressHigh.ToString("X2"),
                v.AddressLow.ToString("X2"),
                v.DataType))];
    }

    /// <summary>
    /// Cerca tra tutti i device/board quella con ProtocolAddress
    /// corrispondente al RecipientId hex. Usa il catalog cached.
    /// </summary>
    private async Task<BoardSummaryDto?> FindBoardByRecipientIdAsync(
        uint recipientId, CancellationToken ct)
    {
        var recipientHex = $"0x{recipientId:X8}";
        var catalog = await GetCatalogAsync(ct).ConfigureAwait(false);

        foreach (var (_, board) in catalog)
        {
            if (string.Equals(board.ProtocolAddress, recipientHex,
                    StringComparison.OrdinalIgnoreCase))
            {
                return board;
            }
        }

        return null;
    }

    /// <summary>
    /// Loads the device + board catalog once and caches it for the lifetime
    /// of this provider. Concurrent first-time callers are serialized via a
    /// semaphore with a double-check so the fan-out runs exactly once.
    /// </summary>
    private async Task<IReadOnlyList<(DeviceSummaryDto Device, BoardSummaryDto Board)>>
        GetCatalogAsync(CancellationToken ct)
    {
        if (_catalog is not null) return _catalog;

        await _catalogLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_catalog is not null) return _catalog;

            var devices = await _http.GetFromJsonAsync<List<DeviceSummaryDto>>(
                "api/devices", ct).ConfigureAwait(false) ?? [];

            var boardTasks = devices
                .Select(d => _http.GetFromJsonAsync<List<BoardSummaryDto>>(
                    $"api/devices/{d.Id}/boards", ct))
                .ToArray();
            var boardLists = await Task.WhenAll(boardTasks).ConfigureAwait(false);

            var catalog = new List<(DeviceSummaryDto, BoardSummaryDto)>();
            for (var i = 0; i < devices.Count; i++)
            {
                foreach (var board in boardLists[i] ?? [])
                {
                    catalog.Add((devices[i], board));
                }
            }

            _catalog = catalog;
            return catalog;
        }
        finally
        {
            _catalogLock.Release();
        }
    }
}
