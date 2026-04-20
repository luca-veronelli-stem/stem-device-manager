using Core.Interfaces;
using Core.Models;
using Services.Protocol;

namespace Services.Cache;

/// <summary>
/// Gestore del canale di comunicazione attivo. Sostituisce il pattern
/// <c>App.Form1.CommunicationPort</c> + <c>SetHardwareChannel(...)</c> sparso
/// sui manager legacy.
///
/// <para><b>Responsabilità:</b></para>
/// <list type="bullet">
/// <item><description>Aggrega le 3 <see cref="ICommunicationPort"/> disponibili (CAN, BLE, Serial) — ricevute via DI.</description></item>
/// <item><description>Espone <see cref="ActiveChannel"/> (canale scelto) e <see cref="ActiveProtocol"/> (<see cref="IProtocolService"/> wrappante la port attiva).</description></item>
/// <item><description>Su <see cref="SwitchToAsync"/>: <see cref="IDisposable.Dispose"/> del vecchio <see cref="ActiveProtocol"/>, <see cref="ICommunicationPort.DisconnectAsync"/> del vecchio port, <see cref="ICommunicationPort.ConnectAsync"/> del nuovo, crea nuovo <see cref="IProtocolService"/>.</description></item>
/// <item><description>Notifica stato via <see cref="StateChanged"/> (per ogni canale) e <see cref="ActiveChannelChanged"/> (transizione active).</description></item>
/// </list>
///
/// <para><b>Factory di <see cref="IProtocolService"/>:</b> ConnectionManager è
/// il punto naturale di creazione perché unisce port + decoder + senderId.
/// Allinea alla decisione che ProtocolService non sta in DI (dipende dalla
/// port runtime).</para>
///
/// <para><b>Stato iniziale:</b> <see cref="ActiveChannel"/> = <see cref="IDeviceVariantConfig.DefaultChannel"/>;
/// <see cref="ActiveProtocol"/> è <c>null</c> finché <see cref="SwitchToAsync"/> non
/// viene chiamato. Nessun auto-connect nel ctor (scelta architetturale: il
/// consumer controlla il timing).</para>
/// </summary>
public sealed class ConnectionManager : IDisposable
{
    private readonly IReadOnlyDictionary<ChannelKind, ICommunicationPort> _ports;
    private readonly IPacketDecoder _decoder;
    private readonly IDeviceVariantConfig _variantConfig;
    private readonly Lock _stateLock = new();
    private IProtocolService? _activeProtocol;
    private ChannelKind _activeChannel;
    private bool _disposed;

    public ConnectionManager(
        IEnumerable<ICommunicationPort> ports,
        IPacketDecoder decoder,
        IDeviceVariantConfig variantConfig)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(decoder);
        ArgumentNullException.ThrowIfNull(variantConfig);

        _ports = ports.ToDictionary(p => p.Kind);
        if (_ports.Count == 0)
            throw new ArgumentException("Almeno una ICommunicationPort richiesta.", nameof(ports));

        _decoder = decoder;
        _variantConfig = variantConfig;
        _activeChannel = variantConfig.DefaultChannel;

