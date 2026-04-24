using Core.Diagnostics;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Services.Boot;

/// <summary>
/// Firmware upload service. Implements <see cref="IBootService"/> using
/// <see cref="IProtocolService"/> as a facade.
///
/// <para><b>Upload sequence (parity with
/// <c>App.STEMProtocol.BootManager.UploadFirmware</c>):</b></para>
/// <list type="number">
/// <item><description><c>CMD_START_PROCEDURE (0x0005)</c> — wait reply, retry up to <see cref="RetryBudget"/>.</description></item>
/// <item><description>For each <see cref="FirmwareBlockSize"/>-byte block (last one padded to 0xFF):
/// payload = <c>[fwType_BE(2) + pageNum_BE(4) + pageSize_BE(4) + 0x00000000(4) + block(1024)]</c>.
/// <c>CMD_PROGRAM_BLOCK (0x0007)</c> with wait reply, retry up to <see cref="RetryBudget"/>.</description></item>
/// <item><description><c>CMD_END_PROCEDURE (0x0006)</c> — wait reply, retry up to <see cref="RetryBudget"/>.</description></item>
/// <item><description><c>CMD_RESTART_MACHINE (0x000A)</c> x <see cref="RestartCount"/> — fire-and-forget, interval <see cref="_restartInterval"/>.</description></item>
/// </list>
///
/// <para>The <c>fwType</c> is read from firmware bytes 14..15 little-endian
/// (parity with legacy <c>BootManager.SetFirmwarePath</c>).</para>
///
/// <para><b>Reply matching:</b> uses the STEM convention "top bit of CodeHigh = 1
/// means reply": e.g. <c>CMD_START_PROCEDURE (00 05)</c> waits for
/// <c>(80 05)</c>. The concrete <see cref="IProtocolService"/> implementation
/// must be built with an <see cref="IPacketDecoder"/> whose dictionary
/// contains the reply commands.</para>
///
/// <para><b>State machine:</b> Idle → Uploading → (Completed | Failed). Calling
/// <see cref="StartFirmwareUploadAsync"/> while the state is not Idle throws
/// <see cref="InvalidOperationException"/>.</para>
///
/// <para><b>spec-001 contract hooks</b> (see
/// <c>specs/001-spark-ble-fw-stabilize/contracts/boot-service.md</c>):</para>
/// <list type="bullet">
/// <item><description>Q1 — every waited step is bounded by <see cref="RetryBudget"/> (default <see cref="DefaultRetryBudget"/>, constructor-configurable, enforced inside <c>SendWithRetry</c>).</description></item>
/// <item><description>P3 — <see cref="CancellationToken"/> is observed at the top of each retry iteration and before each block send.</description></item>
/// <item><description>I1 — the optional <see cref="Func{TResult}"/> session-alive probe is evaluated every retry iteration; when it returns <c>false</c> the step aborts immediately with <see cref="BootSessionLostException"/> (transient reply timeouts still retry; only a lost BLE session short-circuits the loop).</description></item>
/// </list>
/// </summary>
public sealed class BootService : IBootService, IDisposable
{
    /// <summary>
    /// Default per-step retry budget (spec-001 R10/Q1). Applies independently to
    /// StartBoot, UploadBlocks, EndBoot. Configurable via the constructor so
    /// integration tests and diagnostic harnesses can shrink the budget.
    /// </summary>
    public const int DefaultRetryBudget = 3;

    private const int FirmwareBlockSize = 1024;
    private const int FwTypeOffsetLow = 14;
    private const int FwTypeOffsetHigh = 15;
    private const int MinFirmwareLength = 16;
    private const int RestartCount = 2;
    private const string ReplyCodeHigh = "80";
    private static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromMilliseconds(4000);
    private static readonly TimeSpan DefaultRestartInterval = TimeSpan.FromSeconds(1);
    private static readonly byte[] EmptyPayload = [];

    private static readonly Command CmdStartProcedure =
        new("StartProcedure", "00", "05");
    private static readonly Command CmdProgramBlock =
        new("ProgramBlock", "00", "07");
    private static readonly Command CmdEndProcedure =
        new("EndProcedure", "00", "06");
    private static readonly Command CmdRestartMachine =
        new("RestartMachine", "00", "0A");

