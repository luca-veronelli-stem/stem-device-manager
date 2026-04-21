using System.Collections.Immutable;
using System.Globalization;
using Core.Interfaces;
using Core.Models;

namespace Services.Telemetry;

/// <summary>
/// Servizio di telemetria. Implementa <see cref="ITelemetryService"/> usando
/// <see cref="IProtocolService"/> come facade per encode + I/O.
///
/// <para><b>Protocollo (parità con <c>App.STEMProtocol.TelemetryManager</c>):</b></para>
/// <list type="bullet">
/// <item><description><c>CMD_READ_VARIABLE (0x0001)</c> — fire: <c>[AddrH, AddrL]</c>. Reply: <c>CMD 80 01</c>, <c>[80,01,AddrH,AddrL, value_BE...]</c>.</description></item>
/// <item><description><c>CMD_WRITE_VARIABLE (0x0002)</c> — fire: <c>[AddrH, AddrL, value_BE...]</c>.</description></item>
/// <item><description><c>CMD_CONFIGURE_TELEMETRY (0x0015)</c> — fast: tipo(4) + destAddr_BE(4) + instance(1) + period_BE(2) + boardAddr_BE(4) + varAddrs(2*N).</description></item>
/// <item><description><c>CMD_START_TELEMETRY (0x0016)</c> — fast: <c>[instance]</c>.</description></item>
/// <item><description><c>CMD_STOP_TELEMETRY (0x0017)</c> — fast: <c>[instance]</c>.</description></item>
/// <item><description><c>CMD_TELEMETRY_DATA (0x0018)</c> — RX: header 4 byte zero + valori little-endian per ogni variabile in ordine.</description></item>
/// </list>
///
/// <para><b>Stato:</b> dizionario mutabile (<see cref="AddToDictionary"/> /
/// <see cref="RemoveFromDictionary"/> / <see cref="ResetDictionary"/>), lista valori
/// per write in parallelo, running flag, sourceRecipientId. Serializzati via <see cref="Lock"/>.</para>
///
/// <para><b>DataReceived:</b> emesso per ogni campione decodificato, sia da fast
/// stream (<see cref="TelemetrySource.FastStream"/>, LE) sia da read reply
/// (<see cref="TelemetrySource.ReadReply"/>, BE). I pacchetti di fast stream
/// arrivati a <see cref="IsRunning"/> = false sono ignorati; le reply di read invece
/// sono sempre processate se la variabile corrispondente è nel dizionario.</para>
/// </summary>
public sealed class TelemetryService : ITelemetryService, IDisposable
{
    private const string TelemetryDataCodeHigh = "00";
    private const string TelemetryDataCodeLow = "18";
    private const string ReadReplyCodeHigh = "80";
    private const string ReadReplyCodeLow = "01";
    private const int TelemetryHeaderSize = 4;
    private const int ReadReplyHeaderSize = 4; // [cmdHi, cmdLo, AddrH, AddrL]
    private const byte DefaultInstance = 0x00;
    private const ushort DefaultPeriodMs = 200;
    private const int OneShotDelayMs = 150;
    private const ushort WriteValueMax = 32767;

    // Comandi fissi del protocollo. Non passano dal dizionario: sono parte del
    // contratto STEM, non variabili runtime.
    private static readonly Command CmdReadVariable =
        new("ReadVariable", "00", "01");
    private static readonly Command CmdWriteVariable =
        new("WriteVariable", "00", "02");
    private static readonly Command CmdConfigureTelemetry =
        new("ConfigureTelemetry", "00", "15");
    private static readonly Command CmdStartTelemetry =
        new("StartTelemetry", "00", "16");
    private static readonly Command CmdStopTelemetry =
        new("StopTelemetry", "00", "17");

    private readonly IProtocolService _protocol;
    private readonly Lock _stateLock = new();
    private readonly List<Variable> _dict = [];
    private readonly List<string> _writeValues = [];
    private uint _sourceRecipientId;
    private bool _isRunning;
    private bool _disposed;