        foreach (var port in _ports.Values)
        {
            port.StateChanged += OnPortStateChanged;
        }
    }

    /// <summary>
    /// Canale attualmente selezionato. Valore iniziale =
    /// <see cref="IDeviceVariantConfig.DefaultChannel"/>; aggiornato da
    /// <see cref="SwitchToAsync"/>.
    /// </summary>
    public ChannelKind ActiveChannel
    {
        get { lock (_stateLock) return _activeChannel; }
    }

    /// <summary>
    /// <see cref="IProtocolService"/> wrappante la port attiva. <c>null</c>
    /// finché non viene chiamato <see cref="SwitchToAsync"/>.
    /// </summary>
    public IProtocolService? ActiveProtocol
    {
        get { lock (_stateLock) return _activeProtocol; }
    }

    /// <summary>Notifica del cambio di <see cref="ActiveChannel"/>.</summary>
    public event EventHandler<ChannelKind>? ActiveChannelChanged;

    /// <summary>
    /// Notifica dello stato di connessione di una port. Sollevato ogni volta
    /// che una delle 3 port emette il proprio <c>StateChanged</c>.
    /// </summary>
    public event EventHandler<ConnectionStateSnapshot>? StateChanged;

    /// <summary>
    /// Stato corrente della port per il canale indicato.
    /// </summary>
    public ConnectionState StateOf(ChannelKind channel)
        => _ports.TryGetValue(channel, out var p) ? p.State : ConnectionState.Disconnected;

    /// <summary>
    /// Passa al canale indicato:
    /// <list type="number">
    /// <item><description>Dispose del <see cref="ActiveProtocol"/> precedente (se presente).</description></item>
    /// <item><description><see cref="ICommunicationPort.DisconnectAsync"/> della port precedente (se differente).</description></item>
    /// <item><description><see cref="ICommunicationPort.ConnectAsync"/> della nuova port.</description></item>
    /// <item><description>Crea nuovo <see cref="IProtocolService"/> wrappante la port.</description></item>
    /// <item><description>Emette <see cref="ActiveChannelChanged"/>.</description></item>
    /// </list>
    /// </summary>
    public async Task SwitchToAsync(ChannelKind target, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (!_ports.TryGetValue(target, out var newPort))
            throw new ArgumentException(
                $"Nessuna ICommunicationPort registrata per canale {target}.", nameof(target));

        IProtocolService? oldProtocol;
        ICommunicationPort? oldPort;
        ChannelKind previousChannel;
        lock (_stateLock)
        {
            oldProtocol = _activeProtocol;
            previousChannel = _activeChannel;
            oldPort = oldProtocol is not null
                ? _ports.GetValueOrDefault(previousChannel)
                : null;
        }

        // Dispose vecchio protocol (sgancia handler da vecchia port).
        oldProtocol?.Dispose();

        // Disconnect vecchia port solo se diversa dalla nuova.
        if (oldPort is not null && !ReferenceEquals(oldPort, newPort))
        {
            await oldPort.DisconnectAsync(ct).ConfigureAwait(false);
        }

        // Connect nuova port. Se già Connected è no-op (contratto adapter).
        await newPort.ConnectAsync(ct).ConfigureAwait(false);

        var newProtocol = CreateProtocolService(newPort);
        lock (_stateLock)
        {
            _activeChannel = target;
            _activeProtocol = newProtocol;
        }
        if (previousChannel != target || oldProtocol is null)
            ActiveChannelChanged?.Invoke(this, target);
    }

    /// <summary>
    /// Disconnette il canale attivo e azzera <see cref="ActiveProtocol"/>.
    /// <see cref="ActiveChannel"/> resta invariato (il consumer sceglierà il
    /// canale alla prossima <see cref="SwitchToAsync"/>).
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        IProtocolService? protocol;
        ChannelKind current;
        lock (_stateLock)
        {
            protocol = _activeProtocol;
            current = _activeChannel;
            _activeProtocol = null;
        }
        protocol?.Dispose();
        if (_ports.TryGetValue(current, out var port))
            await port.DisconnectAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var port in _ports.Values)
        {
            port.StateChanged -= OnPortStateChanged;
        }
        _activeProtocol?.Dispose();
        _activeProtocol = null;
    }

    /// <summary>
    /// Hook di creazione dell'<see cref="IProtocolService"/>. Isolato in un
    /// metodo privato per eventuale override futuro (es. swap a
    /// <c>Stem.Communication</c> in Fase 4).
    /// </summary>
    private IProtocolService CreateProtocolService(ICommunicationPort port)
        => new ProtocolService(port, _decoder, _variantConfig.SenderId);

    private void OnPortStateChanged(object? sender, ConnectionState state)
    {
        if (sender is ICommunicationPort port)
        {
            StateChanged?.Invoke(this, new ConnectionStateSnapshot(port.Kind, state));
        }
    }

    private void ThrowIfDisposed()
        => ObjectDisposedException.ThrowIf(_disposed, this);
}

/// <summary>
/// Snapshot dello stato di una singola port, emesso da
/// <see cref="ConnectionManager.StateChanged"/>.
/// </summary>
/// <param name="Channel">Canale che ha cambiato stato.</param>
/// <param name="State">Nuovo stato del canale.</param>
public readonly record struct ConnectionStateSnapshot(
    ChannelKind Channel, ConnectionState State);
