# Refactor Plan — Stem.Device.Manager → Architettura Modulare

**Obiettivo:** Portare il progetto da god-object WinForms a clean architecture disaccoppiata e testabile, allineata ai pattern Stem (Production.Tracker, Communication, ButtonPanel.Tester).

**Stato attuale (2026-04-21):** Core/Infrastructure puliti, Services popolato (ProtocolService/TelemetryService/BootService/DictionaryCache/ConnectionManager). Form1 1125 LOC (post-Branch 3 form1-thin-shell, target finale ~250 LOC post-Phase 4). STEMProtocol/ (~2590 LOC) embedded in App, rimosso in Phase 4. 4 tab WF disaccoppiati via DI. Suite: 270 net10.0 / 448 net10.0-windows.

**Vincoli:** WinForms resta (migrazione WPF è Fase 5 futura). Stem.Communication NuGet sostituirà lo stack protocollo ma non è ancora pronto — preparare l'astrazione.

**Decisioni architetturali (confermate):**
- **IProtocolService in Core, ProtocolService non in DI** — `IProtocolService` è definita in `Core/Interfaces/` (contratto: `SendCommandAsync`, `SendCommandAndWaitReplyAsync`, evento `AppLayerDecoded`). TelemetryService/BootService dipendono dall'interfaccia, non dal concreto. ProtocolService NON è registrato in DI perché dipende dalla port scelta a runtime — creato dal ConnectionManager in Fase 3. L'interfaccia abilita: (1) test unitari con mock di IProtocolService senza tirare su lo stack, (2) swap trasparente in Fase 4 quando Stem.Communication sostituisce lo stack legacy.
- **Riscrittura, non wrapping** — la logica in Services/ è codice nuovo, non wrapper sui vecchi file in `App/STEMProtocol/`. I file legacy restano in App/ senza essere chiamati dopo Fase 3, eliminati in Fase 4.
- **Nessun FormRef nei nuovi servizi** — i servizi nascono senza alcun riferimento a Form1 o UI. Il progresso/stato viene comunicato via eventi tipizzati. Il rewiring UI avviene in Fase 3.
- **ImmutableArray<byte> per payload** — `AppLayerDecodedEvent.Payload` usa `ImmutableArray<byte>` (BCL `System.Collections.Immutable`, incluso nel runtime .NET 8+, non è un NuGet esterno). Fornisce immutabilità vera + equality strutturale elemento per elemento.
- **Services referenzia solo Core** — nessun riferimento a Infrastructure. Il wiring concreto avviene nel composition root (`App/Program.cs`).

---

## Branch Map

