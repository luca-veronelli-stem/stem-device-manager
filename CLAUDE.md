# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Commands

**Build:**
```bash
dotnet build Stem.Device.Manager.slnx
```
La variante device (TopLift/Eden/Egicon/Generic) si imposta a runtime via `Device:Variant` in `App/appsettings.json`; non esistono più build configurations device-specific.

**Test:**
```bash
dotnet test Tests/Tests.csproj                                          # tutti i test (dual TFM)
dotnet test Tests/Tests.csproj --framework net10.0                      # cross-platform (CI Linux)
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~Tests.Unit" # solo unit test
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~<ClassName>" # singola classe
```

**Run:**
```bash
dotnet run --project App/App.csproj
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
Core/           [net10.0, zero dipendenze NuGet]    — domain models + interfacce (dizionario + protocollo)
Infrastructure.Persistence/ [net10.0]               — provider dati dizionari (API + Excel + Fallback)
Infrastructure.Protocol/    [net10.0;net10.0-windows] — adapter HW (Can/Ble/Serial), driver nativi
Services/       [net10.0]                           — logica pura (PacketDecoder, DictionarySnapshot, ...)
App/            [net10.0-windows, WinForms]         — GUI + protocollo legacy + DI entry point
Tests/          [dual TFM: net10.0 + net10.0-windows] — 278 test net10.0 / 458 test net10.0-windows
Specs/          [Lean 4]                            — formalizzazioni dei tipi estratti (Phase1/)
```

Dipendenze: `App → {Infrastructure.Persistence, Infrastructure.Protocol, Services} → Core`, `Tests → App, Infrastructure.*, Services, Core`.

### Componenti chiave

**Core** — Modelli dizionario: `Variable`, `Command`, `ProtocolAddress`, `DictionaryData`. Modelli protocol abstractions (Fase 1 refactor): `ConnectionState`, `DeviceVariant`, `DeviceVariantConfig`, `RawPacket`, `AppLayerDecodedEvent`, `TelemetryDataPoint`, `BootState`/`BootProgress`. Interfacce: `IDictionaryProvider`, `ICommunicationPort`, `IPacketDecoder`, `ITelemetryService`, `IBootService`, `IDeviceVariantConfig`.

**Infrastructure.Persistence** — `DictionaryApiProvider` (REST HTTP verso Azure), `ExcelDictionaryProvider` (ClosedXML su `Dizionari STEM.xlsx` embedded), `FallbackDictionaryProvider` (decorator: API → catch `HttpRequestException` → Excel). Registrazione DI via `AddDictionaryProvider(IConfiguration)` — legge `DictionaryApi:BaseUrl/ApiKey/TimeoutSeconds` da `appsettings.json`.

**Infrastructure.Protocol** — Adapter hardware che implementano `ICommunicationPort`: `CanPort` (wrappa `PCANManager` via `IPcanDriver`), `BlePort` (via `IBleDriver`, impl in `App.BLEManager`), `SerialPort` (via `ISerialDriver`, impl in `App.SerialPortManager`). Convention payload: CAN = arbId LE prefix; BLE/Serial = pass-through. Pattern allineato a `Stem.ButtonPanel.Tester/Infrastructure` e `Stem.Production.Tracker/Infrastructure.Protocol`. Dual TFM: compila cross-platform, runtime Windows-only.

**Services** — Logica pura cross-platform. Fase 2 step 1 completato: `PacketDecoder` + `DictionarySnapshot` in `Services/Protocol/`. Ctor injection di `IDictionaryProvider`, `ICommunicationPort`, ecc. (zero dipendenze WinForms).

**App/Program.cs** — Entry point DI: registra `IDictionaryProvider` (via Infrastructure.Persistence). Configurazione da `appsettings.json` + env vars.

**App/Form1.cs** — God Object (~55k LOC). Non toccare senza piano di refactoring. Usa `IDictionaryProvider` via DI.

**App/STEMProtocol/** — Stack multi-layer proprietario: `STEM_protocol.cs` → `PacketManager.cs` → `CanDataLayer.cs` / `SerialDataLayer.cs`. Comunicazione hardware via CAN (PCAN), BLE, Serial.

**App/GUI/** — Moduli di interfaccia per tab (Boot, BLE, Telemetry, ecc.). Architettura da modernizzare in Fase 3 (MVP pattern da valutare per il refactoring incrementale).

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
| 1 | Test coverage codice esistente | ✅ Completata |
| 2 | Migrazione dizionari Excel → API Azure (infrastruttura) | ✅ Completata |
| 3 | Refactoring incrementale (estrarre classi, spezzare Form1) | ✅ Completata (rimozione ExcelHandler) |
| 4 | Consumer migration a `IDictionaryProvider` (Form1 tabs) | ✅ Completata |
| 5 | Valutazione migrazione UI (WPF o altro) | ⏳ Futura |

`ExcelHandler.cs` è stato rimosso. Tutti i consumer usano `IDictionaryProvider` via DI.

### Piano di refactoring architetturale (REFACTOR_PLAN.md)

Vedi [`Docs/REFACTOR_PLAN.md`](Docs/REFACTOR_PLAN.md) per il piano branch-by-branch verso architettura modulare (Core / Infrastructure.Persistence / Infrastructure.Protocol / Services / App).

| Branch | Descrizione | Stato |
|--------|-------------|-------|
| `refactor/protocol-abstractions` | Fase 1 — interfacce + modelli Core, formalizzazioni Lean 4 in `Specs/Phase1/` | ✅ Completata |
| `refactor/services-layer` | Fase 2 — PacketDecoder + HW adapter (CanPort/BlePort/SerialPort) + PROTOCOL.md + rinomina Infrastructure→Infrastructure.Persistence | 🚧 In corso (Step 1-2 fatti; TelemetryService/BootService/ProtocolService pendenti) |
| `refactor/phase-3-form1-decomposition` | Decomporre Form1, rimuovere `#if`, tab autonome, estrarre BLE/Serial manager | ⏳ In attesa |
| `refactor/phase-4-protocol-migration-prep` | Adapter per `Stem.Communication` NuGet + feature flag | ⏳ In attesa |

---

## File di riferimento

| File | Scopo |
|------|-------|
| `.copilot/copilot-instructions.md` | Workflow dettagliato, profilo utente, log sessioni |
| `Docs/REFACTOR_PLAN.md` | Piano branch-by-branch di modernizzazione architetturale |
| `Docs/PROTOCOL.md` | Funzionamento interno del protocollo STEM (layering, CRC, chunking, comandi) |
| `Docs/PREPROCESSOR_DIRECTIVES.md` | Catalogo blocchi `#if TOPLIFT/EDEN/EGICON` da rimuovere in Fase 3 |
| `App/appsettings.json` | Configurazione `DictionaryApi` (BaseUrl, ApiKey, TimeoutSeconds) |
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
