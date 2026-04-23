# Implementation Plan: Spark BLE Batch Firmware Update — Stabilization and Regression Fixes

**Branch**: `plan/001-spark-ble-fw-stabilize` (spec lives on merged `001-spark-ble-fw-stabilize`) | **Date**: 2026-04-23 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/001-spark-ble-fw-stabilize/spec.md`

## Summary

Stabilize the Spark (Egicon variant) BLE batch firmware update path post-Phase-3/4 refactor. `SparkBatchUpdateService` already implements the all-or-nothing + abort-on-first-failure control flow at the orchestration layer (`Services/Boot/SparkBatchUpdateService.cs`). The defects are in the layers below: `IBootService` retry bounds, `ConnectionManager` BLE connection-state truthfulness, and `Infrastructure.Protocol/Legacy/BLEManager.cs` resource lifecycle. The technical approach is (1) bootstrap Lean 4 tooling and formalize the boot state machine + BLE lifecycle, (2) introduce FsCheck, derive generators from the Lean state machine, and add property tests on the `net10.0` target, (3) identify the perf-regression root cause by diffing runtime traces against commit `80bf9c6`, (4) land the fixes as a sequence of small vertical PRs — each keyed to one user story or success criterion.

## Technical Context

**Language/Version**: C# on .NET 10 (dual TFM `net10.0` + `net10.0-windows10.0.19041.0`); Lean 4 (toolchain version TBD in Phase 0) for `Specs/`.
**Primary Dependencies**: xUnit 2.5.3 (already in `Tests/Tests.csproj`), FsCheck (new — exact package + version TBD in Phase 0), Microsoft.Extensions.Logging (already used by `SparkBatchUpdateService`), Plugin.BLE via `Infrastructure.Protocol/Legacy/BLEManager.cs`, Lean 4 + mathlib (new).
**Storage**: N/A — the app streams firmware bytes over BLE; no persistent store introduced by this feature.
**Testing**: xUnit for all `Tests/`. FsCheck-derived property tests on `net10.0` for Boot state machine + BLE lifecycle logic that is cross-platform. xUnit `[Fact]`/`[Theory]` integration tests on `net10.0-windows` for code that touches `Infrastructure.Protocol/Legacy/` or WinForms. Manual bench runs (10× close/relaunch, 10× disconnect, 10× upload, 5× timed HMI upload, 10× three-file batch) to validate SC-001..SC-006; a fault-injected harness validates SC-007.
**Target Platform**: Windows 11 desktop (WinForms GUI, WinForms-dependent `net10.0-windows` code) + GitHub Actions Linux (cross-platform `net10.0` code). Reference bench: SPARK-UC SN 2225998 connected via BLE.
**Project Type**: desktop-app (WinForms, single composition root in `GUI.Windows/`, `Core`/`Services`/`Infrastructure.*` libraries).
**Performance Goals**: HMI v8.2 single-file upload mean ≤ 14 min over 5 runs, no single run > 16 min (SC-004). BLE connection-state UI latency ≤ 5 s from physical link loss (FR-002). Upload success rate ≥ 95 % across 10 runs (SC-005).
**Constraints**: Cannot break existing `net10.0-windows` tests (470 tests per CLAUDE.md). Cannot reintroduce `#if TOPLIFT/EDEN/EGICON` (Constitution principle V). Cannot register `ProtocolService` in DI (Domain Constraint: per-channel, created by `ConnectionManager.SwitchToAsync`). `Infrastructure.Protocol/Legacy/` stays `net10.0-windows` only until Phase 5 `Stem.Communication` NuGet migration.
**Scale/Scope**: Single feature, 5 user stories, 10 FRs, 7 SCs. Target ~10 vertical PRs (setup, Lean bootstrap, FsCheck bootstrap, one fix per defect, regression tests, observability, docs).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates derived from `.specify/memory/constitution.md` (v1.0.0). Record a one-line
justification (or N/A) next to each gate; violations must be logged in
*Complexity Tracking* below.

