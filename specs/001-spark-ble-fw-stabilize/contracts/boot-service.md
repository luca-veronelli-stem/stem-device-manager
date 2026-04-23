# Contract: `IBootService` Stabilization Invariants

**Feature**: [../spec.md](../spec.md) | **Plan**: [../plan.md](../plan.md) | **Date**: 2026-04-23

`IBootService` is defined in `Core/Interfaces/IBootService.cs` and implemented by `Services/Boot/BootService.cs`. This contract captures the **invariants the stabilization work MUST preserve** — not a rewrite of the method surface. It exists so the Lean model and FsCheck tests have a single authoritative pre/post-condition reference.

## Surface (for reference)

The existing methods (unchanged by this plan):

- `Task<bool> StartBootAsync(uint recipient, CancellationToken ct)`
- `Task UploadBlocksOnlyAsync(byte[] firmware, uint recipient, CancellationToken ct)`
- `Task<bool> EndBootAsync(uint recipient, CancellationToken ct)`
- `Task RestartAsync(uint recipient, CancellationToken ct)`
- `Task StartFirmwareUploadAsync(byte[] firmware, uint recipient, CancellationToken ct)` — convenience wrapper for the four-step sequence.
- `BootState State { get; }` — observable snapshot of the state machine.
- `event EventHandler<BootProgress> ProgressChanged`.

## Preconditions

- **P1 (non-null firmware)**: `UploadBlocksOnlyAsync` and `StartFirmwareUploadAsync` require `firmware.Length > 0`. Enforced before any BLE I/O; callers that pass an empty array get `ArgumentException`, not a silent no-op.
- **P2 (active protocol)**: all four step methods require `ConnectionManager.ActiveProtocol != null`. Call without an active BLE session throws `InvalidOperationException` (never `NullReferenceException`).
- **P3 (cancellation)**: every method observes `CancellationToken` at least once per block send + once per retry attempt. A cancelled token transitions the state machine to `Idle`, not `Failed` — `Failed` is reserved for retry exhaustion or protocol errors.

## Postconditions

- **Q1 (retry bound)**: each step retries at most `RetryBudget` times (default 3, configurable via the `BootService` constructor). A 4th attempt MUST NOT be made; the step transitions to `Failed` instead.
- **Q2 (state machine fidelity)**: after each method invocation, `State` reflects the last transition in the Lean `BootStateMachine.State` sense. `State = Failed` MUST be sticky within a single `ExecuteAsync` call; callers re-enter the state machine by constructing a new per-area boot sequence (this is how `SparkBatchUpdateService` already works).
- **Q3 (progress monotonicity)**: `ProgressChanged` events emitted during `UploadBlocksOnlyAsync` satisfy `CurrentOffset ≤ TotalLength` and `CurrentOffset` is non-decreasing within a single upload (even on retry — a retried chunk re-emits the same `CurrentOffset`, never rewinds past it).
- **Q4 (no resource leak on failure)**: when a step transitions to `Failed`, no `IDisposable` resource owned by `BootService` is left un-disposed. Relaunch of the app MUST NOT surface `ObjectDisposedException` attributable to `BootService` state from a previous session.

## Invariants across the full batch

- **I1 (`ActiveProtocol` agreement)**: the boot service MUST NOT cache a protocol reference across a BLE disconnect event. If `ConnectionManager.ActiveProtocol` becomes null mid-step, the step transitions to `Failed` and the `SparkBatchUpdateException.Cause` is `"BLE session lost during <phase>"`.
- **I2 (per-step independence)**: a failure during one area's `EndBoot` MUST NOT taint a subsequent area's `StartBoot` in a freshly reconnected session. The state machine is per-area; the retry counter is per-step.
- **I3 (observability)**: every state transition and every retry emits exactly one `ILogger.LogInformation` (transitions) or `LogWarning` (retries) line with `{ Area, Step, Attempt, Recipient }` in scope. FR-009.

## Change budget (what the stabilization PRs may and may not touch)

- **May**: the internal retry loop in `BootService` (for Q1), the transition-emission log scopes (for I3), the null-out of cached protocol handles on disconnect (for I1), the `ArgumentException` / `InvalidOperationException` precondition checks (for P1, P2).
- **May not**: the public method signatures on `IBootService`, the event signatures (`ProgressChanged`), the `BootState` enum shape. The stabilization is a semantics-preserving fix at the public contract boundary.

## FsCheck property tests this contract enables

Derived from the above, property tests check (property name → invariant):

- `Q1_RetryBudgetBounded` → Q1.
- `Q3_ProgressMonotonic` → Q3.
- `Q2_FailedIsSticky` → Q2 (stickiness within a single step sequence).
- `I1_ProtocolAgreement` → I1, against an in-memory fake `ConnectionManager` that simulates unsolicited drops.

These tests are defined in `Tests/Unit/Services/Boot/BootStateMachinePropertyTests.cs` and run on `net10.0`.