```
main
 └─ refactor/protocol-abstractions                 ✅ Fase 1 (merged)
     └─ refactor/services-foundation               ✅ Fase 2 Branch A (PR #24)
         └─ refactor/protocol-service              ✅ Fase 2 Branch A2 (PR #25)
             └─ refactor/services-business         ✅ Fase 2 Branch B (PR #26)
                 └─ refactor/services-di-integration ✅ Fase 2 Branch C (PR #27)
                     └─ refactor/protocol-interface ✅ Fase 3 Branch 0 (PR #28)
                         └─ refactor/phase-3-dictionary-cache ✅ Fase 3 Branch 1 (PR #29)
                             └─ refactor/phase-3-tab-decoupling ✅ Fase 3 Branch 2 (PR #30)
                                 └─ refactor/phase-3-form1-thin-shell ⏳ Fase 3 Branch 3 (in corso)
                                     └─ refactor/phase-3-remove-ifs    ⏳ Fase 3 Branch 4
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
IProtocolService.cs      — facade protocollo: SendCommandAsync, SendCommandAndWaitReplyAsync, evento AppLayerDecoded (non in DI — creato a runtime)
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

## Fase 2 — `refactor/services-layer` ✅ Completata (2026-04-20)

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

Branch `refactor/services-business` (merged → main 2026-04-20, PR #26, **Branch B** completata):
- ✅ Step 3 — `Services/Configuration/DeviceVariantConfigFactory` (parsing case-insensitive, default Generic, `CanonicalName` round-trip) + `App/appsettings.json` sezione `Device:Variant`
- ✅ Step 4 — `Services/Telemetry/TelemetryService` implementa `ITelemetryService` usando `ProtocolService` come facade (decisione architetturale **opzione b**: niente duplicazione encode/chunking/CRC, decode CMD_TELEMETRY_DATA con uint8/16/32 LE, packet a telemetria spenta ignorati)
- ✅ Step 5 — `Services/Boot/BootService` implementa `IBootService` usando `ProtocolService.SendCommandAndWaitReplyAsync` (sequenza START → loop blocchi 1024B con 10 retry → END con 5 retry → RESTART x2; state machine Idle→Uploading→(Completed|Failed); BootProtocolException assorbita per parità legacy)
- ✅ `Core/Interfaces/IBootService.StartFirmwareUploadAsync` — aggiunto parametro `recipientId` (target device)
- ✅ `Services/Protocol/ProtocolService.SenderId` — getter pubblico (usato da TelemetryService per payload CONFIGURE)
- ✅ Test: **+56 test** (25 DeviceVariantConfigFactory + 18 TelemetryService + 13 BootService), tutti cross-platform → suite **228 net10.0** / **373 net10.0-windows**

Branch `refactor/services-di-integration` (merged → main, PR #27, **Branch C** completata — chiude Fase 2):
- ✅ `Core/Interfaces/IDeviceVariantConfig.SenderId` + `Core/Models/DeviceVariantConfig.DefaultSenderId = 8` + nuovo overload `Create(variant, senderId)`
- ✅ `Services/Configuration/DeviceVariantConfigFactory.FromString(variant, senderId)` overload per host DI
- ✅ `Services/DependencyInjection.AddServices(IConfiguration)` — registra `IDeviceVariantConfig` (da `Device:Variant`+`Device:SenderId`) + `IPacketDecoder` vuoto
- ✅ `Infrastructure.Protocol/DependencyInjection.AddProtocolInfrastructure()` — registra `PCANManager` come `IPcanDriver` + `CanPort`/`BlePort`/`SerialPort` come singleton concreti (scelta canale runtime gestita in Phase 3)
- ✅ NuGet `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.5` aggiunto a Services + Infrastructure.Protocol; `Configuration.Abstractions` a Services
- ✅ `App/appsettings.json` sezione `Device.SenderId = 8`
- ✅ `App/Program.cs` wiring DI completo: `AddDictionaryProvider` + driver `IBleDriver`/`ISerialDriver` + `AddProtocolInfrastructure()` + `AddServices(config)`. Form1 invariato (consumer migration = Phase 3)
- ✅ **Servizi NON registrati per scelta architetturale** (dubbio 1 opzione c): `ProtocolService`/`ITelemetryService`/`IBootService` dipendono dalla port runtime, creati dal `ConnectionManager` Phase 3. `IProtocolService` da introdurre in Core in apertura Fase 3 (refactor leggero: estrarre interfaccia da ProtocolService, cambiare ctor di TelemetryService/BootService per dipendere dall'interfaccia)
- ✅ `Docs/PREPROCESSOR_DIRECTIVES.md` — documentato debito Phase 3: `BLE_Manager.FormRef` da rimpiazzare con evento o `ILogger`
- ✅ Test: **+8 test** (6 DeviceVariantConfig SenderId + 2 DeviceVariantConfigFactory) → suite **236 net10.0** / **381 net10.0-windows**
- ✅ Integration test cross-platform per `AddServices()` + `AddProtocolInfrastructure()` chiusi nel branch `refactor/protocol-interface` (vedi sotto)

Branch `refactor/protocol-interface` (in corso, **prerequisite di Fase 3**):
- ✅ `Core/Interfaces/IProtocolService` — estratto contratto del facade (`SenderId`, `AppLayerDecoded`, `SendCommandAsync`, `SendCommandAndWaitReplyAsync`, `IDisposable`)
- ✅ `Services/Protocol/ProtocolService` implementa `IProtocolService` (signature pubblica invariata)
- ✅ `TelemetryService` e `BootService` ctor cambiati: dipendono da `IProtocolService` invece del concreto `ProtocolService`
- ✅ Suite test esistente (Telemetry/Boot) verde **senza modifiche** ai test (real ProtocolService passato come IProtocolService via implicit conversion)
- ✅ `Tests/Unit/Services/DependencyInjection/AddServicesTests.cs` — 11 test cross-platform (girano in CI Linux): smoke resolve `IDeviceVariantConfig`/`IPacketDecoder`, override `Device:Variant`/`Device:SenderId`, fallback su SenderId invalido, singleton lifetime
- ✅ `Tests/Unit/Infrastructure/Protocol/AddProtocolInfrastructureTests.cs` — 8 test Windows-only: `ServiceDescriptor` per `IPcanDriver`/`CanPort`/`BlePort`/`SerialPort`, resolve di `BlePort`/`SerialPort` con fake driver registrati esternamente, conferma del contratto "host registra `IBleDriver`/`ISerialDriver` prima"
- ✅ Test: **+19 test** (11 cross-platform + 8 Windows-only) → suite **247 net10.0** / **400 net10.0-windows**

### 2.1 Setup progetti Services e Infrastructure.Protocol ✅ Completato

**`Services/Services.csproj`** — target `net10.0` puro (cross-platform), riferimento solo a `Core`. Contiene solo logica pura senza dipendenze da driver hardware o WinForms. I test girano anche in CI Linux.

**`Infrastructure.Protocol/Infrastructure.Protocol.csproj`** — **nuovo progetto**, dual TFM (`net10.0;net10.0-windows10.0.19041.0`), riferimento a `Core`. Contiene gli adapter hardware che dipendono da driver nativi (Peak.PCANBasic.NET, Plugin.BLE, System.IO.Ports). Pattern allineato con `Stem.ButtonPanel.Tester/Infrastructure`.

Creare `Services/DependencyInjection.cs`:
```csharp
public static class DependencyInjection
{
    public static IServiceCollection AddServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddSingleton<IDeviceVariantConfig>(_ => DeviceVariantConfigFactory.FromConfiguration(config));
        services.AddSingleton<IPacketDecoder, PacketDecoder>();  // registrato vuoto, UpdateDictionary dopo load async
        // ProtocolService, ITelemetryService, IBootService NON registrati:
        // dipendono dalla port scelta a runtime → creati dal ConnectionManager (Fase 3)
        return services;
    }
}
```

`Infrastructure.Protocol/DependencyInjection.cs` esporrà `AddProtocolInfrastructure()` per registrare gli adapter (`CanPort`, `BlePort`, `SerialPort`) come `ICommunicationPort`.

### 2.2 Riscrivere logica da App/STEMProtocol/ (codice nuovo, non wrapper)

I servizi in Services/ sono **riscritture pulite** della logica, non wrapper sui file originali. I file in `App/STEMProtocol/` restano intatti e continuano a essere usati dall'app fino a Fase 3 (rewiring). Dopo Fase 3 non sono più chiamati; eliminati in Fase 4.

| Sorgente (riferimento) | Destinazione (Services/) | Dipendenze ctor |
|------------------------|-------------------------|-----------------|
| `TelemetryManager.cs` (422 LOC) | `Services/Telemetry/TelemetryService.cs` ✅ | `IProtocolService`, `IDictionaryProvider`. Usa `SendCommandAsync` per inviare richieste telemetria, si iscrive a `AppLayerDecoded` per decodificare risposte. Nessun riferimento a Form1 o UI. **Nota: attualmente dipende dal concreto — da migrare a IProtocolService.** |
| `BootManager.cs` (371 LOC) | `Services/Boot/BootService.cs` ✅ | `IProtocolService`. Progress via eventi tipizzati (`BootProgress`), stato via state machine. Nessun callback a Form1. **Nota: attualmente dipende dal concreto — da migrare a IProtocolService.** |
| `PacketManager.cs` decode logic | `Services/Protocol/PacketDecoder.cs` ✅ | Implementa `IPacketDecoder`. Solo decode puro, nessun riferimento a canali HW. |
| `STEM_protocol.cs` send/receive | `Services/Protocol/ProtocolService.cs` ✅ | `ICommunicationPort`, `IPacketDecoder`, `uint senderId`. Implementa `IProtocolService`. Non registrato in DI — creato a runtime dal ConnectionManager (Fase 3). |

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
- `TelemetryServiceTests` ✅ — encode payload, decode risposta, UpdateDictionary. Usa `FakeCommunicationPort` per simulare ProtocolService (non serve mock separato — ProtocolService è concreto)
- `BootServiceTests` ✅ — state machine upload (START → blocchi → END → RESTART), eventi progress, retry logic. Usa `FakeCommunicationPort`
- `PacketDecoderTests` ✅ — decode da byte[] a AppLayerDecodedEvent (ImmutableArray<byte> equality strutturale)
- `ProtocolServiceTests` ✅ — encode/decode/chunking/reassembly/request-reply (mock di ICommunicationPort)
- `DeviceVariantConfigFactoryTests` ✅ — ogni variant produce config corretta, SenderId override, round-trip
- Mock manuali: `FakeCommunicationPort` (con OnSent hook), `ICommunicationPort` mock, `IDictionaryProvider` mock in `Tests/`

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

### 3.0 Introdurre IProtocolService ✅ Completata (branch `refactor/protocol-interface`, PR #28, 2026-04-20)

`IProtocolService` estratto in `Core/Interfaces/`, `ProtocolService` lo implementa, `TelemetryService`/`BootService` ctor cambiati a `IProtocolService`. Suite test verde senza modifiche grazie a implicit conversion.

### 3.0bis Branch 1 `refactor/phase-3-dictionary-cache` ✅ Completata (PR #29, 2026-04-21)

Estrai `DictionaryCache` (cache centralizzata commands+addresses+variables, `LoadAsync`/`SelectByRecipientAsync`/`SelectByDeviceBoardAsync`, evento `DictionaryUpdated`, sincronizzazione automatica con `IPacketDecoder`) e `ConnectionManager` (factory `IProtocolService` runtime, gestisce `SwitchToAsync` con dispose+disconnect+connect, eventi `ActiveChannelChanged`/`StateChanged`) in `Services/Cache/`. Aggiunto `ChannelKind DefaultChannel` a `IDeviceVariantConfig` (TOPLIFT→Can, altre→Ble). Promosso `UpdateDictionary` al contratto `IPacketDecoder`. DI: `AddServices` registra entrambi i nuovi servizi; `AddProtocolInfrastructure` espone i 3 port anche come `IEnumerable<ICommunicationPort>`. Test: 47 nuovi (32 cross-platform + 18 Windows-only ConnectionManager + 5 wiring DI). Suite **268 net10.0** / **441 net10.0-windows**.

### 3.0ter Branch 2 `refactor/phase-3-tab-decoupling` ✅ Completata (PR #30, 2026-04-21)

Disaccoppia i 4 tab WF dalla dipendenza statica `Form1.FormRef`: ctor injection di `DictionaryCache`, rimozione dei metodi pubblici `UpdateDictionary(IReadOnlyList<Variable>)` in favore di sottoscrizione all'evento `DictionaryUpdated`. Tab refattorizzati: `Boot_Interface_Tab` (legge `_cache.CurrentRecipientId` invece di `Form1.FormRef.RecipientId`), `Boot_Smart_Tab` (multi-board loop line 300 chiama `_cache.SetCurrentRecipientId(X)` — nuova API `DictionaryCache` — parallelamente a `Form1.FormRef.RecipientId = X` per parità legacy finché `STEM_protocol.cs` lo legge), `Telemetry_Tab` + `TopLiftTelemetry_Tab` (sub `DictionaryUpdated`). `BLE_WF_Tab`/`CAN_WF_Tab` esclusi dallo scope (nessun consumer dizionario). `Form1.LoadDictionaryDataAsync` ora usa `_dictionaryCache.LoadAsync`/`SelectByRecipientAsync` direttamente (una sola chiamata HTTP, notifica tab via evento); rimosse tutte le 5 chiamate `*.UpdateDictionary(Dizionario)`. `TelemetryManager`/`BootManager` restano invariati dentro i tab (rimossi in Phase 4 con lo stack `STEMProtocol/`). Test: 5 smoke test Windows-only (null-ctor + propagation). Suite **270 net10.0** / **448 net10.0-windows**.

### Riepilogo completati Fase 3

| Responsabilità | Dove | Stato |
|----------------|------|-------|
| Dictionary data ownership (3 liste) | `DictionaryCache` in Services/Cache/ | ✅ Branch 1 |
| RecipientId + board selection | `DictionaryCache.SelectByRecipientAsync/SelectByDeviceBoardAsync` | ✅ Branch 1 |
| Connection status + channel switching | `ConnectionManager` in Services/Cache/ | ✅ Branch 1 |
| ProtocolService factory (runtime) | `ConnectionManager.SwitchToAsync` → crea `IProtocolService` | ✅ Branch 1 |
| IProtocolService interfaccia | `Core/Interfaces/IProtocolService.cs` | ✅ Branch 0 |
| Tab DI (Boot, Boot_Smart, Telemetry, TopLift_Telemetry) | ctor `DictionaryCache`, sub `DictionaryUpdated` | ✅ Branch 2 |
| Form1.LoadDictionaryDataAsync → DictionaryCache | Rimossa propagazione manuale UpdateDictionary | ✅ Branch 2 |
| DictionaryCacheTests (14) + ConnectionManagerTests (18) + tab smoke (5) | Tests/ | ✅ Branch 1+2 |

### 3.1 Branch 3 `refactor/phase-3-form1-thin-shell` ⏳ In corso

**Obiettivo:** Eliminare `Form1.FormRef` dal codice nuovo (tab + Form1 + BLE_Manager) senza toccare il legacy `STEMProtocol/`. Riduzione progressiva di Form1.cs (LOC reduction reale dopo Phase 4).

**Scope:** il target "~250 LOC thin shell" è **differito a post-Phase 4**: molto di Form1.cs è UI glue legacy (`SendPS_Async` con `PacketManager`, handler `onAppLayer*` con `NetworkLayer`) che verrà eliminato quando lo stack `STEMProtocol/` sarà rimosso. In Branch 3 ci si limita a rimuovere le self-reference ridondanti e disaccoppiare BLE_Manager.

#### 3.1.1 Eliminazioni FormRef

| Sorgente | Azione | Commit |
|----------|--------|--------|
| `BLE_Manager.cs` l.242 `Form1.FormRef.UpdateTerminal(...)` | Aggiungere evento `Action<string> LogMessageEmitted`, Form1 lo sottoscrive dopo creazione di `BLETabRef.bleManager` | 1 |
| `Form1.cs` — 23 self-reference `Form1.FormRef.X` (l.573-991) | Mechanical replace `Form1.FormRef.` → `this.` | 2 |

**Lascia invariato**: `public static Form1 FormRef { get; private set; }` + `FormRef = this;` (usati dal legacy `STEMProtocol/` — rimossi in Phase 4).

#### 3.1.2 Unificazione handler connection status

I 6 metodi `On[PCAN|Serial|BLE]ConnectionStatusChanged` + `Update[PCAN|Serial|BLE]ConnectionStatus` sono quasi identici (pattern `InvokeRequired` + `Text`/`BackColor`). Consolidati in:
- 1 helper `UpdateConnectionStatus(ToolStripStatusLabel, bool, string connectedText, string disconnectedText)` thread-safe
- 3 one-liner che invocano l'helper

Non tocca `ConnectionManager.StateChanged` (che emette solo per il canale attivo — pattern inadatto alle 3 label UI indipendenti).

#### 3.1.3 Esiti (al 2026-04-21)

- Form1.cs: **1184 → 1125 LOC** (-59)
- `Form1.FormRef` residui in codice nuovo: **0** (solo static property + `FormRef = this` per legacy)
- Suite: **270 net10.0 / 448 net10.0-windows** (zero regressioni, no test nuovi — refactor mechanical)

#### 3.1.4 Deferred / esclusi

- `SendPS_Async`, handler `onAppLayer*` (legacy `PacketManager`/`NetworkLayer`): Phase 4
- `BLE_WF_Tab` / `CAN_WF_Tab` ctor DI: out of scope (nessun consumer dizionario)
- `STEM_protocol.cs` / `PacketManager.cs` / `BootManager.cs` `FormRef` (32 refs): legacy, Phase 4
- `Boot_Smart_WF_Tab` line 307 parità legacy `Form1.FormRef.RecipientId = X`: resta finché `STEM_protocol.cs` la legge

### 3.2 Branch 4 `refactor/phase-3-remove-ifs` ⏳ Non iniziato

**Obiettivo:** Sostituire i blocchi `#if TOPLIFT/EDEN/EGICON` con runtime check su `IDeviceVariantConfig` e rimuovere le build configuration device-specific.

