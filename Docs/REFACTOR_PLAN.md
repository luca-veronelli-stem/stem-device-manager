# Refactor Plan — Stem.Device.Manager → Architettura Modulare

**Obiettivo:** Portare il progetto da god-object WinForms a clean architecture disaccoppiata e testabile, allineata ai pattern Stem (Production.Tracker, Communication, ButtonPanel.Tester).

**Stato attuale:** Core/Infrastructure puliti. Form1 (1183 LOC) è il god-object. STEMProtocol/ (2587 LOC) embedded in App. 6 tab WF accoppiate a Form1. 176 test esistenti.

**Vincoli:** WinForms resta (migrazione WPF è Fase 5 futura). Stem.Communication NuGet sostituirà lo stack protocollo ma non è ancora pronto — preparare l'astrazione.

---

## Branch Map

```
main
 └─ refactor/phase-1-protocol-abstractions
     └─ refactor/phase-2-services-layer
         └─ refactor/phase-3-form1-decomposition
             └─ refactor/phase-4-protocol-migration-prep
```

Ogni branch: PR → review → squash merge in main. Non procedere alla fase successiva senza merge della precedente.

---

## Fase 1 — `refactor/protocol-abstractions` ✅ Completata (2026-04-17)

**Obiettivo:** Estrarre interfacce e contratti nel layer Core per comunicazione e hardware.

**Esito:** branch eseguito come `refactor/protocol-abstractions`. Aggiunti 5 interfacce + 6 modelli + helper di equality strutturale in `Core/`, 33 test in `Tests/Unit/Core/Models/`, 7 file Lean 4 in `Specs/Phase1/`. Build + test verdi (86 net10.0 / 138 net10.0-windows). I feature flag booleani su `IDeviceVariantConfig` sono rimandati a Fase 3 — vedi nota in `Docs/PREPROCESSOR_DIRECTIVES.md`.

### 1.1 Interfacce Core

In `Core/Interfaces/` creare:

```
ICommunicationPort.cs    — astrazione su CAN/BLE/Serial (Connect, Disconnect, Send, Receive, IsConnected, ConnectionStateChanged event)
IPacketDecoder.cs        — decode payload → AppLayerDecodedEventArgs (senza dipendenza Form1)
ITelemetryService.cs     — StartFastTelemetry, StopTelemetry, OnDataReceived event, UpdateDictionary
IBootService.cs          — StartFirmwareUpload, OnProgress event, stato corrente
IDeviceVariantConfig.cs  — RecipientId default, device name, board name, feature flags (sostituisce #if TOPLIFT/EDEN/EGICON)
```

### 1.2 Modelli Core

In `Core/Models/` aggiungere:

```
ConnectionState.cs       — enum: Disconnected, Connecting, Connected, Error
AppLayerDecodedEvent.cs  — record: Command, Variable?, byte[] Payload, string SenderDevice, string SenderBoard
DeviceVariant.cs         — enum: Generic, TopLift, Eden, Egicon
DeviceVariantConfig.cs   — record implementa IDeviceVariantConfig con factory method per variant
TelemetryDataPoint.cs    — record: Variable, byte[] RawValue, DateTime Timestamp
```

### 1.3 Test

In `Tests/Unit/Core/` aggiungere test per:
- `DeviceVariantConfig` factory produce config corretta per ogni variant
- `AppLayerDecodedEvent` immutabilità e equality
- `TelemetryDataPoint` correttezza

### 1.4 Comandi

```bash
git checkout -b refactor/phase-1-protocol-abstractions
# Creare i file sopra elencati
# Verificare: dotnet build Core/Core.csproj
# Verificare: dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~Tests.Unit.Core"
# Commit: "feat(core): add protocol and communication abstractions"
```

---

## Fase 2 — `refactor/phase-2-services-layer`

**Obiettivo:** Popolare Services/ con implementazioni concrete. Spostare logica da App/ a Services/.

### 2.1 Setup progetti Services e Infrastructure.Protocol

**`Services/Services.csproj`** — target `net10.0` puro (cross-platform), riferimento solo a `Core`. Contiene solo logica pura senza dipendenze da driver hardware o WinForms. I test girano anche in CI Linux.

