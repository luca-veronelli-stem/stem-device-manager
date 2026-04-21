# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Commands

**Build:**
```bash
dotnet build Stem.Device.Manager.slnx
```
La variante device (TopLift/Eden/Egicon/Generic) si imposta a runtime via `Device:Variant` in `GUI.Windows/appsettings.json`; non esistono più build configurations device-specific.

**Test:**
```bash
dotnet test Tests/Tests.csproj                                          # tutti i test (dual TFM)
dotnet test Tests/Tests.csproj --framework net10.0                      # cross-platform (CI Linux)
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~Tests.Unit" # solo unit test
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~<ClassName>" # singola classe
```

**Run:**
```bash
dotnet run --project GUI.Windows/GUI.Windows.csproj
```

**CI (Bitbucket Pipelines):**
```bash
dotnet build Stem.Device.Manager.slnx --configuration Release -p:EnableWindowsTargeting=true
dotnet test Tests/Tests.csproj --framework net10.0  # solo test cross-platform su Linux
```

---

## Architettura

### Struttura multi-progetto

```
Core/           [net10.0, zero dipendenze NuGet]     — domain models + interfacce (dizionario + protocollo)
Infrastructure.Persistence/ [net10.0]                — provider dati dizionari (API + Excel + Fallback)
Infrastructure.Protocol/    [net10.0;net10.0-windows] — port HW (CanPort/BlePort/SerialPort) + driver legacy (Legacy/)
Services/       [net10.0]                            — logica pura (ProtocolService, TelemetryService, BootService, ConnectionManager, DictionaryCache)
GUI.Windows/            [net10.0-windows, WinForms]          — GUI + DI entry point (niente più protocollo embedded)
Tests/          [dual TFM: net10.0 + net10.0-windows] — 292 test net10.0 / 470 test net10.0-windows
Specs/          [Lean 4]                             — formalizzazioni dei tipi estratti (Phase1/)
```

Dipendenze: `App → {Infrastructure.Persistence, Infrastructure.Protocol, Services} → Core`, `Tests → App, Infrastructure.*, Services, Core`.

### Componenti chiave

**Core** — Modelli dizionario: `Variable`, `Command`, `ProtocolAddress`, `DictionaryData`. Modelli protocol abstractions: `ConnectionState`, `DeviceVariant`, `DeviceVariantConfig`, `RawPacket`, `AppLayerDecodedEvent`, `TelemetryDataPoint` (+ `TelemetrySource` FastStream/ReadReply, helper `NumericValue`), `BootState`/`BootProgress`, `SmartBootDeviceEntry`. Interfacce: `IDictionaryProvider`, `ICommunicationPort`, `IPacketDecoder`, `IProtocolService`, `ITelemetryService`, `IBootService`, `IDeviceVariantConfig`.

**Infrastructure.Persistence** — `DictionaryApiProvider` (REST HTTP verso Azure), `ExcelDictionaryProvider` (ClosedXML su `Dizionari STEM.xlsx` embedded), `FallbackDictionaryProvider` (decorator: API → catch `HttpRequestException` → Excel). Registrazione DI via `AddDictionaryProvider(IConfiguration)` — legge `DictionaryApi:BaseUrl/ApiKey/TimeoutSeconds` da `appsettings.json`.

**Infrastructure.Protocol** — Due sottocartelle:
- `Hardware/` — port HW che implementano `ICommunicationPort`: `CanPort` (wrappa `PCANManager` via `IPcanDriver`), `BlePort` (via `IBleDriver`), `SerialPort` (via `ISerialDriver`). Convention payload: CAN = arbId LE prefix; BLE/Serial = pass-through.
- `Legacy/` — driver concreti `BLEManager` (implementa `IBleDriver` via Plugin.BLE) e `SerialPortManager` (implementa `ISerialDriver` via `System.IO.Ports`). Compilati solo su TFM `net10.0-windows`. Saranno sostituiti quando `Stem.Communication` NuGet sarà disponibile (Fase 5): il contratto `ICommunicationPort` è già stabile, basta swappare i wrapper.