#### 3.2.1 Sostituzione `#if` → runtime variant

14 occorrenze in 7 file (snapshot 2026-04-20): Form1.cs (8), SplashScreen (1), 2 tab (2), Core docs (3). Sostituire con:
```csharp
if (_variantConfig.Variant == DeviceVariant.TopLift) { ... }
```

#### 3.2.2 Feature flag mancanti

Estendere `IDeviceVariantConfig` con i flag necessari (vedi `Docs/PREPROCESSOR_DIRECTIVES.md` proposte 1-8). Finalizzare naming.

#### 3.2.3 Build configuration cleanup

Rimuovere le 6 configurazioni device-specific da `App.csproj` (TOPLIFT-A2-Debug/Release, EDEN-Debug/Release, EGICON-Debug/Release). Restano solo `Debug` e `Release`. La variant viene da `appsettings.json` (`Device:Variant`).

#### 3.2.4 Aggiornare documentazione

- `Docs/PREPROCESSOR_DIRECTIVES.md`: marcare come rimossi i blocchi convertiti
- `CLAUDE.md`: aggiornare la riga sulle 8 build configuration

#### 3.2.5 Test

- Verifica che la build con solo `Debug`/`Release` continui a produrre gli eseguibili attesi
- Eventuali test su `IDeviceVariantConfig` nuovi flag

