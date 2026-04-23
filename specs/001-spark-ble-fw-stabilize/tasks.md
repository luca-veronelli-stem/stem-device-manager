---

description: "Task list for implementing spec 001 Spark BLE Firmware Stabilization"
---

# Tasks: Spark BLE Batch Firmware Update — Stabilization and Regression Fixes

**Input**: Design documents from `specs/001-spark-ble-fw-stabilize/`
**Prerequisites**: [plan.md](plan.md) (required), [spec.md](spec.md) (required for user stories), [research.md](research.md), [data-model.md](data-model.md), [contracts/](contracts/), [quickstart.md](quickstart.md)

**Tests**: INCLUDED. The spec's success criteria SC-001..SC-007 and the plan's R3 Lean→FsCheck drift-guard pattern require xUnit + FsCheck property tests + bench integration tests as part of the deliverable.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. Each task is one small-PR-sized unit of work; task IDs are assigned in execution order but `[P]` marks tasks that have no conflict with the surrounding task and may run in parallel.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependency on an unfinished task)
- **[Story]**: Which user story this task belongs to (US1..US5). Setup, Foundational, and Polish tasks carry no story label.
- File paths are absolute within the repo (e.g. `Services/Boot/BootService.cs`, not `src/services/...`).

## Path Conventions

- `Core/` — `net10.0`, zero deps: domain models + interfaces
- `Services/` — `net10.0`: pure business logic
- `Infrastructure.Protocol/` — `net10.0;net10.0-windows`: HW ports + legacy drivers
- `GUI.Windows/` — `net10.0-windows`: WinForms + composition root
- `Tests/` — dual TFM
- `Specs/` — Lean 4 sources (NEW — does not exist yet; created by T001)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Bootstrap the Lean 4 toolchain (no prior material in repo) and add FsCheck to the test project. Enables every downstream task that produces a Lean definition or a property test.

- [ ] T001 [P] Bootstrap Lean 4 tooling at repo root: create `Specs/lean-toolchain` pinned to `leanprover/lean4:v4.15.0` (per research.md R1); create `Specs/lakefile.lean` with mathlib4 dependency; create empty skeleton files `Specs/Phase2/BootStateMachine.lean`, `Specs/Phase2/BleLifecycle.lean`, `Specs/Phase2/BatchComposition.lean` (module declarations + `import Mathlib` only). Add `Specs/.lake/`, `Specs/lakefile.olean`, and `Specs/lake-packages/` to `.gitignore`.
- [ ] T002 [P] Add `FsCheck.Xunit` package version `3.1.0` to `Tests/Tests.csproj` (research.md R2). Add a smoke test `Tests/Unit/FsCheckSmokeTests.cs` on `net10.0` that exercises one trivial `[Property]` to confirm the dependency resolves and xUnit discovers FsCheck tests.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Infrastructure that every user story phase depends on. Observability (FR-009) and Lean state-machine skeletons must land before diagnostic work on US1..US3 begins.

**⚠️ CRITICAL**: User stories can start in parallel only AFTER Phase 2 completes.

- [ ] T003 Add structured logging scopes (FR-009, research.md R11) to `Services/Boot/BootService.cs` and `Services/Cache/ConnectionManager.cs`: every state transition emits exactly one `LogInformation` line; every retry emits exactly one `LogWarning`; both use `BeginScope` with `{ Area, Step, Attempt, Recipient }`. Depends on: nothing.
- [ ] T004 [P] Add Debug-configuration shutdown-audit logger `GUI.Windows/Diagnostics/ShutdownAudit.cs` (research.md R8): logs every `IDisposable` owned by `ConnectionManager`, `BootService`, `BLEManager` at dispose time with stack-trace. Wired in `GUI.Windows/Program.cs` under `#if DEBUG`. Depends on: nothing.
- [ ] T005 Define Lean 4 state-machine types and transitions (no preservation theorems yet) in `Specs/Phase2/BootStateMachine.lean`, `Specs/Phase2/BleLifecycle.lean`, `Specs/Phase2/BatchComposition.lean`. Exact shapes per `data-model.md`. `lake build` MUST pass. Depends on: T001.

