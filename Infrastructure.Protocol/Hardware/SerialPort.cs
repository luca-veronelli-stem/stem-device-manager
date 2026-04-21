using System.Collections.Immutable;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Protocol.Hardware;

/// <summary>
/// Adapter seriale (COM) che implementa <see cref="ICommunicationPort"/>
/// wrappando un <see cref="ISerialDriver"/> (in produzione
/// <c>App.SerialPortManager</c> basato su <c>System.IO.Ports.SerialPort</c>).
///
/// <para><b>Nota sul nome:</b></para>
/// Il nome <c>SerialPort</c> collide con
/// <see cref="T:System.IO.Ports.SerialPort"/>. Il namespace
/// <c>Infrastructure.Protocol.Hardware</c> li disambigua. Nei file che usano
/// entrambi i tipi, qualificare con il namespace completo.
///
/// <para><b>Convention payload (pass-through):</b></para>
/// Come <see cref="BlePort"/>, il seriale non ha metadata hardware separate
/// dal payload: i byte sono inviati e ricevuti as-is. Il framing del
/// protocollo STEM (<c>NetInfo + recipientId + chunk</c>) è costruito a monte
/// dal <c>ProtocolService</c>.
///
/// <para><b>Connessione:</b></para>
/// Il ciclo open/close è gestito dal driver (UI-driven in
/// <c>App/Serial_WF_Tab</c>). <see cref="ConnectAsync"/> qui si sincronizza
/// con lo stato corrente del driver con un breve polling.
/// </summary>
public sealed class SerialPort : ICommunicationPort
{
    private readonly ISerialDriver _driver;
    private readonly Lock _stateLock = new();
    private int _state;
    private bool _disposed;

    public SerialPort(ISerialDriver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);
        _driver = driver;
        _state = (int)(driver.IsConnected
            ? ConnectionState.Connected
            : ConnectionState.Disconnected);
        _driver.PacketReceived += OnDriverPacketReceived;
        _driver.ConnectionStatusChanged += OnDriverConnectionChanged;
    }

    /// <inheritdoc/>
    public ChannelKind Kind => ChannelKind.Serial;

    /// <inheritdoc/>
    public ConnectionState State => (ConnectionState)Volatile.Read(ref _state);

    /// <inheritdoc/>
    public bool IsConnected => State == ConnectionState.Connected;

    /// <inheritdoc/>
    public event EventHandler<RawPacket>? PacketReceived;

    /// <inheritdoc/>
    public event EventHandler<ConnectionState>? StateChanged;

    /// <inheritdoc/>
    /// <remarks>
    /// L'apertura della porta COM è pilotata dalla UI (menu seriale in <c>Form1</c>)
    /// tramite l'API del driver (<see cref="ISerialDriver.Connect"/>). Questo metodo
    /// non tenta di aprire la porta: allinea lo stato del port a quello corrente del
    /// driver. Se la porta non è ancora aperta, il port resta
    /// <see cref="ConnectionState.Disconnected"/> e si aggiornerà automaticamente via
    /// <see cref="ISerialDriver.ConnectionStatusChanged"/> quando la UI seleziona la porta.
    /// </remarks>
    public Task ConnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        if (_driver.IsConnected && State != ConnectionState.Connected)
            Transition(ConnectionState.Connected);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task DisconnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        if (State == ConnectionState.Disconnected) return Task.CompletedTask;
        _driver.Disconnect();
        Transition(ConnectionState.Disconnected);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        if (State != ConnectionState.Connected)
        {
            throw new InvalidOperationException(
                $"Stato corrente {State}: richiesto Connected per SendAsync.");
        }
        if (payload.IsEmpty)
        {
            throw new ArgumentException(
                "Payload seriale vuoto: almeno 1 byte richiesto.",
                nameof(payload));
        }
        var data = payload.ToArray();
        var ok = await _driver.SendMessageAsync(data);
        if (!ok)
        {
            throw new InvalidOperationException(
                "Driver seriale ha riportato fallimento nell'invio del frame.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _driver.PacketReceived -= OnDriverPacketReceived;
        _driver.ConnectionStatusChanged -= OnDriverConnectionChanged;
        if (State != ConnectionState.Disconnected)
        {
            try { _driver.Disconnect(); }
            catch { /* best effort */ }
            Transition(ConnectionState.Disconnected);
        }
    }

    private void OnDriverPacketReceived(object? sender, SerialPacketEventArgs e)
    {
        var handler = PacketReceived;
        if (handler is null) return;
        var builder = ImmutableArray.CreateBuilder<byte>(e.Data.Length);
        for (int i = 0; i < e.Data.Length; i++) builder.Add(e.Data[i]);
        handler(this, new RawPacket(builder.MoveToImmutable(), e.Timestamp));
    }

    private void OnDriverConnectionChanged(object? sender, bool isConnected)
    {
        Transition(isConnected
            ? ConnectionState.Connected
            : ConnectionState.Disconnected);
    }

    private void Transition(ConnectionState newState)
    {
        int newInt = (int)newState;
        bool changed;
        lock (_stateLock)
        {
            if (_state == newInt) { changed = false; }
            else
            {
                Volatile.Write(ref _state, newInt);
                changed = true;
            }
        }
        if (changed) StateChanged?.Invoke(this, newState);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
