using Core.Interfaces;
using Core.Models;
using Services.Boot;
using Services.Protocol;
using Services.Telemetry;

namespace Services.Cache;

/// <summary>
/// Gestore del canale di comunicazione attivo. Sostituisce il pattern
/// <c>App.Form1.CommunicationPort</c> + <c>SetHardwareChannel(...)</c> sparso
/// sui manager legacy.
///
/// <para><b>Responsabilità:</b></para>
/// <list type="bullet">
/// <item><description>Aggrega le 3 <see cref="ICommunicationPort"/> disponibili (CAN, BLE, Serial) — ricevute via DI.</description></item>
/// <item><description>Espone <see cref="ActiveChannel"/> (canale scelto), <see cref="ActiveProtocol"/> (facade protocollo), <see cref="CurrentBoot"/> e <see cref="CurrentTelemetry"/> (servizi di dominio agganciati al protocol).</description></item>
/// <item><description>Su <see cref="SwitchToAsync"/>: <see cref="IDisposable.Dispose"/> dei vecchi servizi (telemetry → boot → protocol) e della port vecchia, <see cref="ICommunicationPort.ConnectAsync"/> della nuova, ricrea protocol + boot + telemetry.</description></item>
/// <item><description>Forwarda <see cref="AppLayerDecoded"/> dal protocol attivo: i consumer si iscrivono una sola volta al <c>ConnectionManager</c> e non gestiscono il re-binding ad ogni switch.</description></item>
/// <item><description>Notifica stato via <see cref="StateChanged"/> (per ogni canale) e <see cref="ActiveChannelChanged"/> (transizione active).</description></item>
/// </list>
///
/// <para><b>Factory di <see cref="IProtocolService"/>/<see cref="IBootService"/>/<see cref="ITelemetryService"/>:</b>
/// ConnectionManager è il punto naturale di creazione perché unisce port +
/// decoder + senderId e lega i servizi di dominio al protocol corrente.
/// Allinea alla decisione che questi servizi non stanno in DI (dipendono dalla
/// port runtime).</para>
///
/// <para><b>Stato iniziale:</b> <see cref="ActiveChannel"/> = <see cref="IDeviceVariantConfig.DefaultChannel"/>;
/// <see cref="ActiveProtocol"/>/<see cref="CurrentBoot"/>/<see cref="CurrentTelemetry"/>
/// sono <c>null</c> finché <see cref="SwitchToAsync"/> non viene chiamato.
/// Nessun auto-connect nel ctor (scelta architetturale: il consumer controlla
/// il timing).</para>
/// </summary>
public sealed class ConnectionManager : IDisposable
{
    private readonly IReadOnlyDictionary<ChannelKind, ICommunicationPort> _ports;
    private readonly IPacketDecoder _decoder;
    private readonly IDeviceVariantConfig _variantConfig;
    private readonly Lock _stateLock = new();
    private IProtocolService? _activeProtocol;
    private IBootService? _activeBoot;
    private ITelemetryService? _activeTelemetry;
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

    /// <summary>
    /// <see cref="IBootService"/> agganciato al <see cref="ActiveProtocol"/>
    /// corrente. <c>null</c> finché non viene chiamato <see cref="SwitchToAsync"/>.
    /// Ricreato ad ogni switch insieme al protocol.
    /// </summary>
    public IBootService? CurrentBoot
    {
        get { lock (_stateLock) return _activeBoot; }
    }

    /// <summary>
    /// <see cref="ITelemetryService"/> agganciato al <see cref="ActiveProtocol"/>
    /// corrente. <c>null</c> finché non viene chiamato <see cref="SwitchToAsync"/>.
    /// Ricreato ad ogni switch insieme al protocol.
    /// </summary>
    public ITelemetryService? CurrentTelemetry
    {
        get { lock (_stateLock) return _activeTelemetry; }
    }

    /// <summary>Notifica del cambio di <see cref="ActiveChannel"/>.</summary>
    public event EventHandler<ChannelKind>? ActiveChannelChanged;

    /// <summary>
    /// Notifica dello stato di connessione di una port. Sollevato ogni volta
    /// che una delle 3 port emette il proprio <c>StateChanged</c>.
    /// </summary>
    public event EventHandler<ConnectionStateSnapshot>? StateChanged;

    /// <summary>
    /// Evento applicativo decodificato, forwardato dal <see cref="ActiveProtocol"/>
    /// corrente. Sopravvive agli switch: i consumer si iscrivono una volta sola.
    /// </summary>
    public event EventHandler<AppLayerDecodedEvent>? AppLayerDecoded;

    /// <summary>
    /// Campione di telemetria forwardato dal <see cref="CurrentTelemetry"/> corrente.
    /// Sopravvive agli switch (re-binding interno automatico).
    /// </summary>
    public event EventHandler<TelemetryDataPoint>? TelemetryDataReceived;

