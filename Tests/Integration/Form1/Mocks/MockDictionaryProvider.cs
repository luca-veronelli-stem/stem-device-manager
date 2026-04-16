using Core.Interfaces;
using Core.Models;

namespace Tests.Integration.Form1.Mocks;

/// <summary>
/// Mock manuale di IDictionaryProvider per i test di integrazione di Form1.
/// Registra le chiamate ed espone dati configurabili.
/// </summary>
public class MockDictionaryProvider : IDictionaryProvider
{
    public int LoadProtocolDataAsyncCalls { get; private set; }
    public List<uint> LoadVariablesAsyncRecipientIds { get; } = new();

    public DictionaryData ProtocolDataToReturn { get; set; } = new(
        Array.Empty<ProtocolAddress>(),
        Array.Empty<Command>());

    public IReadOnlyList<Variable> VariablesToReturn { get; set; } =
        Array.Empty<Variable>();

    public Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct = default)
    {
        LoadProtocolDataAsyncCalls++;
        return Task.FromResult(ProtocolDataToReturn);
    }

    public Task<IReadOnlyList<Variable>> LoadVariablesAsync(
        uint recipientId, CancellationToken ct = default)
    {
        LoadVariablesAsyncRecipientIds.Add(recipientId);
        return Task.FromResult(VariablesToReturn);
    }
}
