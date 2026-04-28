using Core.Diagnostics;
using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Services.Boot;
using Services.Protocol;
using Services.Telemetry;

namespace Services.Cache;

/// <summary>
/// Active communication channel manager. Replaces the legacy
/// <c>App.Form1.CommunicationPort</c> + <c>SetHardwareChannel(...)</c> pattern
/// that was scattered across the legacy managers.
///
/// <para><b>Responsibilities:</b></para>
/// <list type="bullet">
/// <item><description>Aggregates the 3 available <see cref="ICommunicationPort"/> instances (CAN, BLE, Serial) — received via DI.</description></item>
/// <item><description>Exposes <see cref="ActiveChannel"/> (selected channel), <see cref="State"/> (lifecycle), <see cref="ActiveProtocol"/> (protocol facade), <see cref="CurrentBoot"/> and <see cref="CurrentTelemetry"/> (domain services bound to the current protocol).</description></item>
/// <item><description>On <see cref="SwitchToAsync"/>: disposes the old services (telemetry → boot → protocol) and the old port, calls <see cref="ICommunicationPort.ConnectAsync"/> on the new port, rebuilds protocol + boot + telemetry.</description></item>
/// <item><description>Forwards <see cref="AppLayerDecoded"/> from the active protocol: consumers subscribe once to the <c>ConnectionManager</c> and never re-bind across switches.</description></item>
/// <item><description>Notifies state via <see cref="StateChanged"/> (per-channel port state) and <see cref="ActiveChannelChanged"/> (active-channel transition).</description></item>
/// </list>
///
/// <para><b>State contract (spec-001 C1/C2):</b>
/// Every mutation of <see cref="State"/> plus <see cref="ActiveProtocol"/> /
/// <see cref="CurrentBoot"/> / <see cref="CurrentTelemetry"/> goes through the
/// single private <c>TransitionTo</c> mutator. The biconditional
/// <c>ActiveProtocol != null ⇔ State == Connected</c> holds by construction.
/// </para>
///
/// <para><b>Initial state:</b> <see cref="ActiveChannel"/> = <see cref="IDeviceVariantConfig.DefaultChannel"/>;
/// <see cref="State"/> = <see cref="ConnectionState.Disconnected"/>;
/// <see cref="ActiveProtocol"/> / <see cref="CurrentBoot"/> / <see cref="CurrentTelemetry"/>
/// are <c>null</c> until <see cref="SwitchToAsync"/> succeeds. No auto-connect
/// in the ctor (architectural choice: the consumer controls timing).</para>
/// </summary>
public sealed class ConnectionManager : IDisposable
{
    private readonly IReadOnlyDictionary<ChannelKind, ICommunicationPort> _ports;
    private readonly IPacketDecoder _decoder;
    private readonly IDeviceVariantConfig _variantConfig;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ConnectionManager> _logger;
    private readonly Lock _stateLock = new();
    private ConnectionState _state = ConnectionState.Disconnected;
    private IProtocolService? _activeProtocol;
    private IBootService? _activeBoot;
    private ITelemetryService? _activeTelemetry;
    private ChannelKind _activeChannel;
    private bool _disposed;

    public ConnectionManager(
        IEnumerable<ICommunicationPort> ports,
        IPacketDecoder decoder,
        IDeviceVariantConfig variantConfig,
        ILoggerFactory? loggerFactory = null)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(decoder);
        ArgumentNullException.ThrowIfNull(variantConfig);

        _ports = ports.ToDictionary(p => p.Kind);
        if (_ports.Count == 0)
            throw new ArgumentException("At least one ICommunicationPort is required.", nameof(ports));

        _decoder = decoder;
        _variantConfig = variantConfig;
        _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
        _logger = _loggerFactory.CreateLogger<ConnectionManager>();
        _activeChannel = variantConfig.DefaultChannel;

