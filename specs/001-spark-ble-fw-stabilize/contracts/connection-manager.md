# Contract: `ConnectionManager` BLE Lifecycle Stabilization Invariants

**Feature**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md) | **Date**: 2026-04-23

`ConnectionManager` is defined in `Services/Cache/ConnectionManager.cs`. This contract captures the invariants the stabilization work MUST enforce so User Stories US1, US2, and US3 hold.

## Surface (for reference)

Existing members (unchanged by this plan unless noted):

- `Task SwitchToAsync(ChannelKind kind, CancellationToken ct)` — selects the active channel (BLE / CAN / Serial); creates a fresh `ProtocolService` bound to the selected port.
- `ProtocolService? ActiveProtocol { get; }` — non-null while a channel is active.
- `ConnectionState State { get; }` — the observable lifecycle state.
- `IBootService CurrentBoot { get; }` / `ITelemetryService CurrentTelemetry { get; }` — bound to the active protocol.
- Forwarded events: `AppLayerDecoded`, `TelemetryDataReceived`, `BootProgressChanged`.

## Invariants

- **C1 (state-protocol biconditional)**: `ActiveProtocol != null` **if and only if** `State == ConnectionState.Connected`. Either direction violated is a bug.
  - Proves User Story US2 ("UI connection state matches reality") at the C# level.
  - Directly corresponds to the Lean theorem T5 in [../data-model.md](../data-model.md).
- **C2 (single mutation site)**: every transition of `State` is performed inside a single private method `TransitionTo(ConnectionState next)` that also sets `ActiveProtocol` (create on enter-`Connected`, null-out on exit-`Connected`) and emits exactly one `ILogger.LogInformation` scoped with `{ Kind, Prev, Next, Source }`. Callers MUST NOT assign `State` or `ActiveProtocol` directly. This structural rule is what makes C1 enforceable by code review + FsCheck + eventual Lean-derived checks.
- **C3 (sources of truth)**: `State` transitions are driven by exactly these sources, and no others:
    1. User-initiated `SwitchToAsync` (→ `Connecting` → `Connected` or back to `Disconnected` on failure).
    2. User-initiated disconnect (→ `Disconnecting` → `Disconnected`).
    3. BLE stack `DeviceDisconnected` event from `Plugin.BLE` surfaced through `BlePort.StateChanged` (→ `Disconnected`, unsolicited drop).
    4. Protocol-layer timeout from `ProtocolService` when the underlying port is dead but the BLE stack has not yet announced it (→ `Disconnected` after a bounded wait).
  - Any *other* code path that wants to change `State` MUST route through one of these four sources. This pins down the bug surface for the US2 / US3 investigation in `research.md` R9.
- **C4 (event forwarding once)**: UI tabs subscribe to `ConnectionManager` events exactly once (Domain Constraint already in the constitution). The stabilization work MUST NOT introduce duplicate subscriptions and MUST ensure subscriptions made against a *previous* `ActiveProtocol` are torn down before the next `SwitchToAsync`.
- **C5 (UI-state latency bound)**: the time between the originating source event (C3 items 3 and 4) and the `TransitionTo(Disconnected)` call is ≤ 5 seconds on the reference bench. FR-002.
- **C6 (no resource leak on disconnect)**: transitioning to `Disconnected` from `Connected` MUST dispose the previous `ProtocolService` and any subscriptions held against it. Relaunch of the app MUST NOT surface `ObjectDisposedException` attributable to `ConnectionManager` state from a previous session. FR-001.

## Preconditions / postconditions per method

- **`SwitchToAsync(kind, ct)`**
  - Pre: `kind` is a supported channel; the `ICommunicationPort` for `kind` is registered.
  - Post on success: `State = Connected`, `ActiveProtocol != null`, previous channel (if any) cleanly disconnected (C6).
  - Post on failure: `State = Disconnected`, `ActiveProtocol = null`, previous channel cleanly disconnected (C6), exception propagated.

- **`TransitionTo(next)` (private; new in this plan)**
  - Pre: called from one of the four sources in C3, holding the internal lock.
  - Post: `State = next`; `ActiveProtocol` satisfies C1; exactly one log line emitted; forwarded-event subscriptions coherent with the new state (C4).

## Change budget

- **May**: introduce the private `TransitionTo` method (C2) as a structural refactor if the current implementation has multiple state-mutation sites; add the `BlePort.StateChanged → TransitionTo(Disconnected)` wiring; add disposal of the previous `ProtocolService` on any exit from `Connected`; add structured-log scopes for FR-009.
- **May not**: change the public method signatures; introduce new channels; change the `IEnumerable<ICommunicationPort>` DI shape; register `ProtocolService` in DI (Domain Constraint violation).

## FsCheck property tests this contract enables

Derived from the above:

- `C1_StateProtocolBiconditional` — for every reachable state, `ActiveProtocol == null ⟺ State != Connected`.
- `C3_NoUnexpectedTransitions` — run a generator of source events and assert that every `State` change corresponds to one of the four sources.
- `C6_NoPostDisconnectLeaks` — simulate Connected → Disconnected transitions in-memory and assert the `ProtocolService` was disposed.

These tests are defined in `Tests/Unit/Services/Cache/ConnectionManagerPropertyTests.cs` and run on `net10.0`. The existing `ConnectionManagerTests.cs` (currently `net10.0-windows` only, see `Tests/Tests.csproj` line 48) is not merged with the new property tests — it stays on the Windows-only TFM because it uses real port implementations.
