# Stem.Device.Manager

[![Version](https://img.shields.io/badge/version-2.15-blue)](./CHANGELOG.md)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-101-brightgreen)](./Tests/)
[![License](https://img.shields.io/badge/license-Proprietary-red)](#licenza)

> **Applicativo desktop per la gestione, diagnostica e test dei dispositivi STEM via protocollo proprietario multi-canale (CAN, BLE, Serial).**

> **Ultimo aggiornamento:** 2026-04-14

---

## Panoramica

STEMPM è un tool Windows desktop utilizzato dal team firmware STEM per:

- **Comunicare** con i dispositivi via protocollo proprietario (CAN, BLE, Serial)
- **Leggere/scrivere** variabili e comandi tramite dizionari Excel embedded
- **Aggiornare firmware** via bootloader proprietario (classico e smart)
- **Monitorare telemetria** in tempo reale con grafici OxyPlot
- **Collaudare pulsantiere** con test automatizzati (DI + MVP)
- **Generare codice** configurazione (`sp_config.h`)

### Stato Attuale

Il progetto è un **monolite legacy** attualmente in fase di modernizzazione:
- ~56k LOC di codice produzione in un singolo progetto
- `Form1.cs` è un God Object (~55k LOC con Designer)
- **101 test automatizzati** (76 unit + 25 integration) — xUnit
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
| **Bootloader** | ✅ | Aggiornamento firmware (classico + smart) |
| **Telemetria** | ✅ | Lettura variabili + grafici OxyPlot (lenta + veloce) |
| **Test Pulsantiere** | ✅ | Collaudo automatizzato con DI + MVP |
| **Code Generator** | ✅ | Genera sp_config.h |
| **Test Automatizzati** | ✅ | 101 test (76 unit + 25 integration) — xUnit |

---

## Requisiti

- **.NET 8.0** (Windows 10+ x64)
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
├── App/                            Progetto Windows Forms (.NET 8)
│   ├── App.csproj                  Progetto principale
│   ├── Program.cs                  Entry point + DI
│   ├── Form1.cs                    Main form (God Object ~55k LOC)
│   │
│   ├── STEMProtocol/               Protocollo comunicazione proprietario
│   │   ├── STEM_protocol.cs        Layer stack (Application, Network, Transport)
│   │   ├── PacketManager.cs        Gestione pacchetti multi-canale
│   │   ├── CanDataLayer.cs         Data layer CAN (PCAN)
│   │   ├── SerialDataLayer.cs      Data layer Serial + BLE
│   │   ├── BootManager.cs          Firmware bootloader
│   │   ├── TelemetryManager.cs     Telemetria lenta + veloce
│   │   └── SPRollingCode.cs        Codice rolling
│   │
│   ├── Core/                       Modelli e interfacce (ButtonPanel)
│   │   ├── Enums/                  5 enums
│   │   ├── Models/                 3 modelli
│   │   └── Interfaces/             2 interfacce
│   │
│   ├── Services/                   Logica business
│   │   └── ButtonPanelTestService.cs Test pulsantiere
│   │
│   ├── GUI/                        Views + Presenters (MVP)
│   │   ├── Views/                  ButtonPanelTestTabControl
│   │   └── Presenters/             ButtonPanelTestPresenter
│   │
│   ├── Resources/                  Risorse embedded
│   │   ├── Dizionari STEM.xlsx     Excel dizionari (~187k)
│   │   └── Ztem.ico                Icona applicazione
│   │
│   ├── *_WF_Tab.cs                 Tab pages WinForms (BLE, CAN, Boot, Telemetry)
│   ├── ExcelHandler.cs             Lettura dizionari Excel
│   ├── Terminal.cs                 Logger basico
│   └── SP_Code_Generator.cs        Generatore sp_config.h
│
├── Docs/                           Documentazione
│   └── Standards/                  Standard e template
│
├── Tests/                          Unit & integration tests (xUnit)
│   ├── Unit/                       76 test unitari
│   └── Integration/                25 test di integrazione
│
└── .copilot/                       Istruzioni Copilot + agents
```

---

## Documentazione

- [App — Progetto principale](./App/README.md)
- [Tests — Test automatizzati](./Tests/README.md)
- [Standards e Templates](./Docs/Standards/)
- [CHANGELOG](./CHANGELOG.md)
- [LICENSE](./LICENSE)

---

## Issue Correlate

→ [ISSUES.md](./ISSUES.md) (da creare)

---

## Licenza

- **Proprietario:** STEM E.m.s.
- **Autore:** Michele Pignedoli, Luca Veronelli
- **Data di Creazione:** 2026-04-14
- **Licenza:** Proprietaria - Tutti i diritti riservati
