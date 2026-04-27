using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Services.Boot;

/// <summary>
/// Phase of the SPARK batch update where a failure was observed. Used for
/// user-facing error reporting and structured logging.
/// </summary>
public enum SparkBatchPhase
{
    StartBoot,
    UploadBlocks,
    EndBoot,
    Restart
}

/// <summary>
/// One firmware area selected for batch update: the area itself and the
/// firmware bytes the user supplied for it.
/// </summary>
public sealed record SparkBatchItem(SparkFirmwareArea Area, byte[] Firmware);

/// <summary>
/// Progress of an area within the batch (re-emit of <see cref="IBootService.ProgressChanged"/>
/// tagged with the active area).
/// </summary>
public sealed record SparkAreaProgress(
    SparkFirmwareArea Area, int CurrentOffset, int TotalLength)
{
    public double Fraction => TotalLength <= 0 ? 0.0 : (double)CurrentOffset / TotalLength;
}

/// <summary>
/// Thrown by <see cref="SparkBatchUpdateService"/> on the first area that
/// fails. Carries the area and phase so the UI can surface a meaningful
/// message ("Area '<X>' failed at phase '<Y>': <cause>").
/// </summary>
public sealed class SparkBatchUpdateException : Exception
{
    public SparkBatchUpdateException(
        SparkAreaDefinition area, SparkBatchPhase phase, string cause, Exception? inner = null)
        : base($"Area '{area.DisplayName}' failed at phase '{phase}': {cause}", inner)
    {
        Area = area;
        Phase = phase;
        Cause = cause;
    }

    public SparkAreaDefinition Area { get; }
    public SparkBatchPhase Phase { get; }
    public string Cause { get; }
}

/// <summary>
/// Orchestrates a multi-area firmware update on a SPARK device. Drives the
/// granular <see cref="IBootService"/> steps for each selected area in the
/// canonical order (<see cref="SparkAreas.All"/>). Stops on the first
/// failure and throws <see cref="SparkBatchUpdateException"/>.
/// </summary>
public sealed class SparkBatchUpdateService
{
    private readonly IBootService _boot;
    private readonly ILogger<SparkBatchUpdateService> _logger;

    public SparkBatchUpdateService(
        IBootService boot, ILogger<SparkBatchUpdateService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(boot);
        _boot = boot;
        _logger = logger ?? NullLogger<SparkBatchUpdateService>.Instance;
    }

    /// <summary>Raised when an area is about to start uploading.</summary>
    public event EventHandler<SparkAreaDefinition>? AreaStarted;

    /// <summary>Raised on every block ack of the currently-uploading area.</summary>
    public event EventHandler<SparkAreaProgress>? AreaProgress;

    /// <summary>Raised when an area has completed successfully (after restart).</summary>
    public event EventHandler<SparkAreaDefinition>? AreaCompleted;

    /// <summary>
    /// Run the selected areas in canonical order. Each area runs
    /// <c>StartBoot → UploadBlocks → EndBoot</c>; <c>RESTART_MACHINE</c> is
    /// hoisted out of the per-area sequence and fires exactly once at the
    /// end of the batch, addressed to the HMI board (issue #74). On the
    /// first area failure, throws <see cref="SparkBatchUpdateException"/>;
    /// remaining areas are not attempted, and the end-of-batch restart is
    /// also skipped — the device is in a partially-flashed state and
    /// rebooting could leave it inconsistent. Recovery is operator-driven.
    /// </summary>
    public async Task ExecuteAsync(
        IReadOnlyList<SparkBatchItem> items, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0) return;

        foreach (var item in items)
        {
            if (item.Firmware.Length == 0)
            {
                var emptyDef = SparkAreas.Get(item.Area);
                _logger.LogError(
                    "SPARK batch rejected: empty firmware for area {Area}",
                    emptyDef.DisplayName);
                throw new ArgumentException(
                    $"Empty firmware for area '{emptyDef.DisplayName}'.",
                    nameof(items));
            }
        }

        var ordered = items
            .Select(i => (Item: i, Def: SparkAreas.Get(i.Area)))
            .OrderBy(x => x.Def.Order)
            .ToList();

        _logger.LogInformation(
            "SPARK batch start: {Count} areas selected ({Areas})",
            ordered.Count,
            string.Join(", ", ordered.Select(x => x.Def.DisplayName)));

        SparkAreaDefinition? currentArea = null;
        void OnBootProgress(object? _, BootProgress p)
        {
            if (currentArea is null) return;
            AreaProgress?.Invoke(this, new SparkAreaProgress(
                currentArea.Area, p.CurrentOffset, p.TotalLength));
        }

