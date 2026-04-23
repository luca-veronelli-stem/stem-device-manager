# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Commands

**Build:**
```bash
dotnet build Stem.Device.Manager.slnx
```
The device variant (TopLift/Eden/Egicon/Generic) is selected at runtime via `Device:Variant` in `GUI.Windows/appsettings.json`; there are no longer any device-specific build configurations.

**Test:**
```bash
dotnet test Tests/Tests.csproj                                          # all tests (dual TFM)
dotnet test Tests/Tests.csproj --framework net10.0                      # cross-platform (CI Linux)
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~Tests.Unit" # unit tests only
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~<ClassName>" # single class
```

**Run:**
```bash
dotnet run --project GUI.Windows/GUI.Windows.csproj
```

**CI (Bitbucket Pipelines):**
```bash
dotnet build Stem.Device.Manager.slnx --configuration Release -p:EnableWindowsTargeting=true
dotnet test Tests/Tests.csproj --framework net10.0  # cross-platform tests only on Linux
```

---

## Architecture

### Multi-project structure

```
Core/           [net10.0, no NuGet dependencies]     — domain models + interfaces (dictionary + protocol)
Infrastructure.Persistence/ [net10.0]                — dictionary data providers (API + Excel + Fallback)
Infrastructure.Protocol/    [net10.0;net10.0-windows] — HW ports (CanPort/BlePort/SerialPort) + legacy drivers (Legacy/)
Services/       [net10.0]                            — pure logic (ProtocolService, TelemetryService, BootService, ConnectionManager, DictionaryCache)
GUI.Windows/            [net10.0-windows, WinForms]          — GUI + DI entry point (no more embedded protocol)
Tests/          [dual TFM: net10.0 + net10.0-windows] — 292 tests net10.0 / 470 tests net10.0-windows
Specs/          [Lean 4]                             — formalizations of extracted types (Phase1/)
```

Dependencies: `App → {Infrastructure.Persistence, Infrastructure.Protocol, Services} → Core`, `Tests → App, Infrastructure.*, Services, Core`.

### Key components

**Core** — Dictionary models: `Variable`, `Command`, `ProtocolAddress`, `DictionaryData`. Protocol abstraction models: `ConnectionState`, `DeviceVariant`, `DeviceVariantConfig`, `RawPacket`, `AppLayerDecodedEvent`, `TelemetryDataPoint` (+ `TelemetrySource` FastStream/ReadReply, `NumericValue` helper), `BootState`/`BootProgress`, `SmartBootDeviceEntry`. Interfaces: `IDictionaryProvider`, `ICommunicationPort`, `IPacketDecoder`, `IProtocolService`, `ITelemetryService`, `IBootService`, `IDeviceVariantConfig`.

**Infrastructure.Persistence** — `DictionaryApiProvider` (REST HTTP to Azure), `ExcelDictionaryProvider` (ClosedXML on embedded `Dizionari STEM.xlsx`), `FallbackDictionaryProvider` (decorator: API → catch `HttpRequestException` → Excel). DI registration via `AddDictionaryProvider(IConfiguration)` — reads `DictionaryApi:BaseUrl/ApiKey/TimeoutSeconds` from `appsettings.json`.

**Infrastructure.Protocol** — Two subfolders:
- `Hardware/` — HW ports that implement `ICommunicationPort`: `CanPort` (wraps `PCANManager` via `IPcanDriver`), `BlePort` (via `IBleDriver`), `SerialPort` (via `ISerialDriver`). Payload convention: CAN = arbId LE prefix; BLE/Serial = pass-through.
- `Legacy/` — concrete drivers `BLEManager` (implements `IBleDriver` via Plugin.BLE) and `SerialPortManager` (implements `ISerialDriver` via `System.IO.Ports`). Compiled only on `net10.0-windows` TFM. Will be replaced when the `Stem.Communication` NuGet becomes available (Phase 5): the `ICommunicationPort` contract is already stable, only the wrappers need to be swapped.

