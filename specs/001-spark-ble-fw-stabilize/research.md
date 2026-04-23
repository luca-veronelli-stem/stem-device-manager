# Phase 0 Research: Spark BLE Batch Firmware Update — Stabilization

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-04-23

This document resolves the technical unknowns surfaced during planning and records each design decision with rationale and alternatives considered. One section per decision.

## R1. Lean 4 toolchain version and layout

- **Decision**: Adopt `leanprover/lean4:v4.15.0` as the `lean-toolchain` pin. Use Lake as the build tool with a root `lakefile.lean` at `Lean/lakefile.lean`. Add mathlib4 as a dependency for state-machine / relational-algebra lemmas. Lean sources organized by phase under `Lean/PhaseN/`; this feature creates `Lean/Phase2/` (Phase 1 was described aspirationally in CLAUDE.md but never committed — there is no prior Lean material in this repo).
- **Rationale**: The tooling does not exist yet. A clean bootstrap on the current-stable Lean release avoids inheriting configuration drift. Pinning the toolchain makes CI reproducible. Organizing by phase matches the repo's modernization-plan narrative (Phase 1..5) so future Lean work slots in naturally.
- **Alternatives considered**: (a) vendor a subset of mathlib to avoid the external dep — rejected, mathlib is a large payload but the `lake` elan-based setup handles it cleanly and we benefit from `Mathlib.Tactic.*` for preservation proofs. (b) place Lean sources under `specs/001-spark-ble-fw-stabilize/lean/` — rejected, cross-feature reuse of state-machine definitions (e.g. `BleLifecycle` will be reused by future specs) requires a repo-wide `Lean/` root.

## R2. FsCheck package and integration pattern

- **Decision**: Add `FsCheck.Xunit` package version `3.1.0` (the 3.x line supports .NET 10 and ships a first-class xUnit `[Property]` attribute). Add it only to `Tests/Tests.csproj`; FsCheck is test-only. Use manual `Arbitrary<T>` implementations under `Tests/Unit/Generators/` (no reflection-driven generation for domain types — this matches the Constitution I preference for explicit over clever).
- **Rationale**: FsCheck 3.x is the current major version with active support and .NET 10 compatibility; the 2.x line is in maintenance. `FsCheck.Xunit` removes the boilerplate of writing xUnit wrappers around `Check.Quick`. Manual generators make the mapping from Lean state-machine definition to C# generator explicit and auditable, which is necessary because the whole point of the FsCheck layer is to stay faithful to the Lean model.
- **Alternatives considered**: (a) `Hedgehog` — rejected, smaller community and adds an unfamiliar API. (b) FsCheck 2.x — rejected, deprecated API surface. (c) reflection-driven arbitrary (`Arb.Default`) — rejected, silently generates combinations that are unreachable in the real state machine, defeating the Lean-derived-invariant story.

## R3. Lean → FsCheck generator derivation pattern

- **Decision**: For each state machine formalized in Lean, define (in the Lean source) an explicit list of constructors as a `List` of tagged transitions. Hand-translate that list to a C# `Arbitrary<Transition>` in `Tests/Unit/Generators/BootTransitionGenerator.cs`. The C# comments reference the Lean declaration by file + line. A **golden-list test** compiles the Lean source (via `lake build`) and diffs the constructor list against the C# list — if they drift, CI fails.
- **Rationale**: Full extraction from Lean to C# is tempting but premature — the Lean tooling story for C# extraction is not mature and would burn the 1–2 day spec budget. A hand-ported generator with a mechanical drift-detector gets 95 % of the benefit at 5 % of the setup cost. The golden-list test is the non-negotiable anti-drift guard.
- **Alternatives considered**: (a) write a Lean-to-C# extractor using `#eval` and JSON — rejected, too much bespoke tooling for a first Lean use in this repo. (b) skip the drift detector and rely on manual review — rejected, defeats the Constitution IV requirement that the Lean spec *gates* test runs.

## R4. Boot state machine formalization shape

- **Decision**: Formalize the per-area boot flow as a Mealy-like machine whose states are `Idle | AwaitingStart | Uploading (offset, total) | AwaitingEnd | Restarting | Failed (phase, cause) | Succeeded` and whose transitions are the `IBootService` method calls. `Uploading` carries `(offset, total)` explicitly so the preservation theorem `offset ≤ total` holds by construction. Retry is modelled as a self-loop on the current state with a bounded counter; the preservation theorem `retries ≤ RetryBudget` is stated and proved.
- **Rationale**: Matches the actual code path in `SparkBatchUpdateService.RunAreaAsync` (StartBoot → UploadBlocks → EndBoot → Restart). Explicit `(offset, total)` in the `Uploading` state makes the progress-reporting invariant directly formalizable without auxiliary predicates. Modelling retry as a self-loop with a counter is the canonical pattern for bounded-retry proofs.
- **Alternatives considered**: (a) Moore-style machine with action outputs as state fields — rejected, verbose for this domain. (b) model each boot step as a separate sub-machine composed via sequential composition — rejected, over-decomposed for a four-step sequence.

