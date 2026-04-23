# Implementation Plan: [FEATURE]

**Branch**: `[###-feature-name]` | **Date**: [DATE] | **Spec**: [link]
**Input**: Feature specification from `/specs/[###-feature-name]/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/plan-template.md` for the execution workflow.

## Summary

[Extract from feature spec: primary requirement + technical approach from research]

## Technical Context

<!--
  ACTION REQUIRED: Replace the content in this section with the technical details
  for the project. The structure here is presented in advisory capacity to guide
  the iteration process.
-->

**Language/Version**: [e.g., Python 3.11, Swift 5.9, Rust 1.75 or NEEDS CLARIFICATION]  
**Primary Dependencies**: [e.g., FastAPI, UIKit, LLVM or NEEDS CLARIFICATION]  
**Storage**: [if applicable, e.g., PostgreSQL, CoreData, files or N/A]  
**Testing**: [e.g., pytest, XCTest, cargo test or NEEDS CLARIFICATION]  
**Target Platform**: [e.g., Linux server, iOS 15+, WASM or NEEDS CLARIFICATION]
**Project Type**: [e.g., library/cli/web-service/mobile-app/compiler/desktop-app or NEEDS CLARIFICATION]  
**Performance Goals**: [domain-specific, e.g., 1000 req/s, 10k lines/sec, 60 fps or NEEDS CLARIFICATION]  
**Constraints**: [domain-specific, e.g., <200ms p95, <100MB memory, offline-capable or NEEDS CLARIFICATION]  
**Scale/Scope**: [domain-specific, e.g., 10k users, 1M LOC, 50 screens or NEEDS CLARIFICATION]

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Gates derived from `.specify/memory/constitution.md` (v1.0.0). Record a one-line
justification (or N/A) next to each gate; violations must be logged in
*Complexity Tracking* below.

- **I. Pragmatic C#** — Does this plan avoid introducing interfaces, abstractions,
  patterns, or configuration knobs that lack a concrete caller in this feature?
- **II. Correctness-biased defaults** — `Nullable=enable` respected, exceptions (not
  `null`) for errors, `CancellationToken` on every blocking `async`, thread-safety
  via `Lock` + `Volatile.Read/Write`, functions ≤ ~15 LOC.
- **III. Dual-TFM testing** — New test-worthy code in `Core/`, `Services/`,
  `Infrastructure.Persistence/`, or the cross-platform portion of
  `Infrastructure.Protocol/` has `net10.0` tests so CI exercises it. Windows-only
  code is tested under `net10.0-windows` and documented as such.
- **IV. Lean 4 formalization (NON-NEGOTIABLE for Core)** — Any domain model or state
  machine in `Core/` whose correctness matters is formalized in `Specs/PhaseN/`
  (state → actions → predicates → preservation theorems) and the flow is
  Lean spec → xUnit test → C# implementation. Changes invalidating a preservation
  theorem update the Lean spec in the same PR.
- **V. Runtime variant selection** — Device variants flow through `IDeviceVariantConfig`
  from the composition root (`Device:Variant` in `appsettings.json`); no
  `#if TOPLIFT/EDEN/EGICON` blocks are reintroduced.
- **VI. English-only artifacts** — Code, XML docs, inline comments, markdown, GUI
  strings, commit bodies, PR descriptions, and CHANGELOG entries are in English
  unless Luca explicitly requests Italian for a specific artifact.
- **Domain Constraints** — The plan upholds (or updates in lock-step) every bullet
  in the *Domain Constraints* section of the constitution: `ICommunicationPort`
  payload convention, protocol layering ownership in `ProtocolService`,
  per-channel `ProtocolService` lifecycle, `DictionaryCache` as single source,
  `FallbackDictionaryProvider` trigger, event forwarding via `ConnectionManager`,
  `Legacy/` scope.

## Project Structure

### Documentation (this feature)

```text
specs/[###-feature]/
├── plan.md              # This file (/speckit.plan command output)
├── research.md          # Phase 0 output (/speckit.plan command)
├── data-model.md        # Phase 1 output (/speckit.plan command)
├── quickstart.md        # Phase 1 output (/speckit.plan command)
├── contracts/           # Phase 1 output (/speckit.plan command)
└── tasks.md             # Phase 2 output (/speckit.tasks command - NOT created by /speckit.plan)
```

### Source Code (repository root)
<!--
  ACTION REQUIRED: Replace the placeholder tree below with the concrete layout
  for this feature. Delete unused options and expand the chosen structure with
  real paths (e.g., apps/admin, packages/something). The delivered plan must
  not include Option labels.
-->

```text
# [REMOVE IF UNUSED] Option 1: Single project (DEFAULT)
src/
├── models/
├── services/
├── cli/
└── lib/

tests/
├── contract/
├── integration/
└── unit/

# [REMOVE IF UNUSED] Option 2: Web application (when "frontend" + "backend" detected)
backend/
├── src/
│   ├── models/
│   ├── services/
│   └── api/
└── tests/

frontend/
├── src/
│   ├── components/
│   ├── pages/
│   └── services/
└── tests/

# [REMOVE IF UNUSED] Option 3: Mobile + API (when "iOS/Android" detected)
api/
└── [same as backend above]

ios/ or android/
└── [platform-specific structure: feature modules, UI flows, platform tests]
```

**Structure Decision**: [Document the selected structure and reference the real
directories captured above]

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| [e.g., 4th project] | [current need] | [why 3 projects insufficient] |
| [e.g., Repository pattern] | [specific problem] | [why direct DB access insufficient] |
