# MIGRATION_API.md — Piano migrazione Excel → API Dizionari Azure

**Creato:** 2026-04-14  
**Ultimo aggiornamento:** 2026-04-14  
**Stato:** Migrazione Fase 2 completata — tutti i branch chiusi

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
      ButtonPanel.cs                 ✅ spostato da App/Core/Models/
      ButtonPanelTestResult.cs       ✅ spostato da App/Core/Models/
    Enums/
      ButtonPanelEnums.cs            ✅ spostato da App/Core/Enums/
    Interfaces/
      IDictionaryProvider.cs         Astrazione: "dammi variabili/comandi/indirizzi"
      IButtonPanelTestService.cs     ✅ spostato da App/Core/Interfaces/

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
    Services.csproj                  → dipende da Core (vuoto, pronto per futuro)

  App/                               net10.0-windows10.0.19041.0 (WinForms)
    App.csproj                       → dipende da Core (+ Services futuro)
    Program.cs                       DI: registra tutto
    Form1.cs                         God Object (invariato in questa migrazione)
    ExcelHandler.cs                  Resta in App (dipende da WinForms, usato direttamente da Form1)
    Core/Interfaces/
      IButtonPanelTestTab.cs         ⚠️ resta in App (dipende da MessageBoxButtons/Icon WinForms)
    Core/Models/
      ButtonIndicator.cs             ⚠️ resta in App (dipende da System.Drawing.RectangleF — view model)
    Services/
      ButtonPanelTestService.cs      ⚠️ resta in App (dipende da Form1.FormRef, NetworkLayer, PacketManager)
    STEMProtocol/                    Resta (forte accoppiamento con Form1)
    GUI/                             MVP ButtonPanel
    Resources/                       Excel embedded, icone
    ...tutti i *_WF_Tab.cs

  Tests/                             net10.0-windows10.0.19041.0
    Tests.csproj                     → dipende da Core + App
    Unit/
      Core/                          Test modelli, enum
      Infrastructure/                Test DictionaryApiProvider
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

### Branch 1+2: `refactor/core-infrastructure` ✅ COMPLETATO

**Scopo:** Creare lo scheletro multi-progetto (Core + Infrastructure + Services) e spostare i tipi puri.

**Risultato:**
- Creati 3 progetti: `Core/` (net10.0), `Infrastructure/` (net10.0), `Services/` (net10.0-windows)
- Spostati in `Core/`: `ButtonPanelEnums.cs`, `ButtonPanel.cs`, `ButtonPanelTestResult.cs`,
  `IButtonPanelTestService.cs`
- Namespace rinominati: `App.Core.*` → `Core.*`
- Using aggiornati in 14 file (App, GUI, Services, Tests)
- `Core.csproj`: `InternalsVisibleTo` per `Tests` e `App`

**⚠️ Scoperte durante implementazione (deviazioni dal piano originale):**
1. **`IButtonPanelTestTab`** resta in `App/Core/Interfaces/` — usa `MessageBoxButtons`, `MessageBoxIcon`
   (tipi WinForms non disponibili in `net10.0` puro)
2. **`ButtonIndicator`** resta in `App/Core/Models/` (namespace `App.Core.Models`) — è un view model
   GUI (`RectangleF` per coordinate disegno su PictureBox), non un modello di dominio.
   Inizialmente spostato in `Core/`, poi riportato in `App/` per mantenere Core privo di dipendenze GUI.
3. **`ButtonPanelTestService`** resta in `App/Services/` — dipende direttamente da `Form1.FormRef` (static),
   `PacketManager`, `NetworkLayer`. Spostarlo richiederebbe estrarre interfacce per il protocollo (Fase 4)
4. **Progetto `Services/`** creato e nel `.slnx` ma vuoto — pronto per codice futuro che non ha
   dipendenze WinForms/Form1

**Build:** ✅ | **Test:** 101/101 ✅

---

### Branch 2: `refactor/modelli-dizionario-core` ✅ COMPLETATO

**Scopo:** Creare i modelli dominio per dizionario in Core e l'interfaccia `IDictionaryProvider`.