    /// <summary>
    /// Progresso di upload firmware forwardato dal <see cref="CurrentBoot"/> corrente.
    /// Sopravvive agli switch (re-binding interno automatico).
    /// </summary>
    public event EventHandler<BootProgress>? BootProgressChanged;

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
        IBootService? oldBoot;
        ITelemetryService? oldTelemetry;
        ICommunicationPort? oldPort;
        ChannelKind previousChannel;
        lock (_stateLock)
        {
            oldProtocol = _activeProtocol;
            oldBoot = _activeBoot;
            oldTelemetry = _activeTelemetry;
            previousChannel = _activeChannel;
            oldPort = oldProtocol is not null
                ? _ports.GetValueOrDefault(previousChannel)
                : null;
        }

        DisposeActiveServices(oldProtocol, oldBoot, oldTelemetry);

        // Disconnect vecchia port solo se diversa dalla nuova.
        if (oldPort is not null && !ReferenceEquals(oldPort, newPort))
        {
            await oldPort.DisconnectAsync(ct).ConfigureAwait(false);
        }

        // Connect nuova port. Se già Connected è no-op (contratto adapter).
        await newPort.ConnectAsync(ct).ConfigureAwait(false);

        var newProtocol = CreateProtocolService(newPort);
        newProtocol.AppLayerDecoded += OnActiveProtocolAppLayer;
        var newBoot = new BootService(newProtocol);
        newBoot.ProgressChanged += OnActiveBootProgress;
        var newTelemetry = new TelemetryService(newProtocol);
        newTelemetry.DataReceived += OnActiveTelemetryData;
        lock (_stateLock)
        {
            _activeChannel = target;
            _activeProtocol = newProtocol;
            _activeBoot = newBoot;
            _activeTelemetry = newTelemetry;
        }
        if (previousChannel != target || oldProtocol is null)
            ActiveChannelChanged?.Invoke(this, target);
    }

    /// <summary>
    /// Disconnette il canale attivo e azzera <see cref="ActiveProtocol"/>/
    /// <see cref="CurrentBoot"/>/<see cref="CurrentTelemetry"/>.
    /// <see cref="ActiveChannel"/> resta invariato (il consumer sceglierà il
    /// canale alla prossima <see cref="SwitchToAsync"/>).
    /// </summary>
    public async Task DisconnectAsync(CancellationToken ct = default)
    {
        ThrowIfDisposed();
        IProtocolService? protocol;
        IBootService? boot;
        ITelemetryService? telemetry;
        ChannelKind current;
        lock (_stateLock)
        {
            protocol = _activeProtocol;
            boot = _activeBoot;
            telemetry = _activeTelemetry;
            current = _activeChannel;
            _activeProtocol = null;
            _activeBoot = null;
            _activeTelemetry = null;
        }
        DisposeActiveServices(protocol, boot, telemetry);
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
        DisposeActiveServices(_activeProtocol, _activeBoot, _activeTelemetry);
        _activeProtocol = null;
        _activeBoot = null;
        _activeTelemetry = null;
    }

    /// <summary>
    /// Hook di creazione dell'<see cref="IProtocolService"/>. Isolato in un
    /// metodo privato per eventuale override futuro (es. swap a
    /// <c>Stem.Communication</c> in Fase 5).
    /// </summary>
    private IProtocolService CreateProtocolService(ICommunicationPort port)
        => new ProtocolService(port, _decoder, _variantConfig.SenderId);

    /// <summary>
    /// Dispose ordinato dei servizi agganciati al protocol: prima i consumer
    /// (telemetry, boot) — sgancia i forwarding event — poi dispose, infine
    /// dispose del protocol stesso (che sgancia i propri handler dalla port).
    /// </summary>
    private void DisposeActiveServices(
        IProtocolService? protocol, IBootService? boot, ITelemetryService? telemetry)
    {
        if (telemetry is not null) telemetry.DataReceived -= OnActiveTelemetryData;
        if (boot is not null) boot.ProgressChanged -= OnActiveBootProgress;
        (telemetry as IDisposable)?.Dispose();
        (boot as IDisposable)?.Dispose();
        if (protocol is not null)
            protocol.AppLayerDecoded -= OnActiveProtocolAppLayer;
        protocol?.Dispose();
    }

    private void OnPortStateChanged(object? sender, ConnectionState state)
    {
        if (sender is ICommunicationPort port)
        {
            StateChanged?.Invoke(this, new ConnectionStateSnapshot(port.Kind, state));
        }
    }

    private void OnActiveProtocolAppLayer(object? sender, AppLayerDecodedEvent evt)
        => AppLayerDecoded?.Invoke(this, evt);

    private void OnActiveTelemetryData(object? sender, TelemetryDataPoint dp)
        => TelemetryDataReceived?.Invoke(this, dp);

    private void OnActiveBootProgress(object? sender, BootProgress p)
        => BootProgressChanged?.Invoke(this, p);

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