**Services** — Pure cross-platform logic:
- `Protocol/ProtocolService` — STEM facade (encode TP + CRC16 + chunking + per-channel framing; decode + reassembly + `AppLayerDecoded` event; request/reply pattern). Implements `IProtocolService`. **Not registered in DI**: created at runtime by the `ConnectionManager` when a channel is selected.
- `Telemetry/TelemetryService` — implements `ITelemetryService`. Fast stream (`StartFastTelemetryAsync`) + one-shot `ReadOneShotAsync`/`WriteOneShotAsync` + mutable dictionary (`AddToDictionary`, `AddToDictionaryForWrite`, `RemoveFromDictionary`, `ResetDictionary`, `GetVariableName`). Decodes CMD_TELEMETRY_DATA (LE) and CMD_READ_VARIABLE reply (BE).
- `Boot/BootService` — implements `IBootService`. `StartFirmwareUploadAsync` (full sequence) + separate steps `StartBootAsync`/`EndBootAsync`/`RestartAsync`/`UploadBlocksOnlyAsync` for the Egicon multi-step workflow.
- `Cache/DictionaryCache` — centralized cache of commands+addresses+variables, load via `IDictionaryProvider`, `DictionaryUpdated` event, automatic sync with `IPacketDecoder`.
- `Cache/ConnectionManager` — aggregates the 3 ports (via `IEnumerable<ICommunicationPort>`), exposes `ActiveProtocol`/`CurrentBoot`/`CurrentTelemetry` (recreated on each `SwitchToAsync`), forwards `AppLayerDecoded`/`TelemetryDataReceived`/`BootProgressChanged` events (consumers subscribe once).

**GUI.Windows/Program.cs** — DI entry point: registers `IDictionaryProvider` + `IBleDriver`→`BLEManager` + `ISerialDriver`→`SerialPortManager` + `AddProtocolInfrastructure()` + `AddServices(config)`.

**GUI.Windows/Forms/Form1.cs** — WinForms shell (731 LOC post-Phase 4). Uses `IServiceProvider` to obtain `DictionaryCache`, `ConnectionManager`, `IDeviceVariantConfig`. `SendPS_Async` sends via `ConnectionManager.ActiveProtocol`; RX handler subscribed to `ConnectionManager.AppLayerDecoded`. Channel menu → `SwitchToAsync`.

