<!--
SYNC IMPACT REPORT
==================
Version change: none (initial publication audit) — stays at 1.0.0
Ratification: 2026-04-23 (unchanged)
Last amended: 2026-04-23 (unchanged)

Principles in scope (all initial):
  I.   Pragmatic C# (works > elegant)
  II.  Correctness-biased C# defaults
  III. Dual-TFM testing is mandatory
  IV.  Lean 4 formalization of domain invariants (NON-NEGOTIABLE for Core)
  V.   Runtime variant selection, not compile-time
  VI.  English as the single artifact language

Added sections: none (initial publication).
Removed sections: none.

Templates audited for alignment:
  - .specify/templates/plan-template.md       ✅ Constitution Check gates propagated (this sync)
  - .specify/templates/spec-template.md       ✅ no principle-driven changes required
  - .specify/templates/tasks-template.md      ✅ no principle-driven changes required
                                                 (illustrative paths; real task paths come
                                                  from plan.md structure at /speckit.plan time)
  - .specify/templates/checklist-template.md  ✅ no principle-driven changes required
  - CLAUDE.md                                 ✅ aligned with principles I–VI
  - README.md                                 ✅ no conflicting claims
  - Docs/REFACTOR_PLAN.md, Docs/PROTOCOL.md   ✅ descriptive, no conflict

Follow-up TODOs: none.
-->

# Stem.Device.Manager Constitution

This constitution governs the Stem.Device.Manager project: a Windows desktop tool
(WinForms today, WPF under evaluation) for configuring, telemetering, and
firmware-updating STEM embedded industrial devices over BLE, CAN, and Serial.

## Core Principles

### I. Pragmatic C# (works > elegant)

Prefer the smallest change that correctly solves the problem. Do not introduce
interfaces, abstractions, patterns, or configuration knobs until a concrete
caller needs them. Manual DI in the composition root; interfaces only where
they earn their keep; manual fakes in `Tests/Integration/**/Mocks/` — no
mocking libraries.

### II. Correctness-biased C# defaults

`Nullable=enable` everywhere. Errors are reported via exceptions, never by
returning `null`. `CancellationToken` on every `async` method that can block
or perform I/O. Thread-safety via `Lock` + `Volatile.Read/Write`; do not roll
ad-hoc primitives. Short functions (soft limit 15 LOC), early returns,
100–110 soft / 120 hard column limit.

### III. Dual-TFM testing is mandatory

Tests target both `net10.0` (cross-platform, runs in CI on Linux) and
`net10.0-windows` (WinForms-dependent, runs locally on Windows). Any new
test-worthy code in `Core/`, `Services/`, `Infrastructure.Persistence/`, and
the cross-platform portion of `Infrastructure.Protocol/` MUST have `net10.0`
tests so CI can exercise it. Windows-only code is tested under
`net10.0-windows` and documented as such. xUnit naming: `{ClassName}Tests`
with `{Method}_{Scenario}_{ExpectedResult}`.

### IV. Lean 4 formalization of domain invariants (NON-NEGOTIABLE for Core)

Domain models and state machines in `Core/` whose correctness matters —
`ConnectionState`, `DeviceVariantConfig`, `RawPacket`, protocol framing,
boot sequence, telemetry sampling — are formalized in `Lean/PhaseN/` using
the state → actions → predicates → preservation-theorem pattern. The flow is
Lean spec → xUnit test → C# implementation, in that order. Changes that
invalidate a preservation theorem require updating the Lean spec in the
same PR.

### V. Runtime variant selection, not compile-time

Device variants (TopLift / Eden / Egicon / Generic) are selected at runtime
via `IDeviceVariantConfig` injected from the composition root and read from
`Device:Variant` in `appsettings.json`. `#if TOPLIFT/EDEN/EGICON` blocks are
forbidden; reintroducing them requires a constitution amendment.

### VI. English as the single artifact language