### 3.3 Comandi

```bash
# Branch 3 — form1 thin shell (in corso)
git checkout -b refactor/phase-3-form1-thin-shell
# Commit 1: BLE_Manager event LogMessageEmitted
# Commit 2: Form1 internal self-reference cleanup (Form1.FormRef.X -> this.X)
# Commit 3: unificare handler connection status in helper
# Verificare: dotnet build Stem.Device.Manager.slnx
# Verificare: dotnet test Tests/Tests.csproj

# Branch 4 — remove-ifs
git checkout -b refactor/phase-3-remove-ifs
# Estendere IDeviceVariantConfig con feature flag
# Sostituire #if TOPLIFT/EDEN/EGICON con runtime check (un commit per file)
# Rimuovere build configuration device-specific da App.csproj
# Aggiornare Docs/PREPROCESSOR_DIRECTIVES.md e CLAUDE.md
# Commit finale: "refactor(app): remove #if TOPLIFT/EDEN/EGICON, single Debug/Release config"
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

## Struttura finale target

```
Core/                    [net10.0, zero deps NuGet esterni — System.Collections.Immutable è BCL]
  Interfaces/            ICommunicationPort, IPacketDecoder, IProtocolService, ITelemetryService, IBootService, IDeviceVariantConfig, IDictionaryProvider
  Models/                Variable, Command, ProtocolAddress, DictionaryData, ConnectionState, DeviceVariant, ChannelKind, AppLayerDecodedEvent (ImmutableArray<byte>), TelemetryDataPoint, RawPacket, BootState, BootProgress