        _boot.ProgressChanged += OnBootProgress;
        try
        {
            foreach (var (item, def) in ordered)
            {
                ct.ThrowIfCancellationRequested();
                currentArea = def;
                AreaStarted?.Invoke(this, def);
                _logger.LogInformation(
                    "SPARK area start: {Area} ({Bytes} bytes) → recipient {Recipient:X8}",
                    def.DisplayName, item.Firmware.Length, def.RecipientId);

                await RunAreaAsync(item, def, ct).ConfigureAwait(false);

                AreaCompleted?.Invoke(this, def);
                _logger.LogInformation(
                    "SPARK area done: {Area}", def.DisplayName);
            }
        }
        finally
        {
            _boot.ProgressChanged -= OnBootProgress;
        }

        // End-of-batch restart, addressed to the HMI board (issue #74,
        // STEM firmware-team confirmation 2026-04-27). Only reached when
        // every area completed without throwing — on the abort path the
        // SparkBatchUpdateException propagates above and this line never
        // runs, so a half-flashed device is not auto-rebooted.
        await RestartAtEndOfBatchAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("SPARK batch end: all selected areas completed");
    }

    private async Task RunAreaAsync(
        SparkBatchItem item, SparkAreaDefinition def, CancellationToken ct)
    {
        await StartBootAsync(def, ct).ConfigureAwait(false);
        await UploadBlocksAsync(item, def, ct).ConfigureAwait(false);
        await EndBootAsync(def, ct).ConfigureAwait(false);
    }

    private async Task RestartAtEndOfBatchAsync(CancellationToken ct)
    {
        var hmiRecipient = SparkAreas.Get(SparkFirmwareArea.HmiApplication).RecipientId;
        _logger.LogInformation(
            "SPARK batch restart: addressing CMD_RESTART_MACHINE to HMI {Recipient:X8}",
            hmiRecipient);
        try
        {
            await _boot.RestartAsync(hmiRecipient, ct).ConfigureAwait(false);
        }
        catch (BootSessionLostException ex)
        {
            // I1: session lost on the final restart. Surface it as a Restart-phase
            // failure addressed to HMI (the recipient we just talked to).
            throw FailSessionLost(SparkAreas.Get(SparkFirmwareArea.HmiApplication), ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw Fail(
                SparkAreas.Get(SparkFirmwareArea.HmiApplication),
                SparkBatchPhase.Restart, ex.Message, ex);
        }
    }

    private async Task StartBootAsync(SparkAreaDefinition def, CancellationToken ct)
    {
        try
        {
            bool ok = await _boot.StartBootAsync(def.RecipientId, ct).ConfigureAwait(false);
            if (!ok)
                throw Fail(def, SparkBatchPhase.StartBoot, "no reply to CMD_START_PROCEDURE");
        }
        catch (BootSessionLostException ex)
        {
            throw FailSessionLost(def, ex);
        }
    }

    private async Task UploadBlocksAsync(
        SparkBatchItem item, SparkAreaDefinition def, CancellationToken ct)
    {
        try
        {
            await _boot.UploadBlocksOnlyAsync(item.Firmware, def.RecipientId, ct)
                .ConfigureAwait(false);
        }
        catch (BootSessionLostException ex)
        {
            throw FailSessionLost(def, ex);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw Fail(def, SparkBatchPhase.UploadBlocks, ex.Message, ex);
        }
        if (_boot.State == BootState.Failed)
            throw Fail(def, SparkBatchPhase.UploadBlocks,
                "block upload aborted (see logs for the failing block)");
    }

    private async Task EndBootAsync(SparkAreaDefinition def, CancellationToken ct)
    {
        try
        {
            bool ok = await _boot.EndBootAsync(def.RecipientId, ct).ConfigureAwait(false);
            if (!ok)
                throw Fail(def, SparkBatchPhase.EndBoot, "no reply to CMD_END_PROCEDURE");
        }
        catch (BootSessionLostException ex)
        {
            throw FailSessionLost(def, ex);
        }
    }

    private SparkBatchUpdateException Fail(
        SparkAreaDefinition def, SparkBatchPhase phase, string cause, Exception? inner = null)
    {
        _logger.LogError(
            "SPARK area failed: {Area} at phase {Phase}: {Cause}",
            def.DisplayName, phase, cause);
        return new SparkBatchUpdateException(def, phase, cause, inner);
    }

    /// <summary>
    /// Wraps a <see cref="BootSessionLostException"/> into the contract-mandated
    /// <c>SparkBatchUpdateException.Cause = "BLE session lost during &lt;phase&gt;"</c>
    /// (spec-001 boot-service.md I1).
    /// </summary>
    private SparkBatchUpdateException FailSessionLost(
        SparkAreaDefinition def, BootSessionLostException ex)
        => Fail(def, ex.Phase, $"BLE session lost during {ex.Phase}", ex);
}