**Checkpoint**: After T003–T005, each user story phase can proceed in any order subject to the dependencies noted below.

---

## Phase 3: User Story 1 — Session Lifecycle Is Crash-Free (Priority: P1) 🎯 MVP

**Goal**: Close/relaunch the app does not produce `ObjectDisposedException` or stale-handle errors. SC-001: 0 across 10 cycles on the reference bench.

**Independent Test**: Run quickstart.md SC-001 procedure (10 close/relaunch cycles on SPARK-UC SN 2225998); 0 disposed-object errors in logs or UI.

### Implementation for User Story 1

- [ ] T006 [US1] Run shutdown-audit diagnostic (T004) across 10 close/relaunch cycles on the reference bench; record findings in a PR description or a temporary scratch file (not merged). Identify the disposed object(s) and the reuse site(s).
- [ ] T007 [US1] Apply the root-cause fix identified in T006. Expected location: `Services/Cache/ConnectionManager.cs` (event unsubscription on dispose) and/or `Infrastructure.Protocol/Legacy/BLEManager.cs` (Plugin.BLE adapter handle release). Keep the change surgical — no refactor beyond what the fix requires. Depends on: T006.
- [ ] T008 [P] [US1] Add integration test `Tests/Integration/Boot/SparkBleStabilizationTests.Us1_CloseRelaunch_NoDisposed_Errors` on `net10.0-windows` that simulates 10 rapid close/relaunch cycles and asserts 0 `ObjectDisposedException` thrown + 0 in captured logs. Depends on: T007.

**Checkpoint**: US1 is complete when T008 passes on the reference bench. PR description links the SC-001 bench log from `Docs/BenchLog-Spec001.md` (see T027).

---

## Phase 4: User Story 2 — UI Connection State Matches Reality (Priority: P1)

**Goal**: The UI's "connected" indicator is truthful. SC-002: in 10/10 physical power-off events on SPARK-UC, the UI transitions to "disconnected" within ≤ 5 s (FR-002).

**Independent Test**: Run quickstart.md SC-002 procedure (10 physical power-off events, stopwatch); every transition observed within 5 s.

### Implementation for User Story 2

- [ ] T009 [US2] Refactor `Services/Cache/ConnectionManager.cs` to introduce a single private mutator `TransitionTo(ConnectionState next)` (contracts/connection-manager.md C2). Every state mutation goes through it; `ActiveProtocol` is created on enter-`Connected` and nulled on exit-`Connected`, enforcing the C1 biconditional by construction. Existing callers updated; no public API change.
- [ ] T010 [US2] Wire the disconnect event path (`Plugin.BLE` `DeviceDisconnected` surfaced via `Infrastructure.Protocol/Hardware/BlePort.StateChanged`) into `ConnectionManager.TransitionTo(Disconnected)` (contracts/connection-manager.md C3 item 3). Verify subscription is torn down on disconnect (C4). Depends on: T009.
- [ ] T011 [P] [US2] Add FsCheck property test `C1_StateProtocolBiconditional` in `Tests/Unit/Services/Cache/ConnectionManagerPropertyTests.cs` on `net10.0`: for every reachable `(State, ActiveProtocol)`, assert `ActiveProtocol != null ⇔ State == Connected`. Also `C3_NoUnexpectedTransitions`: generator emits only the four legal source events and asserts no other `State` change occurs. Depends on: T002, T005, T010.
- [ ] T012 [P] [US2] Add integration test `Tests/Integration/Boot/SparkBleStabilizationTests.Us2_PhysicalPowerOff_UiTransitionsWithin_5s` on `net10.0-windows` using a manual bench harness or a fake `BlePort` whose `StateChanged` is driven synchronously. Depends on: T010.

**Checkpoint**: US2 is complete when T011 and T012 pass. The C1 biconditional is now property-enforced.

---

## Phase 5: User Story 3 — BLE Session Survives a Full Upload (Priority: P1)

