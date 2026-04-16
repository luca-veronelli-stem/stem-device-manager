# Tests

> Test automatizzati per Stem.Device.Manager — xUnit, 176 test.  
> **Ultimo aggiornamento:** 2026-04-16

---

## Panoramica

| Aspetto | Valore |
|---------|--------|
| **Framework** | xUnit 2.5.3 |
| **TFM** | `net10.0` + `net10.0-windows10.0.19041.0` (dual target) |
| **Test totali** | 176 |
| **Unit test** | 68 (modelli, provider, protocol, etc.) |
| **Integration test** | 34 (DI, CodeGenerator, Form1, ecc.) |
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
├── Unit/                                   68 test unitari
│   ├── Core/
│   │   └── Models/
│   │       ├── VariableTests.cs            Record equality (3 test)
│   │       ├── CommandTests.cs             Record equality (3 test)
│   │       ├── ProtocolAddressTests.cs     Record equality (3 test)
│   │       └── DictionaryDataTests.cs      Constructor + order (3 test)
│   │
│   ├── Infrastructure/
│   │   ├── MockHttpMessageHandler.cs       Mock HTTP per API provider
│   │   ├── DictionaryApiProviderTests.cs   API provider (18 test)
│   │   ├── ExcelDictionaryProviderTests.cs Excel provider (14 test)
│   │   └── FallbackDictionaryProviderTests.cs Fallback decorator (9 test)
│   │
│   ├── Terminal/
│   │   └── TerminalTests.cs                Append, get, write (6 test)
│   │
│   ├── Protocol/
│   │   └── RollingCodeGeneratorTests.cs    Range, ciclo, thread-safety (4 test)
│   │
│   ├── CodeGenerator/
│   │   └── SP_Code_GeneratorTests.cs       Header C, #define (7 test)
│   │
│   └── CircularProgressBar/
│       └── CircularProgressBarTests.cs     Clamping, validazione (5 test)
│
└── Integration/                            34 test di integrazione
    ├── Infrastructure/
    │   └── ExcelDictionaryProviderCrossReferenceTests.cs  Confronto campo per campo (6 test)
    │
    ├── DependencyInjection/
    │   └── ServiceRegistrationTests.cs         DI wiring + IDictionaryProvider (5 test)
    │
    ├── CodeGenerator/
    │   └── SP_Code_GeneratorIntegrationTests.cs  Multi-config E2E (4 test)
    │
    └── Form1/
        ├── Form1DictionaryLoadingTests.cs         Contratto IDictionaryProvider + flusso (9 test)
        └── Mocks/
            └── MockDictionaryProvider.cs          Mock manuale IDictionaryProvider
```

---

## Copertura per modulo

| Modulo | File test | Test | Tipo |
|--------|-----------|------|------|
| **Core Models (Dictionary)** | `VariableTests.cs`, `CommandTests.cs`, `ProtocolAddressTests.cs`, `DictionaryDataTests.cs` | 12 | Unit |
| **Infrastructure API** | `DictionaryApiProviderTests.cs` | 18 | Unit |
| **Infrastructure Excel** | `ExcelDictionaryProviderTests.cs` | 14 | Unit |
| **Infrastructure Fallback** | `FallbackDictionaryProviderTests.cs` | 9 | Unit |
| **Terminal** | `TerminalTests.cs` | 6 | Unit |
| **SPRollingCode** | `RollingCodeGeneratorTests.cs` | 4 | Unit |
| **SP_Code_Generator** | `SP_Code_GeneratorTests.cs` | 7 | Unit |
| **CircularProgressBar** | `CircularProgressBarTests.cs` | 5 | Unit |
| **Infrastructure Excel** | `ExcelDictionaryProviderCrossReferenceTests.cs` | 6 | Integration |
| **DI Container** | `ServiceRegistrationTests.cs` | 5 | Integration |
| **SP_Code_Generator** | `SP_Code_GeneratorIntegrationTests.cs` | 4 | Integration |
| **IDictionaryProvider (Form1)** | `Form1DictionaryLoadingTests.cs` | 9 | Integration |

---

## Convenzioni

| Aspetto | Standard |
|---------|----------|
| **Naming classi** | `{Classe}Tests` |
| **Naming metodi** | `{Method}_{Scenario}_{ExpectedResult}` |
| **Pattern** | Arrange-Act-Assert (implicito) |
| **Attributi** | `[Fact]` singoli, `[Theory]` + `[InlineData]` parametrici |
| **Mock** | Manual — classi in `Integration/*/Mocks/` |
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
Build → Test (176 test) → ✅/❌
```

---

## Links

- [README Soluzione](../README.md)
- [App](../App/README.md)
- [CHANGELOG](../CHANGELOG.md)
