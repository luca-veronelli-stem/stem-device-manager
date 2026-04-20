# Refactor Plan — Stem.Device.Manager → Architettura Modulare

**Obiettivo:** Portare il progetto da god-object WinForms a clean architecture disaccoppiata e testabile, allineata ai pattern Stem (Production.Tracker, Communication, ButtonPanel.Tester).

**Stato attuale:** Core/Infrastructure puliti. Form1 (1183 LOC) è il god-object. STEMProtocol/ (2587 LOC) embedded in App. 6 tab WF accoppiate a Form1. 176 test esistenti.

**Vincoli:** WinForms resta (migrazione WPF è Fase 5 futura). Stem.Communication NuGet sostituirà lo stack protocollo ma non è ancora pronto — preparare l'astrazione.

**Decisioni architetturali (confermate):**
- **IProtocolService facade** — TelemetryService/BootService dipendono da `IProtocolService` (non da `ICommunicationPort` + `IPacketDecoder` direttamente). `ProtocolService` incapsula encode/chunking/CRC/reassembly/decode; i servizi di business lo usano via `SendCommandAsync`/`SendCommandAndWaitReplyAsync` + evento `AppLayerDecoded`.
- **Riscrittura, non wrapping** — la logica in Services/ è codice nuovo, non wrapper sui vecchi file in `App/STEMProtocol/`. I file legacy restano in App/ senza essere chiamati dopo Fase 3, eliminati in Fase 4.
- **Nessun FormRef nei nuovi servizi** — i servizi nascono senza alcun riferimento a Form1 o UI. Il progresso/stato viene comunicato via eventi tipizzati. Il rewiring UI avviene in Fase 3.
- **ImmutableArray<byte> per payload** — `AppLayerDecodedEvent.Payload` usa `ImmutableArray<byte>` (BCL `System.Collections.Immutable`, incluso nel runtime .NET 8+, non è un NuGet esterno). Fornisce immutabilità vera + equality strutturale elemento per elemento.
- **Services referenzia solo Core** — nessun riferimento a Infrastructure. Il wiring concreto avviene nel composition root (`App/Program.cs`).

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
IProtocolService.cs      — facade protocollo: SendCommandAsync, SendCommandAndWaitReplyAsync, evento AppLayerDecoded
ITelemetryService.cs     — StartFastTelemetry, StopTelemetry, OnDataReceived event, UpdateDictionary
IBootService.cs          — StartFirmwareUpload, OnProgress event, stato corrente
IDeviceVariantConfig.cs  — RecipientId default, device name, board name, feature flags (sostituisce #if TOPLIFT/EDEN/EGICON)
```

### 1.2 Modelli Core

In `Core/Models/` aggiungere:

```
ConnectionState.cs       — enum: Disconnected, Connecting, Connected, Error
AppLayerDecodedEvent.cs  — record: Command, Variable?, ImmutableArray<byte> Payload, string SenderDevice, string SenderBoard
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

## Fase 2 — `refactor/services-layer` 🚧 In corso

**Obiettivo:** Popolare Services/ con implementazioni concrete + introdurre Infrastructure.Protocol per gli adapter HW. Spostare logica da App/ a Services/ / Infrastructure.Protocol/.

**Progresso al 2026-04-20:**

Branch `refactor/services-foundation` (merged → main, PR #24):
- ✅ Step 1 — Setup struttura progetti: Services `net10.0` puro + nuovo `Infrastructure.Protocol` dual TFM + rinomina `Infrastructure` → `Infrastructure.Persistence` (allineamento pattern Stem)
- ✅ Step 1 — `PacketDecoder` + `DictionarySnapshot` in `Services/Protocol/`
- ✅ Step 2 — Adapter HW `CanPort` (option A arbId LE prefix), `BlePort` e `SerialPort` (pass-through) in `Infrastructure.Protocol/Hardware/`
- ✅ `PCANManager` spostato da `App/` a `Infrastructure.Protocol/Hardware/` (driver autonomo)
- ✅ `Docs/PROTOCOL.md` — documentazione completa del protocollo STEM legacy

Branch `refactor/protocol-service` (merged → main 2026-04-20, PR #25, **Branch A** completata):
- ✅ `NetInfo` struct + `PacketReassembler` thread-safe (Services/Protocol/)
- ✅ `ChannelKind` enum in Core.Models + proprietà `Kind` su `ICommunicationPort`
- ✅ Step 6 — `ProtocolService` facade (encode TP + CRC16 + chunking + framing per canale; decode + reassembly + event; pattern request/reply con validator custom)
- ✅ Test: **317 net10.0-windows** (era 274) / **172 net10.0** (era 132) — +43 test (13 NetInfo + 13 PacketReassembler + 13 ProtocolService + 3 Kind adapter + 1 refactor PacketDecoder)

Branch `refactor/services-business` (in corso, **Branch B**, Step 3-5 completati):
- ✅ Step 3 — `Services/Configuration/DeviceVariantConfigFactory` (parsing case-insensitive, default Generic, `CanonicalName` round-trip) + `App/appsettings.json` sezione `Device:Variant`
- ✅ Step 4 — `Services/Telemetry/TelemetryService` implementa `ITelemetryService` usando `ProtocolService` come facade (decisione architetturale **opzione b**: niente duplicazione encode/chunking/CRC, decode CMD_TELEMETRY_DATA con uint8/16/32 LE, packet a telemetria spenta ignorati)
- ✅ Step 5 — `Services/Boot/BootService` implementa `IBootService` usando `ProtocolService.SendCommandAndWaitReplyAsync` (sequenza START → loop blocchi 1024B con 10 retry → END con 5 retry → RESTART x2; state machine Idle→Uploading→(Completed|Failed); BootProtocolException assorbita per parità legacy)
- ✅ `Core/Interfaces/IBootService.StartFirmwareUploadAsync` — aggiunto parametro `recipientId` (target device)
- ✅ `Services/Protocol/ProtocolService.SenderId` — getter pubblico (usato da TelemetryService per payload CONFIGURE)
- ✅ Test: **+56 test** (25 DeviceVariantConfigFactory + 18 TelemetryService + 13 BootService), tutti cross-platform → suite **228 net10.0** / **373 net10.0-windows**

Rimanente:
- ⏳ **Branch C** `refactor/services-di-integration` — Step 7 (`AddServices()` + `AddProtocolInfrastructure()` + wiring `App/Program.cs`)

### 2.1 Setup progetti Services e Infrastructure.Protocol ✅ Completato

**`Services/Services.csproj`** — target `net10.0` puro (cross-platform), riferimento solo a `Core`. Contiene solo logica pura senza dipendenze da driver hardware o WinForms. I test girano anche in CI Linux.

**`Infrastructure.Protocol/Infrastructure.Protocol.csproj`** — **nuovo progetto**, dual TFM (`net10.0;net10.0-windows10.0.19041.0`), riferimento a `Core`. Contiene gli adapter hardware che dipendono da driver nativi (Peak.PCANBasic.NET, Plugin.BLE, System.IO.Ports). Pattern allineato con `Stem.ButtonPanel.Tester/Infrastructure`.

Creare `Services/DependencyInjection.cs`:
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddSingleton<IPacketDecoder, PacketDecoder>();
        services.AddSingleton<IProtocolService, ProtocolService>();
        services.AddSingleton<ITelemetryService, TelemetryService>();  // dipende da IProtocolService, IDictionaryProvider
        services.AddSingleton<IBootService, BootService>();            // dipende da IProtocolService
        return services;
    }
}
```

`Infrastructure.Protocol/DependencyInjection.cs` esporrà `AddProtocolInfrastructure()` per registrare gli adapter (`CanPort`, `BlePort`, `SerialPort`) come `ICommunicationPort`.

### 2.2 Riscrivere logica da App/STEMProtocol/ (codice nuovo, non wrapper)

I servizi in Services/ sono **riscritture pulite** della logica, non wrapper sui file originali. I file in `App/STEMProtocol/` restano intatti e continuano a essere usati dall'app fino a Fase 3 (rewiring). Dopo Fase 3 non sono più chiamati; eliminati in Fase 4.

| Sorgente (riferimento) | Destinazione (Services/) | Dipendenze ctor |
|------------------------|-------------------------|-----------------|
| `TelemetryManager.cs` (422 LOC) | `Services/Telemetry/TelemetryService.cs` | `IProtocolService`, `IDictionaryProvider`. Usa `SendCommandAsync` per inviare richieste telemetria, si iscrive a `AppLayerDecoded` per decodificare risposte. Nessun riferimento a Form1 o UI. |
| `BootManager.cs` (371 LOC) | `Services/Boot/BootService.cs` | `IProtocolService`. Progress via eventi tipizzati (`BootProgress`), stato via state machine. Nessun callback a Form1. |
| `PacketManager.cs` decode logic | `Services/Protocol/PacketDecoder.cs` ✅ | Implementa `IPacketDecoder`. Solo decode puro, nessun riferimento a canali HW. |
| `STEM_protocol.cs` send/receive | `Services/Protocol/ProtocolService.cs` ✅ | `ICommunicationPort`, `IPacketDecoder`. Facade: encode TP+CRC+chunking+framing, decode+reassembly+event, request/reply con validator. Implementa `IProtocolService`. |

### 2.3 Adattatori hardware ✅ Completato

In `Infrastructure.Protocol/Hardware/` (non in Services — dipendono da driver nativi):
```
CanPort.cs       — implementa ICommunicationPort, wrappa PCANManager via IPcanDriver (option A: arbId LE prefix)
BlePort.cs       — implementa ICommunicationPort, wrappa App.BLEManager via IBleDriver (pass-through)
SerialPort.cs    — implementa ICommunicationPort, wrappa App.SerialPortManager via ISerialDriver (pass-through)
PCANManager.cs   — driver PCAN-USB embedded (spostato da App/)
IPcanDriver.cs, IBleDriver.cs, ISerialDriver.cs — abstraction per testability + dependency inversion
BlePacketEventArgs.cs, SerialPacketEventArgs.cs — event args dei driver
```

`BLE_Manager.cs` e `SerialPort_Manager.cs` restano in `App/` (refs a `Form1.FormRef` e `MessageBox`, da rimuovere in Fase 3). Implementano le interfacce `IBleDriver` / `ISerialDriver` di Infrastructure.Protocol (dependency inversion).

Le tre convention payload sono documentate in [PROTOCOL.md](PROTOCOL.md) e negli XML-doc degli adapter.

### 2.4 DeviceVariantConfig

In `Services/Configuration/`:
```
DeviceVariantConfigFactory.cs — legge la variant da appsettings.json ("Device:Variant": "TopLift"|"Eden"|"Egicon"|"Generic"), produce IDeviceVariantConfig
```

Aggiornare `appsettings.json` con sezione `"Device"`.

### 2.5 Test

Target: ≥90% coverage su Services/.
- `TelemetryServiceTests` — encode payload, decode risposta, UpdateDictionary. Mock di `IProtocolService` + `IDictionaryProvider` (non serve ICommunicationPort — ProtocolService lo nasconde)
- `BootServiceTests` — state machine upload, eventi progress. Mock di `IProtocolService`
- `PacketDecoderTests` — decode da byte[] a AppLayerDecodedEvent (ImmutableArray<byte> equality strutturale)
- `ProtocolServiceTests` ✅ — encode/decode/chunking/reassembly/request-reply (mock di ICommunicationPort)
- `DeviceVariantConfigFactoryTests` — ogni variant produce config corretta
- Mock manuali di `IProtocolService`, `ICommunicationPort`, `IDictionaryProvider` in `Tests/Mocks/`

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
Core/                    [net10.0, zero deps NuGet esterni — System.Collections.Immutable è BCL]
  Interfaces/            ICommunicationPort, IPacketDecoder, IProtocolService, ITelemetryService, IBootService, IDeviceVariantConfig
  Models/                Variable, Command, ProtocolAddress, DictionaryData, ConnectionState, DeviceVariant, AppLayerDecodedEvent (ImmutableArray<byte>), TelemetryDataPoint

Infrastructure.Persistence/ [net10.0]
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
  Unit/Infrastructure/   (Persistence providers)
  Unit/Infrastructure/Protocol/  (HW adapters, Windows-only)
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
