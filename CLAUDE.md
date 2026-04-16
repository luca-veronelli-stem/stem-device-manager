# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## Commands

**Build:**
```bash
dotnet build Stem.Device.Manager.slnx
dotnet build App/App.csproj -c TOPLIFT-A2-Release  # device-specific config
```

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
Core/          [net10.0, zero dipendenze NuGet]   — domain models + interfacce
Infrastructure/ [net10.0]                          — provider dati (API + Excel + Fallback)
Services/       [net10.0-windows, vuoto]           — placeholder Fase 3
App/            [net10.0-windows, WinForms]        — GUI + protocollo + DI entry point
Tests/          [dual TFM: net10.0 + net10.0-windows] — 258 test
```

Dipendenze: `App → Infrastructure → Core`, `Tests → App, Infrastructure, Core`.

### Componenti chiave

**Core** — Modelli: `Variable`, `Command`, `ProtocolAddress`, `DictionaryData`. Interfacce: `IDictionaryProvider`.

**Infrastructure** — `DictionaryApiProvider` (REST HTTP verso Azure), `ExcelDictionaryProvider` (ClosedXML su `Dizionari STEM.xlsx` embedded), `FallbackDictionaryProvider` (decorator: API → catch `HttpRequestException` → Excel). Registrazione DI via `AddDictionaryProvider(IConfiguration)` — legge `DictionaryApi:BaseUrl/ApiKey/TimeoutSeconds` da `appsettings.json`.

**App/Program.cs** — Entry point DI: registra `IDictionaryProvider` (via Infrastructure). Configurazione da `appsettings.json` + env vars.

**App/Form1.cs** — God Object (~55k LOC). Non toccare senza piano di refactoring. Usa `IDictionaryProvider` via DI.

**App/STEMProtocol/** — Stack multi-layer proprietario: `STEM_protocol.cs` → `PacketManager.cs` → `CanDataLayer.cs` / `SerialDataLayer.cs`. Comunicazione hardware via CAN (PCAN), BLE, Serial.

**App/GUI/** — Moduli di interfaccia per tab (Boot, BLE, Telemetry, ecc.). Architettura da modernizzare in Fase 3 (MVP pattern da valutare per il refactoring incrementale).

### Strategia test dual TFM

`net10.0` (test cross-platform): Core models + Infrastructure providers — girano in CI su Linux.  
`net10.0-windows`: test che dipendono da WinForms/App — richiedono Windows, non girano in CI.  
I mock sono manuali (no librerie esterne) in `Tests/Integration/Presenter/Mocks/`.

### Build configurations

8 configurazioni: `Debug`, `Release`, `TOPLIFT-A2-Debug`, `TOPLIFT-A2-Release`, `EDEN-Debug`, `EDEN-Release`, `EGICON-Debug`, `EGICON-Release`. Le varianti device usano `#if TOPLIFT / EDEN / EGICON` nel codice.

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

---

## File di riferimento

| File | Scopo |
|------|-------|
| `.copilot/copilot-instructions.md` | Workflow dettagliato, profilo utente, log sessioni |
| `Docs/MIGRATION_API.md` | Piano e log della migrazione dizionari (Fase 2) |
| `App/appsettings.json` | Configurazione `DictionaryApi` (BaseUrl, ApiKey, TimeoutSeconds) |
| `Directory.Build.props` | Versione SemVer, autori, copyright |
| `bitbucket-pipelines.yml` | CI/CD Bitbucket |

---

## Specifiche di dominio (Lean 4)

Sezione da popolare durante la Fase 3 man mano che i moduli vengono estratti da Form1, seguendo il metodo TDD+Lean: formalizzare il comportamento esistente in Lean 4 → confermare con l'utente → scrivere test → implementare.

I moduli candidati in ordine di priorità per la formalizzazione:
1. `STEMProtocol/PacketManager` — logica parsing/encoding pacchetti
2. `STEMProtocol/STEM_protocol` — stack layer applicativo
3. `TelemetryManager` — lettura variabili e campionamento
4. `BootManager` — sequenza aggiornamento firmware