Infrastructure.Persistence/ [net10.0]
  Api/, Excel/           DictionaryApiProvider, ExcelDictionaryProvider, FallbackDictionaryProvider
  DependencyInjection.cs

Infrastructure.Protocol/ [net10.0;net10.0-windows]
  Hardware/              CanPort, BlePort, SerialPort, PCANManager (wrappano driver nativi via IPcanDriver/IBleDriver/ISerialDriver)
  DependencyInjection.cs

Services/                [net10.0, pure logic]
  Telemetry/             TelemetryService
  Boot/                  BootService
  Protocol/              ProtocolService (implementa IProtocolService, non in DI), PacketDecoder, PacketReassembler, DictionarySnapshot
  Configuration/         DeviceVariantConfigFactory
  Cache/                 DictionaryCache (Fase 3), ConnectionManager (Fase 3)
  DependencyInjection.cs

App/                     [net10.0-windows, WinForms]
  Form1.cs               ~250 LOC thin shell (dopo Fase 3)
  *_WF_Tab.cs            Tab autonome con DI (dopo Fase 3)
  BLE_Manager.cs         Implementa IBleDriver (FormRef debt da risolvere in Fase 3)
  SerialPort_Manager.cs  Implementa ISerialDriver
  Program.cs             Composition root DI
  appsettings.json

