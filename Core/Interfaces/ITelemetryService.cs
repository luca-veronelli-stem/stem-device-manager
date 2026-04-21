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
    /// <summary>True se la telemetria è attiva (fast stream o one-shot in corso).</summary>
    bool IsRunning { get; }

    /// <summary>RecipientId del device sorgente corrente.</summary>
    uint SourceRecipientId { get; }

    /// <summary>Variabili attualmente sotto monitoraggio.</summary>
    IReadOnlyList<Variable> CurrentVariables { get; }

    /// <summary>Nuovo campione di telemetria.</summary>
    event EventHandler<TelemetryDataPoint>? DataReceived;

    /// <summary>Avvia la telemetria veloce sulle variabili correnti.</summary>
    Task StartFastTelemetryAsync(CancellationToken ct = default);

    /// <summary>Ferma la telemetria (fast stream o one-shot).</summary>
    Task StopTelemetryAsync(CancellationToken ct = default);

    /// <summary>
    /// Esegue un giro di letture one-shot: invia <c>CMD_READ_VARIABLE</c>
    /// per ciascuna variabile in <see cref="CurrentVariables"/>, ~150ms
    /// tra una richiesta e l'altra. Le risposte vengono decodificate e emesse
    /// via <see cref="DataReceived"/> con <see cref="TelemetrySource.ReadReply"/>.
    /// Ritorna al completamento del giro.
    /// </summary>
    Task ReadOneShotAsync(CancellationToken ct = default);

    /// <summary>
    /// Esegue un giro di scritture one-shot: invia <c>CMD_WRITE_VARIABLE</c>
    /// per ciascuna variabile in <see cref="CurrentVariables"/> usando i valori
    /// accumulati da <see cref="AddToDictionaryForWrite"/>. Valori non parseabili
    /// (fuori range 0..32767) sono saltati silenziosamente.
    /// </summary>
    Task WriteOneShotAsync(CancellationToken ct = default);

    /// <summary>Sostituisce il dizionario delle variabili monitorate.</summary>
    void UpdateDictionary(IReadOnlyList<Variable> variables);

    /// <summary>Aggiunge una variabile al dizionario corrente (accumulo).</summary>
    void AddToDictionary(Variable variable);

    /// <summary>
    /// Aggiunge una variabile e il valore testuale (uint16) da scrivere con
    /// <see cref="WriteOneShotAsync"/>. Le due liste sono parallele per indice.
    /// </summary>
    void AddToDictionaryForWrite(Variable variable, string valueText);

    /// <summary>Rimuove la variabile all'indice indicato dal dizionario corrente.</summary>
    void RemoveFromDictionary(int index);

    /// <summary>Svuota il dizionario e la lista dei valori da scrivere.</summary>
    void ResetDictionary();

    /// <summary>Ritorna il nome della variabile all'indice indicato, o stringa diagnostica se fuori range.</summary>
    string GetVariableName(int index);

    /// <summary>Aggiorna il RecipientId del device sorgente.</summary>
    void UpdateSourceAddress(uint recipientId);
}
