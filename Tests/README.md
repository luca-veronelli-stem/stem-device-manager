# Tests

> Test automatizzati per Stem.Device.Manager — xUnit.  
> **Ultimo aggiornamento:** 2026-04-20

---

## Panoramica

| Aspetto | Valore |
|---------|--------|
| **Framework** | xUnit 2.5.3 |
| **TFM** | `net10.0` + `net10.0-windows10.0.19041.0` (dual target) |
| **Test run su `net10.0`** | 132 (cross-platform, CI Linux) |
| **Test run su `net10.0-windows`** | 274 (Windows-only, include cross) |
| **Mock** | Manual (nessuna libreria esterna) |

**Nota:** xUnit esegue ogni test sul TFM in cui il file compila. I test che dipendono da `App`/WinForms o driver nativi girano solo su `net10.0-windows`; i test su `Core` / `Infrastructure.Persistence` / `Services` girano su entrambi.

---

## Requisiti

- **.NET 10.0** (Windows 10+ x64 per suite completa; Linux per suite cross-platform)
- **xUnit 2.5.3** (incluso via NuGet)
- Progetto `App` compilabile (riferimento via `ProjectReference`)

---

## Quick Start

```bash
# Tutti i test (dual TFM)
dotnet test Tests/Tests.csproj

# Solo cross-platform (CI Linux)
dotnet test Tests/Tests.csproj --framework net10.0

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
├── Unit/
│   ├── Core/
│   │   └── Models/
│   │       ├── VariableTests.cs                  Record equality (3 test)
│   │       ├── CommandTests.cs                   Record equality (3 test)
│   │       ├── ProtocolAddressTests.cs           Record equality (3 test)
│   │       ├── DictionaryDataTests.cs            Constructor + order (3 test)
│   │       ├── ConnectionStateTests.cs           Enum values (2 test) — Fase 1
│   │       ├── DeviceVariantTests.cs             Enum values (5 test) — Fase 1
│   │       ├── DeviceVariantConfigTests.cs       Factory + equality (12 test) — Fase 1
│   │       ├── RawPacketTests.cs                 Immutabilità + equality (5 test) — Fase 1
│   │       ├── AppLayerDecodedEventTests.cs      Immutabilità + equality (5 test) — Fase 1
│   │       └── TelemetryDataPointTests.cs        Immutabilità + equality (4 test) — Fase 1
│   │
│   ├── Infrastructure/
│   │   ├── MockHttpMessageHandler.cs             Mock HTTP per API provider
│   │   ├── DictionaryApiProviderTests.cs         API provider (18 test)
│   │   ├── ExcelDictionaryProviderTests.cs       Excel provider (14 test)
│   │   ├── FallbackDictionaryProviderTests.cs    Fallback decorator (9 test)
│   │   └── Protocol/                             Adapter HW (Windows-only)
│   │       ├── FakePcanDriver.cs                 Mock manuale IPcanDriver
│   │       ├── FakeBleDriver.cs                  Mock manuale IBleDriver
│   │       ├── FakeSerialDriver.cs               Mock manuale ISerialDriver
│   │       ├── CanPortTests.cs                   State machine + arbId LE convention (28 test)
│   │       ├── BlePortTests.cs                   State machine + pass-through (27 test)
│   │       ├── SerialPortTests.cs                State machine + pass-through (27 test)
│   │       └── PacketEventArgsTests.cs           Ble/Serial/CAN args ctor + null validation (7 test)
│   │
│   ├── Services/
│   │   └── Protocol/
│   │       ├── PacketDecoderTests.cs             Decoder + thread-safety (19 test)
│   │       └── DictionarySnapshotTests.cs        Lookup + hex case-insensitive (21 test)
│   │
│   ├── Terminal/
│   │   └── TerminalTests.cs                      Append, get, write (6 test)
│   │
│   ├── Protocol/
│   │   └── RollingCodeGeneratorTests.cs          Range, ciclo, thread-safety (4 test)
│   │
│   ├── CodeGenerator/
│   │   └── SP_Code_GeneratorTests.cs             Header C, #define (7 test)
│   │
│   └── CircularProgressBar/
│       └── CircularProgressBarTests.cs           Clamping, validazione (5 test)
│
└── Integration/
    ├── Infrastructure/
    │   └── ExcelDictionaryProviderCrossReferenceTests.cs  Confronto campo per campo (6 test)
    │
    ├── DependencyInjection/
    │   └── ServiceRegistrationTests.cs                    DI wiring + IDictionaryProvider (5 test)
    │
    ├── CodeGenerator/
    │   └── SP_Code_GeneratorIntegrationTests.cs           Multi-config E2E (4 test)
    │
    └── Form1/
        ├── Form1DictionaryLoadingTests.cs                 Contratto IDictionaryProvider + flusso (9 test)
        └── Mocks/
            └── MockDictionaryProvider.cs                  Mock manuale IDictionaryProvider
```