**Goal**: BLE link stays up for the full duration of a firmware upload under nominal conditions. SC-003: 10/10 HMI uploads without mid-upload disconnect; SC-005: ≥ 95 % upload success rate.

**Independent Test**: Run quickstart.md SC-003 procedure (10 HMI v8.2 uploads to SPARK-UC); 10/10 complete without the session dropping.

### Implementation for User Story 3

- [ ] T013 [US3] Audit retry logic in `Services/Boot/BootService.cs` against contracts/boot-service.md P3/Q1/Q2/Q3. Introduce (or correct) the `RetryBudget` constructor parameter with default 3 (research.md R10). Ensure the retry loop (a) caps at `RetryBudget`, (b) observes `CancellationToken` every iteration, (c) distinguishes transient errors (retry) from session-drop (fail immediately via invariant I1 check against `ConnectionManager.ActiveProtocol`). Depends on: T009 (needs the C1 biconditional in place to check `ActiveProtocol != null` reliably).
- [ ] T014 [P] [US3] Add FsCheck property tests in `Tests/Unit/Services/Boot/BootStateMachinePropertyTests.cs` on `net10.0`: `Q1_RetryBudgetBounded`, `Q2_FailedIsSticky`, `Q3_ProgressMonotonic`, `I1_ProtocolAgreement` (contracts/boot-service.md). Generators live in `Tests/Unit/Generators/BootTransitionGenerator.cs` and hand-port the Lean `BootStateMachine` transition list (research.md R3). Depends on: T002, T005, T013.
- [ ] T015 [P] [US3] Add integration test `Tests/Integration/Boot/SparkBleStabilizationTests.Us3_Hmi_Upload_SurvivesFullSession` on `net10.0-windows`: drives 10 single-file HMI v8.2 uploads against a reference-bench-backed fixture or a fake `ICommunicationPort`, asserts no mid-upload disconnect event. Also validates SC-005 (≥ 95 % upload success rate) via the same 10-run procedure per `quickstart.md`.

**Checkpoint**: US3 is complete when T014 and T015 pass and SC-003/SC-005 pass on the bench.

---

## Phase 6: User Story 4 — Multi-File Batch Is All-Success With Abort-On-First-Failure (Priority: P1)

**Goal**: The canonical three-file batch succeeds 3/3 on nominal runs (SC-006) and aborts cleanly on per-file failure after retries (SC-007).

**Independent Test**: Run quickstart.md SC-006 (10 canonical batches) + SC-007 (5 fault-injected batches).

### Implementation for User Story 4

- [ ] T016 [US4] Verify `Services/Boot/SparkBatchUpdateService.cs` already satisfies FR-005 / FR-006 (it structurally does per the source read). Add FR-010 precondition: validate `SparkBatchItem.Firmware.Length > 0` before any on-device side effect; throw `ArgumentException` (with the offending `Area.DisplayName`) up-front if missing/empty. Log a single `LogError` with the offending filename.
- [ ] T017 [P] [US4] Strengthen `Tests/Unit/Services/Boot/SparkBatchUpdateServiceTests.cs` (already exists) with two new `[Fact]`s: `Execute_SecondAreaFailsAfterRetries_AbortsAndNamesArea` and `Execute_EmptyFirmwareAtStart_ThrowsBeforeAnyDeviceCall`. Depends on: T016.
- [ ] T018 [P] [US4] Add integration test `Tests/Integration/Boot/SparkBleStabilizationTests.Us4_CanonicalBatch_And_FaultInjected_Abort` on `net10.0-windows` covering SC-006 (10 nominal batches) and SC-007 (5 fault-injected batches with a corrupted `WEMOTOR1.corrupt.bin` in position 2).

**Checkpoint**: US4 is complete when T017 and T018 pass. The existing `SparkBatchUpdateException` surface is unchanged; tests codify the contract.

---

## Phase 7: User Story 5 — HMI Upload Completes In v2.15 Time Budget (Priority: P2)

**Goal**: HMI v8.2 upload on SPARK-UC completes in ≤ 14 min mean over 5 runs (SC-004); no single run > 16 min. Recover from the ~2× regression observed on the refactored stack.