Tests/                   [dual TFM: net10.0 + net10.0-windows]
  Unit/Core/Models/
  Unit/Infrastructure/Persistence/
  Unit/Infrastructure/Protocol/  (HW adapters, Windows-only)
  Unit/Services/Protocol/
  Unit/Services/Telemetry/
  Unit/Services/Boot/
  Unit/Services/Configuration/
  Integration/
  Mocks/                 FakeCommunicationPort, mock IDictionaryProvider
```

---

## Checklist pre-merge per ogni fase

1. `dotnet build Stem.Device.Manager.slnx` — zero warning
2. `dotnet test Tests/Tests.csproj` — tutti i test passano
3. `dotnet test Tests/Tests.csproj --framework net10.0` — cross-platform OK
4. Nessun `using` circolare tra layer
5. Core non referenzia Infrastructure, Services, o App
6. Services non referenzia Infrastructure o App
7. PR description elenca LOC prima/dopo per Form1 (Fase 3)
8. CLAUDE.md aggiornato con nuovi componenti

---

## Note per l'esecuzione

- **Un commit per ogni componente estratto** — facilita review e bisect
- **Non rompere mai la build** — ogni commit compila e i test passano
- **Mock manuali** — niente Moq/NSubstitute, come da convenzione progetto
- **Naming:** classi/metodi in inglese, commenti/doc in italiano
- **CancellationToken** su ogni metodo async
- **Nullable types** abilitati, mai restituire null per errori
- **Funzioni < 15 LOC**, early returns
- **ProtocolService non in DI** — creato a runtime quando l'utente sceglie il canale (ConnectionManager, Fase 3)