**Services** — Logica pura cross-platform:
- `Protocol/ProtocolService` — facade STEM (encode TP + CRC16 + chunking + framing per canale; decode + reassembly + event `AppLayerDecoded`; pattern request/reply). Implementa `IProtocolService`. **Non registrato in DI**: creato a runtime dal `ConnectionManager` quando si sceglie il canale.
- `Telemetry/TelemetryService` — implementa `ITelemetryService`. Fast stream (`StartFastTelemetryAsync`) + one-shot `ReadOneShotAsync`/`WriteOneShotAsync` + dizionario mutabile (`AddToDictionary`, `AddToDictionaryForWrite`, `RemoveFromDictionary`, `ResetDictionary`, `GetVariableName`). Decode CMD_TELEMETRY_DATA (LE) e CMD_READ_VARIABLE reply (BE).
- `Boot/BootService` — implementa `IBootService`. `StartFirmwareUploadAsync` (sequenza completa) + step separati `StartBootAsync`/`EndBootAsync`/`RestartAsync`/`UploadBlocksOnlyAsync` per workflow Egicon multi-step.
- `Cache/DictionaryCache` — cache centralizzata commands+addresses+variables, load via `IDictionaryProvider`, evento `DictionaryUpdated`, sincronizzazione automatica con `IPacketDecoder`.
- `Cache/ConnectionManager` — aggrega le 3 port (via `IEnumerable<ICommunicationPort>`), espone `ActiveProtocol`/`CurrentBoot`/`CurrentTelemetry` (ricreati ad ogni `SwitchToAsync`), forward events `AppLayerDecoded`/`TelemetryDataReceived`/`BootProgressChanged` (i consumer si iscrivono una volta sola).

**GUI.Windows/Program.cs** — Entry point DI: registra `IDictionaryProvider` + `IBleDriver`→`BLEManager` + `ISerialDriver`→`SerialPortManager` + `AddProtocolInfrastructure()` + `AddServices(config)`.

**GUI.Windows/Forms/Form1.cs** — Shell WinForms (731 LOC post-Phase 4). Usa `IServiceProvider` per ottenere `DictionaryCache`, `ConnectionManager`, `IDeviceVariantConfig`. `SendPS_Async` invia via `ConnectionManager.ActiveProtocol`; handler RX sottoscritto a `ConnectionManager.AppLayerDecoded`. Menu canale → `SwitchToAsync`.

