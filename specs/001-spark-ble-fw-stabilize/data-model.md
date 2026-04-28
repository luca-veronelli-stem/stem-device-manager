# Phase 1 Data Model: Spark BLE Batch Firmware Update — Stabilization

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-04-23

This document maps the conceptual entities from the spec to their concrete C# types and Lean-formalization counterparts. It is a design artifact, not an implementation spec — it records the *shape* of the types and the *invariants* they carry, so the Lean model, the FsCheck generators, and the C# code stay aligned.

## Entity map

| Spec entity | C# type (existing or new) | Lean type | Layer |
|---|---|---|---|
| Firmware File | `SparkBatchItem` (existing — `Services/Boot/SparkBatchUpdateService.cs`) | `FirmwareFile` (`Lean/Spec001/BatchComposition.lean`) | `Services` |
| Batch Upload Job | `IReadOnlyList<SparkBatchItem>` + `SparkBatchUpdateException` on failure (existing) | `Batch` (list of `FirmwareFile`) + `BatchState` (`InProgress _ _ | Succeeded | Failed _ _ _`) | `Services` |
| BLE Session | `ConnectionManager` state + `ActiveProtocol` (existing) | `BleLifecycle.State` + `Option ProtocolHandle` | `Services/Cache`, `Infrastructure.Protocol` |
| Boot Step | `BootState` enum (existing `Core/Models/`) | `BootStateMachine.State` | `Core` |
| Reference Bench Configuration | documented in `specs/001-spark-ble-fw-stabilize/quickstart.md` | — (configuration, not formalized) | docs |

## `SparkBatchItem` (existing)

```csharp
public sealed record SparkBatchItem(SparkFirmwareArea Area, byte[] Firmware);
```

- `Area` identifies the on-device target (HMI / Motor1 / Motor2 / …). Drives `SparkAreas.Get(area).RecipientId` which becomes the addressee in the `CMD_START_PROCEDURE` frame.
- `Firmware` is the raw bytes of the signed firmware image on disk.
- **Invariant (validated at batch start, FR-010)**: `Firmware.Length > 0` and the bytes parse to a valid firmware header. Failing either MUST abort the batch *before* any on-device state change.

**Lean counterpart**:

```lean
structure FirmwareFile where
  area      : FirmwareArea
  bytes     : ByteArray
  nonEmpty  : bytes.size > 0
```

## Batch Upload Job (state machine)

The batch state is implicit in `SparkBatchUpdateService.ExecuteAsync` today (a fold with early exit). The Lean formalization makes it explicit:

```lean
inductive BatchState
  | InProgress (completed : List FirmwareArea) (remaining : List FirmwareFile)
  | Succeeded  (completed : List FirmwareArea)
  | Failed     (at : FirmwareArea) (phase : BootPhase) (cause : String)
```

Transitions (all from `InProgress`):

- `ExecuteOne` : `InProgress (done) (f :: rest) → InProgress (f.area :: done) rest` when the per-area boot machine reaches `Succeeded`.
- `FinalOne`  : `InProgress (done) (f :: [])  → Succeeded (f.area :: done)` when the last file succeeds.
- `AbortOne`  : `InProgress (done) (f :: rest) → Failed f.area phase cause` when the per-area machine reaches `Failed phase cause`.

**Preservation theorem**:

> `∀ s : BatchState, reachable s → (s = Succeeded done → done.length = initial.length) ∧ (s = Failed a _ _ → a ∈ initial.map (·.area))`.

In English: if the batch succeeded, every area was completed; if it failed, the failing area is one of the submitted areas.

> **Issue #74 / SPARK firmware constraint.** In batch context the per-area machine
> below terminates at `AwaitingEnd → Succeeded` (skipping `Restarting`), and the
> batch composition has a single terminal `BatchRestart` step that fires
> `RESTART_MACHINE` once at the end of the whole batch, addressed to the HMI
> board recipient. On the abort path no restart fires at all. The single-file
> `IBootService.StartFirmwareUploadAsync` entry point still drives the full
> per-area machine including `Restarting → Succeeded` — that path is unchanged.
> The Lean encoding of the batch terminal `Restart` step belongs to T024
> (`BatchComposition.lean`), not formalised here.

## Boot Step (per-area state machine)