    private readonly IProtocolService _protocol;
    private readonly ILogger<BootService> _logger;
    private readonly TimeSpan _responseTimeout;
    private readonly TimeSpan _restartInterval;
    private readonly int _retryBudget;
    private readonly Func<bool>? _isSessionAlive;
    private readonly Lock _stateLock = new();
    private BootState _state = BootState.Idle;
    private int _currentOffset;
    private int _totalLength;
    private bool _disposed;

    /// <summary>
    /// Production constructor. <paramref name="retryBudget"/> defaults to
    /// <see cref="DefaultRetryBudget"/> (spec-001 R10). <paramref name="isSessionAlive"/>
    /// is the optional I1 probe: when provided and it returns <c>false</c>
    /// mid-step, the step aborts with <see cref="BootSessionLostException"/>.
    /// </summary>
    public BootService(
        IProtocolService protocol,
        int retryBudget = DefaultRetryBudget,
        Func<bool>? isSessionAlive = null,
        ILogger<BootService>? logger = null)
        : this(
            protocol,
            DefaultResponseTimeout,
            DefaultRestartInterval,
            retryBudget,
            isSessionAlive,
            logger)
    { }

    /// <summary>
    /// Legacy convenience overload (kept for the <see cref="Cache.ConnectionManager"/>
    /// call site that passes only protocol + logger). Uses
    /// <see cref="DefaultRetryBudget"/> and no session probe.
    /// </summary>
    public BootService(IProtocolService protocol, ILogger<BootService>? logger)
        : this(protocol, DefaultRetryBudget, null, logger) { }

    /// <summary>
    /// Internal overload for tests: allows injecting tighter timeouts
    /// (e.g. 50 ms instead of 4000 ms), plus a custom retry budget and
    /// the I1 session-alive probe.
    /// </summary>
    internal BootService(
        IProtocolService protocol,
        TimeSpan responseTimeout,
        TimeSpan restartInterval,
        int retryBudget = DefaultRetryBudget,
        Func<bool>? isSessionAlive = null,
        ILogger<BootService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        if (retryBudget < 1)
            throw new ArgumentOutOfRangeException(
                nameof(retryBudget),
                retryBudget,
                "RetryBudget must be >= 1 (spec-001 R10).");
        _protocol = protocol;
        _logger = logger ?? NullLogger<BootService>.Instance;
        _responseTimeout = responseTimeout;
        _restartInterval = restartInterval;
        _retryBudget = retryBudget;
        _isSessionAlive = isSessionAlive;
    }

    /// <summary>
    /// Retry budget applied per waited step (StartBoot, UploadBlocks block,
    /// EndBoot). Set at construction time from the <c>retryBudget</c> ctor
    /// parameter; default <see cref="DefaultRetryBudget"/>.
    /// </summary>
    public int RetryBudget => _retryBudget;

    /// <inheritdoc/>
    public event EventHandler<BootProgress>? ProgressChanged;

    /// <inheritdoc/>
    public BootState State
    {
        get { lock (_stateLock) return _state; }
    }

    /// <inheritdoc/>
    public double Progress
    {
        get
        {
            lock (_stateLock)
            {
                return _totalLength <= 0 ? 0.0 : (double)_currentOffset / _totalLength;
            }
        }
    }

