# Stem.Device.Manager

[![Version](https://img.shields.io/badge/version-2.15-blue)](./CHANGELOG.md)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-272-brightgreen)](./Tests/)
[![License](https://img.shields.io/badge/license-Proprietary-red)](#licenza)

> **Applicativo desktop per la gestione, diagnostica e test dei dispositivi STEM via protocollo proprietario multi-canale (CAN, BLE, Serial).**  
> **Ultimo aggiornamento:** 2026-04-16

---

## Panoramica

Stem Device Manager è un tool Windows desktop utilizzato per:

- **Comunicare** con i dispositivi via protocollo proprietario (CAN, BLE, Serial)
- **Leggere/scrivere** variabili e comandi tramite dizionari Excel embedded
- **Aggiornare firmware** via bootloader proprietario (classico e smart)
- **Monitorare telemetria** in tempo reale con grafici OxyPlot
- **Collaudare pulsantiere** con test automatizzati (DI + MVP)
- **Generare codice** configurazione (`sp_config.h`)

### Stato Attuale

Il progetto è un **monolite legacy** attualmente in fase di modernizzazione:  
- ~56k LOC di codice produzione in un singolo progetto  
- `Form1.cs` è un God Object (~55k LOC con Designer) — ora usa `IDictionaryProvider` per i dizionari  
- **272 test automatizzati** (92 net10.0 + 180 net10.0-windows) — xUnit  
- Architettura multi-progetto: **Core** (net10.0) + **Infrastructure** (net10.0) + **App** (WinForms)  
- Infrastruttura API Azure pronta con fallback Excel via DI; Form1 migrata a `IDictionaryProvider`  
- Unico modulo con architettura pulita: test pulsantiere (DI + MVP)

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Protocollo STEM** | ✅ | Layer stack proprietario (Application, Network, Transport) |
| **CAN (PCAN)** | ✅ | Comunicazione via Peak PCAN USB |
| **BLE** | ✅ | Bluetooth Low Energy via Plugin.BLE |
| **Seriale** | ✅ | Comunicazione via porta COM |
| **Dizionari Excel** | ✅ | Lettura variabili/comandi da Excel embedded |
| **API Dizionari Azure** | ✅ | Provider API REST con fallback Excel (DI) |
| **Bootloader** | ✅ | Aggiornamento firmware (classico + smart) |
| **Telemetria** | ✅ | Lettura variabili + grafici OxyPlot (lenta + veloce) |
| **Test Pulsantiere** | ✅ | Collaudo automatizzato con DI + MVP |
| **Code Generator** | ✅ | Genera sp_config.h |
| **Test Automatizzati** | ✅ | 272 test (unit + integration) — xUnit |

---

## Requisiti

- **.NET 10.0** (Windows 10+ x64)
- **Visual Studio 2022/2026** con workload Desktop Development
- **PCAN USB** (opzionale, per comunicazione CAN)

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| ClosedXML | 0.105.0 | Lettura dizionari Excel |
| DocumentFormat.OpenXml | 3.5.1 | Supporto formati Excel |
| OxyPlot.WindowsForms | 2.2.0 | Grafici telemetria |
| Peak.PCANBasic.NET | 5.0.1 | Interfaccia CAN PCAN |
| Plugin.BLE | 3.2.0 | Bluetooth Low Energy |
| System.IO.Ports | 10.0.5 | Comunicazione seriale |
| Microsoft.Extensions.DependencyInjection | 10.0.5 | DI container |

---

## Quick Start

```bash
# Clona il repository
git clone https://bitbucket.org/stem-fw/win10-stem-dev-man

# Build
dotnet build

# Test
dotnet test Tests/Tests.csproj

# Esegui
dotnet run --project App/App.csproj
```

### Build Configurations

| Configurazione | Descrizione |
|----------------|-------------|
| `Debug` | Default, tutte le feature |
| `Release` | Build di rilascio |
| `TOPLIFT-A2-Debug/Release` | Solo funzionalità TopLift A2 |
| `EDEN-Debug/Release` | Solo funzionalità Eden XP |
| `EGICON-Debug/Release` | Solo funzionalità Spark |
| `BUTTONPANEL` | Solo test pulsantiere |

---

## Struttura Soluzione

```
Stem.Device.Manager/
├── Stem.Device.Manager.slnx        Solution file (XML moderno)
├── Core/                            Modelli dominio, interfacce (net10.0)
│   ├── Models/                      Variable, Command, ProtocolAddress, ButtonPanel
│   ├── Enums/                       ButtonPanelEnums
│   └── Interfaces/                  IDictionaryProvider, IButtonPanelTestService
├── Infrastructure/                   Provider dati (net10.0)
│   ├── Api/                         DictionaryApiProvider + DTO
│   ├── Excel/                       ExcelDictionaryProvider
│   ├── FallbackDictionaryProvider   Decorator API→Excel
│   └── DependencyInjection.cs       Registrazione DI
├── Services/                        Logica business (net10.0-windows, vuoto)
├── App/                             Windows Forms (.NET 10)
│   ├── Program.cs                   Entry point + DI + IConfiguration
│   ├── Form1.cs                     Main form (God Object ~55k LOC) — usa IDictionaryProvider
│   ├── ExcelHandler.cs              Legacy Excel (non più usato da Form1)
│   ├── STEMProtocol/                Protocollo comunicazione proprietario
│   ├── GUI/                         Views + Presenters (MVP)
│   └── Resources/                   Excel embedded, icone
├── Tests/                           272 test (xUnit, dual TFM)
│   ├── Unit/                        Core, Infrastructure, ExcelHandler, Protocol
│   └── Integration/                 DI, Presenter, ExcelHandler, CodeGenerator
└── Docs/                            Documentazione + Standards
```

---

## Documentazione

- [App — Progetto principale](./App/README.md)
- [Core — Modelli dominio e interfacce](./Core/README.md)
- [Infrastructure — Provider dati (API + Excel)](./Infrastructure/README.md)
- [Tests — Test automatizzati](./Tests/README.md)
- [Standards e Templates](./Docs/Standards/)
- [Analisi refactoring Fase 3](./Docs/REFACTOR_ANALYSIS.md)
- [Direttive preprocessore (#if)](./Docs/PREPROCESSOR_DIRECTIVES.md)
- [CHANGELOG](./CHANGELOG.md)
- [LICENSE](./LICENSE)

---

## Issue Correlate

→ [ISSUES.md](./ISSUES.md) (da creare)

---

## Licenza

- **Proprietario:** STEM E.m.s.
- **Autore:** Michele Pignedoli, Luca Veronelli
- **Data di Creazione:** 2024-06-27
- **Licenza:** Proprietaria - Tutti i diritti riservati