**`Infrastructure.Protocol/Infrastructure.Protocol.csproj`** — **nuovo progetto**, dual TFM (`net10.0;net10.0-windows10.0.19041.0`), riferimento a `Core`. Contiene gli adapter hardware che dipendono da driver nativi (Peak.PCANBasic.NET, Plugin.BLE, System.IO.Ports). Pattern allineato con `Stem.ButtonPanel.Tester/Infrastructure`.

Creare `Services/DependencyInjection.cs`:
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<ITelemetryService, TelemetryService>();
        services.AddSingleton<IBootService, BootService>();
        services.AddSingleton<IPacketDecoder, PacketDecoder>();
        return services;
    }
}
```

`Infrastructure.Protocol/DependencyInjection.cs` esporrà `AddProtocolInfrastructure()` per registrare gli adapter (`CanPort`, `BlePort`, `SerialPort`) come `ICommunicationPort`.

### 2.2 Estrarre da App/STEMProtocol/

| Sorgente | Destinazione (Services/) | Note |
|----------|-------------------------|------|
| `TelemetryManager.cs` (422 LOC) | `Services/Telemetry/TelemetryService.cs` | Implementa `ITelemetryService`. Rimuovere dipendenze UI. Riceve `IDictionaryProvider` via DI. |
| `BootManager.cs` (371 LOC) | `Services/Boot/BootService.cs` | Implementa `IBootService`. Progress via eventi tipizzati, non callback a Form1. |
| `PacketManager.cs` decode logic | `Services/Protocol/PacketDecoder.cs` | Implementa `IPacketDecoder`. Solo decode, nessun riferimento a canali HW. |
| `STEM_protocol.cs` send/receive | `Services/Protocol/ProtocolService.cs` | Facade: usa `ICommunicationPort`, gestisce encode/decode/routing. |

### 2.3 Adattatori hardware

In `Infrastructure.Protocol/Hardware/` (non in Services — dipendono da driver nativi Windows):
```
CanPort.cs       — implementa ICommunicationPort wrappando PCAN_Manager (Peak.PCANBasic.NET)
BlePort.cs       — implementa ICommunicationPort wrappando BLE_SDL (Plugin.BLE)
SerialPort.cs    — implementa ICommunicationPort wrappando SerialDataLayer (System.IO.Ports)
```

I file originali in `App/STEMProtocol/` restano come delegate interno (verranno rimossi in Fase 4 quando arriva Stem.Communication).

### 2.4 DeviceVariantConfig

In `Services/Configuration/`:
```
DeviceVariantConfigFactory.cs — legge la variant da appsettings.json ("Device:Variant": "TopLift"|"Eden"|"Egicon"|"Generic"), produce IDeviceVariantConfig
```

Aggiornare `appsettings.json` con sezione `"Device"`.

### 2.5 Test

Target: ≥90% coverage su Services/.
- `TelemetryServiceTests` — encode payload, decode risposta, UpdateDictionary con mock IDictionaryProvider
- `BootServiceTests` — state machine upload, eventi progress
- `PacketDecoderTests` — decode da byte[] a AppLayerDecodedEvent
- `DeviceVariantConfigFactoryTests` — ogni variant produce config corretta
- Mock manuali di `ICommunicationPort` in `Tests/Mocks/`

### 2.6 Comandi

```bash
git checkout -b refactor/phase-2-services-layer
# Creare Services.csproj, DependencyInjection.cs
# Estrarre TelemetryService, BootService, PacketDecoder, ProtocolService
# Creare adapter hardware (CanPort, BlePort, SerialPort)
# Creare DeviceVariantConfigFactory
# Aggiornare Stem.Device.Manager.slnx con Services
# Verificare: dotnet build Stem.Device.Manager.slnx
# Aggiungere test in Tests/Unit/Services/
# Verificare: dotnet test Tests/Tests.csproj
# Commit incrementali per ogni componente estratto
# Commit finale: "feat(services): extract protocol, telemetry, boot services from App"
```

---

## Fase 3 — `refactor/phase-3-form1-decomposition`

**Obiettivo:** Ridurre Form1 a thin shell che delega ai servizi. Tab autonome.

### 3.1 Eliminare `static FormRef`

Ogni classe che usa `Form1.FormRef` deve ricevere le dipendenze via constructor/DI. Grep per `FormRef` e sostituire ogni occorrenza con l'interfaccia appropriata.

### 3.2 Estrarre da Form1 i ruoli

| Responsabilità | Dove va | Come |
|----------------|---------|------|
| Dictionary data ownership (3 liste) | Servizio `DictionaryCache` in Services/ | Singleton, espone `IReadOnlyList<T>`, evento `DictionaryUpdated` |
| RecipientId + board selection | `DictionaryCache.SelectBoard(device, board)` | Chiama `IDictionaryProvider.LoadVariablesAsync`, aggiorna cache |
| Connection status 3 canali | `ConnectionManager` in Services/ | Aggrega stato `ICommunicationPort[]`, espone evento unico |
| Packet decoding + event dispatch | `IPacketDecoder` (già in Fase 2) | Form1 si iscrive a eventi |
| SP_Code_Generator | Resta in App/ | Basso accoppiamento, non critico |

### 3.3 Tab autonome

Ogni `*_WF_Tab.cs` riceve dipendenze via constructor injection (non da Form1):

```csharp
// Pattern per ogni tab
public class Telemetry_WF_Tab : TabPage
{
    private readonly ITelemetryService _telemetryService;
    private readonly DictionaryCache _dictionaryCache;