    /// <inheritdoc/>
    public async Task StartFirmwareUploadAsync(
        byte[] firmware, uint recipientId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ValidateFirmware(firmware);

        const string step = "StartFirmwareUpload";
        BeginUpload(firmware.Length, recipientId, step);
        try
        {
            await RunUploadSequence(firmware, recipientId, ct).ConfigureAwait(false);
            CompleteUpload(recipientId, step);
        }
        catch (OperationCanceledException)
        {
            FailUpload(recipientId, step);
            throw;
        }
        catch (BootSessionLostException)
        {
            // I1: session drop mid-step. Transition to Failed and rethrow so
            // SparkBatchUpdateService can wrap the cause as
            // "BLE session lost during <phase>" per contract.
            FailUpload(recipientId, step);
            throw;
        }
        catch (BootProtocolException)
        {
            // Protocol failure (reply timeout after RetryBudget exhausted) —
            // Failed state, no rethrow (parity with legacy BootManager that
            // showed a MessageBox and returned). Observability via State.
            FailUpload(recipientId, step);
        }
        catch
        {
            FailUpload(recipientId, step);
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<bool> StartBootAsync(uint recipientId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return SendWithRetry(
            recipientId, CmdStartProcedure, EmptyPayload,
            _retryBudget, SparkBatchPhase.StartBoot, ct);
    }

    /// <inheritdoc/>
    public Task<bool> EndBootAsync(uint recipientId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return SendWithRetry(
            recipientId, CmdEndProcedure, EmptyPayload,
            _retryBudget, SparkBatchPhase.EndBoot, ct);
    }

    /// <inheritdoc/>
    public Task RestartAsync(uint recipientId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        return _protocol.SendCommandAsync(recipientId, CmdRestartMachine, EmptyPayload, ct);
    }

    /// <inheritdoc/>
    public async Task UploadBlocksOnlyAsync(
        byte[] firmware, uint recipientId, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ValidateFirmware(firmware);

        const string step = "UploadBlocksOnly";
        BeginUpload(firmware.Length, recipientId, step);
        try
        {
            await SendAllBlocks(firmware, recipientId, ct).ConfigureAwait(false);
            CompleteUpload(recipientId, step);
        }
        catch (OperationCanceledException)
        {
            FailUpload(recipientId, step);
            throw;
        }
        catch (BootSessionLostException)
        {
            // I1: session drop mid-step. See StartFirmwareUploadAsync comment.
            FailUpload(recipientId, step);
            throw;
        }
        catch (BootProtocolException)
        {
            FailUpload(recipientId, step);
        }
        catch
        {
            FailUpload(recipientId, step);
            throw;
        }
    }

    private static void ValidateFirmware(byte[] firmware)
    {
        ArgumentNullException.ThrowIfNull(firmware);
        if (firmware.Length < MinFirmwareLength)
            throw new ArgumentException(
                $"Firmware troppo corto ({firmware.Length} byte): servono almeno {MinFirmwareLength} byte per estrarre fwType.",
                nameof(firmware));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ShutdownAuditHook.Record(nameof(BootService), "(self)");
    }

    private void BeginUpload(int totalLength, uint recipientId, string step)
    {
        BootState old;
        lock (_stateLock)
        {
            if (_state == BootState.Uploading)
                throw new InvalidOperationException(
                    "Upload firmware già in corso.");
            old = _state;
            _state = BootState.Uploading;
            _currentOffset = 0;
            _totalLength = totalLength;
        }
        LogTransition(old, BootState.Uploading, recipientId, step);
        EmitProgress(0, totalLength);
    }

    private void CompleteUpload(uint recipientId, string step)
    {
        BootState old;
        bool changed;
        lock (_stateLock)
        {
            old = _state;
            changed = old != BootState.Completed;
            _state = BootState.Completed;
        }
        if (changed) LogTransition(old, BootState.Completed, recipientId, step);
    }

    private void FailUpload(uint recipientId, string step)
    {
        BootState old;
        bool changed;
        lock (_stateLock)
        {
            old = _state;
            changed = old != BootState.Failed;
            _state = BootState.Failed;
        }
        if (changed) LogTransition(old, BootState.Failed, recipientId, step);
    }

    private void LogTransition(BootState old, BootState newState, uint recipientId, string step)
    {
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["Area"] = "Boot",
            ["Step"] = step,
            ["Attempt"] = 0,
            ["Recipient"] = recipientId
        });
        _logger.LogInformation("BootState {Old} -> {New}", old, newState);
    }

    private async Task RunUploadSequence(
        byte[] firmware, uint recipientId, CancellationToken ct)
    {
        await SendStartProcedure(recipientId, ct).ConfigureAwait(false);
        await SendAllBlocks(firmware, recipientId, ct).ConfigureAwait(false);
        await SendEndProcedure(recipientId, ct).ConfigureAwait(false);
        await SendRestartSequence(recipientId, ct).ConfigureAwait(false);
    }

    private async Task SendStartProcedure(uint recipientId, CancellationToken ct)
    {
        _logger.LogInformation(
            "Boot start: sending CMD_START_PROCEDURE to {Recipient:X8}", recipientId);
        bool ok = await SendWithRetry(
            recipientId, CmdStartProcedure, EmptyPayload,
            _retryBudget, SparkBatchPhase.StartBoot, ct).ConfigureAwait(false);
        if (!ok)
        {
            _logger.LogError(
                "Boot start failed: no reply from {Recipient:X8} after {Retries} retries",
                recipientId, _retryBudget);
            throw new BootProtocolException(
                $"Bootloader start failed (CMD_START_PROCEDURE, no reply after {_retryBudget} attempts).");
        }
    }