    public TelemetryService(IProtocolService protocol)
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
        get { lock (_stateLock) return _dict.ToArray(); }
    }

    /// <inheritdoc/>
    public void UpdateDictionary(IReadOnlyList<Variable> variables)
    {
        ArgumentNullException.ThrowIfNull(variables);
        lock (_stateLock)
        {
            _dict.Clear();
            _dict.AddRange(variables);
        }
    }

    /// <inheritdoc/>
    public void AddToDictionary(Variable variable)
    {
        ArgumentNullException.ThrowIfNull(variable);
        lock (_stateLock) _dict.Add(variable);
    }

    /// <inheritdoc/>
    public void AddToDictionaryForWrite(Variable variable, string valueText)
    {
        ArgumentNullException.ThrowIfNull(variable);
        ArgumentNullException.ThrowIfNull(valueText);
        lock (_stateLock)
        {
            _dict.Add(variable);
            _writeValues.Add(valueText);
        }
    }

    /// <inheritdoc/>
    public void RemoveFromDictionary(int index)
    {
        lock (_stateLock)
        {
            if (index < 0 || index >= _dict.Count) return;
            _dict.RemoveAt(index);
        }
    }

    /// <inheritdoc/>
    public void ResetDictionary()
    {
        lock (_stateLock)
        {
            _dict.Clear();
            _writeValues.Clear();
        }
    }

    /// <inheritdoc/>
    public string GetVariableName(int index)
    {
        lock (_stateLock)
        {
            if (index < 0 || index >= _dict.Count) return "Index out of range";
            return _dict[index].Name;
        }
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
        List<Variable> variables;
        uint sourceAddr;
        lock (_stateLock)
        {
            if (_isRunning) return;
            variables = [.. _dict];
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
    public async Task ReadOneShotAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        List<Variable> snapshot;
        uint sourceAddr;
        lock (_stateLock)
        {
            if (_isRunning) return;
            if (_dict.Count == 0) return;
            snapshot = [.. _dict];
            sourceAddr = _sourceRecipientId;
            _isRunning = true;
        }

        try
        {
            foreach (var variable in snapshot)
            {
                ct.ThrowIfCancellationRequested();
                byte[] addr =
                [
                    ParseHexByte(variable.AddressHigh),
                    ParseHexByte(variable.AddressLow)
                ];
                await _protocol.SendCommandAsync(sourceAddr, CmdReadVariable, addr, ct)
                    .ConfigureAwait(false);
                await Task.Delay(OneShotDelayMs, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_stateLock) _isRunning = false;
        }
    }

    /// <inheritdoc/>
    public async Task WriteOneShotAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        List<(Variable v, string text)> snapshot;
        uint sourceAddr;
        lock (_stateLock)
        {
            if (_isRunning) return;
            if (_dict.Count == 0) return;
            int pairs = Math.Min(_dict.Count, _writeValues.Count);
            snapshot = new List<(Variable, string)>(pairs);
            for (int i = 0; i < pairs; i++) snapshot.Add((_dict[i], _writeValues[i]));
            sourceAddr = _sourceRecipientId;
            _isRunning = true;
        }

        try
        {
            foreach (var (variable, text) in snapshot)
            {
                ct.ThrowIfCancellationRequested();
                if (!TryParseWriteValue(text, out ushort value)) continue;
                byte[] payload =
                [
                    ParseHexByte(variable.AddressHigh),
                    ParseHexByte(variable.AddressLow),
                    (byte)((value >> 8) & 0xFF),
                    (byte)(value & 0xFF)
                ];
                await _protocol.SendCommandAsync(sourceAddr, CmdWriteVariable, payload, ct)
                    .ConfigureAwait(false);
                await Task.Delay(OneShotDelayMs, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            lock (_stateLock) _isRunning = false;
        }
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

        if (IsTelemetryDataCommand(evt.Command))
        {
            HandleFastTelemetry(evt);
        }
        else if (IsReadReplyCommand(evt.Command))
        {
            HandleReadReply(evt);
        }
    }

    private void HandleFastTelemetry(AppLayerDecodedEvent evt)
    {
        IReadOnlyList<Variable> variables;
        lock (_stateLock)
        {
            if (!_isRunning) return;
            variables = [.. _dict];
        }

        var payload = evt.Payload;
        if (!HasValidTelemetryHeader(payload)) return;

        EmitFastStreamDataPoints(payload, variables);
    }

    private void HandleReadReply(AppLayerDecodedEvent evt)
    {
        // payload = [80, 01, AddrH, AddrL, value_BE...]
        var payload = evt.Payload;
        if (payload.Length < ReadReplyHeaderSize + 1) return;
        byte addrH = payload[2];
        byte addrL = payload[3];

        IReadOnlyList<Variable> variables;
        lock (_stateLock) variables = [.. _dict];

        foreach (var variable in variables)
        {
            if (ParseHexByte(variable.AddressHigh) != addrH) continue;
            if (ParseHexByte(variable.AddressLow) != addrL) continue;
            int width = DataTypeWidth(variable.DataType);
            if (width == 0) return;
            if (payload.Length < ReadReplyHeaderSize + width) return;
            var builder = ImmutableArray.CreateBuilder<byte>(width);
            for (int i = 0; i < width; i++) builder.Add(payload[ReadReplyHeaderSize + i]);
            DataReceived?.Invoke(this, new TelemetryDataPoint(
                variable, builder.MoveToImmutable(), DateTime.UtcNow, TelemetrySource.ReadReply));
            return;
        }
    }

    private void EmitFastStreamDataPoints(ImmutableArray<byte> payload, IReadOnlyList<Variable> variables)
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
                variable, rawValue.MoveToImmutable(), timestamp, TelemetrySource.FastStream));
        }
    }

    private static bool IsTelemetryDataCommand(Command command)
        => command.CodeHigh == TelemetryDataCodeHigh
        && command.CodeLow == TelemetryDataCodeLow;

    private static bool IsReadReplyCommand(Command command)
        => command.CodeHigh == ReadReplyCodeHigh
        && command.CodeLow == ReadReplyCodeLow;

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

    private static bool TryParseWriteValue(string text, out ushort value)
    {
        value = 0;
        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return false;
        if (parsed < 0 || parsed > WriteValueMax) return false;
        value = (ushort)parsed;
        return true;
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