    public Telemetry_WF_Tab(ITelemetryService telemetryService, DictionaryCache dictionaryCache)
    {
        _telemetryService = telemetryService;
        _dictionaryCache = dictionaryCache;
        _dictionaryCache.DictionaryUpdated += OnDictionaryUpdated;
    }
}
```

Eliminare `UpdateDictionary(List<Variable>)` da tutte le tab — usano l'evento di `DictionaryCache`.

### 3.4 Form1 risultante

~200-300 LOC: constructor DI, creazione tab, layout, menu handler che delegano ai servizi. Nessuna logica di business.

### 3.5 Rimuovere #if condizionali

Sostituire tutti i 17 blocchi `#if TOPLIFT/EDEN/EGICON` con:
```csharp
if (_variantConfig.Variant == DeviceVariant.TopLift) { ... }
```

Rimuovere le 8 build configuration device-specific dal .csproj. Tenere solo Debug/Release. La variant viene da `appsettings.json`.

### 3.6 Test

- `Form1IntegrationTests` — verifica che Form1 si istanzia con mock services
- `DictionaryCacheTests` — SelectBoard aggiorna, eventi fired
- `ConnectionManagerTests` — aggregazione stati, eventi
- Test tab: ogni tab funziona con mock services

### 3.7 Comandi

```bash
git checkout -b refactor/phase-3-form1-decomposition
# Creare DictionaryCache, ConnectionManager
# Refactor tab per DI (una tab per commit)
# Refactor Form1 (eliminare FormRef, delegare a servizi)
# Sostituire #if con runtime variant config
# Aggiornare App.csproj (rimuovere build config device-specific)
# Aggiornare Program.cs DI registration
# Verificare: dotnet build Stem.Device.Manager.slnx
# Verificare: dotnet test Tests/Tests.csproj
# Documentare #if rimossi in Docs/PREPROCESSOR_DIRECTIVES.md
# Commit: "refactor(app): decompose Form1 into services and autonomous tabs"
```

---

## Fase 4 — `refactor/phase-4-protocol-migration-prep`

**Obiettivo:** Preparare la sostituzione dello stack protocollo embedded con Stem.Communication NuGet.

### 4.1 Adapter per Stem.Communication

In `Services/Protocol/`:
```
StemProtocolAdapter.cs  — implementa ICommunicationPort usando Stem.Communication.StemClient
                          (ProtocolStackBuilder → ProtocolStack → StemClient wrapper)
StemCanDriver.cs        — implementa Stem.Communication.IChannelDriver wrappando PCAN_Manager
StemBleDriver.cs        — implementa Stem.Communication.IChannelDriver wrappando BLE_Manager
```

### 4.2 Mappatura comandi

