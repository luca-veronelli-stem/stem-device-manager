using Core.Models;

namespace Core.Interfaces;

/// <summary>
/// Servizio di telemetria veloce. Gestisce il polling di variabili dal device
/// e l'emissione di <see cref="TelemetryDataPoint"/>.
///
/// Implementazione concreta in Fase 2: <c>Services/Telemetry/TelemetryService</c>.
///
/// Formalizzazione: <c>Specs/Phase1/Interfaces.lean</c>.
/// </summary>
public interface ITelemetryService
{
    /// <summary>True se la telemetria è attiva.</summary>
    bool IsRunning { get; }

    /// <summary>RecipientId del device sorgente corrente.</summary>
    uint SourceRecipientId { get; }

    /// <summary>Variabili attualmente sotto monitoraggio.</summary>
    IReadOnlyList<Variable> CurrentVariables { get; }

    /// <summary>Nuovo campione di telemetria.</summary>
    event EventHandler<TelemetryDataPoint>? DataReceived;

    /// <summary>Avvia la telemetria veloce sulle variabili correnti.</summary>
    Task StartFastTelemetryAsync(CancellationToken ct = default);

    /// <summary>Ferma la telemetria.</summary>
    Task StopTelemetryAsync(CancellationToken ct = default);

    /// <summary>Aggiorna il dizionario delle variabili monitorate.</summary>
    void UpdateDictionary(IReadOnlyList<Variable> variables);

    /// <summary>Aggiorna il RecipientId del device sorgente.</summary>
    void UpdateSourceAddress(uint recipientId);
}