Code, XML docs, inline comments, markdown docs, GUI strings, commit bodies,
PR descriptions, and CHANGELOG entries are written in English. Italian
appears only when explicitly requested for a specific artifact (e.g. a
customer-facing Italian GUI string).

## Domain Constraints

Invariants the code MUST uphold. Violations are bugs, not style issues.

- **`ICommunicationPort` payload convention.** CAN ports prefix the arbId in
  little-endian; BLE and Serial ports pass the payload through unchanged.
  Any new port implementation follows the same convention.
- **Protocol layering.** The STEM application protocol is TP + CRC16 +
  chunking + per-channel framing. `ProtocolService` owns encode/decode and
  reassembly; no other component parses or emits frames directly.
- **`ProtocolService` is per-channel.** It is created by `ConnectionManager`
  on `SwitchToAsync` and is NOT registered in DI. Consumers access it through
  `ConnectionManager.ActiveProtocol`.
- **`DictionaryCache` is the single source of commands, addresses, and
  variables.** Loads via `IDictionaryProvider` (API → fallback to Excel),
  raises `DictionaryUpdated`, and keeps `IPacketDecoder` in sync.
- **`FallbackDictionaryProvider` fallback is triggered by
  `HttpRequestException` only.** Other exceptions propagate.
- **Event forwarding happens in `ConnectionManager`.** UI components
  subscribe to `ConnectionManager` events (`AppLayerDecoded`,
  `TelemetryDataReceived`, `BootProgressChanged`) exactly once; they do not
  subscribe to underlying services directly.
- **Legacy drivers are `net10.0-windows` only.**
  `Infrastructure.Protocol/Legacy/` is scheduled for replacement by the
  `Stem.Communication` NuGet (Phase 5). The `ICommunicationPort` contract is
  the stable boundary: only wrappers change in the migration.

## Development Workflow

- **Dual-remote.** `github` is the active remote (PRs, Actions CI, issues,
  Artifacts, project board). `bitbucket` is a mirror for the STEM team. PRs
  are opened on GitHub only. Push configuration mirrors to both on every
  `git push github`.
- **No commits on `main`.** All work happens on feature branches. Merge to
  `main` is rebase-only; linear history is a project property, not a
  preference.
- **Conventional commits.** `feat:`, `fix:`, `refactor:`, `docs:`, `test:`,
  `chore:`. Commit bodies and PR descriptions are minimal and factual.
- **CI of record: GitHub Actions.** `bitbucket-pipelines.yml` is maintained
  as a minimal build-only stub that stays green during mirror pushes, but
  does not gate merges. A PR is mergeable when GitHub Actions is green.
- **Specs before code.** Non-trivial features go through the spec-kit
  workflow: `/speckit-constitution` (this file) → `/speckit-specify` →
  `/speckit-plan` → `/speckit-tasks` → `/speckit-implement`. For features
  touching protocol or state machines, a Lean formalization in `Lean/PhaseN/`
  runs in parallel and gates the `dotnet test` runs that close each task.

## Governance

This constitution supersedes ad-hoc decisions and informal conventions. When
`CLAUDE.md`, a skill, or an inline comment conflicts with the constitution,
the constitution wins and the other artifact is updated in the same PR.

**Amendments.** Changes to this file are made via PR. The PR description
must state the motivation, the affected principle(s), and the downstream
artifacts updated (CLAUDE.md, skills, `.specify/templates/**`, Lean specs).
Amendments that relax a NON-NEGOTIABLE principle require an explicit
rationale and, where applicable, a migration plan.

**Versioning.** `MAJOR.MINOR.PATCH`.
- MAJOR: removing or redefining a core principle.
- MINOR: adding a principle or domain constraint.
- PATCH: clarifications, typo fixes, rewording that does not change intent.

**Compliance review.** Every PR that touches `Core/`, `Services/`, or
`Infrastructure.Protocol/` must confirm — explicitly in the PR description —
that the change upholds the Domain Constraints section or updates it in
lock-step.

**Version**: 1.0.0 | **Ratified**: 2026-04-23 | **Last Amended**: 2026-04-23
