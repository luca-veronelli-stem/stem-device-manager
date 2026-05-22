# Stem.Device.Manager

[![Version](https://img.shields.io/badge/version-0.4.0-blue)](./CHANGELOG.md)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-350%20%2F%20552-brightgreen)](./Tests/)
[![License](https://img.shields.io/badge/license-Proprietary-red)](#license)

> **Desktop application for managing, diagnosing and communicating with STEM devices over the proprietary multi-channel protocol (CAN, BLE, Serial).**
> **Last updated:** 2026-05-21

---

## Overview

Stem Device Manager is a Windows desktop tool used to:

- **Communicate** with STEM devices via the proprietary protocol (CAN, BLE, Serial).
- **Read / write** variables and commands using dictionaries served by the Azure REST API (with embedded Excel fallback).
- **Update firmware** via the proprietary bootloader (classic + Spark batch).
- **Monitor telemetry** in real time with OxyPlot charts (slow + fast streams).
- **Generate code** for protocol configuration (`sp_config.h`).

### Current state

The codebase is being modernized incrementally. Phases 1–4 of the refactor are complete:

- `Form1.cs` has been reduced to a thin shell (~731 LOC) on top of `ConnectionManager` + `DictionaryCache`.
- **350 cross-platform tests** + **552 Windows-only tests** (xUnit).
- Multi-project layout:
  - **Core** (`net10.0`) — domain models + interfaces.
  - **Infrastructure.Persistence** (`net10.0`) — dictionary providers (Azure API + Excel + Fallback).
  - **Infrastructure.Protocol** (dual TFM) — HW adapters for CAN/BLE/Serial + legacy drivers.
  - **Services** (`net10.0`) — pure logic (`ProtocolService`, `TelemetryService`, `BootService`, `ConnectionManager`, `DictionaryCache`).
  - **GUI.Windows** (`net10.0-windows`, WinForms) — entry point + GUI.
- Architectural roadmap in [`Docs/REFACTOR_PLAN.md`](./Docs/REFACTOR_PLAN.md):
  - Phase 1 — protocol abstractions in Core ✅
  - Phase 2 — services layer + HW adapters ✅
  - Phase 3 — Form1 decomposition ✅
  - Phase 4 — switch to the new stack, removal of legacy `STEMProtocol/` ✅
  - Phase 5 — migration to the `Stem.Communication` NuGet (when available) ⏳

---

## Features

| Feature | Status | Description |
|---------|--------|-------------|
| **STEM protocol** | ✅ | Proprietary stack (Application, Network, Transport) |
| **CAN (PCAN)** | ✅ | Communication via Peak PCAN USB |
| **BLE** | ✅ | Bluetooth Low Energy via Plugin.BLE |
| **Serial** | ✅ | Communication over a COM port |
| **Azure dictionary API** | ✅ | REST provider with embedded Excel fallback (DI) |
| **Bootloader** | ✅ | Firmware update (classic + Spark batch) |
| **Telemetry** | ✅ | Variable reads + OxyPlot charts (slow + fast) |
| **Code generator** | ✅ | Generates `sp_config.h` |
| **Automated tests** | ✅ | 350 cross-platform + 552 Windows (xUnit) |

---

## Requirements

- **.NET 10.0** (Windows 10+ x64).
- **Visual Studio 2022/2026** with the *Desktop development with .NET* workload.
- **PCAN USB** (optional, only required for CAN communication).

### Dependencies

| Package | Version | Use |
|---------|---------|-----|
| ClosedXML | 0.105.0 | Excel dictionary reader (Infrastructure.Persistence) |
| DocumentFormat.OpenXml | 3.5.1 | Excel format support |
| OxyPlot.WindowsForms | 2.2.0 | Telemetry charts |
| Peak.PCANBasic.NET | 5.0.1 | PCAN CAN interface |
| Plugin.BLE | 3.2.0 | Bluetooth Low Energy |
| System.IO.Ports | 10.0.5 | Serial communication |
| Microsoft.Extensions.DependencyInjection | 10.0.5 | DI container |

---

## Quick Start

```powershell
# Clone
git clone https://bitbucket.org/stem-fw/stem-device-manager

# Build
dotnet build Stem.Device.Manager.slnx

# Test (cross-platform only — matches CI)
dotnet test Tests/Tests.csproj --framework net10.0

# Run
dotnet run --project GUI.Windows/GUI.Windows.csproj
```

### Build configurations

Two configurations: `Debug` and `Release`. The device variant (TopLift / Eden / Egicon / Generic) is selected at runtime via `Device:Variant` in [`GUI.Windows/appsettings.json`](./GUI.Windows/appsettings.json) — the historical `#if TOPLIFT/EDEN/EGICON` blocks were removed in Phase 3.

### Single-file publish (field-test executable)

The **recommended** path is the [`Release` GitHub Actions workflow](./.github/workflows/release.yml): pushing a `v*.*.*` tag (after creating the GitHub Release with `gh release create`) automatically builds the self-contained single-file exe and attaches it to the release. The workflow can also be re-run on demand via `gh workflow run release.yml -f tag=v0.4.0` to attach the exe to a release that was cut before the workflow existed. The workflow is marked **temporary** until this repo adopts the STEM standards — see [issue #111](https://github.com/luca-veronelli-stem/stem-device-manager/issues/111).

For local field tests (or when Actions is unavailable), the same recipe runs by hand:

```powershell
dotnet publish GUI.Windows/GUI.Windows.csproj `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o publish/v0.4.0
```

The Excel dictionary (`Resources/Dizionari STEM.xlsx`) is already an embedded resource, so the fallback works without shipping a separate file. `appsettings.json` is the only loose file copied next to the `.exe`; if you want it embedded too, set `Device:Variant` and `DictionaryApi:*` via environment variables (see [Configuration](#configuration)) and remove the `appsettings.json` from the publish folder.

---

## Configuration

`GUI.Windows/appsettings.json` (committed defaults):

```json
{
  "DictionaryApi": {
    "BaseUrl": "https://app-dictionaries-manager-prod.azurewebsites.net/",
    "ApiKey": "<api-key>",
    "TimeoutSeconds": 30
  },
  "Device": {
    "Variant": "Generic",
    "SenderId": 8
  }
}
```

Any value can be overridden at runtime via environment variables (use `__` for the section separator):

```powershell
$env:DictionaryApi__ApiKey   = "<api-key>"
$env:DictionaryApi__BaseUrl  = "https://..."
$env:Device__Variant         = "TopLift"
```

### API key (v0.4.0 stopgap)

The Azure dictionary API key is **not committed**. `appsettings.json` ships with an empty `ApiKey` placeholder; set `DictionaryApi__ApiKey` in the environment before first run, or place the value in a gitignored `GUI.Windows/appsettings.Production.json`. See [issue #94](https://github.com/luca-veronelli-stem/stem-device-manager/issues/94) for the full security rationale (current stopgap + deferred DPAPI / zero-touch provisioning posture for external supplier deployment).

---

## Solution Structure

```
Stem.Device.Manager/
├── Stem.Device.Manager.slnx              Solution file (modern XML)
├── Directory.Build.props                 Centralized version + author metadata
├── Core/                                 Domain models + interfaces (net10.0, zero deps)
├── Infrastructure.Persistence/           Dictionary providers (net10.0)
│   ├── Api/                              DictionaryApiProvider + DTOs (Azure REST)
│   ├── Excel/                            ExcelDictionaryProvider (embedded fallback)
│   ├── FallbackDictionaryProvider        API → Excel decorator
│   └── DependencyInjection.cs            AddDictionaryProvider()
├── Infrastructure.Protocol/              HW adapters (dual TFM)
│   ├── Hardware/                         CanPort, BlePort, SerialPort + driver abstractions
│   └── Legacy/                           BLEManager, SerialPortManager (Windows-only)
├── Services/                             Pure cross-platform logic (net10.0)
│   ├── Protocol/                         ProtocolService, PacketDecoder, DictionarySnapshot
│   ├── Telemetry/                        TelemetryService
│   ├── Boot/                             BootService, SparkBatchUpdateService
│   └── Cache/                            DictionaryCache, ConnectionManager
├── GUI.Windows/                          Windows Forms entry point (net10.0-windows)
│   ├── Program.cs                        DI + IConfiguration
│   ├── Forms/Form1.cs                    Main form (thin shell)
│   ├── Tabs/                             WinForms tab pages
│   └── Resources/                        Icons + embedded Excel dictionary
├── Lean/Spec001/                         Lean 4 invariants for Spark BLE stabilization
├── Tests/                                xUnit, dual TFM (292 / 470)
└── Docs/                                 REFACTOR_PLAN, PROTOCOL, Standards
```

---

## Documentation

- [GUI.Windows — main project](./GUI.Windows/README.md)
- [Core — domain models and interfaces](./Core/README.md)
- [Infrastructure.Persistence — dictionary providers (API + Excel)](./Infrastructure.Persistence/README.md)
- [Infrastructure.Protocol — HW adapters CAN/BLE/Serial](./Infrastructure.Protocol/README.md)
- [Services — pure application logic](./Services/README.md)
- [Tests — automated tests](./Tests/README.md)
- [Standards & templates](./Docs/Standards/)
- [Architectural refactoring plan](./Docs/REFACTOR_PLAN.md)
- [STEM protocol — internal workings](./Docs/PROTOCOL.md)
- [Preprocessor directives — historical map](./Docs/PREPROCESSOR_DIRECTIVES.md)
- [Lean/Spec001 — Lean 4 formalizations](./Lean/Spec001/)
- [CHANGELOG](./CHANGELOG.md)
- [LICENSE](./LICENSE)

---

## License

- **Owner:** STEM E.m.s.
- **Authors:** Michele Pignedoli, Luca Veronelli
- **Creation date:** 2024-06-27
- **License:** Proprietary — All rights reserved
