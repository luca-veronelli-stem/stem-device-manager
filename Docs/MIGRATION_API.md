# MIGRATION_API.md — Piano migrazione Excel → API Dizionari Azure

**Creato:** 2026-04-14  
**Branch corrente:** `feature/api-dizionari`  
**Stato:** Step 3 (Strategy) — piano approvato, implementazione non iniziata

---

## Obiettivo

Sostituire la lettura dei dizionari (variabili, comandi, indirizzi protocollo) dal file Excel
embedded (`Dizionari STEM.xlsx`) con chiamate all'API REST di **Stem.Dictionaries.Manager**
ospitata su Azure, mantenendo un fallback Excel quando l'API non è raggiungibile.

## Contesto

### Situazione attuale (legacy)
- `ExcelHandler.cs` legge da `Dizionari STEM.xlsx` (embedded resource o file esterno)
- 3 tipi di dato: `RowData` (indirizzi protocollo), `CommandData` (comandi), `VariableData` (variabili)
- 4 metodi pubblici: 2 overload `EstraiDatiProtocollo`, 2 overload `EstraiDizionario`
- Consumato da: `Form1.cs` (~15 punti), `TelemetryManager.cs` (6), `Boot_Smart_WF_Tab.cs` (2),
  `Telemetry_WF_Tab.cs` (2), `TopLift_Telemetry_WF_Tab.cs` (2)
- Seleziona variabili per `RecipientId` (indirizzo protocollo hex della board)

### Situazione target
- L'API di Dictionaries.Manager espone:
  - `GET /api/devices` → lista device (`DeviceSummaryDto`)
  - `GET /api/devices/{id}/boards` → board con `ProtocolAddress` e `DictionaryId`
  - `GET /api/dictionaries/{id}/resolved` → variabili risolte (standard + specifiche)
  - `GET /api/commands` → lista comandi
  - `GET /api/commands/device/{deviceId}` → comandi abilitati per device
  - `GET /api/boards/{id}/definition` → definizione board con variabili
- Autenticazione: header `X-Api-Key` con chiave `ApiKeys__DeviceManager`
- Fallback: se API non raggiungibile → usa Excel embedded (come oggi)

### Flusso dati target
```
RecipientId (hex) → cerca board con ProtocolAddress matching
                   → ottieni DictionaryId
                   → GET /api/dictionaries/{id}/resolved → variabili
                   → GET /api/commands → comandi (globali, non per-board)
```

---

## Architettura target (multi-progetto)

### Struttura soluzione

```
Stem.Device.Manager/
  Stem.Device.Manager.slnx

  Core/                              net10.0 (cross-platform)
    Core.csproj                      Zero dipendenze NuGet
    Models/
      Variable.cs                    Modello dominio (ex ExcelHandler.VariableData)
      Command.cs                     Modello dominio (ex ExcelHandler.CommandData)
      ProtocolAddress.cs             Modello dominio (ex ExcelHandler.RowData)
      ButtonPanel.cs                 ← da App/Core/Models/
      ButtonIndicator.cs             ← da App/Core/Models/
      ButtonPanelTestResult.cs       ← da App/Core/Models/
    Enums/
      ButtonPanelEnums.cs            ← da App/Core/Enums/
    Interfaces/
      IDictionaryProvider.cs         Astrazione: "dammi variabili/comandi/indirizzi"
      IButtonPanelTestService.cs     ← da App/Core/Interfaces/
      IButtonPanelTestTab.cs         ← da App/Core/Interfaces/

  Infrastructure/                    net10.0 (cross-platform)
    Infrastructure.csproj            → dipende da Core
    Api/
      DictionaryApiOptions.cs        Opzioni: BaseUrl, ApiKey, Timeout
      DictionaryApiProvider.cs       HttpClient → API Azure (implementa IDictionaryProvider)
      Dtos/                          DTO deserializzazione risposte API
        DeviceSummaryDto.cs
        BoardSummaryDto.cs
        DictionaryResolvedDto.cs
        ResolvedVariableDto.cs
        CommandDto.cs
    Excel/
      ExcelDictionaryProvider.cs     Wrapper ExcelHandler (implementa IDictionaryProvider)
    DependencyInjection.cs           Registra provider (API con fallback Excel)

  Services/                          net10.0-windows10.0.19041.0
    Services.csproj                  → dipende da Core + Infrastructure
    ButtonPanelTestService.cs        ← da App/Services/

  App/                               net10.0-windows10.0.19041.0 (WinForms)
    App.csproj                       → dipende da Services (+ Core transitivo)
    Program.cs                       DI: registra tutto
    Form1.cs                         God Object (invariato in questa migrazione)
    ExcelHandler.cs                  Resta (usato da ExcelDictionaryProvider + Form1 diretto)
    STEMProtocol/                    Resta (forte accoppiamento con Form1)
    GUI/                             MVP ButtonPanel
    Resources/                       Excel embedded, icone
    ...tutti i *_WF_Tab.cs

  Tests/                             net10.0 + net10.0-windows10.0.19041.0
    Tests.csproj                     → dipende da Core + Infrastructure + App
    Unit/
      Core/                          Test modelli, enum (cross-platform, CI Linux!)
      Infrastructure/                Test DictionaryApiProvider (cross-platform, CI Linux!)
    Integration/                     Test esistenti (Windows-only)
```

