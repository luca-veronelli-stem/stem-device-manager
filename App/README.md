# App

> Progetto Windows Forms principale — gestione, diagnostica e comunicazione dispositivi STEM.  
> **Ultimo aggiornamento:** 2026-04-16

---

## Panoramica

| Aspetto | Valore |
|---------|--------|
| **Tipo** | Windows Forms (.NET 10) |
| **TFM** | `net10.0-windows10.0.19041.0` |
| **Target** | `win-x64` (Windows 10+) |
| **LOC** | ~54,000 |
| **Entry point** | `Program.cs` |

---

## Struttura

```
App/
├── Program.cs                      Entry point + DI setup
├── Form1.cs                        Main form (God Object ~54k LOC)
├── Form1.Designer.cs               WinForms designer
│
├── STEMProtocol/                   Protocollo comunicazione proprietario
│   ├── STEM_protocol.cs            Layer stack (Application, Network, Transport)
│   ├── PacketManager.cs            Gestione pacchetti multi-canale
│   ├── CanDataLayer.cs             Data layer CAN (PCAN)
│   ├── SerialDataLayer.cs          Data layer Serial + BLE
│   ├── BootManager.cs              Firmware bootloader
│   ├── TelemetryManager.cs         Telemetria lenta + veloce
│   └── SPRollingCode.cs            Codice rolling per sicurezza
│
├── GUI/                            Tab pages WinForms
│   ├── BLE_WF_Tab.cs               Tab interfaccia BLE
│   ├── CAN_WF_Tab.cs               Tab interfaccia CAN
│   ├── Boot_WF_Tab.cs              Tab bootloader
│   ├── Boot_Smart_WF_Tab.cs        Tab bootloader smart
│   ├── Telemetry_WF_Tab.cs         Tab telemetria
│   └── TopLift_Telemetry_WF_Tab.cs Tab telemetria TopLift
│
├── Resources/
│   ├── Dizionari STEM.xlsx         Excel dizionari embedded (~187k)
│   └── Ztem.ico                    Icona applicazione
│
├── ExcelHandler.cs                 Lettura dizionari da Excel (ClosedXML) — legacy
├── Terminal.cs                     Logger basico (StringBuilder)
├── SP_Code_Generator.cs            Generatore sp_config.h
├── CircularProgressBar.cs          Custom control progresso circolare
├── SerialPort_Manager.cs           Gestione porte seriali
├── PCAN_Manager.cs                 Gestione hardware PCAN
├── BLE_Manager.cs                  Gestione BLE
└── ExcelHandler.cs                 Legacy Excel — candidato rimozione Fase 3
```

---

## Dependency Injection

Configurazione minimale in `Program.cs`:

| Servizio | Implementazione | Lifetime |
|----------|----------------|----------|
| `IDictionaryProvider` | `ExcelDictionaryProvider` o `FallbackDictionaryProvider` | Singleton |

`IDictionaryProvider` viene registrato da `Infrastructure.DependencyInjection.AddDictionaryProvider()`.
Se `DictionaryApi:BaseUrl` + `ApiKey` sono configurati in `appsettings.json` o environment variables,
viene usato `FallbackDictionaryProvider(API, Excel)`. Altrimenti solo `ExcelDictionaryProvider`.

`Form1` riceve `IDictionaryProvider` via `IServiceProvider` nel constructor e chiama
`LoadDictionaryDataAsync(CancellationToken)` nell'evento `Load` per caricare protocollo e variabili.

---

## Build Configurations

| Configurazione | `DefineConstants` | Descrizione |
|----------------|--------------------|-------------|
| `Debug` / `Release` | — | Default, tutte le feature |
| `TOPLIFT-A2-Debug/Release` | `TOPLIFT` | Solo TopLift A2 |
| `EDEN-Debug/Release` | `EDEN` | Solo Eden XP |
| `EGICON-Debug/Release` | `EGICON` | Solo Spark |

---

## Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| ClosedXML | 0.105.0 | Lettura dizionari Excel |
| DocumentFormat.OpenXml | 3.5.1 | Supporto formati Excel |
| OxyPlot.WindowsForms | 2.2.0 | Grafici telemetria |
| Peak.PCANBasic.NET | 5.0.1 | Interfaccia CAN |
| Plugin.BLE | 3.2.0 | Bluetooth Low Energy |
| System.IO.Ports | 10.0.5 | Comunicazione seriale |
| Microsoft.Extensions.Configuration.EnvironmentVariables | 10.0.5 | Configurazione env vars |
| Microsoft.Extensions.Configuration.Json | 10.0.5 | Configurazione appsettings.json |
| Microsoft.Extensions.DependencyInjection | 10.0.5 | DI container |

---

## Requisiti

- **.NET 10.0** (Windows 10+ x64)
- **Visual Studio 2022/2026** con workload Desktop Development
- **PCAN USB** (opzionale, per comunicazione CAN)

---

## Quick Start

```bash
# Build
dotnet build App/App.csproj

# Esegui
dotnet run --project App/App.csproj

# Build per configurazione specifica
dotnet build App/App.csproj -c TOPLIFT-A2-Release
```

---

## Note Legacy

- `Form1.cs` è un God Object (~54k LOC con Designer) — contiene GUI + logica protocollo + telemetria
- Il caricamento dizionari avviene tramite `IDictionaryProvider` (async, event `Load`)
- `ExcelHandler.cs` è ancora presente ma non ha consumer attivi — candidato alla rimozione nella Fase 3
- Le configurazioni device usano `#if` preprocessor (`TOPLIFT`, `EDEN`, `EGICON`) — documentate in `Docs/PREPROCESSOR_DIRECTIVES.md`
- Il file Excel dei dizionari è embedded in `Infrastructure` (usato come fallback da `ExcelDictionaryProvider`)
- `InternalsVisibleTo("Tests")` abilitato per permettere test su tipi `internal`
- `ProjectReference` a `Core` e `Infrastructure` per accesso a `IDictionaryProvider`

---

## Links

- [README Soluzione](../README.md)
- [Tests](../Tests/README.md)
- [CHANGELOG](../CHANGELOG.md)
