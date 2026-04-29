namespace Core.Interfaces;

/// <summary>
/// Servizio di upload firmware. Macchina a stati:
/// <see cref="BootState.Idle"/> → <see cref="BootState.Uploading"/> →
/// <see cref="BootState.Completed"/> oppure <see cref="BootState.Failed"/>.
///
/// Implementazione concreta in Fase 2: <c>Services/Boot/BootService</c>.
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
    /// Sequenza completa di upload: START → blocchi da 1024B → END → RESTART x2.
    /// Stato passa da <see cref="BootState.Idle"/> a <see cref="BootState.Uploading"/>
    /// a <see cref="BootState.Completed"/> o <see cref="BootState.Failed"/>.
    /// </summary>
    /// <param name="firmware">Bytes del firmware da inviare.</param>
    /// <param name="recipientId">RecipientId del device target dell'aggiornamento.</param>
    /// <param name="ct">Token di cancellazione (rispettato fra blocchi).</param>
    Task StartFirmwareUploadAsync(byte[] firmware, uint recipientId, CancellationToken ct = default);

    /// <summary>
    /// Invia solo <c>CMD_START_PROCEDURE</c> e attende reply.
    /// Ritorna <c>true</c> se la reply arriva in tempo, <c>false</c> su timeout.
    /// Non modifica <see cref="State"/>.
    /// </summary>
    Task<bool> StartBootAsync(uint recipientId, CancellationToken ct = default);

    /// <summary>
    /// Invia solo <c>CMD_END_PROCEDURE</c> e attende reply.
    /// Ritorna <c>true</c> se la reply arriva in tempo, <c>false</c> su timeout.
    /// </summary>
    Task<bool> EndBootAsync(uint recipientId, CancellationToken ct = default);

    /// <summary>
    /// Invia <c>CMD_RESTART_MACHINE</c> fire-and-forget (no wait reply).
    /// </summary>
    Task RestartAsync(uint recipientId, CancellationToken ct = default);

    /// <summary>
    /// Invia solo i blocchi <c>CMD_PROGRAM_BLOCK</c>, senza START/END/RESTART.
    /// Usato per workflow Egicon multi-step in cui i comandi di contorno sono gestiti
    /// separatamente dai pulsanti StartBoot/EndBoot/Restart.
    /// Stato passa da <see cref="BootState.Idle"/> a <see cref="BootState.Uploading"/>
    /// a <see cref="BootState.Completed"/> o <see cref="BootState.Failed"/>.
    /// </summary>
    Task UploadBlocksOnlyAsync(byte[] firmware, uint recipientId, CancellationToken ct = default);
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