### Grafo dipendenze

```
Core  (net10.0, zero dipendenze)
  ↑
Infrastructure  (net10.0, System.Net.Http.Json, ClosedXML)
  ↑
Services  (net10.0-windows)
  ↑
App  (net10.0-windows, WinForms)

Tests → Core + Infrastructure + App
```

### Interfaccia chiave: IDictionaryProvider

```csharp
/// <summary>
/// Astrazione per l'accesso ai dati dizionario (variabili, comandi, indirizzi protocollo).
/// Implementata da DictionaryApiProvider (API Azure) e ExcelDictionaryProvider (fallback).
/// </summary>
public interface IDictionaryProvider
{
    /// <summary>Carica indirizzi protocollo e comandi globali.</summary>
    Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct = default);

    /// <summary>Carica le variabili per un indirizzo protocollo specifico (RecipientId).</summary>
    Task<IReadOnlyList<Variable>> LoadVariablesAsync(
        uint recipientId, CancellationToken ct = default);
}
```

### Pattern: API con fallback Excel

```csharp
// In DictionaryApiProvider
public async Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct)
{
    try { return await LoadFromApiAsync(ct); }
    catch (HttpRequestException)
    {
        // Fallback a Excel se API non raggiungibile
        return _excelFallback.LoadProtocolDataAsync(ct).Result;
    }
}
```

---

## Roadmap branch

Ogni branch è **funzionalmente chiuso**: compila, i test passano, il comportamento è coerente.

---

### Branch 1: `refactor/crea-progetti-core-infrastructure`

**Scopo:** Creare lo scheletro multi-progetto (Core + Infrastructure) senza cambiare comportamento.

**Azioni:**
1. Creare `Core/Core.csproj` (`net10.0`, zero dipendenze)
2. Creare `Infrastructure/Infrastructure.csproj` (`net10.0`, ref → Core)
3. Aggiungere entrambi al `.slnx`
4. **Spostare** da `App/Core/` a `Core/`:
   - `Models/ButtonPanel.cs`, `ButtonIndicator.cs`, `ButtonPanelTestResult.cs`
   - `Enums/ButtonPanelEnums.cs`
   - `Interfaces/IButtonPanelTestService.cs`, `IButtonPanelTestTab.cs`
5. Aggiornare namespace: `App.Core.*` → `Core.*`
6. Aggiornare `App.csproj`: aggiungere `ProjectReference → Core`
7. Aggiornare `Tests.csproj`: aggiungere `ProjectReference → Core`
8. Fix tutti i `using` in App, Services, GUI, Tests
9. Build + test (101/101 devono passare)

**File creati:** 2 (.csproj)
**File spostati:** 6 (da App/Core/ a Core/)
**File modificati:** ~15 (using updates)
**Rischio:** Basso (solo spostamento + rinomina namespace)

---

### Branch 2: `refactor/sposta-services`

**Scopo:** Spostare `ButtonPanelTestService` nel progetto `Services/`.

