# Infrastructure.Persistence

> **Provider dati cross-platform: API Azure, Excel fallback, DI registration.**  
> **Ultimo aggiornamento:** 2026-04-20

---

## Panoramica

| Aspetto | Valore |
|---------|--------|
| **Tipo** | Class Library |
| **TFM** | `net10.0` (cross-platform) |
| **Dipendenze** | Core, ClosedXML, Microsoft.Extensions.* |
| **Scopo** | Implementazioni di `IDictionaryProvider` + registrazione DI |

Infrastructure.Persistence contiene la logica I/O per accedere ai dati dizionario:
- **API Azure** — chiama l'API REST di Stem.Dictionaries.Manager
- **Excel** — legge dal file `Dizionari STEM.xlsx` embedded
- **Fallback** — decorator che tenta API, su errore HTTP delega a Excel
- **DI** — extension method per registrare tutto nel container

Il nome riflette il pattern Stem (`Infrastructure.<Concern>`): in questa solution
la persistence è l'accesso ai dizionari, mentre gli adapter HW protocollari vivono
in [Infrastructure.Protocol](../Infrastructure.Protocol/README.md).

---

## Struttura

```
Infrastructure.Persistence/
├── Infrastructure.Persistence.csproj
├── DependencyInjection.cs           Extension method AddDictionaryProvider()
├── FallbackDictionaryProvider.cs    Decorator: API → catch → Excel
├── Api/
│   ├── DictionaryApiOptions.cs      Config: BaseUrl, ApiKey, TimeoutSeconds
│   ├── DictionaryApiProvider.cs     HttpClient → API REST (IDictionaryProvider)
│   └── Dtos/
│       ├── DeviceSummaryDto.cs      GET /api/devices
│       ├── BoardSummaryDto.cs       GET /api/devices/{id}/boards
│       ├── DictionaryResolvedDto.cs GET /api/dictionaries/{id}/resolved
│       ├── ResolvedVariableDto.cs   Variabile risolta
│       └── CommandDto.cs            GET /api/commands
└── Excel/
    ├── ExcelDictionaryProvider.cs   ClosedXML → Core.Models (IDictionaryProvider)
    └── Dizionari STEM.xlsx          Embedded resource (fallback)
```

Namespace root: `Infrastructure.Persistence` (prima della rinomina Fase 2 era `Infrastructure`).

---

## API / Componenti

### DependencyInjection.AddDictionaryProvider()

```csharp
services.AddDictionaryProvider(configuration);
```

| Configurazione | Risultato |
|---|---|
| `DictionaryApi:BaseUrl` + `ApiKey` vuoti | `ExcelDictionaryProvider` (singleton) |
| `DictionaryApi:BaseUrl` + `ApiKey` popolati | `FallbackDictionaryProvider(API, Excel)` (singleton) |

### FallbackDictionaryProvider

Decorator pattern: tenta il provider primario (API), su `HttpRequestException` delega al fallback (Excel).
Eccezioni non-HTTP vengono propagate normalmente.

### DictionaryApiProvider

| Metodo | Flusso |
|--------|--------|
| `LoadProtocolDataAsync` | GET devices → per ogni device GET boards → raccoglie indirizzi + GET commands |
| `LoadVariablesAsync(recipientId)` | Cerca board con ProtocolAddress matching (hex, case-insensitive) → GET dictionaries/{id}/resolved |

### ExcelDictionaryProvider

Legge da `Dizionari STEM.xlsx` con ClosedXML. Produce `Core.Models.*`.
Equivalente al 100% a `ExcelHandler` legacy (provato campo per campo nei test cross-reference).

---

## Configurazione

`appsettings.json` in App/:

```json
{
  "DictionaryApi": {
    "BaseUrl": "",
    "ApiKey": "",
    "TimeoutSeconds": 30
  }
}
```

Oppure environment variables: `DictionaryApi__BaseUrl`, `DictionaryApi__ApiKey`.

---

## Quick Start

```csharp
// In Program.cs (già configurato)
var configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

services.AddDictionaryProvider(configuration);

// Poi inietta IDictionaryProvider dove serve
var provider = serviceProvider.GetRequiredService<IDictionaryProvider>();
var data = await provider.LoadProtocolDataAsync();
```

---

## Requisiti

- **.NET 10.0** (cross-platform)

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| ClosedXML | 0.105.0 | Lettura Excel |
| Microsoft.Extensions.Configuration.Abstractions | 10.0.5 | IConfiguration |
| Microsoft.Extensions.Configuration.Binder | 10.0.5 | section.Bind() |
| Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5 | IServiceCollection |
| Microsoft.Extensions.Http | 10.0.5 | AddHttpClient<T> |

---

## Issue Correlate

→ [ISSUES.md](../ISSUES.md) (da creare)

---

## Links

- [README Soluzione](../README.md)
- [Core](../Core/README.md)
- [Infrastructure.Protocol](../Infrastructure.Protocol/README.md)
- [Services](../Services/README.md)
- [Tests](../Tests/README.md)
- [REFACTOR_PLAN](../Docs/REFACTOR_PLAN.md)