**Risultato:**
- Creati 4 record in `Core/Models/`: `Variable`, `Command`, `ProtocolAddress`, `DictionaryData`
- Creata interfaccia `Core/Interfaces/IDictionaryProvider` (2 metodi async)
- Scritti 12 test unitari (4 file) — tutti cross-platform, girano in CI su Linux
- Mappatura campi: `RowData` → `ProtocolAddress`, `CommandData` → `Command`, `VariableData` → `Variable`

**Nota:** `DictionaryData` usa `IReadOnlyList<T>` — record equality confronta per reference,
non per contenuto. Test adattati di conseguenza.

**Build:** ✅ | **Test:** 164/164 ✅ (51 cross-platform + 113 Windows)

---

### Branch 3: `feature/excel-dictionary-provider` ✅ COMPLETATO

**Scopo:** Implementare `ExcelDictionaryProvider` che implementa `IDictionaryProvider` leggendo da Excel.

**⚠️ Deviazione dal piano originale:**
Il piano prevedeva di spostare `ExcelHandler.cs` in `Infrastructure/Excel/`. Analisi ha rivelato che:
- `ExcelHandler` usa `MessageBox.Show` (3 punti) + `Application.Exit()` → dipendenze WinForms
- Infrastructure è `net10.0` puro → non può referenziare WinForms
- `ExcelHandler` è consumato direttamente da Form1 in 30+ punti via `using static ExcelHandler`
  (tipi inner: `RowData`, `CommandData`, `VariableData` usati ovunque)
- Spostarlo e pulirlo richiederebbe toccare ~40 righe in Form1 — troppo rischioso