**Azioni:**
1. Creare `Services/Services.csproj` (`net10.0-windows10.0.19041.0`, ref → Core)
2. Aggiungere al `.slnx`
3. **Spostare** `App/Services/ButtonPanelTestService.cs` → `Services/`
4. Aggiornare namespace: `App.Services` → `Services`
5. Aggiornare `App.csproj`: aggiungere `ProjectReference → Services`, rimuovere ref Core diretto
   (transitivo via Services)
6. Aggiornare `Tests.csproj`: aggiungere `ProjectReference → Services`
7. Fix `using` in App (`Program.cs`, mock nei test)
8. Build + test

**File creati:** 1 (.csproj)
**File spostati:** 1
**File modificati:** ~6
**Rischio:** Basso

---

### Branch 3: `refactor/modelli-dizionario-core`

**Scopo:** Creare i modelli dominio per dizionario in Core e l'interfaccia `IDictionaryProvider`.

**Azioni:**
1. Creare modelli in `Core/Models/`:
   - `Variable.cs` — record con `Name`, `AddressHigh`, `AddressLow`, `DataType`
   - `Command.cs` — record con `Name`, `CodeHigh`, `CodeLow`
   - `ProtocolAddress.cs` — record con `DeviceName`, `BoardName`, `Address`
   - `DictionaryData.cs` — record aggregato con `Addresses`, `Commands`
2. Creare `Core/Interfaces/IDictionaryProvider.cs`
3. Scrivere **test unitari** per i modelli (in `Tests/Unit/Core/`)
4. Build + test

**File creati:** 5 (modelli) + 1 (interfaccia) + test
**File modificati:** 0 (nessun impatto su codice legacy)
**Rischio:** Zero (solo aggiunta, niente tocca il codice esistente)

---

### Branch 4: `feature/excel-dictionary-provider`

**Scopo:** Implementare `ExcelDictionaryProvider` che wrappa `ExcelHandler` dietro `IDictionaryProvider`.

**Azioni:**
1. Creare `Infrastructure/Excel/ExcelDictionaryProvider.cs`
   - Usa `ExcelHandler` internamente
   - Converte `ExcelHandler.VariableData` → `Core.Models.Variable`
   - Converte `ExcelHandler.CommandData` → `Core.Models.Command`
   - Converte `ExcelHandler.RowData` → `Core.Models.ProtocolAddress`
2. Spostare `ExcelHandler.cs` da `App/` a `Infrastructure/Excel/`
   - Rimuovere dipendenze da `MessageBox` (UI) → throw exception
   - `App/` mantiene un `using Infrastructure.Excel` (o proxy se serve)
3. Aggiornare `Infrastructure.csproj`: aggiungere `ClosedXML`, `DocumentFormat.OpenXml`
4. Scrivere **test unitari** per `ExcelDictionaryProvider` (in `Tests/Unit/Infrastructure/`)
5. Scrivere **test integrazione** con Excel embedded reale
6. Build + test

**File creati:** 1 (provider) + test
**File spostati:** 1 (`ExcelHandler.cs` → Infrastructure)
**File modificati:** `App.csproj` (ref Infrastructure), ~5 (fix using per ExcelHandler)
**Rischio:** Medio (ExcelHandler usato in 40+ punti — il file si sposta ma `using static ExcelHandler`
va aggiornato ovunque)

**Nota:** `ExcelHandler` va pulito dai `MessageBox.Show` (violazione layer separation).
Le chiamate dirette di Form1 a `ExcelHandler` continuano a funzionare via ref Infrastructure.

---

### Branch 5: `feature/api-dictionary-provider`

**Scopo:** Implementare `DictionaryApiProvider` che chiama l'API Azure.

**Azioni:**
1. Creare DTO in `Infrastructure/Api/Dtos/`:
   - `DeviceSummaryDto.cs`, `BoardSummaryDto.cs`
   - `DictionaryResolvedDto.cs`, `ResolvedVariableDto.cs`
   - `CommandDto.cs`
2. Creare `Infrastructure/Api/DictionaryApiOptions.cs` (BaseUrl, ApiKey, Timeout)
3. Creare `Infrastructure/Api/DictionaryApiProvider.cs`
   - `HttpClient` con header `X-Api-Key`
   - `LoadProtocolDataAsync`: GET devices → boards → match indirizzi + GET commands
   - `LoadVariablesAsync(recipientId)`: trova board per ProtocolAddress → GET resolved
