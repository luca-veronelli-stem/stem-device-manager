# Tests

> Test automatizzati per Stem.Device.Manager — xUnit, 101 test (76 unit + 25 integration).  
> **Ultimo aggiornamento:** 2026-04-14

---

## Panoramica

| Aspetto | Valore |
|---------|--------|
| **Framework** | xUnit 2.5.3 |
| **TFM** | `net10.0-windows10.0.19041.0` |
| **Test totali** | 101 |
| **Unit test** | 76 |
| **Integration test** | 25 |
| **Mock** | Manual (nessuna libreria esterna) |

---

## Requisiti

- **.NET 10.0** (Windows 10+ x64)
- **xUnit 2.5.3** (incluso via NuGet)
- Progetto `App` compilabile (riferimento via `ProjectReference`)

---

## Quick Start

```bash
# Tutti i test
dotnet test Tests/Tests.csproj

# Solo unit test (per namespace)
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~Tests.Unit"

# Solo integration test
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~Tests.Integration"
```

---

## Struttura

```
Tests/
├── Tests.csproj
│
├── Unit/                                   76 test unitari
│   ├── Core/
│   │   ├── Enums/
│   │   │   └── ButtonPanelEnumsTests.cs        Contratti enum (7 test)
│   │   └── Models/
│   │       ├── ButtonPanelTests.cs             Factory + coerenza (11 test)
│   │       ├── ButtonIndicatorTests.cs         Stato default (2 test)
│   │       └── ButtonPanelTestResultTests.cs   Default values (3 test)
│   │
│   ├── Terminal/
│   │   └── TerminalTests.cs                    Append, get, write (6 test)
│   │
│   ├── Protocol/
│   │   └── RollingCodeGeneratorTests.cs        Range, ciclo, thread-safety (4 test)
│   │
│   ├── CodeGenerator/
│   │   └── SP_Code_GeneratorTests.cs           Header C, #define (7 test)
│   │
│   ├── ExcelHandler/
│   │   └── ExcelHandlerTests.cs                Guard clauses, DTO (7 test)
│   │
│   └── CircularProgressBar/
│       └── CircularProgressBarTests.cs         Clamping, validazione (5 test)
│
└── Integration/                            25 test di integrazione
    ├── ExcelHandler/
    │   └── ExcelHandlerIntegrationTests.cs     Excel embedded reale (8 test)
    │
    ├── DependencyInjection/
    │   └── ServiceRegistrationTests.cs         DI wiring (3 test)
    │
    ├── Presenter/
    │   ├── ButtonPanelTestPresenterTests.cs    Orchestrazione MVP (11 test)
    │   └── Mocks/
    │       ├── MockButtonPanelTestTab.cs       Mock manuale IButtonPanelTestTab
    │       └── MockButtonPanelTestService.cs   Mock manuale IButtonPanelTestService
    │
    └── CodeGenerator/
        └── SP_Code_GeneratorIntegrationTests.cs  Multi-config E2E (4 test)
```

---

## Copertura per modulo

| Modulo | File test | Test | Tipo |
|--------|-----------|------|------|
| **Core Models** | `ButtonPanelTests.cs` | 11 | Unit |
| **Core Enums** | `ButtonPanelEnumsTests.cs` | 7 | Unit |
| **Core Models** | `ButtonIndicatorTests.cs` | 2 | Unit |
| **Core Models** | `ButtonPanelTestResultTests.cs` | 3 | Unit |
| **Terminal** | `TerminalTests.cs` | 6 | Unit |
| **SPRollingCode** | `RollingCodeGeneratorTests.cs` | 4 | Unit |
| **SP_Code_Generator** | `SP_Code_GeneratorTests.cs` | 7 | Unit |
| **ExcelHandler** | `ExcelHandlerTests.cs` | 7 | Unit |
| **CircularProgressBar** | `CircularProgressBarTests.cs` | 5 | Unit |
| **ExcelHandler** | `ExcelHandlerIntegrationTests.cs` | 8 | Integration |
| **DI Container** | `ServiceRegistrationTests.cs` | 3 | Integration |
| **Presenter (MVP)** | `ButtonPanelTestPresenterTests.cs` | 11 | Integration |
| **SP_Code_Generator** | `SP_Code_GeneratorIntegrationTests.cs` | 4 | Integration |

---

## Convenzioni

| Aspetto | Standard |
|---------|----------|
| **Naming classi** | `{Classe}Tests` |
| **Naming metodi** | `{Method}_{Scenario}_{ExpectedResult}` |
| **Pattern** | Arrange-Act-Assert (implicito) |
| **Attributi** | `[Fact]` singoli, `[Theory]` + `[InlineData]` parametrici |
| **Mock** | Manual — classi in `Integration/Presenter/Mocks/` |
| **File temporanei** | `Path.GetTempPath()` + cleanup via `IDisposable` |
| **Nomenclatura** | Inglese (nomi), italiano (commenti XML) |

---

## Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| xunit | 2.5.3 | Framework test |
| xunit.runner.visualstudio | 2.5.3 | Runner VS |
| Microsoft.NET.Test.Sdk | 17.8.0 | Test SDK |
| coverlet.collector | 6.0.0 | Code coverage |

---

## CI/CD

I test vengono eseguiti automaticamente nella pipeline Bitbucket (`bitbucket-pipelines.yml`):

```
Build → Test (101 test) → ✅/❌
```

---

## Issue Correlate

→ [ISSUES.md](../ISSUES.md) (da creare)

---

## Links

- [README Soluzione](../README.md)
- [App](../App/README.md)
- [CHANGELOG](../CHANGELOG.md)
