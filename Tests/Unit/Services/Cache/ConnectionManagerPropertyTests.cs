using Core.Interfaces;
using Core.Models;
using FsCheck;
using FsCheck.Xunit;
using Services.Cache;

namespace Tests.Unit.Services.Cache;

/// <summary>
/// FsCheck property tests that codify spec-001 ConnectionManager invariants
/// C1 and C3 (<c>specs/001-spark-ble-fw-stabilize/contracts/connection-manager.md</c>).
///
/// <para>Runs on the <c>net10.0</c> TFM only: uses an in-file fake
/// <see cref="ICommunicationPort"/> instead of the real BLE/CAN/Serial ports
/// (those live in <c>Infrastructure.Protocol</c>, which is Windows-only).
/// The existing <c>ConnectionManagerTests</c> stays on <c>net10.0-windows</c>.
/// </para>
/// </summary>
public class ConnectionManagerPropertyTests
{
    private static readonly DeviceVariantConfig GenericConfig =
        DeviceVariantConfig.Create(DeviceVariant.Generic);

    /// <summary>
    /// C1 — state/protocol biconditional: for every reachable
    /// <c>(State, ActiveProtocol)</c> pair, <c>ActiveProtocol != null</c> iff
    /// <c>State == Connected</c>.
    /// </summary>
    [Property]
    public bool C1_StateProtocolBiconditional(NonEmptyArray<byte> raw)
    {
        var script = DecodeEvents(raw.Get);
        using var fixture = new Fixture();

        foreach (var evt in script)
        {
            Apply(fixture, evt);
            var state = fixture.Manager.State;
            var protocol = fixture.Manager.ActiveProtocol;
            if ((protocol is not null) != (state == ConnectionState.Connected))
                return false;
        }

        return true;
    }

    /// <summary>
    /// C3 — only the four legal source events in the spec may change
    /// <c>State</c>. The generator emits exclusively those sources;
    /// the property asserts that every observed change is attributable to
    /// the event just applied.
    /// </summary>
    [Property]
    public bool C3_NoUnexpectedTransitions(NonEmptyArray<byte> raw)
    {
        var script = DecodeEvents(raw.Get);
        using var fixture = new Fixture();

        foreach (var evt in script)
        {
            var before = fixture.Manager.State;
            Apply(fixture, evt);
            var after = fixture.Manager.State;
            if (before == after) continue;
            if (!IsLegalTransition(before, after, evt))
                return false;
        }

        return true;
    }

    // --- Source-event model ---

    /// <summary>
    /// Enum mirror of the four C3 source events. Item 4 (protocol-timeout)
    /// is represented by the same port-drop pathway (<c>BlePort.StateChanged</c>
    /// → <c>TransitionTo(Disconnected)</c>) because there is no separate C#
    /// entry point for it in <c>ConnectionManager</c>.
    /// </summary>
    private enum SourceEvent
    {
        UserSwitchTo,       // C3 item 1: user-initiated SwitchToAsync
        UserDisconnect,     // C3 item 2: user-initiated DisconnectAsync
        PortDrop,           // C3 item 3: BlePort.StateChanged -> Disconnected
        UserSwitchToFails,  // C3 item 1 failure branch: SwitchToAsync throws
    }

    private static SourceEvent[] DecodeEvents(byte[] raw)
    {
        // Cap to 16 events per script — property tests are small and fast.
        var length = Math.Min(raw.Length, 16);
        var script = new SourceEvent[length];
        for (var i = 0; i < length; i++)
            script[i] = (SourceEvent)(raw[i] % 4);
        return script;
    }

    private static void Apply(Fixture fixture, SourceEvent evt)
    {
        switch (evt)
        {
            case SourceEvent.UserSwitchTo:
                fixture.Port.NextConnectSucceeds = true;
                TryRun(() => fixture.Manager.SwitchToAsync(ChannelKind.Ble));
                break;
            case SourceEvent.UserSwitchToFails:
                fixture.Port.NextConnectSucceeds = false;
                TryRun(() => fixture.Manager.SwitchToAsync(ChannelKind.Ble));
                break;
            case SourceEvent.UserDisconnect:
                TryRun(() => fixture.Manager.DisconnectAsync());
                break;
            case SourceEvent.PortDrop:
                fixture.Port.RaiseStateChanged(ConnectionState.Disconnected);
                break;
        }
    }

    private static void TryRun(Func<Task> action)
    {
        try { action().GetAwaiter().GetResult(); }
        catch (InvalidOperationException) { /* port-level connect failure */ }
    }

    private static bool IsLegalTransition(
        ConnectionState prev, ConnectionState next, SourceEvent trigger)
    {
        // Every state change observed at the ConnectionManager level must be
        // attributable to one of the four C3 sources. The enum only contains
        // those sources, so the check reduces to: did this source produce a
        // state delta that is in the state-machine's allowed set?
        return trigger switch
        {
            SourceEvent.UserSwitchTo => next == ConnectionState.Connected
                                          || prev == ConnectionState.Connecting,
            SourceEvent.UserSwitchToFails => next == ConnectionState.Disconnected,
            SourceEvent.UserDisconnect => next == ConnectionState.Disconnected,
            SourceEvent.PortDrop => next == ConnectionState.Disconnected,
            _ => false,
        };
    }

    // --- Minimal in-process fixture (no Infrastructure.Protocol deps) ---

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Port = new FakeBlePort();
            Manager = new ConnectionManager(
                [Port],
                new NoopDecoder(),
                GenericConfig);
        }

        public FakeBlePort Port { get; }
        public ConnectionManager Manager { get; }

        public void Dispose()
        {
            Manager.Dispose();
            Port.Dispose();
        }
    }

    /// <summary>
    /// Minimal <see cref="ICommunicationPort"/> fake. Kind = BLE (matches the
    /// Generic variant's default channel). Drives <c>StateChanged</c>
    /// synchronously via <see cref="RaiseStateChanged"/>; connect/disconnect
    /// are gated by <see cref="NextConnectSucceeds"/> so the generator can
    /// exercise both the success and failure branches of C3 item 1.
    /// </summary>
    private sealed class FakeBlePort : ICommunicationPort
    {
        private ConnectionState _state = ConnectionState.Disconnected;

        public ChannelKind Kind => ChannelKind.Ble;
        public ConnectionState State => _state;
        public bool IsConnected => _state == ConnectionState.Connected;
        public bool NextConnectSucceeds { get; set; } = true;

        public event EventHandler<RawPacket>? PacketReceived;
        public event EventHandler<ConnectionState>? StateChanged;

        public Task ConnectAsync(CancellationToken ct = default)
        {
            if (!NextConnectSucceeds)
                throw new InvalidOperationException("fake connect failure");
            Set(ConnectionState.Connected);
            return Task.CompletedTask;
        }

        public Task DisconnectAsync(CancellationToken ct = default)
        {
            Set(ConnectionState.Disconnected);
            return Task.CompletedTask;
        }

        public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken ct = default)
            => Task.CompletedTask;

        public void RaiseStateChanged(ConnectionState next)
        {
            Set(next);
        }

        public void Dispose()
        {
            _ = PacketReceived; // silence CS0067
        }

        private void Set(ConnectionState next)
        {
            if (_state == next) return;
            _state = next;
            StateChanged?.Invoke(this, next);
        }
    }

    private sealed class NoopDecoder : IPacketDecoder
    {
        public AppLayerDecodedEvent? Decode(RawPacket packet) => null;
        public void UpdateDictionary(
            IReadOnlyList<Command> commands,
            IReadOnlyList<Variable> variables,
            IReadOnlyList<ProtocolAddress> addresses) { }
    }
}
