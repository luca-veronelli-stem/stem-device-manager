using System.Collections.Immutable;
using Core.Interfaces;
using Core.Models;

namespace Infrastructure.Protocol.Hardware;

/// <summary>
/// Adapter BLE che implementa <see cref="ICommunicationPort"/> wrappando
/// un <see cref="IBleDriver"/> (in produzione <c>App.BLEManager</c>, che
/// usa Plugin.BLE su profilo Nordic UART).
///
/// <para><b>Convention payload (pass-through):</b></para>
/// A differenza di <see cref="CanPort"/>, BLE non espone un campo di
/// metadata separato per il destinatario (niente arbitration ID hardware).
/// Il frame BLE applicativo è costruito dal caller (<c>ProtocolService</c>
/// in Fase 2+) con <c>[NetInfo(2) + recipientId_LE(4) + chunk(≤98)]</c>
/// ed è inviato integralmente via
/// <see cref="IBleDriver.SendMessageAsync"/>.
/// <list type="bullet">
/// <item><description><c>SendAsync(payload)</c>: i byte sono inoltrati as-is
///   (nessun prefisso interpretato dall'adapter).</description></item>
/// <item><description><c>PacketReceived.RawPacket.Payload</c>: i byte
///   ricevuti sulla caratteristica Nordic UART TX as-is (il framing del
///   protocollo STEM è gestito a monte dal <c>ProtocolService</c>).</description></item>
/// </list>
///
/// <para><b>Connessione:</b></para>
/// Il ciclo scan/select/connect è gestito dal driver BLE stesso (UI-driven
/// in <c>App/BLE_WF_Tab</c>). <see cref="ConnectAsync"/> qui si limita a
/// sincronizzarsi con lo stato corrente del driver.
/// </summary>
public sealed class BlePort : ICommunicationPort
{
    private readonly IBleDriver _driver;
    private readonly Lock _stateLock = new();
    private int _state;
    private bool _disposed;

    public BlePort(IBleDriver driver)
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
    public ChannelKind Kind => ChannelKind.Ble;

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
    /// Il ciclo scan/select/connect BLE è pilotato dalla UI (<c>BLE_WF_Tab</c>) tramite
    /// l'API del driver (<see cref="IBleDriver.ConnectToAsync"/>). Questo metodo quindi
    /// non tenta di attivare il driver: allinea semplicemente lo stato del port a quello
    /// corrente del driver. Se il driver non è ancora connesso, il port resta
    /// <see cref="ConnectionState.Disconnected"/> e si aggiornerà automaticamente via
    /// <see cref="IBleDriver.ConnectionStatusChanged"/> quando la UI completa la connessione.
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
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        ct.ThrowIfCancellationRequested();
        if (State == ConnectionState.Disconnected) return;
        await _driver.DisconnectAsync();
        Transition(ConnectionState.Disconnected);
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
                "Payload BLE vuoto: almeno 1 byte richiesto.",
                nameof(payload));
        }
        var data = payload.ToArray();
        var ok = await _driver.SendMessageAsync(data);
        if (!ok)
        {
            throw new InvalidOperationException(
                "Driver BLE ha riportato fallimento nell'invio del frame.");
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
            try { _ = _driver.DisconnectAsync(); }
            catch { /* best effort */ }
            Transition(ConnectionState.Disconnected);
        }
    }

    private void OnDriverPacketReceived(object? sender, BlePacketEventArgs e)
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