4. Scrivere **test unitari** con `HttpClient` mockato (`MockHttpMessageHandler`)
5. Build + test

**File creati:** ~8 (DTO + provider + options) + test
**File modificati:** `Infrastructure.csproj` (aggiunta `System.Net.Http.Json`)
**Rischio:** Basso (solo aggiunta, niente tocca il codice legacy)
**CI:** Questi test girano su Linux! (net10.0, no WinForms)

---

### Branch 6: `feature/fallback-e-di-registration`

**Scopo:** Creare il `DependencyInjection.cs` che registra il provider giusto con fallback.

**Azioni:**
1. Creare `Infrastructure/DependencyInjection.cs`:
   - Extension method `AddDictionaryProvider(this IServiceCollection, IConfiguration)`
   - Se `DictionaryApi:BaseUrl` e `DictionaryApi:ApiKey` configurati → `DictionaryApiProvider`
   - Altrimenti → `ExcelDictionaryProvider`
   - Fallback runtime: `DictionaryApiProvider` cattura `HttpRequestException` → delega a Excel
2. Creare `appsettings.json` in `App/`:
   ```json
   {
     "DictionaryApi": {
       "BaseUrl": "",
       "ApiKey": ""
     }
   }
   ```
3. Aggiornare `App/Program.cs`:
   - Aggiungere `IConfiguration` (da `appsettings.json` + environment variables)
   - Chiamare `services.AddDictionaryProvider(configuration)`
4. Scrivere **test integrazione** per DI registration
5. Build + test

**File creati:** 2 (`DependencyInjection.cs`, `appsettings.json`) + test
**File modificati:** `Program.cs`, `App.csproj`
**Rischio:** Basso-medio (tocca Program.cs, ma è piccolo)

---

### Branch 7: `feature/integra-provider-in-app`

**Scopo:** Far usare `IDictionaryProvider` ai primi consumer (non Form1).

**Azioni:**
1. Identificare consumer più semplici (es. `Boot_Smart_WF_Tab`, `Telemetry_WF_Tab`)
2. Iniettare `IDictionaryProvider` tramite DI o property injection
3. Sostituire le chiamate dirette a `ExcelHandler` con `IDictionaryProvider`
4. Verificare che il fallback Excel funzioni quando API non è configurata
5. Build + test

**File modificati:** 2-4 (tab WinForms + Program.cs DI wiring)
**Rischio:** Medio (tocca codice legacy, ma tab isolati)

**Nota:** Form1.cs NON viene modificato in questo branch. È troppo rischioso.
La migrazione di Form1 è un lavoro separato (Fase 3-4 del piano di modernizzazione).

---

## Riepilogo branch

| # | Branch | Scopo | Rischio | CI Linux |
|---|--------|-------|---------|----------|
| 1 | `refactor/crea-progetti-core-infrastructure` | Scheletro multi-progetto | Basso | ✅ Core |
| 2 | `refactor/sposta-services` | Sposta ButtonPanelTestService | Basso | — |
| 3 | `refactor/modelli-dizionario-core` | Modelli dominio + IDictionaryProvider | Zero | ✅ test |
| 4 | `feature/excel-dictionary-provider` | ExcelHandler dietro astrazione | Medio | — |
| 5 | `feature/api-dictionary-provider` | HttpClient → API Azure | Basso | ✅ test |
| 6 | `feature/fallback-e-di-registration` | DI + fallback + appsettings | Basso-medio | — |
| 7 | `feature/integra-provider-in-app` | Primi consumer usano provider | Medio | — |

**Dopo branch 7:** L'app funziona con API Azure (se configurata) o Excel (fallback).
Form1.cs continua a usare ExcelHandler direttamente — la sua migrazione è un lavoro separato.

---

## Note sulla API Key

La gestione sicura della API key (distribuzione, protezione nell'eseguibile) è rimandata.
Per ora: `appsettings.json` locale + environment variables (`DictionaryApi__ApiKey`).
Pattern identico a `FirmwareApiOptions` in Production.Tracker.

## Note sui test CI

Con Core e Infrastructure su `net10.0`, i test unitari di modelli, enum e API provider
**girano su Linux** nella pipeline Bitbucket. Questo risolve CI-001 per la parte nuova.
I test WinForms (integration) restano Windows-only.