---

## Copertura per modulo

| Modulo | File test | Test | Tipo |
|--------|-----------|------|------|
| **Core Models (Dictionary)** | `VariableTests.cs`, `CommandTests.cs`, `ProtocolAddressTests.cs`, `DictionaryDataTests.cs` | 12 | Unit |
| **Core Models (Protocol Abstractions)** | `ConnectionStateTests.cs`, `DeviceVariantTests.cs`, `DeviceVariantConfigTests.cs`, `RawPacketTests.cs`, `AppLayerDecodedEventTests.cs`, `TelemetryDataPointTests.cs` | 33 | Unit |
| **Infrastructure.Persistence API** | `DictionaryApiProviderTests.cs` | 18 | Unit |
| **Infrastructure.Persistence Excel** | `ExcelDictionaryProviderTests.cs` | 14 | Unit |
| **Infrastructure.Persistence Fallback** | `FallbackDictionaryProviderTests.cs` | 9 | Unit |
| **Infrastructure.Protocol CanPort** | `CanPortTests.cs` + `FakePcanDriver.cs` | 28 | Unit (Windows) |
| **Infrastructure.Protocol BlePort** | `BlePortTests.cs` + `FakeBleDriver.cs` | 27 | Unit (Windows) |
| **Infrastructure.Protocol SerialPort** | `SerialPortTests.cs` + `FakeSerialDriver.cs` | 27 | Unit (Windows) |
| **Infrastructure.Protocol Event Args** | `PacketEventArgsTests.cs` | 7 | Unit (Windows) |
| **Services PacketDecoder** | `PacketDecoderTests.cs` | 19 | Unit |
| **Services DictionarySnapshot** | `DictionarySnapshotTests.cs` | 21 | Unit |
| **Terminal** | `TerminalTests.cs` | 6 | Unit |
| **SPRollingCode** | `RollingCodeGeneratorTests.cs` | 4 | Unit |
| **SP_Code_Generator** | `SP_Code_GeneratorTests.cs` | 7 | Unit |
| **CircularProgressBar** | `CircularProgressBarTests.cs` | 5 | Unit |
| **Infrastructure.Persistence Excel** | `ExcelDictionaryProviderCrossReferenceTests.cs` | 6 | Integration |
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

I test cross-platform (`net10.0`) vengono eseguiti automaticamente nella pipeline Bitbucket (`bitbucket-pipelines.yml`) su runner Linux:

```
Build → Test (net10.0, 132 test) → ✅/❌
```

I test `net10.0-windows` (274 test, include integration Form1 + App + Infrastructure.Protocol adapter) girano solo in locale su Windows.

---

## Links

- [README Soluzione](../README.md)
- [App](../GUI.Windows/README.md)
- [Core](../Core/README.md)
- [Infrastructure.Persistence](../Infrastructure.Persistence/README.md)
- [Infrastructure.Protocol](../Infrastructure.Protocol/README.md)
- [Services](../Services/README.md)
- [CHANGELOG](../CHANGELOG.md)