**Independent Test**: Run quickstart.md SC-004 procedure (5 timed HMI uploads); verify mean and single-run bounds.

### Implementation for User Story 5

- [ ] T019 [US5] Check out commit `80bf9c6` on a second git worktree (`git worktree add ../Stem.Device.Manager-v215 80bf9c6`). Run the single-file HMI v8.2 upload against SPARK-UC three times on each worktree with structured logs captured via T003 (for the current branch) and a comparable ad-hoc logger for v2.15. Record per-step timings side-by-side in `Docs/PerfRegression-Spec001.md` (new file). Rank hypotheses H1–H4 from research.md R7 by the observed deltas.
- [ ] T020 [US5] Apply the root-cause fix identified in T019. Expected location depends on the hypothesis; most likely candidates: `Services/Boot/BootService.cs` (chunk-loop `ConfigureAwait` / CRC verification), `Infrastructure.Protocol/Hardware/BlePort.cs` (MTU handling), `Infrastructure.Protocol/Legacy/BLEManager.cs` (Plugin.BLE notification vs indication). Keep the change surgical. Depends on: T019.
- [ ] T021 [P] [US5] Add integration test `Tests/Integration/Boot/SparkBleStabilizationTests.Us5_Hmi_Upload_WithinV215_Budget` on `net10.0-windows` that times a single HMI upload and asserts ≤ 16 min. Five-run mean check is performed by the bench runbook (quickstart.md SC-004), not by the test itself (tests must be deterministic).

**Checkpoint**: US5 is complete when T021 passes and SC-004 measurement on the bench shows mean ≤ 14 min across 5 runs.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Lock in the Lean formalization, the drift guard, and the docs.

- [ ] T022 [P] Complete Lean preservation theorems in `Specs/Phase2/BootStateMachine.lean` (T1 offset-total, T2 retry-bounded, T3 terminal stability, T4 phase preservation on abort) per data-model.md. `lake build` MUST pass. Depends on: T005.
- [ ] T023 [P] Complete Lean preservation theorem T5 (state-protocol biconditional) in `Specs/Phase2/BleLifecycle.lean` per data-model.md. `lake build` MUST pass. Depends on: T005.
- [ ] T024 [P] Complete Lean batch composition theorem in `Specs/Phase2/BatchComposition.lean` per data-model.md (batch succeeded ⇒ every area completed; batch failed ⇒ failing area ∈ input). `lake build` MUST pass. Depends on: T005.
- [ ] T025 [P] Add Lean→FsCheck drift-guard test `Tests/Unit/Generators/LeanDriftGuardTests.cs` on `net10.0` (research.md R3). Test invokes `lake build` to emit the transition-constructor list (e.g. via a small Lean `#eval` producing JSON), diffs against the hand-ported C# generator lists in `Tests/Unit/Generators/BootTransitionGenerator.cs` and `Tests/Unit/Generators/BleLifecycleTransitionGenerator.cs`, fails on any divergence. Depends on: T014, T022, T023.
- [ ] T026 [P] Update `CHANGELOG.md` with a spec-001 entry under an `Unreleased` heading. Format follows existing CHANGELOG conventions in this repo (one line per change, English).
- [ ] T027 [P] Update `Docs/REFACTOR_PLAN.md` to reference spec 001 as the Phase 4b stabilization gate.
- [ ] T028 [P] Add `Docs/BenchLog-Spec001.md` seeded with table headers only (per quickstart.md "Reporting" section). Each SC validation run appends one row.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001, T002 — no dependencies, start immediately.
- **Foundational (Phase 2)**: T003, T004 need nothing; T005 needs T001. All three block Phase 3+.
- **User Stories (Phase 3..7)**: All depend on Foundational. Between user stories:
  - US1 (Phase 3) can start immediately after Foundational.
  - US2 (Phase 4) can start immediately after Foundational.
  - US3 (Phase 5) needs T009 (C1 biconditional) completed, so starts after T009 lands (within Phase 4).
  - US4 (Phase 6) can start immediately after Foundational.
  - US5 (Phase 7) can start immediately after Foundational (independent of other US work).
