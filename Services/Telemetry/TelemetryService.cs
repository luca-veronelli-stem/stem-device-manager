using System.Collections.Immutable;
using System.Globalization;
using Core.Interfaces;
using Core.Models;
using Services.Protocol;

namespace Services.Telemetry;

/// <summary>
/// Servizio di telemetria veloce. Implementa <see cref="ITelemetryService"/>
/// usando <see cref="ProtocolService"/> come facade per encode + I/O.
///
/// <para><b>Protocollo (parità con <c>App.STEMProtocol.TelemetryManager</c>):</b></para>
/// <list type="bullet">
/// <item><description><c>CMD_CONFIGURE_TELEMETRY (0x0015)</c> — payload: tipo(4) + destAddr_BE(4) + instance(1) + period_BE(2) + boardAddr_BE(4) + varAddrs(2*N)</description></item>
/// <item><description><c>CMD_START_TELEMETRY (0x0016)</c> — payload: <c>[instance]</c></description></item>
/// <item><description><c>CMD_STOP_TELEMETRY (0x0017)</c> — payload: <c>[instance]</c></description></item>
/// <item><description><c>CMD_TELEMETRY_DATA (0x0018)</c> — RX: header 4 byte (tipo telemetria, deve essere 0x00000000) + valori little-endian per ogni variabile in ordine, ampiezza da <see cref="Variable.DataType"/>.</description></item>
/// </list>
///
/// <para><b>Stato:</b> stopped/running, lista variabili correnti, RecipientId della
/// scheda sorgente. Tutte le mutazioni sono serializzate da <see cref="Lock"/>.
/// I pacchetti CMD_TELEMETRY_DATA arrivati a telemetria spenta sono ignorati.</para>
///
/// <para><b>Indirizzo destinazione:</b> letto da <see cref="ProtocolService.SenderId"/>
/// — è il nostro indirizzo, presso cui la scheda sorgente invia i pacchetti
/// di telemetria.</para>
/// </summary>
public sealed class TelemetryService : ITelemetryService, IDisposable
{
    private const string TelemetryDataCodeHigh = "00";
    private const string TelemetryDataCodeLow = "18";
    private const int TelemetryHeaderSize = 4;
    private const byte DefaultInstance = 0x00;
    private const ushort DefaultPeriodMs = 200;

    // Comandi fissi del protocollo telemetria. Non passano dal dizionario:
    // sono parte del contratto STEM, non variabili runtime.
    private static readonly Command CmdConfigureTelemetry =
        new("ConfigureTelemetry", "00", "15");
    private static readonly Command CmdStartTelemetry =
        new("StartTelemetry", "00", "16");
    private static readonly Command CmdStopTelemetry =
        new("StopTelemetry", "00", "17");

    private readonly ProtocolService _protocol;
    private readonly Lock _stateLock = new();
    private IReadOnlyList<Variable> _currentVariables = [];
    private uint _sourceRecipientId;
    private bool _isRunning;
    private bool _disposed;

