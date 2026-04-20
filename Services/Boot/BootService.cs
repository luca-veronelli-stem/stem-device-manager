using Core.Interfaces;
using Core.Models;
using Services.Protocol;

namespace Services.Boot;

/// <summary>
/// Servizio di upload firmware. Implementa <see cref="IBootService"/> usando
/// <see cref="ProtocolService"/> come facade.
///
/// <para><b>Sequenza upload (parità con <c>App.STEMProtocol.BootManager.UploadFirmware</c>):</b></para>
/// <list type="number">
/// <item><description><c>CMD_START_PROCEDURE (0x0005)</c> — wait reply, retry <see cref="StartRetries"/>.</description></item>
/// <item><description>Per ogni blocco da <see cref="FirmwareBlockSize"/> byte (ultimo paddato a 0xFF):
/// payload = <c>[fwType_BE(2) + pageNum_BE(4) + pageSize_BE(4) + 0x00000000(4) + block(1024)]</c>.
/// <c>CMD_PROGRAM_BLOCK (0x0007)</c> con wait reply, retry <see cref="BlockRetries"/>.</description></item>
/// <item><description><c>CMD_END_PROCEDURE (0x0006)</c> — wait reply, retry <see cref="EndRetries"/>.</description></item>
/// <item><description><c>CMD_RESTART_MACHINE (0x000A)</c> x <see cref="RestartCount"/> — fire-and-forget, intervallo <see cref="RestartInterval"/>.</description></item>
/// </list>
///
/// <para>Il <c>fwType</c> viene letto dai byte 14..15 del firmware in
/// little-endian (parità col legacy <c>BootManager.SetFirmwarePath</c>).</para>
///
/// <para><b>Reply matching:</b> usa la convenzione STEM "bit alto di CodeHigh = 1
/// indica risposta": ad esempio <c>CMD_START_PROCEDURE (00 05)</c> attende
/// <c>(80 05)</c>. Il <see cref="ProtocolService"/> deve essere costruito con un
/// <see cref="IPacketDecoder"/> il cui dizionario contiene i comandi di reply.</para>
///
/// <para><b>State machine:</b> Idle → Uploading → (Completed | Failed). Una nuova
/// chiamata a <see cref="StartFirmwareUploadAsync"/> mentre lo stato non è Idle
/// lancia <see cref="InvalidOperationException"/>.</para>
/// </summary>
public sealed class BootService : IBootService, IDisposable
{
    private const int FirmwareBlockSize = 1024;
    private const int FwTypeOffsetLow = 14;
    private const int FwTypeOffsetHigh = 15;
    private const int MinFirmwareLength = 16;
    private const int StartRetries = 1;
    private const int BlockRetries = 10;
    private const int EndRetries = 5;
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

    private readonly ProtocolService _protocol;
    private readonly TimeSpan _responseTimeout;
    private readonly TimeSpan _restartInterval;
    private readonly Lock _stateLock = new();
    private BootState _state = BootState.Idle;
    private int _currentOffset;
    private int _totalLength;
    private bool _disposed;

    public BootService(ProtocolService protocol)
        : this(protocol, DefaultResponseTimeout, DefaultRestartInterval) { }