- **I. Pragmatic C#** — PASS. No new interfaces or configuration knobs introduced; the plan reuses `IBootService`, `ICommunicationPort`, `ConnectionManager`, and the existing `SparkBatchUpdateService`. FsCheck is the only new dependency and is testability-driven.
- **II. Correctness-biased defaults** — PASS. All new test code and any fixes land in a codebase already configured with `Nullable=enable`; fixes MUST preserve `CancellationToken` flow and the `Lock` / `Volatile` thread-safety idioms already in place.
- **III. Dual-TFM testing** — PASS. Property tests for the Boot state machine and BLE lifecycle derive from Lean models over pure data — they run on `net10.0` and are exercised by GitHub Actions on Linux. Driver-glue regression tests (BLEManager resource lifecycle) are `net10.0-windows` only and documented as such.
- **IV. Lean 4 formalization (NON-NEGOTIABLE for Core)** — PASS with scope extension. Boot state machine and BLE connection lifecycle are formalized under `Specs/Phase2/`. This is the first Lean work actually committed to the repo — tooling bootstrap (`lean-toolchain`, `lakefile.lean`, mathlib dep) lands as PR #1 of this plan. CLAUDE.md mentioned `Specs/Phase1/` aspirationally; the directory does not exist yet and is created by PR #1.
- **V. Runtime variant selection** — PASS. No `#if` blocks introduced. All fixes stay inside the runtime-variant-selected code path.
- **VI. English-only artifacts** — PASS. Plan, research, data model, quickstart, contracts, Lean sources, XML comments, commit bodies, PR descriptions all in English.
- **Domain Constraints** — PASS. `ProtocolService` stays per-channel and DI-free; `ConnectionManager` remains the sole event-forwarding point; `DictionaryCache` is not touched; `Infrastructure.Protocol/Legacy/` scope is preserved; `ICommunicationPort` pass-through-for-BLE convention is unchanged. Fixes live inside these boundaries, never across them.

**Result**: All gates pass. No complexity to track.

## Project Structure

### Documentation (this feature)

```text
specs/001-spark-ble-fw-stabilize/
├── plan.md              # This file (/speckit.plan command output)
├── spec.md              # Feature specification (/speckit.specify output, already merged)
├── research.md          # Phase 0 output — decision log (this command)
├── data-model.md        # Phase 1 output — entities and state machines (this command)
├── quickstart.md        # Phase 1 output — bench validation runbook (this command)
├── contracts/           # Phase 1 output — Boot/Connection contracts
│   ├── boot-service.md  # IBootService contract (existing surface + stabilization invariants)
│   └── connection-manager.md  # ConnectionManager BLE lifecycle contract
├── checklists/
│   └── requirements.md  # Spec quality checklist (/speckit.specify output, already merged)
└── tasks.md             # Phase 2 output (/speckit.tasks command — NOT created here)
```

### Source Code (repository root)

```text
Core/                                    # net10.0, zero deps — domain models + interfaces
├── Models/
│   └── (BootState, BootProgress, ConnectionState, SparkFirmwareArea, …)
└── Interfaces/
    └── IBootService.cs                  # stabilization target: retry-bound contract, state transitions

Services/                                # net10.0 — pure logic (no HW)
├── Boot/
│   ├── BootService.cs                   # IBootService impl; retry logic + state machine live here
│   └── SparkBatchUpdateService.cs       # already implements abort-on-first-failure; unchanged in this plan
└── Cache/
    └── ConnectionManager.cs             # stabilization target: connection-state truthfulness (US2)

Infrastructure.Protocol/                 # net10.0;net10.0-windows — HW ports
├── Hardware/
│   └── BlePort.cs                       # ICommunicationPort impl over BLE
└── Legacy/                              # net10.0-windows only
    └── BLEManager.cs                    # Plugin.BLE-backed driver; resource-lifecycle target (US1)

Tests/                                   # dual TFM
├── Unit/
│   └── Services/
│       └── Boot/
│           ├── SparkBatchUpdateServiceTests.cs     # existing
│           └── BootStateMachinePropertyTests.cs    # NEW — FsCheck-driven
└── Integration/
    └── Boot/
        └── SparkBleStabilizationTests.cs          # NEW — net10.0-windows, bench-adjacent

Specs/                                   # Lean 4 (NEW — directory does not yet exist)
├── lean-toolchain                       # NEW (PR #1)
├── lakefile.lean                        # NEW (PR #1)
└── Phase2/                              # NEW (PR #1)
    ├── BootStateMachine.lean            # boot state machine + preservation theorems
    ├── BleLifecycle.lean                # BLE lifecycle + invariant `ActiveProtocol ⇔ Connected`
    └── BatchComposition.lean            # ordered sequential composition + all-success / abort-on-fail
```

**Structure Decision**: Reuse the existing project layout; add a single new top-level directory `Specs/` for Lean sources. No new .NET projects. Tests grow within existing `Tests/` organized by layer (Unit / Integration). The speckit `specs/` (lowercase) directory remains the authoring home for spec / plan / tasks; the `Specs/` (capital) directory is Lean sources — this matches the convention already declared in `.gitignore` and CLAUDE.md. (Capital-S `Specs/` is the Lean-formalization home per CLAUDE.md; if this double-naming proves confusing on Windows case-insensitive file systems, PR #1 can rename to `Formalization/` before landing.)

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

No Constitution violations. Table intentionally empty.
