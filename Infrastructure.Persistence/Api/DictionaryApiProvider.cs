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

    public DictionaryApiProvider(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _http = httpClient;
    }

    public async Task<DictionaryData> LoadProtocolDataAsync(
        CancellationToken ct = default)
    {
        // Carica tutti i device
        var devices = await _http.GetFromJsonAsync<List<DeviceSummaryDto>>(
            "api/devices", ct) ?? [];

        // Per ogni device, carica le board e raccogli gli indirizzi
        var addresses = new List<ProtocolAddress>();
        foreach (var device in devices)
        {
            var boards = await _http.GetFromJsonAsync<List<BoardSummaryDto>>(
                $"api/devices/{device.Id}/boards", ct) ?? [];

            foreach (var board in boards)
            {
                if (!string.IsNullOrWhiteSpace(board.ProtocolAddress))
                {
                    addresses.Add(new ProtocolAddress(
                        device.Name, board.Name, board.ProtocolAddress));
                }
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
    /// corrispondente al RecipientId hex.
    /// </summary>
    private async Task<BoardSummaryDto?> FindBoardByRecipientIdAsync(
        uint recipientId, CancellationToken ct)
    {
        var recipientHex = $"0x{recipientId:X8}";

        var devices = await _http.GetFromJsonAsync<List<DeviceSummaryDto>>(
            "api/devices", ct) ?? [];

        foreach (var device in devices)
        {
            var boards = await _http.GetFromJsonAsync<List<BoardSummaryDto>>(
                $"api/devices/{device.Id}/boards", ct) ?? [];

            var match = boards.FirstOrDefault(b =>
                string.Equals(b.ProtocolAddress, recipientHex,
                    StringComparison.OrdinalIgnoreCase));

            if (match is not null)
                return match;
        }

        return null;
    }
}