    /// <summary>
    /// Overload interno per i test: consente di iniettare timeout più aggressivi
    /// (es. 50ms invece di 4000ms) per accelerare le suite.
    /// </summary>
    internal BootService(
        ProtocolService protocol, TimeSpan responseTimeout, TimeSpan restartInterval)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        _protocol = protocol;
        _responseTimeout = responseTimeout;
        _restartInterval = restartInterval;
    }

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
        ArgumentNullException.ThrowIfNull(firmware);
        if (firmware.Length < MinFirmwareLength)
            throw new ArgumentException(
                $"Firmware troppo corto ({firmware.Length} byte): servono almeno {MinFirmwareLength} byte per estrarre fwType.",
                nameof(firmware));

        BeginUpload(firmware.Length);
        try
        {
            await RunUploadSequence(firmware, recipientId, ct).ConfigureAwait(false);
            CompleteUpload();
        }
        catch (OperationCanceledException)
        {
            FailUpload();
            throw;
        }
        catch (BootProtocolException)
        {
            // Fallimento di protocollo (timeout reply) — stato Failed, niente
            // rethrow (parità col legacy BootManager che mostrava MessageBox e
            // ritornava). L'osservabilità del fallimento avviene tramite State.
            FailUpload();
        }
        catch
        {
            FailUpload();
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    private void BeginUpload(int totalLength)
    {
        lock (_stateLock)
        {
            if (_state == BootState.Uploading)
                throw new InvalidOperationException(
                    "Upload firmware già in corso.");
            _state = BootState.Uploading;
            _currentOffset = 0;
            _totalLength = totalLength;
        }
        EmitProgress(0, totalLength);
    }

    private void CompleteUpload()
    {
        lock (_stateLock) _state = BootState.Completed;
    }

    private void FailUpload()
    {
        lock (_stateLock) _state = BootState.Failed;
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
        if (!await SendWithRetry(recipientId, CmdStartProcedure, EmptyPayload, StartRetries, ct))
            throw new BootProtocolException(
                "Avvio procedura bootloader fallito (CMD_START_PROCEDURE senza reply).");
    }

    private async Task SendAllBlocks(
        byte[] firmware, uint recipientId, CancellationToken ct)
    {
        ushort fwType = ExtractFwType(firmware);
        uint pageNum = 0;
        for (int offset = 0; offset < firmware.Length; offset += FirmwareBlockSize)
        {
            ct.ThrowIfCancellationRequested();
            var block = BuildPaddedBlock(firmware, offset);
            var payload = BuildProgramBlockPayload(fwType, pageNum, FirmwareBlockSize, block);
            if (!await SendWithRetry(recipientId, CmdProgramBlock, payload, BlockRetries, ct))
                throw new BootProtocolException(
                    $"Programmazione blocco {pageNum} fallita dopo {BlockRetries} tentativi.");
            pageNum++;
            int newOffset = Math.Min(offset + FirmwareBlockSize, firmware.Length);
            lock (_stateLock) _currentOffset = newOffset;
            EmitProgress(newOffset, firmware.Length);
        }
    }

    private async Task SendEndProcedure(uint recipientId, CancellationToken ct)
    {
        if (!await SendWithRetry(recipientId, CmdEndProcedure, EmptyPayload, EndRetries, ct))
            throw new BootProtocolException(
                "Chiusura procedura bootloader fallita (CMD_END_PROCEDURE senza reply).");
    }

    private async Task SendRestartSequence(uint recipientId, CancellationToken ct)
    {
        for (int i = 0; i < RestartCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            await _protocol.SendCommandAsync(recipientId, CmdRestartMachine, EmptyPayload, ct)
                .ConfigureAwait(false);
            if (i < RestartCount - 1)
                await Task.Delay(_restartInterval, ct).ConfigureAwait(false);
        }
    }

    private async Task<bool> SendWithRetry(
        uint recipientId, Command command, byte[] payload, int retries, CancellationToken ct)
    {
        for (int i = 0; i < retries; i++)
        {
            ct.ThrowIfCancellationRequested();
            bool ok = await _protocol.SendCommandAndWaitReplyAsync(
                recipientId,
                command,
                payload,
                evt => MatchesReply(evt, command),
                _responseTimeout,
                ct).ConfigureAwait(false);
            if (ok) return true;
        }
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
/// Eccezione interna usata per propagare un fallimento di protocollo
/// (timeout reply esaurito) attraverso lo stack di <see cref="BootService"/>
/// fino al catch che imposta <see cref="BootState.Failed"/>.
/// </summary>
internal sealed class BootProtocolException : Exception
{
    public BootProtocolException(string message) : base(message) { }
}