        foreach (var port in _ports.Values)
        {
            port.StateChanged += OnPortStateChanged;
        }
    }

    /// <summary>
    /// Currently selected channel. Initial value =
    /// <see cref="IDeviceVariantConfig.DefaultChannel"/>; updated by
    /// <see cref="SwitchToAsync"/>.
    /// </summary>
    public ChannelKind ActiveChannel
    {
        get { lock (_stateLock) return _activeChannel; }
    }

    /// <summary>
    /// Lifecycle state of this manager. The biconditional
    /// <c>ActiveProtocol != null ⇔ State == Connected</c> is enforced at
    /// every mutation site (spec-001 C1).
    /// </summary>
    public ConnectionState State
    {
        get { lock (_stateLock) return _state; }
    }

    /// <summary>
    /// <see cref="IProtocolService"/> wrapping the active port. <c>null</c>
    /// when <see cref="State"/> is not <see cref="ConnectionState.Connected"/>
    /// (spec-001 C1).
    /// </summary>
    public IProtocolService? ActiveProtocol
    {
        get { lock (_stateLock) return _activeProtocol; }
    }

    /// <summary>
    /// <see cref="IBootService"/> bound to the current
    /// <see cref="ActiveProtocol"/>. <c>null</c> when
    /// <see cref="State"/> is not <see cref="ConnectionState.Connected"/>.
    /// Rebuilt on every successful switch.
    /// </summary>
    public IBootService? CurrentBoot
    {
        get { lock (_stateLock) return _activeBoot; }
    }

    /// <summary>
    /// <see cref="ITelemetryService"/> bound to the current
    /// <see cref="ActiveProtocol"/>. <c>null</c> when
    /// <see cref="State"/> is not <see cref="ConnectionState.Connected"/>.
    /// Rebuilt on every successful switch.
    /// </summary>
    public ITelemetryService? CurrentTelemetry
    {
        get { lock (_stateLock) return _activeTelemetry; }
    }

    /// <summary>Notification fired when <see cref="ActiveChannel"/> changes.</summary>
    public event EventHandler<ChannelKind>? ActiveChannelChanged;

    /// <summary>
    /// Port-level connection-state notification. Raised every time one of the
    /// 3 ports emits its own <c>StateChanged</c>.
    /// </summary>
    public event EventHandler<ConnectionStateSnapshot>? StateChanged;

    /// <summary>
    /// Decoded application-layer event forwarded from the current
    /// <see cref="ActiveProtocol"/>. Survives switches: consumers subscribe
    /// once.
    /// </summary>
    public event EventHandler<AppLayerDecodedEvent>? AppLayerDecoded;

    /// <summary>
    /// Telemetry sample forwarded from the current <see cref="CurrentTelemetry"/>.
    /// Survives switches (internal re-binding is automatic).
    /// </summary>
    public event EventHandler<TelemetryDataPoint>? TelemetryDataReceived;

    /// <summary>
    /// Firmware-upload progress forwarded from the current <see cref="CurrentBoot"/>.
    /// Survives switches (internal re-binding is automatic).
    /// </summary>
    public event EventHandler<BootProgress>? BootProgressChanged;

    /// <summary>
    /// Current state of the port for the given channel.
    /// </summary>
    public ConnectionState StateOf(ChannelKind channel)
        => _ports.TryGetValue(channel, out var p) ? p.State : ConnectionState.Disconnected;

    /// <summary>
    /// Switch to the given channel:
    /// <list type="number">
    /// <item><description>No-op early return when <paramref name="target"/> is already the <see cref="ActiveChannel"/> and <see cref="ActiveProtocol"/> is live (spec-001 C1: <c>ActiveProtocol != null ⇔ State == Connected</c>).</description></item>
    /// <item><description>Transition to <see cref="ConnectionState.Connecting"/> and dispose the previous <see cref="ActiveProtocol"/> (if any).</description></item>
    /// <item><description><see cref="ICommunicationPort.DisconnectAsync"/> on the previous port (if different from the new one).</description></item>
    /// <item><description><see cref="ICommunicationPort.ConnectAsync"/> on the new port.</description></item>
    /// <item><description>Build a new <see cref="IProtocolService"/> wrapping the port and transition to <see cref="ConnectionState.Connected"/>.</description></item>
    /// <item><description>Raise <see cref="ActiveChannelChanged"/>.</description></item>
    /// </list>
    /// On connect failure the state rolls back to <see cref="ConnectionState.Disconnected"/>
    /// and the exception propagates.
    /// </summary>
    public async Task SwitchToAsync(ChannelKind target, CancellationToken ct = default)
    {
        ThrowIfDisposed();
        if (!_ports.TryGetValue(target, out var newPort))
            throw new ArgumentException(
                $"No ICommunicationPort registered for channel {target}.", nameof(target));

        IProtocolService? oldProtocol;
        IBootService? oldBoot;
        ITelemetryService? oldTelemetry;
        ICommunicationPort? oldPort;
        ChannelKind previousChannel;
        bool wasConnected;
        lock (_stateLock)
        {
            // issue #51: clicking the already-active channel menu item must be
            // a no-op. Guard precedes any TransitionTo so no Connecting log and
            // no ShutdownAudit dispose record are emitted. Check is inside the
            // lock to serialize against concurrent SwitchToAsync / port drops;
            // ActiveProtocol is not null is the C1 biconditional phrasing of
            // State == Connected, so a dropped-but-still-on-Ble state still
            // rebuilds the trio (covered by SwitchToAsync_AfterPortDrop test).
            if (target == _activeChannel && _activeProtocol is not null)
                return;

            oldProtocol = _activeProtocol;
            oldBoot = _activeBoot;
            oldTelemetry = _activeTelemetry;
            previousChannel = _activeChannel;
            wasConnected = _state == ConnectionState.Connected;
            oldPort = wasConnected ? _ports.GetValueOrDefault(previousChannel) : null;
            TransitionTo(ConnectionState.Connecting, "SwitchTo");
        }

        DisposeActiveServices(oldProtocol, oldBoot, oldTelemetry);

        if (oldPort is not null && !ReferenceEquals(oldPort, newPort))
        {
            await oldPort.DisconnectAsync(ct).ConfigureAwait(false);
        }

        try
        {
            await newPort.ConnectAsync(ct).ConfigureAwait(false);

            var newProtocol = CreateProtocolService(newPort);
            newProtocol.AppLayerDecoded += OnActiveProtocolAppLayer;
            // spec-001 I1: BootService gets an I1 probe that returns true only
            // while *this* protocol is still the ActiveProtocol. A port drop or
            // SwitchToAsync replaces the protocol trio via TransitionTo, the
            // probe flips to false and the running boot step aborts with
            // BootSessionLostException instead of consuming the retry budget.
            var capturedProtocol = newProtocol;
            var newBoot = new BootService(
                newProtocol,
                retryBudget: BootService.DefaultRetryBudget,
                isSessionAlive: () => ReferenceEquals(ActiveProtocol, capturedProtocol),
                logger: _loggerFactory.CreateLogger<BootService>());
            newBoot.ProgressChanged += OnActiveBootProgress;
            var newTelemetry = new TelemetryService(newProtocol);
            newTelemetry.DataReceived += OnActiveTelemetryData;

            bool channelChanged;
            lock (_stateLock)
            {
                _activeChannel = target;
                TransitionTo(
                    ConnectionState.Connected, "SwitchTo",
                    newProtocol, newBoot, newTelemetry);
                channelChanged = previousChannel != target || !wasConnected;
            }

            if (channelChanged)
                ActiveChannelChanged?.Invoke(this, target);
        }
        catch
        {
            lock (_stateLock)
                TransitionTo(ConnectionState.Disconnected, "SwitchTo");
            throw;
        }
    }

    /// <summary>
    /// Disconnect the active channel and transition to
    /// <see cref="ConnectionState.Disconnected"/>. <see cref="ActiveChannel"/>
    /// is unchanged (the consumer picks a channel on the next
    /// <see cref="SwitchToAsync"/>).
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
            if (_state != ConnectionState.Disconnected)
                TransitionTo(ConnectionState.Disconnected, "Disconnect");
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

        IProtocolService? protocol;
        IBootService? boot;
        ITelemetryService? telemetry;
        lock (_stateLock)
        {
            protocol = _activeProtocol;
            boot = _activeBoot;
            telemetry = _activeTelemetry;
            if (_state != ConnectionState.Disconnected)
                TransitionTo(ConnectionState.Disconnected, "Dispose");
        }
        DisposeActiveServices(protocol, boot, telemetry);
        ShutdownAuditHook.Record(nameof(ConnectionManager), "(self)");
    }

    /// <summary>
    /// Hook for building the <see cref="IProtocolService"/>. Isolated in a
    /// private method so it can be overridden in the future (e.g. swapping to
    /// <c>Stem.Communication</c> in Phase 5).
    /// </summary>
    private IProtocolService CreateProtocolService(ICommunicationPort port)
        => new ProtocolService(
            port, _decoder, _variantConfig.SenderId,
            _loggerFactory.CreateLogger<ProtocolService>());

    /// <summary>
    /// Single state-mutation site (spec-001 C2). All four state fields plus
    /// <see cref="_state"/> are written atomically here. The C1 biconditional
    /// — <c>ActiveProtocol != null ⇔ State == Connected</c> — is enforced by
    /// the precondition check on the service trio.
    /// </summary>
    /// <remarks>
    /// Callers MUST hold <see cref="_stateLock"/>. The method emits exactly one
    /// <see cref="LoggerExtensions.LogInformation(ILogger, string, object?[])"/>
    /// line scoped with <c>{ Kind, Prev, Next, Source }</c>.
    /// </remarks>
    private void TransitionTo(
        ConnectionState next,
        string source,
        IProtocolService? protocol = null,
        IBootService? boot = null,
        ITelemetryService? telemetry = null)
    {
        bool shouldBeConnected = next == ConnectionState.Connected;
        if (shouldBeConnected != (protocol is not null)
            || shouldBeConnected != (boot is not null)
            || shouldBeConnected != (telemetry is not null))
        {
            throw new InvalidOperationException(
                $"C1 biconditional violation: next={next} requires "
                + (shouldBeConnected ? "non-null" : "null")
                + " protocol/boot/telemetry trio.");
        }

        var prev = _state;
        _state = next;
        _activeProtocol = protocol;
        _activeBoot = boot;
        _activeTelemetry = telemetry;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["Area"] = "Connection",
            ["Step"] = "TransitionTo",
            ["Kind"] = _activeChannel,
            ["Prev"] = prev,
            ["Next"] = next,
            ["Source"] = source,
        }))
        {
            _logger.LogInformation(
                "State {Prev} -> {Next} on {Channel} (source: {Source})",
                prev, next, _activeChannel, source);
        }
    }

    /// <summary>
    /// Ordered disposal of the services bound to a protocol: first the
    /// consumers (telemetry, boot) — detaches the forwarded events — then
    /// disposes them; finally disposes the protocol itself (which detaches
    /// its own handlers from the port).
    /// </summary>
    private void DisposeActiveServices(
        IProtocolService? protocol, IBootService? boot, ITelemetryService? telemetry)
    {
        if (telemetry is not null) telemetry.DataReceived -= OnActiveTelemetryData;
        if (boot is not null) boot.ProgressChanged -= OnActiveBootProgress;
        if (telemetry is IDisposable td)
        {
            ShutdownAuditHook.Record(
                nameof(ConnectionManager),
                $"ActiveTelemetry ({telemetry.GetType().Name})");
            td.Dispose();
        }
        if (boot is IDisposable bd)
        {
            ShutdownAuditHook.Record(
                nameof(ConnectionManager),
                $"ActiveBoot ({boot.GetType().Name})");
            bd.Dispose();
        }
        if (protocol is not null)
        {
            protocol.AppLayerDecoded -= OnActiveProtocolAppLayer;
            ShutdownAuditHook.Record(
                nameof(ConnectionManager),
                $"ActiveProtocol ({protocol.GetType().Name})");
            protocol.Dispose();
        }
    }

    private void OnPortStateChanged(object? sender, ConnectionState state)
    {
        if (sender is not ICommunicationPort port) return;
        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["Area"] = "Connection",
            ["Step"] = "PortStateChanged",
            ["Attempt"] = 0,
            ["Recipient"] = port.Kind
        }))
        {
            _logger.LogInformation("Port {Channel} state -> {State}", port.Kind, state);
        }
        StateChanged?.Invoke(this, new ConnectionStateSnapshot(port.Kind, state));

        // spec-001 C3 item 3 — unsolicited port drop on the active channel
        // must drive the manager to Disconnected (C1 biconditional).
        if (state != ConnectionState.Disconnected) return;
        IProtocolService? protocol;
        IBootService? boot;
        ITelemetryService? telemetry;
        lock (_stateLock)
        {
            if (_disposed) return;
            if (port.Kind != _activeChannel) return;
            if (_state != ConnectionState.Connected) return;
            protocol = _activeProtocol;
            boot = _activeBoot;
            telemetry = _activeTelemetry;
            TransitionTo(ConnectionState.Disconnected, "PortStateChanged");
        }
        DisposeActiveServices(protocol, boot, telemetry);
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
/// Snapshot of a single port's state, emitted by
/// <see cref="ConnectionManager.StateChanged"/>.
/// </summary>
/// <param name="Channel">Channel that changed state.</param>
/// <param name="State">New state of the channel.</param>
public readonly record struct ConnectionStateSnapshot(
    ChannelKind Channel, ConnectionState State);