    private async Task SendAllBlocks(
        byte[] firmware, uint recipientId, CancellationToken ct)
    {
        ushort fwType = ExtractFwType(firmware);
        int totalBlocks = (firmware.Length + FirmwareBlockSize - 1) / FirmwareBlockSize;
        _logger.LogInformation(
            "Boot blocks: uploading {Bytes} bytes ({Blocks} blocks of {BlockSize}B) " +
            "fwType=0x{FwType:X4} to {Recipient:X8}",
            firmware.Length, totalBlocks, FirmwareBlockSize, fwType, recipientId);
        uint pageNum = 0;
        for (int offset = 0; offset < firmware.Length; offset += FirmwareBlockSize)
        {
            ct.ThrowIfCancellationRequested();
            var block = BuildPaddedBlock(firmware, offset);
            var payload = BuildProgramBlockPayload(fwType, pageNum, FirmwareBlockSize, block);
            bool ok = await SendWithRetry(
                recipientId, CmdProgramBlock, payload,
                _retryBudget, SparkBatchPhase.UploadBlocks, ct).ConfigureAwait(false);
            if (!ok)
            {
                _logger.LogError(
                    "Boot blocks failed: block {PageNum}/{TotalBlocks} not acknowledged " +
                    "after {Retries} retries (recipient {Recipient:X8})",
                    pageNum, totalBlocks, _retryBudget, recipientId);
                throw new BootProtocolException(
                    $"Block {pageNum} programming failed after {_retryBudget} attempts.");
            }
            LogBlockProgress(pageNum, totalBlocks);
            pageNum++;
            int newOffset = Math.Min(offset + FirmwareBlockSize, firmware.Length);
            lock (_stateLock) _currentOffset = newOffset;
            EmitProgress(newOffset, firmware.Length);
        }
        _logger.LogInformation(
            "Boot blocks: all {Blocks} blocks acknowledged by {Recipient:X8}",
            totalBlocks, recipientId);
    }

    private void LogBlockProgress(uint pageNum, int totalBlocks)
    {
        bool isFirst = pageNum == 0;
        bool isLast = pageNum == totalBlocks - 1;
        bool isMilestone = (pageNum + 1) % 16 == 0;
        if (!(isFirst || isLast || isMilestone)) return;
        _logger.LogDebug(
            "Boot blocks: ack {Done}/{Total}", pageNum + 1, totalBlocks);
    }

    private async Task SendEndProcedure(uint recipientId, CancellationToken ct)
    {
        _logger.LogInformation(
            "Boot end: sending CMD_END_PROCEDURE to {Recipient:X8}", recipientId);
        bool ok = await SendWithRetry(
            recipientId, CmdEndProcedure, EmptyPayload,
            _retryBudget, SparkBatchPhase.EndBoot, ct).ConfigureAwait(false);
        if (!ok)
        {
            _logger.LogError(
                "Boot end failed: no reply from {Recipient:X8} after {Retries} retries",
                recipientId, _retryBudget);
            throw new BootProtocolException(
                $"Bootloader end failed (CMD_END_PROCEDURE, no reply after {_retryBudget} attempts).");
        }
    }

