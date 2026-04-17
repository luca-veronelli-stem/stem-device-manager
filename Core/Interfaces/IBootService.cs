namespace Core.Interfaces;

/// <summary>
/// Servizio di upload firmware. Macchina a stati:
/// <see cref="BootState.Idle"/> → <see cref="BootState.Uploading"/> →
/// <see cref="BootState.Completed"/> oppure <see cref="BootState.Failed"/>.
///
/// Implementazione concreta in Fase 2: <c>Services/Boot/BootService</c>.
///
/// Formalizzazione: <c>Specs/Phase1/Interfaces.lean</c>.
/// </summary>
public interface IBootService
{
    /// <summary>Stato corrente del servizio di boot.</summary>
    BootState State { get; }

    /// <summary>Progresso corrente (0.0 – 1.0). In <see cref="BootState.Idle"/> è 0.</summary>
    double Progress { get; }

    /// <summary>Progresso di upload.</summary>
    event EventHandler<BootProgress>? ProgressChanged;

    /// <summary>
    /// Avvia l'upload di un firmware. Il binario deve essere già interamente in memoria.
    /// </summary>
    Task StartFirmwareUploadAsync(byte[] firmware, CancellationToken ct = default);
}

/// <summary>Stato della macchina a stati di <see cref="IBootService"/>.</summary>
public enum BootState
{
    Idle,
    Uploading,
    Completed,
    Failed
}

/// <summary>Snapshot di progresso dell'upload firmware.</summary>
/// <param name="CurrentOffset">Byte già inviati.</param>
/// <param name="TotalLength">Byte totali del firmware.</param>
public readonly record struct BootProgress(int CurrentOffset, int TotalLength)
{
    /// <summary>Frazione [0, 1] di completamento.</summary>
    public double Fraction => TotalLength <= 0 ? 0.0 : (double)CurrentOffset / TotalLength;
}