**GUI.Windows/*_WF_Tab.cs** — Tab WinForms (`Boot_Interface_Tab`, `Boot_Smart_Tab`, `Telemetry_Tab`, `TopLiftTelemetry_Tab`, `BLEInterfaceTab`). Ctor DI di `DictionaryCache` + `ConnectionManager`. I tab consumano `_connMgr.CurrentBoot`/`CurrentTelemetry` e sottoscrivono i forward events.

### Strategia test dual TFM

`net10.0` (test cross-platform): Core models + Infrastructure.Persistence providers + Services pure logic — girano in CI su Linux.  
`net10.0-windows`: test che dipendono da WinForms/App — richiedono Windows, non girano in CI.  
I mock sono manuali (no librerie esterne) in `Tests/Integration/Presenter/Mocks/`.

### Build configurations

2 configurazioni: `Debug` e `Release`. La variante device (TopLift/Eden/Egicon/Generic) è selezionata a runtime tramite `IDeviceVariantConfig` iniettato dal composition root e letto da `Device:Variant` in `appsettings.json`. I blocchi `#if TOPLIFT/EDEN/EGICON` sono stati rimossi nel branch `refactor/remove-ifs` (2026-04-21).

---

## Convenzioni

**Nomenclatura:** nomi classi/metodi/variabili/enum in **INGLESE**. Documentazione (markdown, XML comments, commenti inline, GUI) in **ITALIANO**.

**Stile C#:** nullable types abilitati (non restituire mai `null` per errori — lanciare eccezioni), `CancellationToken` in tutti i metodi async, `Lock` + `Volatile.Read/Write` per thread-safety, funzioni < 15 LOC, early returns, limite 100-110 soft / 120 hard caratteri per riga.

**Test naming:** `{ClassName}Tests`, metodi `{Method}_{Scenario}_{ExpectedResult}`. `[Fact]` per casi singoli, `[Theory]` + `[InlineData]` per parametrici.

**Workflow:** discutere il piano prima di implementare. Preferire approccio pragmatico (funziona > elegante). Non aggiungere interfacce o astrazioni senza necessità concreta.

---

## Piano di modernizzazione (stato corrente)

| Fase | Descrizione | Stato |
|------|-------------|-------|
| 1 | Protocol abstractions in Core (interfacce + modelli + Lean 4 specs) | ✅ Completata |
| 2 | Services layer (ProtocolService/TelemetryService/BootService/HW adapter) | ✅ Completata |
| 3 | Decomposizione Form1 (DictionaryCache + ConnectionManager + tab decoupling + remove #if) | ✅ Completata |
| 4 | Switch al nuovo stack + eliminazione protocollo embedded | ✅ Completata |
| 5 | Migrazione a `Stem.Communication` NuGet (quando disponibile) + valutazione UI WPF | ⏳ Futura |

### Piano di refactoring architetturale (REFACTOR_PLAN.md)

Vedi [`Docs/REFACTOR_PLAN.md`](Docs/REFACTOR_PLAN.md) per il piano branch-by-branch.

| Branch | Descrizione | Stato |
|--------|-------------|-------|
| `refactor/protocol-abstractions` | Fase 1 — interfacce + modelli Core, Lean 4 specs | ✅ merged |
| `refactor/services-foundation` → `protocol-service` → `services-business` → `services-di-integration` | Fase 2 — multi-branch popolamento Services/ e Infrastructure.Protocol/ | ✅ merged (PR #24-#27) |
| `refactor/protocol-interface` | Fase 3 prerequisite — `IProtocolService` in Core | ✅ merged (PR #28) |
| `refactor/dictionary-cache` → `tab-decoupling` → `form1-thin-shell` → `remove-ifs` | Fase 3 — decomposizione Form1, DictionaryCache/ConnectionManager, `#if` rimossi | ✅ merged (PR #29-#32) |
| `refactor/phase-4-switch-to-new-stack` | Fase 4 — wiring Form1+tab sul nuovo stack, eliminazione `GUI.Windows/STEMProtocol/` (2590 LOC), driver in `Infrastructure.Protocol/Legacy/` | ✅ Completata (in merge) |
| `refactor/phase-4b-app-reorganization` | Fase 4b — riorganizzazione cartelle progetto App (Forms/, Tabs/, Resources/) | ⏳ In attesa |

---

## File di riferimento

| File | Scopo |
|------|-------|
| `.copilot/copilot-instructions.md` | Workflow dettagliato, profilo utente, log sessioni |
| `Docs/REFACTOR_PLAN.md` | Piano branch-by-branch di modernizzazione architetturale |
| `Docs/PROTOCOL.md` | Funzionamento interno del protocollo STEM (layering, CRC, chunking, comandi) |
| `Docs/PREPROCESSOR_DIRECTIVES.md` | Mappa storica dei blocchi `#if TOPLIFT/EDEN/EGICON` rimossi (Fase 3 Branch 4) |
| `GUI.Windows/appsettings.json` | Configurazione `DictionaryApi` (BaseUrl, ApiKey, TimeoutSeconds) |
| `Directory.Build.props` | Versione SemVer, autori, copyright |
| `bitbucket-pipelines.yml` | CI/CD Bitbucket |

---

## Specifiche di dominio (Lean 4)

Formalizzazioni esistenti in `Specs/`:

| Cartella | Branch | Contenuto |
|----------|--------|-----------|
| `Specs/Phase1/` | `refactor/protocol-abstractions` | Tipi e interfacce Core: `ConnectionState`, `DeviceVariant`, `DeviceVariantConfig` (+ factory totale + teoremi di correttezza), `RawPacket`, `AppLayerDecodedEvent`, `TelemetryDataPoint`, `Interfaces` (ICommunicationPort, IPacketDecoder, ITelemetryService, IBootService, IDeviceVariantConfig) |

Moduli candidati per le formalizzazioni future (Fase 2–3):
1. `STEMProtocol/PacketManager` → `PacketDecoder` — logica parsing/encoding pacchetti
2. `STEMProtocol/STEM_protocol` → `ProtocolService` — stack layer applicativo
3. `TelemetryManager` → `TelemetryService` — lettura variabili e campionamento
4. `BootManager` → `BootService` — sequenza aggiornamento firmware (macchina a stati già abbozzata in `Specs/Phase1/Interfaces.lean`)