The per-area machine (single-file path, captured from `IBootService.StartFirmwareUploadAsync` + `BootService.cs` — the batch path `SparkBatchUpdateService.RunAreaAsync` terminates one transition earlier per the issue #74 note above):

```lean
inductive BootState
  | Idle
  | AwaitingStart
  | Uploading      (offset : Nat) (total : Nat) (attempt : Nat)
  | AwaitingEnd
  | Restarting
  | Succeeded
  | Failed         (phase : BootPhase) (cause : String)
```

Transitions (abridged; one label per `IBootService` method invocation):

- `Idle → AwaitingStart` on `StartBootAsync` invocation.
- `AwaitingStart → Uploading 0 total 1` on ack received.
- `AwaitingStart → Failed StartBoot "no reply"` on timeout after the retry budget is exhausted.
- `Uploading off tot att → Uploading (off + chunk) tot att` on chunk ack (`off + chunk ≤ tot`).
- `Uploading off tot att → Uploading off tot (att + 1)` on retriable error, when `att < RetryBudget`.
- `Uploading off tot att → Failed UploadBlocks cause` when `att = RetryBudget` and retry fails.
- `Uploading tot tot _ → AwaitingEnd` when `off = tot` (all chunks acked).
- `AwaitingEnd → Restarting` on `EndBootAsync` ack.
- `Restarting → Succeeded` on `RestartAsync` ack.
- Any state + retry exhaustion → `Failed phase cause` with the current phase tag.

**Preservation theorems**:

- T1 (offset-total): `∀ off tot att, reachable (Uploading off tot att) → off ≤ tot`.
- T2 (retry-bounded): `∀ att, reachable (Uploading _ _ att) → att ≤ RetryBudget` (analogous for the other states carrying `attempt`).
- T3 (terminal stability): `Succeeded` and `Failed _ _` have no outgoing transitions.
- T4 (phase-preservation on abort): if the machine fails, the `phase` tag equals the phase in which the last attempted action took place.

**C# counterpart** (existing `Core/Models/BootState`; no change expected other than adding an `attempt` field or keeping it as a parallel variable in `BootService`):

```csharp
public enum BootState
{
    Idle,
    AwaitingStart,
    Uploading,
    AwaitingEnd,
    Restarting,
    Succeeded,
    Failed
}

// Retry counter lives inside BootService, not in the enum. If the Lean model's
// `attempt` parameter needs first-class representation in C# (e.g. for the
// FsCheck property tests), a companion record may be introduced under
// Core/Models/ — but only if the property tests genuinely need it.
public sealed record BootProgress(int CurrentOffset, int TotalLength, int Attempt);
```

## BLE Session (state machine)

The BLE lifecycle as formalized. Existing C# surface: `ConnectionState` in `Core/Models/` plus `ConnectionManager.ActiveProtocol : ProtocolService?`.

```lean
inductive BleLifecycleState
  | Disconnected
  | Connecting
  | Connected
  | Disconnecting
```

Associated app-level field: `activeProtocol : Option ProtocolHandle`.

**The core invariant (directly formalizes US2)**:

> `∀ s : (BleLifecycleState × Option ProtocolHandle), reachable s → s.2.isSome ↔ s.1 = Connected`.

Transitions:

- `Disconnected → Connecting` on user-initiated connect.
- `Connecting → Connected` on BLE stack `DeviceConnected` event + `ProtocolHandle` created. Both must happen atomically (one "action" from the Lean PoV); the C# code currently does this inside `ConnectionManager.SwitchToAsync`.
- `Connecting → Disconnected` on connect failure.
- `Connected → Disconnecting` on user-initiated disconnect.
- `Disconnecting → Disconnected` on BLE stack `DeviceDisconnected` event + `activeProtocol` set to `None`. Same atomicity requirement.
- `Connected → Disconnected` on **unsolicited drop** (the failure mode behind US2 and US3). Must also set `activeProtocol := None`; the invariant T5 below says that if this simultaneity is ever violated, the UI will claim "connected" when it isn't.

**Preservation theorem T5 (protocol-state agreement)**: the core invariant above, proved by induction over transitions.

**C# invariant**: `ConnectionManager.ActiveProtocol` is `null` iff `ConnectionState != Connected`. The stabilization work must enforce this with a **single** state-mutation site inside `ConnectionManager` so the invariant is syntactically local — this is the structural fix for US2.

## Reference Bench Configuration

Not a runtime entity — a canonical test fixture recorded in `quickstart.md`. Captured here only as a pointer so readers of `data-model.md` know where to find it.

## Cross-references

- Spec FRs mapped to entities: FR-001 (BLE Session disposal), FR-002 (BLE Session transition latency), FR-003 (Batch Upload Job during active session), FR-004/FR-008/SC-004/SC-005 (Batch Upload Job measurable outcomes), FR-005/FR-006 (Batch Upload Job state transitions: `ExecuteOne`/`FinalOne`/`AbortOne`), FR-007 (Boot Step retry bound T2), FR-009 (logging hooks on every transition of Boot Step and BLE Session), FR-010 (firmware-file validation pre-invariant).
- Spec SCs mapped to theorems: SC-002/US2 ↔ T5, SC-006/US4/FR-005-FR-006 ↔ batch preservation, FR-007 ↔ T2.