    private async Task SendRestartSequence(uint recipientId, CancellationToken ct)
    {
        _logger.LogInformation(
            "Boot restart: sending CMD_RESTART_MACHINE x{Count} to {Recipient:X8}",
            RestartCount, recipientId);
        for (int i = 0; i < RestartCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            await _protocol.SendCommandAsync(recipientId, CmdRestartMachine, EmptyPayload, ct)
                .ConfigureAwait(false);
            if (i < RestartCount - 1)
                await Task.Delay(_restartInterval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Core retry loop. Honours the three spec-001 clauses in one place:
    /// <list type="bullet">
    /// <item><description>Q1 — iteration count is capped at <paramref name="retries"/> (always <see cref="_retryBudget"/> for the waited steps).</description></item>
    /// <item><description>P3 — <paramref name="ct"/> is observed at the top of every iteration.</description></item>
    /// <item><description>I1 — <see cref="_isSessionAlive"/> is evaluated every iteration; on <c>false</c> the loop short-circuits with <see cref="BootSessionLostException"/>, distinguishing a session drop from a transient reply timeout.</description></item>
    /// </list>
    /// Returns <c>true</c> on the first acknowledged attempt; returns
    /// <c>false</c> when the budget is exhausted with only transient
    /// (reply-timeout) failures; throws on cancellation or session loss.
    /// </summary>
    private async Task<bool> SendWithRetry(
        uint recipientId,
        Command command,
        byte[] payload,
        int retries,
        SparkBatchPhase phase,
        CancellationToken ct)
    {
        for (int i = 0; i < retries; i++)
        {
            // P3: cancellation observed at every iteration, before any I/O.
            ct.ThrowIfCancellationRequested();

            // I1: session-drop check — fail immediately, do NOT consume the
            // transient-error retry budget for something that is not transient.
            if (_isSessionAlive is not null && !_isSessionAlive())
                throw new BootSessionLostException(phase);

            using var scope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["Area"] = "Boot",
                ["Step"] = command.Name,
                ["Attempt"] = i + 1,
                ["Recipient"] = recipientId
            });
            if (i > 0)
                _logger.LogWarning("Retrying {Step}", command.Name);
            bool ok = await _protocol.SendCommandAndWaitReplyAsync(
                recipientId,
                command,
                payload,
                evt => MatchesReply(evt, command),
                _responseTimeout,
                ct).ConfigureAwait(false);
            if (ok) return true;
        }
        // Q1: budget exhausted. Return false so the caller can surface a
        // BootProtocolException with the explicit attempt count.
        return false;
    }

    private static bool MatchesReply(AppLayerDecodedEvent evt, Command request)
        => evt.Command.CodeHigh == ReplyCodeHigh
        && evt.Command.CodeLow == request.CodeLow;

    private static ushort ExtractFwType(byte[] firmware)
        => (ushort)((firmware[FwTypeOffsetHigh] << 8) | firmware[FwTypeOffsetLow]);

    private static byte[] BuildPaddedBlock(byte[] firmware, int offset)
    {
        var block = new byte[FirmwareBlockSize];
        Array.Fill(block, (byte)0xFF);
        int remaining = Math.Min(FirmwareBlockSize, firmware.Length - offset);
        Buffer.BlockCopy(firmware, offset, block, 0, remaining);
        return block;
    }

    private static byte[] BuildProgramBlockPayload(
        ushort fwType, uint pageNum, uint pageSize, byte[] block)
    {
        // [fwType_BE(2) + pageNum_BE(4) + pageSize_BE(4) + 0x00000000(4) + block(1024)]
        var payload = new byte[2 + 4 + 4 + 4 + block.Length];
        payload[0] = (byte)((fwType >> 8) & 0xFF);
        payload[1] = (byte)(fwType & 0xFF);
        WriteUInt32BigEndian(payload, 2, pageNum);
        WriteUInt32BigEndian(payload, 6, pageSize);
        // bytes 10..13 già 0x00000000
        Buffer.BlockCopy(block, 0, payload, 14, block.Length);
        return payload;
    }

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private void EmitProgress(int currentOffset, int totalLength)
        => ProgressChanged?.Invoke(this, new BootProgress(currentOffset, totalLength));

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// Internal exception used to propagate a protocol failure (reply timeout
/// after the retry budget is exhausted) up the <see cref="BootService"/>
/// stack to the catch that sets <see cref="BootState.Failed"/>.
/// </summary>
internal sealed class BootProtocolException : Exception
{
    public BootProtocolException(string message) : base(message) { }
}

/// <summary>
/// Raised by <see cref="BootService"/> when the I1 session-alive probe reports
/// that <c>ConnectionManager.ActiveProtocol</c> is no longer the one the boot
/// step was launched against (spec-001 boot-service.md I1). Distinct from
/// <see cref="BootProtocolException"/>: a session loss is NOT a transient
/// error and must not consume the retry budget — it short-circuits the loop.
/// </summary>
/// <remarks>
/// Callers that orchestrate multiple boot steps (e.g.
/// <c>SparkBatchUpdateService</c>) observe this type to build the contract-mandated
/// <c>SparkBatchUpdateException.Cause = "BLE session lost during &lt;phase&gt;"</c>.
/// </remarks>
public sealed class BootSessionLostException : Exception
{
    public BootSessionLostException(SparkBatchPhase phase)
        : base($"BLE session lost during {phase}.")
    {
        Phase = phase;
    }

    /// <summary>Phase at which the session loss was detected.</summary>
    public SparkBatchPhase Phase { get; }
}
