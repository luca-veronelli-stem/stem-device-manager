# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and this
project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

### Legend

- **Added**: new features
- **Changed**: changes to existing functionality
- **Deprecated**: features marked for removal
- **Removed**: features removed
- **Fixed**: bug fixes
- **Security**: vulnerability fixes

---

## [Unreleased]

_No unreleased changes yet._

---

## [0.3.0] - 2026-04-29

First SemVer release after the modernization wave (legacy versioning was `2.x`,
reset to `0.3.0` to align with SemVer and reflect the still-evolving public surface).

This release covers everything between the legacy `0.2.15` (commit `80bf9c6`) snapshot
and today: full modernization (multi-project architecture, Azure dictionary API,
Form1 decomposition, removal of `#if`-based device variants, Spark BLE firmware
stabilization, Lean 4 invariants, dual-TFM tests).

### Added

- **spec-001 — Spark BLE Firmware Stabilization** (see `specs/001-spark-ble-fw-stabilize/`).
  Closes the post-Phase 4 stabilization debt against the SPARK-UC reference bench. Five
  user stories landed across PRs #41..#85:
  - **US1 — Close/relaunch is crash-free**: structured `ShutdownAudit` (Debug-only) +
    event-handler unsubscription fixes in `ConnectionManager`/`BLEManager`. Bench
    acceptance: 0 `ObjectDisposedException` over 10 close/relaunch cycles.
  - **US2 — UI state matches reality**: `ConnectionManager.TransitionTo` private
    mutator funnels every state change through a single site so the C1 biconditional
    (`ActiveProtocol != null ⇔ State == Connected`) holds by construction.
    `BlePort.StateChanged` wired into `TransitionTo(Disconnected)`. `BlePort.Dispose`
    now awaits driver disconnect (closes #50). FsCheck `C1_StateProtocolBiconditional`
    + `C3_NoUnexpectedTransitions` codify the contract.
  - **US3 — BLE session survives a full upload**: `BootService` retry-budget audit
    against contract Q1/Q2/Q3/I1; transient errors retry up to `RetryBudget`,
    session-drop fails immediately. FsCheck property tests Q1/Q2/Q3/I1 in
    `BootStateMachinePropertyTests`.
  - **US4 — Multi-file batch is all-success or abort-on-first-failure**: FR-010
    empty-firmware precondition rejects batches up-front; `SparkBatchUpdateService`
    end-of-batch `RESTART_MACHINE` only (closes #74) — STEM firmware shuts down on
    restart, so the per-area restart broke areas 2..N.
  - **US5 — HMI upload time budget (SC-004)**: bench investigation
    `Docs/PerfRegression-Spec001.md` recorded that the ~2× regression motivating
    SC-004 is no longer reproducing on `main` as of 2026-04-27. T020 root-cause fix
    skipped; T021 single-run guard (`Us5_Hmi_Upload_WithinV215_Budget`) is the
    safety net.
  - **Lean 4 formalization** (`Lean/Spec001/`): `BootStateMachine` (T1
    offset-total, T2 retry-bounded, T3 terminal stability, T4 phase preservation),
    `BleLifecycle` (T5 state-protocol biconditional), `BatchComposition` (succeeded
    → all-completed, failed → area-in-input). `lake build` green, no `sorry`/`admit`.
  - **Lean → C# drift guard** (`LeanDriftGuardTests`): parses the `inductive Step`
    declarations in each Lean file and asserts the constructor list matches the
    hand-ported C# arrays in `BootTransitionGenerator` /
    `BleLifecycleTransitionGenerator`. Fails CI on any divergence.
  - **Observability (FR-009)**: structured `ILogger.BeginScope`
    `{ Area, Step, Attempt, Recipient }` in `BootService` + `ConnectionManager`;
    one `LogInformation` per state transition, one `LogWarning` per retry.
    Discard-frame logging in `ProtocolService` receive path (#76 / PR #77).
  - **Bench operations**: `Docs/BenchLog-Spec001.md` seeded for chronological
    per-run logging across SC-001..SC-007.

- **REFACTOR_PLAN Phase 4 — `refactor/phase-4-switch-to-new-stack`** (merged):
  switched `Form1` and the WinForms tabs to the new stack
  (`ConnectionManager.ActiveProtocol`, `CurrentBoot`, `CurrentTelemetry`); removed
  the legacy `GUI.Windows/STEMProtocol/` folder (~2590 LOC) and the corresponding
  `BootManager` / `TelemetryManager` shims. Drivers `BLEManager` /
  `SerialPortManager` moved to `Infrastructure.Protocol/Legacy/`.

- **REFACTOR_PLAN Phase 3** (merged in main, PRs #29–#32):
  - **Branch 1 — `refactor/phase-3-dictionary-cache`**: `DictionaryCache` singleton
    (commands + addresses + variables + `CurrentRecipientId`), `ConnectionManager`
    aggregating the three ports and exposing `ActiveChannel` / `ActiveProtocol` /
    `StateChanged`. `IDeviceVariantConfig.DefaultChannel` (TopLift→CAN, others→BLE
    for legacy parity). DI registration in `Services/DependencyInjection.cs` +
    factory bridges in `Infrastructure.Protocol/DependencyInjection.cs`.
  - **Branch 2 — `refactor/phase-3-tab-decoupling`**: WinForms tabs receive
    `DictionaryCache` via constructor injection and subscribe to
    `DictionaryUpdated`; the legacy public `UpdateDictionary(...)` methods on tabs
    were removed and replaced by event-driven updates.
    `Form1.LoadDictionaryDataAsync` is now a single HTTP call funneled through the
    cache.
  - **Branch 3 — `refactor/phase-3-form1-thin-shell`**: `Form1` reduced to a thin
    shell (~731 LOC), composition of `ConnectionManager` + `DictionaryCache` +
    `IDeviceVariantConfig` from the service provider; `SendPS_Async` uses
    `ConnectionManager.ActiveProtocol`; RX path subscribes to
    `ConnectionManager.AppLayerDecoded`.
  - **Branch 4 — `refactor/remove-ifs`**: removal of all `#if TOPLIFT/EDEN/EGICON`
    blocks. The device variant is now selected at runtime via `Device:Variant` in
    `appsettings.json` consumed by `IDeviceVariantConfig`. Build configurations
    reduced to `Debug` and `Release` only.

- **REFACTOR_PLAN — `refactor/protocol-interface`** (merged, PR #28):
  prerequisite for Phase 3 + closure of the Phase 2 integration-test debt.
  - `Core/Interfaces/IProtocolService.cs` — facade contract (`SenderId`,
    `AppLayerDecoded`, `SendCommandAsync`, `SendCommandAndWaitReplyAsync`,
    `IDisposable`).
  - `Services/Protocol/ProtocolService` implements `IProtocolService`.
  - `TelemetryService` and `BootService` now depend on `IProtocolService`.
  - **+19 tests** for `Services.AddServices(IConfiguration)` and
    `Infrastructure.Protocol.AddProtocolInfrastructure()`.

- **REFACTOR_PLAN Phase 2** (merged in main 2026-04-20, PRs #24–#27):
  - **Branch A — `refactor/protocol-service`**: `ChannelKind` enum,
    `ICommunicationPort.Kind`, `NetInfo` struct (NL bit layout), thread-safe
    `PacketReassembler`, and the `ProtocolService` facade (encode/decode TP +
    CRC16 Modbus + per-channel chunking + request/reply pattern). +49 cross-platform
    tests.
  - **Branch B — `refactor/services-business`**: `TelemetryService`
    (CMD_CONFIGURE/START/STOP_TELEMETRY + decoding of CMD_TELEMETRY_DATA) and
    `BootService` (CMD_START_PROCEDURE → 1024-byte blocks → CMD_END_PROCEDURE →
    CMD_RESTART_MACHINE x2, retry budget, `CancellationToken` honored between
    blocks). +56 cross-platform tests.
  - **Branch C — `refactor/services-di-integration`**:
    `IDeviceVariantConfig.SenderId` (default 8 for legacy parity);
    `Services.AddServices(IConfiguration)` +
    `Infrastructure.Protocol.AddProtocolInfrastructure()`. `ProtocolService`,
    `ITelemetryService`, `IBootService` are intentionally **not** registered — they
    depend on the runtime-selected port and are created by `ConnectionManager`.
  - **Branch — `refactor/services-foundation`**: renamed `Infrastructure` →
    `Infrastructure.Persistence`; new `Infrastructure.Protocol` project (dual TFM)
    hosting `CanPort` / `BlePort` / `SerialPort`; `Services/` populated with
    `PacketDecoder` (thread-safe `UpdateDictionary` via `Volatile.Read/Write` on
    snapshots) and `DictionarySnapshot`. `Docs/PROTOCOL.md` written from scratch.
    Test count rose from 209 to 274.

- **REFACTOR_PLAN Phase 1 — Protocol abstractions in Core**
  (branch `refactor/protocol-abstractions`):
  - 5 new `Core/Interfaces/`: `ICommunicationPort`, `IPacketDecoder`,
    `ITelemetryService`, `IBootService` (+ `BootState` enum, `BootProgress`
    record), `IDeviceVariantConfig`.
  - 6 new `Core/Models/`: `ConnectionState`, `DeviceVariant`, `DeviceVariantConfig`
    (record + total factory `Create(DeviceVariant)`), `RawPacket`,
    `AppLayerDecodedEvent`, `TelemetryDataPoint`.
  - `Core/Models/ImmutableArrayEquality.cs` — internal helper for structural
    equality of `ImmutableArray<byte>`.
  - `Tests/Unit/Core/Models/` — 6 new test files (33 tests).

- **Excel → Azure dictionary API migration** (Phase 2):
  - `Core/Models/`: 4 domain records (`Variable`, `Command`, `ProtocolAddress`,
    `DictionaryData`).
  - `Core/Interfaces/IDictionaryProvider` — async abstraction with
    `CancellationToken`.
  - `Infrastructure.Persistence/Excel/ExcelDictionaryProvider` — reads from the
    embedded Excel resource.
  - `Infrastructure.Persistence/Api/DictionaryApiProvider` — calls the
    Stem.Dictionaries.Manager REST API; 5 DTOs match the real JSON shape.
  - `Infrastructure.Persistence/FallbackDictionaryProvider` — decorator
    (API → catch `HttpRequestException` → Excel).
  - `AddDictionaryProvider(IConfiguration)` extension method.
  - `appsettings.json` — `DictionaryApi` section.

- **Multi-project architecture** — migration from a monolith to four/five projects
  (`Core`, `Infrastructure.Persistence`, `Infrastructure.Protocol`, `Services`,
  `GUI.Windows`).

- **`.copilot/`** — Copilot long-term-memory instructions, 5 agent files
  (CODING, TEST, DOCS, ISSUES, FORMAL).
- **`Docs/Standards/`** — reusable standards (README, ISSUES, STANDARD,
  TEMPLATE_STANDARD).
- **`README.md`** — project root documentation.
- **`CHANGELOG.md`** — this file.
- **`LICENSE`** — proprietary license.
- **`Stem.Device.Manager.slnx`** — solution file migrated to the modern XML format.
- **`Tests/`** — xUnit project, dual TFM (`net10.0` + `net10.0-windows`).
- **Per-project `README.md`** for `Core/`, `Infrastructure.Persistence/`,
  `Infrastructure.Protocol/`, `Services/`, `GUI.Windows/`, `Tests/`.
- **`Docs/PREPROCESSOR_DIRECTIVES.md`** — historical map of the `#if` blocks that
  used to drive the device variant.

### Changed

- **.NET 8 → .NET 10 migration** — TFM `net10.0-windows10.0.19041.0` for the
  WinForms project, `net10.0` for the cross-platform projects.
- `bitbucket-pipelines.yml` — image `sdk:10.0`, build via `.slnx`, added a `Test`
  step after `Build`.
- Renamed the WinForms project from `STEMPM` → `App` → `GUI.Windows`.
- Migrated the solution file from `.sln` (legacy) to `.slnx` (modern XML, ~58%
  fewer lines).
- Build configurations reduced from 10 → 8 (removed `STEMDM`, `BUTTONPANEL`) and
  finally to 2 (`Debug`, `Release`) once the runtime device-variant selection
  landed.
- `README.md` — multi-project structure, runtime device variant, single-file
  publish recipe.
- `Program.cs` — `IConfiguration` (`appsettings.json` + env vars) and
  `AddDictionaryProvider()` / `AddProtocolInfrastructure()` /
  `AddServices(config)`.
- Namespaces `App.Core.*` → `Core.*` for the models / interfaces moved to `Core/`.

### Removed

- **Legacy `Boot_Interface_Tab`** (`Boot_WF_Tab.cs`) and its 3 null-arg DI tests;
  `Spark_FirmwareUpdate_WF_Tab` is the canonical bootloader UI. Closes #78,
  closes #57.
- **`STEMDM` build configuration** — never used, no `#if STEMDM` in code.
- **`BUTTONPANEL` build configuration** and the entire ButtonPanel module —
  removed in 6 phases (test suite, Form1 wiring, MVP services / presenter / view,
  core models, build config, resources). 27 files deleted, 142 tests removed,
  ~1500 LOC dropped.
- **`ExcelHandler.cs`** — replaced by
  `Infrastructure.Persistence.ExcelDictionaryProvider`.
- **`Stem.Device.Manager.sln`** — replaced by `.slnx`.
- **`STEMProtocol/`** legacy folder (Phase 4) — ~2590 LOC removed once tabs were
  switched to `ConnectionManager` / `ProtocolService`.
- **All `#if TOPLIFT/EDEN/EGICON` blocks** — runtime selection via
  `IDeviceVariantConfig`.

### Fixed

- **spec-001 / issue #74 — `SparkBatchUpdateService` end-of-batch restart only.**
  `RESTART_MACHINE` (CMD `0x000A`) used to fire once per area in a multi-area
  batch, but the SPARK firmware shuts the device down on restart, preventing
  areas 2..N from running. Per STEM firmware-team confirmation (2026-04-27),
  `RESTART_MACHINE` is hoisted out of `RunAreaAsync` and fires exactly once at
  the end of the batch, addressed to the HMI board recipient (`0x000702C1`). On
  the abort path no restart fires — recovery from a half-flashed device is
  operator-driven. The single-file `IBootService.StartFirmwareUploadAsync` is
  unchanged. Tests + `Docs/PROTOCOL.md` §8 +
  `specs/001-spark-ble-fw-stabilize/data-model.md` updated to reflect the batch
  composition.
- `#if BUTTONPANEL` blocks removed from `Form1.cs` (~lines 156, 344) and
  `SplashScreen.cs` (~line 12).
- `ExcelHandler` decoupled from `Form1`: no more direct calls to
  `hExcel.EstraiDizionario()`.

### Removal statistics (cumulative across the modernization wave)

- Files deleted: 27 (19 ButtonPanel + 8 ExcelHandler-related) + the legacy
  `STEMProtocol/` folder.
- Tests removed: 142 (34 ButtonPanel + 8 ExcelHandler).
- LOC removed: ~1500 (legacy modules) + ~2590 (`STEMProtocol/`).
- Build configurations: 10 → 8 → 2.

---

## [0.2.15] - 2026-04-14

State of the legacy project before the modernization wave. ~330 commits, ~56k LOC,
0 automated tests.

### STEM protocol

- Full proprietary stack: Application Layer, Network Layer, Transport Layer,
  Protocol Manager.
- Multi-channel support: CAN (PCAN USB), BLE (Plugin.BLE), Serial
  (`System.IO.Ports`).
- `PacketManager` for multi-channel packet handling with chunk reassembly and
  events.
- Application-layer sniffer for communication debug.
- `SPRollingCode` for rolling-code security.
- Selectable CAN baud rate (100 kbps / 250 kbps).

### Bootloader

- Classic `BootManager`: firmware update over CAN/BLE/Serial with progress bar.
- `Boot_Smart_Tab`: smart bootloader with dynamic HW channel handling.
- Page-number filter, 1024-byte block transfer.
- Reads firmware type from the file to send the correct one to the board.
- Start / End procedure, restart machine, program block.

### Telemetry

- Slow telemetry: single-variable reads via Read Variable command (0x0001).
- Fast telemetry: configure (0x0015), start (0x0016), stop (0x0017), data
  receive (0x0018).
- Real-time charts via OxyPlot (custom X-axis zoom).
- Standard telemetry tab + dedicated TopLift telemetry tab (~42k LOC).

### Excel dictionaries

- `ExcelHandler`: reads dictionaries from an embedded Excel file
  (`Dizionari STEM.xlsx`, ~187k).
- Stream-based support (embedded resource via `MemoryStream`).
- 3 data types: `RowData` (protocol addresses), `CommandData` (commands),
  `VariableData` (variables).
- Sheets: machine/board addresses, command list, per-device variable dictionary.

### Button panel test (test bench)

- Clean MVP architecture: `IButtonPanelTestService` →
  `ButtonPanelTestPresenter` → `ButtonPanelTestTabControl`.
- Dependency injection via `Microsoft.Extensions.DependencyInjection`.
- 4 panel types: DIS0023789, DIS0025205, DIS0026166, DIS0026182.
- 4 test types: full, buttons, LEDs, buzzer.
- `ButtonPanel` factory method with bit masks per panel type.
- Button test: wait-press with 10s timeout, bit-mask check.
- LED test: green / red on with operator confirm.
- Buzzer test: activation with operator confirm.
- `CancellationToken` for test interruption.
- Download of test results.
- Visual indicators per button (Idle, Waiting, Success, Failed).
- Colored RichTextBox messages for visual feedback.
- Error handling in view, presenter and service (PR #9).

### Code generator

- `SP_Code_Generator`: generates `sp_config.h` with protocol configuration
  values (CAN channels, serial buffers, router, device table, logs, …).

### GUI (Windows Forms)

- Main form with multi-page TabControl.
- BLE Scanner tab (device scan + connect).
- CAN tab (TX/RX, PCAN connection state).
- Bootloader tab (classic + smart).
- Telemetry tab (standard + dedicated TopLift).
- Button panel test tab (MVP).
- SplashScreen at startup.
- Status bar with PCAN connection state.
- 10 build configurations: Debug, Release, TOPLIFT-A2-Debug/Release,
  EDEN-Debug/Release, EGICON-Debug/Release, STEMDM, BUTTONPANEL.
- Device-variant configuration via `#if` preprocessor (TOPLIFT, EDEN, EGICON,
  BUTTONPANEL).
- Dynamic form title per configuration.
- Communication channel selection (CAN/BLE/Serial) via menu.
- Minimum form size set.
- Basic logger (`Terminal`) backed by `StringBuilder`.

### Utilities

- `CircularProgressBar`: custom control for a circular progress bar.
- `SerialPortManager`: serial port scan and management.
- `PCAN_Manager`: PCAN USB hardware management.
- `BLE_Manager`: Bluetooth Low Energy management (excluded from compilation in
  some configs).

### Dependencies

- ClosedXML 0.105.0, DocumentFormat.OpenXml 3.5.1 (Excel).
- OxyPlot.WindowsForms 2.2.0 (charts).
- Peak.PCANBasic.NET 5.0.1 (CAN).
- Plugin.BLE 3.2.0 (Bluetooth).
- System.IO.Ports 10.0.5 (serial).
- Microsoft.Extensions.DependencyInjection 10.0.5.
- Microsoft.Windows.SDK.BuildTools 10.0.28000.1721.

### Notes

- Monolithic project: a single `App.csproj` (formerly `STEMPM.csproj`), no
  separation into libraries.
- `Form1.cs` is a God Object (~55k LOC including the Designer file) — GUI +
  protocol logic + telemetry mixed together.
- 0 automated tests.
- 0 formal documentation (READMEs empty up to v2.15).
- Target: `net8.0-windows10.0.19041.0`, `win-x64`.
- Original author: Michele Pignedoli.
- Modernization started by: Luca Veronelli (branch `test/copertura-iniziale`).

---

## Earlier version history (from commit messages)

| Version | SemVer | Milestone |
|---------|--------|-----------|
| 1.0 | 0.1.0 | STEM protocol packaged, sniffer completed |
| 1.1 | 0.1.1 | Reorder for response handling |
| 1.2 | 0.1.2 | First working bootloader |
| 1.3 | 0.1.3 | Pack ID fix, faster boot |
| 1.4 | 0.1.4 | TopLift mainboard update |
| 1.5 | 0.1.5 | Keypad handling + faster boot |
| 1.6 | 0.1.6 | Packet response working |
| 1.7 | 0.1.7 | Bootloader page filter |
| 1.8 | 0.1.8 | PCAN connection status bar, logical-variables dictionary |
| 2.1 | 0.2.1 | Telemetry working |
| 2.7 | 0.2.7 | BLE with/without response |
| 2.8 | 0.2.8 | Read firmware type from file |
| 2.10 | 0.2.10 | Simplification for international distributors |
| 2.11 | 0.2.11 | Complete/Spark/Eden/TopLiftA2 variants, fault decoding |
| 2.14 | 0.2.14 | Serial + selectable CAN baud rate (100/250 kbps) |
| 2.15 | 0.2.15 | Slow-telemetry fix + fast-telemetry activation |

---

## Version URLs

[Unreleased]: https://github.com/luca-veronelli-stem/stem-device-manager/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/luca-veronelli-stem/stem-device-manager/releases/tag/v0.3.0
[0.2.15]: https://bitbucket.org/stem-fw/stem-device-manager/src/80bf9c6/