## R5. BLE lifecycle formalization and the `ActiveProtocol ⇔ Connected` invariant

- **Decision**: Formalize BLE lifecycle as `Disconnected | Connecting | Connected | Disconnecting`. The companion state on the app side is `ActiveProtocol : Option ProtocolService`. The preservation theorem is `ActiveProtocol.isSome ↔ (state = Connected)` and MUST hold across every transition. The state mutation events are `ConnectAck`, `DisconnectEvent` (from the BLE stack), `UserDisconnect`, and `UnsolicitedDrop`. The theorem is proved by induction over the transition relation.
- **Rationale**: The `ActiveProtocol` / `ConnectionState` agreement is the direct formalization of User Story 2 ("UI connection state matches reality"). Proving the biconditional in Lean gives us a machine-checked statement that, **assuming the implementation refines the transition relation**, the UI cannot be in a lying state. The refinement claim is validated by the FsCheck property tests (R3).
- **Alternatives considered**: (a) treat `ActiveProtocol` as a functional projection of `ConnectionState` so the invariant holds trivially — rejected, does not match the existing `ConnectionManager` API where `ActiveProtocol` is a recreated object on `SwitchToAsync` and needs explicit null-out on disconnect. (b) weaker invariant (`state = Connected → ActiveProtocol.isSome`) — rejected, would allow phantom `ActiveProtocol` after a drop, which is exactly the bug class we're trying to eliminate.

## R6. Batch composition formalization

- **Decision**: Model a batch as an ordered list of per-area boot machines. The batch execution is a fold over the list with short-circuit on `Failed`. The batch's terminal state is `Succeeded` iff every per-area machine reached `Succeeded`; any per-area `Failed (phase, cause)` propagates to the batch as `BatchFailed (area, phase, cause)`, with the remaining areas not stepped.
- **Rationale**: Directly mirrors `SparkBatchUpdateService.ExecuteAsync` and encodes FR-005 / FR-006 as a theorem. The fold-with-short-circuit pattern is a one-liner in Lean; the preservation theorem `BatchFailed → reason unambiguously identifies a single area` follows from the fold definition.
- **Alternatives considered**: (a) model parallel area execution — rejected, not what the code does and not what the hardware supports. (b) model retry *at batch level* on top of per-area retry — rejected, retries are a property of individual boot steps (per R4), not of the batch.

## R7. Perf-regression investigation methodology

- **Decision**: Establish a reproducible perf baseline by checking out commit `80bf9c6` on a separate worktree, running the single-file HMI v8.2 upload three times on SPARK-UC #2225998, capturing structured logs. Repeat on `main`. Compare per-step wall-clock time and per-chunk ack latency. Use `ILogger` scopes (`LogInformation` at every state transition and every retry) to instrument both builds identically. Hypotheses to rank by evidence: (H1) BLE MTU re-negotiation per chunk, (H2) extra `ConfigureAwait(false)` serializations in the refactored call chain, (H3) CRC verification round-trip added during refactor, (H4) logging I/O in the hot path. Profile with Visual Studio 2026 Performance Profiler on Windows for a third data source.
- **Rationale**: A root-cause search without a fixed reproducer wastes bench time. Two instrumented worktrees running identical uploads against the same device produce a time-delta matrix that pins the culprit to one or two steps; from there the code-diff between the two commits localizes the cause. The Visual Studio profiler on the hot run gives the third vote — if step timings disagree with profiler results, the discrepancy itself is a signal.
- **Alternatives considered**: (a) dotnet-trace on the release build only — rejected, gives absolute timings without the delta; does not distinguish regression from absolute cost. (b) BLE sniffer hardware capture — rejected for Phase 0, escalate only if software-layer profiling is inconclusive. The regression is 2× which is unlikely to be link-layer only.

## R8. ObjectDisposed / stale-handle root-cause investigation methodology

- **Decision**: Add a `Debug`-configuration shutdown-audit that logs every `IDisposable` owned by `ConnectionManager`, `BootService`, and `BLEManager` with a stack-trace of the disposal call site. Run the close/relaunch cycle (US1 acceptance scenario) with the audit enabled; any disposed object that is subsequently accessed on the next launch will be identified by the stack-trace of both its disposal and its reuse. Typical culprits in .NET refactors of this shape: (a) event handlers not unsubscribed, keeping the disposed object rooted; (b) static caches holding BLE adapter references across process restarts within the same elevated process (unlikely on Windows app launches but possible with Visual Studio hosting); (c) finalizer-order dependencies after a crash.
- **Rationale**: Shotgun-fixing disposable lifecycle bugs in a multi-layer stack causes regressions. An audit log with origin-of-disposal and origin-of-access converts the bug from "it crashes sometimes on relaunch" to "object X disposed at line Y is accessed at line Z" — which is one fix away.
- **Alternatives considered**: (a) wrap every use in try/catch ObjectDisposedException — rejected, hides the bug instead of fixing it. (b) adopt a dependency-injection container with scope management — rejected, Constitution I forbids introducing a DI container without a concrete forcing function.