**GUI.Windows/*_WF_Tab.cs** — WinForms tabs (`Boot_Interface_Tab`, `Boot_Smart_Tab`, `Telemetry_Tab`, `TopLiftTelemetry_Tab`, `BLEInterfaceTab`). Constructor DI of `DictionaryCache` + `ConnectionManager`. Tabs consume `_connMgr.CurrentBoot`/`CurrentTelemetry` and subscribe to the forwarded events.

### Dual TFM test strategy

`net10.0` (cross-platform tests): Core models + Infrastructure.Persistence providers + Services pure logic — run in CI on Linux.  
`net10.0-windows`: tests that depend on WinForms/App — require Windows, do not run in CI.  
Mocks are manual (no external libraries) in `Tests/Integration/Presenter/Mocks/`.

### Build configurations

2 configurations: `Debug` and `Release`. The device variant (TopLift/Eden/Egicon/Generic) is selected at runtime via `IDeviceVariantConfig` injected by the composition root and read from `Device:Variant` in `appsettings.json`. The `#if TOPLIFT/EDEN/EGICON` blocks were removed on branch `refactor/remove-ifs` (2026-04-21).

---

## Conventions

**Naming:** class/method/variable/enum names in **English**. Documentation (markdown, XML comments, inline comments, GUI strings) in **English**.

**C# style:** nullable types enabled (never return `null` for errors — throw exceptions), `CancellationToken` on all async methods, `Lock` + `Volatile.Read/Write` for thread-safety, functions < 15 LOC, early returns, 100-110 soft / 120 hard column limit.

**Test naming:** `{ClassName}Tests`, methods `{Method}_{Scenario}_{ExpectedResult}`. `[Fact]` for single cases, `[Theory]` + `[InlineData]` for parametric.

**Workflow:** discuss the plan before implementing. Prefer a pragmatic approach (works > elegant). Do not add interfaces or abstractions without a concrete need.

---

## Modernization plan (current state)

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Protocol abstractions in Core (interfaces + models + Lean 4 specs) | ✅ Completed |
| 2 | Services layer (ProtocolService/TelemetryService/BootService/HW adapter) | ✅ Completed |
| 3 | Form1 decomposition (DictionaryCache + ConnectionManager + tab decoupling + remove #if) | ✅ Completed |
| 4 | Switch to new stack + removal of embedded protocol | ✅ Completed |
| 5 | Migration to `Stem.Communication` NuGet (when available) + WPF UI evaluation | ⏳ Future |

### Architectural refactoring plan (REFACTOR_PLAN.md)

See [`Docs/REFACTOR_PLAN.md`](Docs/REFACTOR_PLAN.md) for the branch-by-branch plan.

| Branch | Description | Status |
|--------|-------------|--------|
| `refactor/protocol-abstractions` | Phase 1 — Core interfaces + models, Lean 4 specs | ✅ merged |
| `refactor/services-foundation` → `protocol-service` → `services-business` → `services-di-integration` | Phase 2 — multi-branch population of Services/ and Infrastructure.Protocol/ | ✅ merged (PR #24-#27) |
| `refactor/protocol-interface` | Phase 3 prerequisite — `IProtocolService` in Core | ✅ merged (PR #28) |
| `refactor/dictionary-cache` → `tab-decoupling` → `form1-thin-shell` → `remove-ifs` | Phase 3 — Form1 decomposition, DictionaryCache/ConnectionManager, `#if` removed | ✅ merged (PR #29-#32) |
| `refactor/phase-4-switch-to-new-stack` | Phase 4 — wiring Form1+tabs on the new stack, removal of `GUI.Windows/STEMProtocol/` (2590 LOC), drivers in `Infrastructure.Protocol/Legacy/` | ✅ Completed (in merge) |
| `refactor/phase-4b-app-reorganization` | Phase 4b — reorganization of App project folders (Forms/, Tabs/, Resources/) | ⏳ Pending |

---

## Reference files

| File | Purpose |
|------|---------|
| `.copilot/copilot-instructions.md` | Detailed workflow, user profile, session logs |
| `Docs/REFACTOR_PLAN.md` | Branch-by-branch architectural modernization plan |
| `Docs/PROTOCOL.md` | Internal workings of the STEM protocol (layering, CRC, chunking, commands) |
| `Docs/PREPROCESSOR_DIRECTIVES.md` | Historical map of `#if TOPLIFT/EDEN/EGICON` blocks removed (Phase 3 Branch 4) |
| `GUI.Windows/appsettings.json` | `DictionaryApi` configuration (BaseUrl, ApiKey, TimeoutSeconds) |
| `Directory.Build.props` | SemVer version, authors, copyright |
| `bitbucket-pipelines.yml` | Bitbucket CI/CD |

---

## Domain specifications (Lean 4)

Existing formalizations in `Specs/`:

| Folder | Branch | Content |
|--------|--------|---------|
| `Specs/Phase1/` | `refactor/protocol-abstractions` | Core types and interfaces: `ConnectionState`, `DeviceVariant`, `DeviceVariantConfig` (+ total factory + correctness theorems), `RawPacket`, `AppLayerDecodedEvent`, `TelemetryDataPoint`, `Interfaces` (ICommunicationPort, IPacketDecoder, ITelemetryService, IBootService, IDeviceVariantConfig) |

Candidate modules for future formalizations (Phase 2–3):
1. `STEMProtocol/PacketManager` → `PacketDecoder` — packet parsing/encoding logic
2. `STEMProtocol/STEM_protocol` → `ProtocolService` — application layer stack
3. `TelemetryManager` → `TelemetryService` — variable reads and sampling
4. `BootManager` → `BootService` — firmware update sequence (state machine already sketched in `Specs/Phase1/Interfaces.lean`)

<!-- SPECKIT START -->
For additional context about technologies to be used, project structure,
shell commands, and other important information, read the current plan
<!-- SPECKIT END -->