**Strategia rivista:** `ExcelHandler` **resta in App/**. `ExcelDictionaryProvider` in Infrastructure
replica la logica di lettura Excel usando ClosedXML direttamente, producendo `Core.Models.*`.
La duplicazione temporanea di logica ClosedXML è accettabile — quando Form1 migrerà a
`IDictionaryProvider` (Fase 3-4), `ExcelHandler` verrà eliminato.

**Risultato:**
- Creato `Infrastructure/Excel/ExcelDictionaryProvider.cs`
  - Implementa `IDictionaryProvider`, accetta `Stream` nel costruttore
  - Legge con ClosedXML → produce `ProtocolAddress`, `Command`, `Variable`
  - Stessa logica di ExcelHandler ma senza dipendenze WinForms
- `Dizionari STEM.xlsx` embedded in `Infrastructure.csproj` (self-contained per fallback Branch 5)
- Aggiornati `Infrastructure.csproj` (ClosedXML + InternalsVisibleTo) e `Tests.csproj` (ProjectReference)
- **20 test** per ExcelDictionaryProvider (2 file):
  - `ExcelDictionaryProviderTests.cs` (14 test, cross-platform net10.0 + Windows):
    struttura, cancellation token, idempotenza, interleaving, DataType, edge case
  - `ExcelDictionaryProviderCrossReferenceTests.cs` (6 test, Windows-only):
    confronto **campo per campo** con legacy ExcelHandler (addresses, commands, variables TopLift)
    — prova equivalenza al 100% tra nuovo provider e codice legacy

**File creati:** 3 (`ExcelDictionaryProvider.cs`, 2 file test)
**File NON spostati:** `ExcelHandler.cs` resta in App (invariato)
**File modificati:** `Infrastructure.csproj`, `Tests.csproj`
**Rischio:** Basso (solo additive — Form1 e codice legacy non toccati)
**CI:** I 14 test unit del provider girano su Linux (net10.0, ClosedXML cross-platform);
  i 6 test cross-reference girano solo su Windows (dipendono da ExcelHandler/WinForms)

**Build:** ✅ | **Test:** 198/198 ✅ (65 cross-platform + 133 Windows)

---

### Branch 4: `feature/api-dictionary-provider` ✅ COMPLETATO

**Scopo:** Implementare `DictionaryApiProvider` che chiama l'API Azure.

**Risultato:**
- Creati 5 DTO in `Infrastructure/Api/Dtos/`:
  `DeviceSummaryDto`, `BoardSummaryDto`, `DictionaryResolvedDto`, `ResolvedVariableDto`, `CommandDto`
- Creato `Infrastructure/Api/DictionaryApiOptions.cs` (BaseUrl, ApiKey, TimeoutSeconds)
- Creato `Infrastructure/Api/DictionaryApiProvider.cs`:
  - Implementa `IDictionaryProvider`, accetta `HttpClient` nel costruttore
  - `LoadProtocolDataAsync`: GET devices → per ogni device GET boards → raccoglie indirizzi + GET commands
  - `LoadVariablesAsync(recipientId)`: cerca board con ProtocolAddress matching (hex, case-insensitive)
    → GET dictionaries/{id}/resolved → mappa a `Variable`
  - Board senza `ProtocolAddress` o con `DictionaryId` null: gestite gracefully (escluse/empty)
- Aggiornato `Infrastructure.csproj`: aggiunta `System.Net.Http.Json`
- **26 test** per DictionaryApiProvider (2 file, cross-platform net10.0 + Windows):
  - `MockHttpMessageHandler.cs`: handler HTTP fittizio con matching per URL (longest-first),
    crea risposte fresche per ogni chiamata (evita ObjectDisposedException su content riusato)
  - `DictionaryApiProviderTests.cs` (13 test × 2 TFM):
    constructor, addresses da multi-device, valori corretti, commands mappati,
    empty devices, cancellation token, variables con recipientId noto/sconosciuto,
    board senza dictionary, case-insensitive matching, board senza address

**File creati:** 9 (5 DTO + options + provider + mock + test)
**File modificati:** `Infrastructure.csproj`
**Rischio:** Zero (solo additive — nessun file legacy toccato)
**CI:** Tutti i 26 test girano su Linux (net10.0, zero dipendenze WinForms)

**Build:** ✅ | **Test:** 224/224 ✅ (91 cross-platform + 133 Windows)

---

### Branch 5: `feature/fallback-e-di-registration` ✅ COMPLETATO

**Scopo:** Creare DI registration, fallback decorator e configurazione.

**⚠️ Deviazione dal piano originale:**
Il piano prevedeva il fallback dentro `DictionaryApiProvider`. Analisi ha rivelato che viola SRP:
il provider API dovrebbe solo parlare con l'API. Creato invece `FallbackDictionaryProvider`
(decorator pattern) che wrappa primary (API) e delega a fallback (Excel) su `HttpRequestException`.
Più pulito, testabile, riusabile.

**Risultato:**
- Creato `Infrastructure/FallbackDictionaryProvider.cs`:
  - Decorator: tenta primary, su `HttpRequestException` delega a fallback
  - Eccezioni non-HTTP propagate normalmente
- Creato `Infrastructure/DependencyInjection.cs`:
  - Extension method `AddDictionaryProvider(IServiceCollection, IConfiguration)`
  - Se `DictionaryApi:BaseUrl` + `ApiKey` configurati → `FallbackDictionaryProvider(API, Excel)`
  - Altrimenti → solo `ExcelDictionaryProvider`
  - HttpClient configurato con `BaseAddress`, `X-Api-Key`, `Timeout` tramite `AddHttpClient<T>`
- Creato `App/appsettings.json` (sezione `DictionaryApi` vuota → default Excel)
- Aggiornato `App/Program.cs`:
  - `IConfiguration` da `appsettings.json` + environment variables
  - `services.AddDictionaryProvider(configuration)` → IDictionaryProvider disponibile via DI
- Aggiornato `App/App.csproj`: ref Infrastructure + pacchetti Configuration
- Aggiornato `Infrastructure.csproj`: pacchetti DI/Configuration/Http abstractions
- **21 test** nuovi (2 file):
  - `FallbackDictionaryProviderTests.cs` (7 test × 2 TFM): primary OK, primary HTTP fail → fallback,
    non-HTTP propagata, constructor null checks
  - `ServiceRegistrationTests.cs` aggiornati (7 test): resolve Excel senza config,
    resolve Fallback con config, singleton, empty API key → Excel

**File creati:** 3 (`FallbackDictionaryProvider.cs`, `DependencyInjection.cs`, `appsettings.json`)
**File modificati:** `Program.cs`, `App.csproj`, `Infrastructure.csproj`, `ServiceRegistrationTests.cs`
**Rischio:** Basso (Program.cs: 6 righe aggiunte, Form1 non toccato)
**CI:** 7 test Fallback girano su Linux (net10.0); test DI sono Windows-only (App reference)

**Build:** ✅ | **Test:** 252/252 ✅ (105 cross-platform + 147 Windows)

---

### Branch 6: `feature/integra-provider-in-app` ✅ CHIUSO (nessuna modifica codice)

**Scopo originale:** Far usare `IDictionaryProvider` ai primi consumer (tab WinForms).

**⚠️ Decisione architetturale: Strategia B — rimandare a Fase 3-4**

Analisi dei consumer candidati ha rivelato che:
- I tipi `ExcelHandler.VariableData`/`RowData`/`CommandData` **attraversano il confine** tab↔Form1
- Migrare un tab a `Core.Models.Variable` senza migrare Form1 richiede un layer di conversione
  a ogni confine, oppure migrare anche Form1 (∼15 punti)
- Form1.cs è un God Object (~55k LOC) — toccarlo ora è troppo rischioso

**Perché rimandare è la scelta giusta:**
1. Il refactor di Fase 3-4 (estrarre interfacce per NetworkLayer/PacketManager, spezzare Form1)
   disaccoppia i componenti → ogni componente riceve `IDictionaryProvider` via DI
2. A quel punto la sostituzione `ExcelHandler` → `IDictionaryProvider` diventa **meccanica**
3. Il lavoro dei branch 1-5 non è sprecato — è esattamente l'infrastruttura che il refactor userà
4. Zero rischio ora: 258/258 test verdi, nessun file legacy toccato

**Stato infrastruttura pronta:**
- `IDictionaryProvider` → interfaccia nel DI container
- `DictionaryApiProvider` → pronto per API Azure
- `ExcelDictionaryProvider` → equivalente al 100% a ExcelHandler (provato campo per campo)
- `FallbackDictionaryProvider` → decorator API→Excel
- `appsettings.json` → `BaseUrl`/`ApiKey` vuoti = Excel, popolati = API con fallback
- Per attivare: basta popolare `DictionaryApi__BaseUrl` + `DictionaryApi__ApiKey`

**File creati:** 0
**File modificati:** 0 (solo `MIGRATION_API.md`)
**Rischio:** Zero

**Build:** ✅ | **Test:** 258/258 ✅

---

## Riepilogo branch

| # | Branch | Scopo | Rischio | Stato |
|---|--------|-------|---------|-------|
| 1+2 | `refactor/core-infrastructure` | Scheletro multi-progetto + spostamento tipi | Basso | ✅ Completato |
| 2 | `refactor/modelli-dizionario-core` | Modelli dominio + IDictionaryProvider | Zero | ✅ Completato |
| 3 | `feature/excel-dictionary-provider` | ExcelHandler dietro astrazione + test correttezza | Basso | ✅ Completato |
| 4 | `feature/api-dictionary-provider` | HttpClient → API Azure | Zero | ✅ Completato |
| 5 | `feature/fallback-e-di-registration` | DI + fallback decorator + appsettings | Basso | ✅ Completato |
| 6 | `feature/integra-provider-in-app` | Consumer migration (rimandato a Fase 3-4) | Zero | ✅ Chiuso |

**Migrazione Fase 2 completata.** L'infrastruttura API+fallback è pronta nel DI container.
Form1 e i tab continuano a usare ExcelHandler direttamente.
La sostituzione effettiva avverrà durante il refactor di Fase 3-4, quando Form1 verrà spezzato
e ogni componente riceverà `IDictionaryProvider` via DI.

### Tipi rimasti in App/ (da spostare in fasi future)

| Tipo | Motivo | Quando spostare |
|------|--------|-----------------|
| `IButtonPanelTestTab` | Usa `MessageBoxButtons`, `MessageBoxIcon` (WinForms) | Fase 4: astrarre con tipi propri |
| `ButtonIndicator` | View model GUI (`RectangleF` per coordinate disegno) | Fase 5: migrazione UI |
| `ButtonPanelTestService` | Dipende da `Form1.FormRef`, `NetworkLayer`, `PacketManager` | Fase 4: estrarre interfacce protocollo |

---

## Note sulla API Key

La gestione sicura della API key (distribuzione, protezione nell'eseguibile) è rimandata.
Per ora: `appsettings.json` locale + environment variables (`DictionaryApi__ApiKey`).
Pattern identico a `FirmwareApiOptions` in Production.Tracker.

## Note sui test CI

Con Core e Infrastructure su `net10.0`, i test unitari di modelli, enum e API provider
**girano su Linux** nella pipeline Bitbucket. Questo risolve CI-001 per la parte nuova.
I test WinForms (integration) restano Windows-only.