Creare `Services/Protocol/CommandMapper.cs`:
- Mappa `Core.Models.Command` ↔ `Stem.Communication.CommandId` enum (43 comandi)
- Mappa `AppLayerDecodedEvent` ↔ `Stem.Communication.ApplicationMessage`

### 4.3 Feature flag

In `appsettings.json`:
```json
{
  "Protocol": {
    "UseNewStack": false
  }
}
```

`DependencyInjection.cs` registra il vecchio o nuovo stack in base al flag. Permette rollback immediato.

### 4.4 Rimuovere codice legacy

Quando `UseNewStack: true` è stabile:
- Eliminare `App/STEMProtocol/` (2587 LOC)
- Eliminare `App/PCAN_Manager.cs`, `App/BLE_Manager.cs`, `App/SerialPort_Manager.cs`
- Aggiungere PackageReference a `Stem.Communication`, `Stem.Communication.Drivers.Can`, `Stem.Communication.Drivers.Ble`

### 4.5 Test

- `StemProtocolAdapterTests` — mock StemClient, verifica send/receive
- `CommandMapperTests` — round-trip Core.Command ↔ CommandId
- Integration test con feature flag on/off

### 4.6 Comandi

```bash
git checkout -b refactor/phase-4-protocol-migration-prep
# Aggiungere PackageReference Stem.Communication (quando disponibile come NuGet)
# Creare adapter e mapper
# Aggiungere feature flag
# Test con entrambi gli stack
# Commit: "feat(services): add Stem.Communication adapter with feature flag"
# Quando stabile:
# Commit: "refactor(app): remove legacy STEMProtocol stack"
```

---

## Struttura finale

```
Core/                    [net10.0, zero deps]
  Interfaces/            ICommunicationPort, IPacketDecoder, ITelemetryService, IBootService, IDeviceVariantConfig
  Models/                Variable, Command, ProtocolAddress, DictionaryData, ConnectionState, DeviceVariant, AppLayerDecodedEvent, TelemetryDataPoint

Infrastructure/          [net10.0]
  Api/, Excel/           DictionaryApiProvider, ExcelDictionaryProvider, FallbackDictionaryProvider
  DependencyInjection.cs

Infrastructure.Protocol/ [net10.0;net10.0-windows]
  Hardware/              CanPort, BlePort, SerialPort (wrappano driver nativi)
  DependencyInjection.cs

Services/                [net10.0, pure logic]
  Telemetry/             TelemetryService
  Boot/                  BootService
  Protocol/              ProtocolService, PacketDecoder, DictionarySnapshot, StemProtocolAdapter (Fase 4), CommandMapper (Fase 4)
  Configuration/         DeviceVariantConfigFactory
  Cache/                 DictionaryCache, ConnectionManager
  DependencyInjection.cs

App/                     [net10.0-windows, WinForms]
  Form1.cs               ~250 LOC thin shell
  *_WF_Tab.cs            Tab autonome con DI
  Program.cs             Host DI setup
  appsettings.json

Tests/                   [dual TFM]
  Unit/Core/
  Unit/Infrastructure/
  Unit/Services/
  Integration/
  Mocks/
```

---

## Checklist pre-merge per ogni fase

1. `dotnet build Stem.Device.Manager.slnx` — zero warning
2. `dotnet test Tests/Tests.csproj` — tutti i test passano (inclusi i 176 esistenti)
3. `dotnet test Tests/Tests.csproj --framework net10.0` — cross-platform OK
4. Nessun `using` circolare tra layer
5. Core non referenzia Infrastructure, Services, o App
6. PR description elenca LOC prima/dopo per Form1
7. CLAUDE.md aggiornato con nuovi componenti

---

## Note per l'esecuzione

- **Un commit per ogni componente estratto** — facilita review e bisect
- **Non rompere mai la build** — ogni commit compila e i test passano
- **Mock manuali** — niente Moq/NSubstitute, come da convenzione progetto
- **Naming:** classi/metodi in inglese, commenti/doc in italiano
- **CancellationToken** su ogni metodo async
- **Nullable types** abilitati, mai restituire null per errori
- **Funzioni < 15 LOC**, early returns