- **Polish (Phase 8)**: T022/T023/T024 depend on T005; T025 depends on T014, T022, T023; T026/T027/T028 depend on all user stories being complete.

### User Story Dependencies

- **US1 (P1)**: depends on Foundational only.
- **US2 (P1)**: depends on Foundational only.
- **US3 (P1)**: depends on US2's T009 (for the C1-backed `ActiveProtocol` check in I1).
- **US4 (P1)**: depends on Foundational only.
- **US5 (P2)**: depends on Foundational only; benefits from T003 structured logging for the investigation.

### Within Each User Story

- The "diagnose/verify" task runs before the "fix" task.
- Property tests (`[P]`, `net10.0`) and integration tests (`[P]`, `net10.0-windows`) are added after the fix lands.

### Parallel Opportunities

- **Setup**: T001 and T002 are fully parallel.
- **Foundational**: T003, T004, T005 are parallel (T005 needs T001 from Setup).
- **Across user stories**: US1, US2, US4, US5 can be worked by different contributors concurrently after Foundational. US3 waits for T009.
- **Within a user story**: the property-test task and the integration-test task are usually `[P]` with each other (different files, different TFMs).
- **Polish**: T022/T023/T024 are parallel; T026/T027/T028 are parallel.

---

## Parallel Example: User Story 2

```text
# After T009 lands on the branch, these three can proceed in parallel:
T010 [US2] Wire BlePort.StateChanged → ConnectionManager.TransitionTo(Disconnected)
T011 [P] [US2] FsCheck property tests C1/C3 on net10.0
T012 [P] [US2] Integration test for SC-002 on net10.0-windows
```

---

## Implementation Strategy

### MVP Scope (minimum viable stabilization)

US1 only (T001, T002, T003, T004, T006, T007, T008). After this, the technician can launch the app and run a bench session without the most disruptive crash class. The remaining work is higher-value but non-blocking for day-to-day bench use.

### Incremental Delivery (matches research.md R12's 12-PR cadence)

1. Setup (T001, T002) → two small PRs, each independently reviewable.
2. Foundational (T003, T004, T005) → three small PRs.
3. US1 (T006, T007, T008) → one PR (small, surgical fix + one test file).
4. US2 (T009, T010, T011, T012) → one PR (the `TransitionTo` refactor is structural but bounded).
5. US3 (T013, T014, T015) → one PR.
6. US4 (T016, T017, T018) → one PR.
7. US5 (T019 + T020 + T021) → one PR (after the investigation write-up in `Docs/PerfRegression-Spec001.md` is reviewed separately if needed).
8. Polish theorems (T022, T023, T024) → one PR per theorem, or one consolidated PR.
9. Drift guard (T025) → one PR.
10. Docs (T026, T027, T028) → one PR.

Total: ~12 PRs as predicted by R12. Each one independently reviewable and independently CI-green.

### Parallel Team Strategy (if Luca is not solo on this)

- Developer A: Phase 3 (US1) + Phase 5 (US3, after T009 lands).
- Developer B: Phase 4 (US2) — owns the `TransitionTo` refactor.
- Developer C: Phase 6 (US4) + Phase 7 (US5 investigation T019).
- Lean work (Phase 2 T005 + Phase 8 T022/T023/T024) is naturally a single-person subproject since it all lives under `Specs/Phase2/`.

---

## Notes

- `[P]` tasks = different files, no unmet dependencies.
- `[Story]` label maps task to a spec user story for traceability.
- Each user story is independently completable and testable; nothing in this list forces a monolithic stabilization PR.
- Verify tests fail before implementing where TDD applies (the property tests and the integration tests both follow this pattern: add the failing test, land the fix, test goes green).
- Commit after each task or at most one logical group; conventional-commit subjects (`fix:`, `refactor:`, `test:`, `docs:`, `chore:`).
- Stop at any checkpoint to validate the user story on the reference bench before moving on.
- Avoid: vague tasks, same-file conflicts hidden behind `[P]`, cross-story dependencies that break independence beyond the US3-needs-T009 case documented above.
