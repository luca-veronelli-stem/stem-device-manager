# Stem.Device.Manager

[![Version](https://img.shields.io/badge/version-2.15-blue)](./CHANGELOG.md)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![Tests](https://img.shields.io/badge/tests-274-brightgreen)](./Tests/)
[![License](https://img.shields.io/badge/license-Proprietary-red)](#licenza)

> **Applicativo desktop per la gestione, diagnostica e comunicazione dei dispositivi STEM via protocollo proprietario multi-canale (CAN, BLE, Serial).**  
> **Ultimo aggiornamento:** 2026-04-20

---

## Panoramica

Stem Device Manager è un tool Windows desktop utilizzato per:

- **Comunicare** con i dispositivi via protocollo proprietario (CAN, BLE, Serial)
- **Leggere/scrivere** variabili e comandi tramite dizionari Azure API
- **Aggiornare firmware** via bootloader proprietario (classico e smart)
- **Monitorare telemetria** in tempo reale con grafici OxyPlot
- **Generare codice** configurazione (`sp_config.h`)

### Stato Attuale

Il progetto è un **monolite legacy** in fase di modernizzazione incrementale:  
- `Form1.cs` è un God Object (~54k LOC con Designer) — usa `IDictionaryProvider` per i dizionari  
- **274 test automatizzati** (xUnit) — unit + integration, di cui 132 cross-platform (CI Linux)  
- Architettura multi-progetto:
  - **Core** (net10.0) — modelli dominio + interfacce  
  - **Infrastructure.Persistence** (net10.0) — provider dati (API Azure + Excel + Fallback)  
  - **Infrastructure.Protocol** (dual TFM) — adapter HW CAN/BLE/Serial + driver PCAN  
  - **Services** (net10.0) — logica pura (PacketDecoder, DictionarySnapshot, ...)  
  - **App** (net10.0-windows, WinForms) — entry point + GUI + legacy STEMProtocol  
- Refactor architetturale (vedi [`Docs/REFACTOR_PLAN.md`](./Docs/REFACTOR_PLAN.md)):
  - Fase 1 — protocol abstractions in Core ✅
  - Fase 2 — services layer + HW adapter (Step 1-2 completati, in corso) 🚧
  - Fase 3 — decomposizione Form1 ⏳

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **Protocollo STEM** | ✅ | Layer stack proprietario (Application, Network, Transport) |
| **CAN (PCAN)** | ✅ | Comunicazione via Peak PCAN USB |
| **BLE** | ✅ | Bluetooth Low Energy via Plugin.BLE |
| **Seriale** | ✅ | Comunicazione via porta COM |
| **Dizionari Azure API** | ✅ | Provider API REST con fallback Excel (DI) |
| **Bootloader** | ✅ | Aggiornamento firmware (classico + smart) |
| **Telemetria** | ✅ | Lettura variabili + grafici OxyPlot (lenta + veloce) |
| **Code Generator** | ✅ | Genera sp_config.h |
| **Test Automatizzati** | ✅ | 274 test (unit + integration) — xUnit |

---

## Requisiti

- **.NET 10.0** (Windows 10+ x64)
- **Visual Studio 2022/2026** con workload Desktop Development
- **PCAN USB** (opzionale, per comunicazione CAN)

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| ClosedXML | 0.105.0 | Lettura dizionari Excel (Infrastructure) |
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
git clone https://bitbucket.org/stem-fw/stem-device-manager

# Build
dotnet build

# Test
dotnet test Tests/Tests.csproj

# Esegui
dotnet run --project GUI.Windows/GUI.Windows.csproj
```

### Build Configurations

| Configurazione | Descrizione |
|----------------|-------------|
| `Debug` | Default, tutte le feature |
| `Release` | Build di rilascio |
| `TOPLIFT-A2-Debug/Release` | Solo funzionalità TopLift A2 |
| `EDEN-Debug/Release` | Solo funzionalità Eden XP |
| `EGICON-Debug/Release` | Solo funzionalità Spark |

---

## Struttura Soluzione

```
Stem.Device.Manager/
├── Stem.Device.Manager.slnx          Solution file (XML moderno)
├── Core/                             Modelli dominio + interfacce (net10.0, zero deps)
│   ├── Models/                       Dizionario + protocol abstractions
│   │                                 (Variable, Command, ProtocolAddress, DictionaryData,
│   │                                  ConnectionState, DeviceVariant, DeviceVariantConfig,
│   │                                  RawPacket, AppLayerDecodedEvent, TelemetryDataPoint)
│   └── Interfaces/                   IDictionaryProvider, ICommunicationPort, IPacketDecoder,
│                                     ITelemetryService, IBootService, IDeviceVariantConfig
├── Infrastructure.Persistence/       Provider dati dizionario (net10.0)
│   ├── Api/                          DictionaryApiProvider + DTO (REST Azure)
│   ├── Excel/                        ExcelDictionaryProvider (fallback embedded)
│   ├── FallbackDictionaryProvider    Decorator API→Excel
│   └── DependencyInjection.cs        AddDictionaryProvider()
├── Infrastructure.Protocol/          Adapter HW (dual TFM: net10.0 + net10.0-windows)
│   └── Hardware/                     CanPort, BlePort, SerialPort (ICommunicationPort)
│                                     + PCANManager driver + IPcanDriver/IBleDriver/ISerialDriver
├── Services/                         Logica applicativa pura (net10.0 — cross-platform)
│   └── Protocol/                     PacketDecoder, DictionarySnapshot
├── GUI.Windows/                              Windows Forms (net10.0-windows)
│   ├── Program.cs                    Entry point + DI + IConfiguration
│   ├── Form1.cs                      Main form (God Object ~54k LOC)
│   ├── STEMProtocol/                 Protocollo legacy (da migrare in Fase 4)
│   ├── BLE_Manager.cs                Driver BLE (implementa IBleDriver)
│   ├── SerialPort_Manager.cs         Driver seriale (implementa ISerialDriver)
│   └── GUI/                          Tab pages WinForms
├── Specs/                            Formalizzazioni Lean 4
│   └── Phase1/                       Tipi/interfacce protocol (Fase 1)
├── Tests/                            274 test (xUnit, dual TFM)
│   ├── Unit/                         Core, Infrastructure.Persistence, Infrastructure.Protocol, Services, Terminal
│   └── Integration/                  DI, ExcelHandler, CodeGenerator, Form1
└── Docs/                             REFACTOR_PLAN, PROTOCOL, PREPROCESSOR_DIRECTIVES, Standards
```

---

## Documentazione

- [App — Progetto principale](./GUI.Windows/README.md)
- [Core — Modelli dominio e interfacce](./Core/README.md)
- [Infrastructure.Persistence — Provider dati dizionario (API + Excel)](./Infrastructure.Persistence/README.md)
- [Infrastructure.Protocol — Adapter HW CAN/BLE/Serial](./Infrastructure.Protocol/README.md)
- [Services — Logica applicativa pura](./Services/README.md)
- [Tests — Test automatizzati](./Tests/README.md)
- [Standards e Templates](./Docs/Standards/)
- [Piano di refactoring architetturale](./Docs/REFACTOR_PLAN.md)
- [Protocollo STEM — funzionamento interno](./Docs/PROTOCOL.md)
- [Direttive preprocessore (#if)](./Docs/PREPROCESSOR_DIRECTIVES.md)
- [Lean/Spec001 — formalizzazioni Lean 4](./Lean/Spec001/)
- [CHANGELOG](./CHANGELOG.md)
- [LICENSE](./LICENSE)

---

## Licenza

- **Proprietario:** STEM E.m.s.
- **Autore:** Michele Pignedoli, Luca Veronelli
- **Data di Creazione:** 2024-06-27
- **Licenza:** Proprietaria - Tutti i diritti riservati