    public TelemetryService(ProtocolService protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);
        _protocol = protocol;
        _protocol.AppLayerDecoded += OnAppLayerDecoded;
    }

    /// <inheritdoc/>
    public event EventHandler<TelemetryDataPoint>? DataReceived;

    /// <inheritdoc/>
    public bool IsRunning
    {
        get { lock (_stateLock) return _isRunning; }
    }

    /// <inheritdoc/>
    public uint SourceRecipientId
    {
        get { lock (_stateLock) return _sourceRecipientId; }
    }

    /// <inheritdoc/>
    public IReadOnlyList<Variable> CurrentVariables
    {
        get { lock (_stateLock) return _currentVariables; }
    }

    /// <inheritdoc/>
    public void UpdateDictionary(IReadOnlyList<Variable> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        lock (_stateLock) _currentVariables = [.. variables];
    }

    /// <inheritdoc/>
    public void UpdateSourceAddress(uint recipientId)
    {
        lock (_stateLock) _sourceRecipientId = recipientId;
    }

    /// <inheritdoc/>
    public async Task StartFastTelemetryAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        IReadOnlyList<Variable> variables;
        uint sourceAddr;
        lock (_stateLock)
        {
            if (_isRunning) return;
            variables = _currentVariables;
            sourceAddr = _sourceRecipientId;
            _isRunning = true;
        }

        var configurePayload = BuildConfigurePayload(variables, sourceAddr, _protocol.SenderId);
        await _protocol.SendCommandAsync(sourceAddr, CmdConfigureTelemetry, configurePayload, ct)
            .ConfigureAwait(false);
        await _protocol.SendCommandAsync(sourceAddr, CmdStartTelemetry, [DefaultInstance], ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task StopTelemetryAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        uint sourceAddr;
        lock (_stateLock)
        {
            if (!_isRunning) return;
            _isRunning = false;
            sourceAddr = _sourceRecipientId;
        }
        await _protocol.SendCommandAsync(sourceAddr, CmdStopTelemetry, [DefaultInstance], ct)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _protocol.AppLayerDecoded -= OnAppLayerDecoded;
    }

    private void OnAppLayerDecoded(object? sender, AppLayerDecodedEvent evt)
    {
        if (_disposed) return;
        if (!IsTelemetryDataCommand(evt.Command)) return;

        IReadOnlyList<Variable> variables;
        lock (_stateLock)
        {
            if (!_isRunning) return;
            variables = _currentVariables;
        }

        var payload = evt.Payload;
        if (!HasValidTelemetryHeader(payload)) return;

        EmitDataPoints(payload, variables);
    }

    private void EmitDataPoints(ImmutableArray<byte> payload, IReadOnlyList<Variable> variables)
    {
        var timestamp = DateTime.UtcNow;
        int pos = TelemetryHeaderSize;
        foreach (var variable in variables)
        {
            int width = DataTypeWidth(variable.DataType);
            if (width == 0) continue;
            if (pos + width > payload.Length) break;
            var rawValue = ImmutableArray.CreateBuilder<byte>(width);
            for (int i = 0; i < width; i++) rawValue.Add(payload[pos + i]);
            pos += width;
            DataReceived?.Invoke(this, new TelemetryDataPoint(
                variable, rawValue.MoveToImmutable(), timestamp));
        }
    }

    private static bool IsTelemetryDataCommand(Command command)
        => command.CodeHigh == TelemetryDataCodeHigh
        && command.CodeLow == TelemetryDataCodeLow;

    private static bool HasValidTelemetryHeader(ImmutableArray<byte> payload)
    {
        if (payload.Length < TelemetryHeaderSize) return false;
        for (int i = 0; i < TelemetryHeaderSize; i++)
        {
            if (payload[i] != 0x00) return false;
        }
        return true;
    }

    private static byte[] BuildConfigurePayload(
        IReadOnlyList<Variable> variables, uint sourceAddr, uint myAddress)
    {
        // [tipo(4) + destAddr_BE(4) + instance(1) + period_BE(2) + boardAddr_BE(4) + varAddrs(2*N)]
        var payload = new byte[4 + 4 + 1 + 2 + 4 + (variables.Count * 2)];
        // Tipo telemetria = 0x00000000 (default) — bytes 0..3 già a zero.
        WriteUInt32BigEndian(payload, 4, myAddress);
        payload[8] = DefaultInstance;
        payload[9] = (byte)((DefaultPeriodMs >> 8) & 0xFF);
        payload[10] = (byte)(DefaultPeriodMs & 0xFF);
        WriteUInt32BigEndian(payload, 11, sourceAddr);
        for (int i = 0; i < variables.Count; i++)
        {
            payload[15 + i * 2] = ParseHexByte(variables[i].AddressHigh);
            payload[16 + i * 2] = ParseHexByte(variables[i].AddressLow);
        }
        return payload;
    }

    private static void WriteUInt32BigEndian(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)((value >> 24) & 0xFF);
        buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
        buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
        buffer[offset + 3] = (byte)(value & 0xFF);
    }

    private static byte ParseHexByte(string hex)
        => byte.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static int DataTypeWidth(string? dataType) => dataType?.Trim() switch
    {
        "uint8_t" => 1,
        "uint16_t" => 2,
        "uint32_t" => 4,
        _ => 0
    };

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}