## R9. Phantom connection state root-cause investigation methodology

- **Decision**: Instrument `ConnectionManager` to log every transition of `ConnectionState` with the event-source tag (user action, BLE event, protocol-layer detected timeout). Subscribe a dedicated test-only listener to `Plugin.BLE.DeviceDisconnected` and to `BlePort.StateChanged`, and compare event-arrival timestamps. The bug is one of three: the BLE stack raises a disconnect event that `ConnectionManager` never observes; the event is observed but not translated to a state transition; the transition occurs but `ActiveProtocol` is not null'd out (violates invariant R5). The three cases have distinct log signatures.
- **Rationale**: US2 acceptance scenario 2 (device power-off → UI transitions within 5 s) is observable — we just need to make the internal event flow observable too. Once the loss path is visible, the fix is either wiring a new subscription, adding a state transition, or nulling `ActiveProtocol` in the existing disconnect handler.
- **Alternatives considered**: (a) poll BLE status periodically and reconcile — rejected, latency bound in FR-002 is 5 s which is already above a polling interval's resolution; polling masks the root cause. (b) rewrite `ConnectionManager` state machine — rejected, scope blow-up.

## R10. Retry-budget shape and location (FR-007)

- **Decision**: The retry budget is a policy value `RetryBudget` on `BootService` with a default of 3 attempts per step, exposed as a constructor argument (manual DI from composition root per Constitution I; no configuration file indirection unless a second caller requires it). Budget applies per-step (StartBoot, UploadBlocks, EndBoot, Restart) independently. The Lean model encodes this as the `retries ≤ RetryBudget` invariant from R4.
- **Rationale**: A single-value policy in the constructor is the simplest possible shape that makes the budget visible in test harnesses and changeable per-variant if ever needed. Per-step budgets (rather than per-area) match how transient BLE errors actually cluster — one flaky step, not one flaky area.
- **Alternatives considered**: (a) hard-coded constant — rejected, no test harness overridability and violates Constitution II's general "inject policies" hint. (b) `appsettings.json` key — rejected, Constitution V configures variants at runtime but this is a test/diagnostic value, not a variant policy.

## R11. Observability hooks for FR-009 (traceable state transitions)

- **Decision**: Introduce a single structured-logging line per state transition and per retry, using `ILogger.BeginScope` to attach `{ Area, Step, Attempt, Recipient }` to every subsequent log emission inside the step. Use log level `Information` for state transitions and `Warning` for retries. No new logging dependency (we already take `ILogger<T>`). Output destination is the existing `appsettings.json`-configured sink (unchanged by this plan).
- **Rationale**: Observability is FR-009 and is also a precondition for the diagnostic work in R7–R9. Reusing the existing `ILogger<T>` injection from the composition root is the minimal change; `BeginScope` is the idiomatic way to attach structured context in Microsoft.Extensions.Logging.
- **Alternatives considered**: (a) introduce OpenTelemetry tracing — rejected, large dependency for a single-machine desktop tool. (b) write a custom transition-log type — rejected, Constitution I ("no abstractions without a concrete caller").

## R12. PR cadence and independence

- **Decision**: Vertical-commit PRs, each of which lands a single concern and passes CI independently. Target sequence: (1) Lean tooling bootstrap + empty `Lean/Phase2/*.lean` skeletons, (2) FsCheck package + one smoke test, (3) Lean state-machine definitions (no proofs yet), (4) Lean preservation theorems, (5) FsCheck generators + golden-list drift test, (6) observability hooks (FR-009, R11), (7) ObjectDisposed fix (US1), (8) Phantom-state fix (US2), (9) Session-survival fix (US3), (10) Perf regression fix (US5, US4 time budget), (11) Batch-failure contract test + any fixes found (US4 spec US5 numbered), (12) Docs update in `CHANGELOG.md` + `Docs/REFACTOR_PLAN.md`. Each PR references spec 001 by ID.
- **Rationale**: Matches Luca's stated preference for small PRs. Each PR is independently review-sized (< 500 LOC for most, Lean-tooling-bootstrap is the largest at the start). Ordering puts tooling and observability first so diagnostic PRs have the infrastructure to debug with. Docs last so descriptive text matches the final code.
- **Alternatives considered**: (a) one big stabilization PR — rejected, would be unreviewable and would couple unrelated fixes. (b) stack-of-dependent PRs (topic branches) — rejected, adds rebase overhead; linear history with sequential merges is simpler.

## Open items deferred to Phase 2 (`/speckit.tasks`)

- Exact Lean file names and theorem names (they depend on mathlib conventions discovered during PR #1).
- Exact FsCheck property-test names (they follow xUnit naming conventions once the generators are in).
- Root-cause findings for US1 (disposed), US2 (phantom), US3 (drops), US5 (perf) — these are diagnostic outputs, not design choices.
