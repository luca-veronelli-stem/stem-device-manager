# Refactor Plan — Stem.Device.Manager → Architettura Modulare

**Obiettivo:** Portare il progetto da god-object WinForms a clean architecture disaccoppiata e testabile, allineata ai pattern Stem (Production.Tracker, Communication, ButtonPanel.Tester).

**Stato attuale (2026-04-21, post-Phase 4):** Core/Infrastructure puliti, Services popolato (ProtocolService/TelemetryService/BootService/DictionaryCache/ConnectionManager). Form1 731 LOC (-35% rispetto a inizio Phase 4). `GUI.Windows/STEMProtocol/` (2590 LOC) **eliminato**. Driver legacy `BLEManager`/`SerialPortManager` spostati in `Infrastructure.Protocol/Legacy/` (sostituibili quando arriva Stem.Communication NuGet). 4 tab WF migrati su `ConnectionManager.CurrentBoot`/`CurrentTelemetry`. Tutti i FormRef legacy eliminati. 2 build configuration (Debug/Release). Suite: **292 net10.0 / 470 net10.0-windows**.

**Vincoli:** WinForms resta (migrazione UI = Fase 5 futura). Stem.Communication NuGet sostituirà i driver in `Legacy/` — Fase 5 futura, non blocca la chiusura di Phase 4.

**Decisioni architetturali (confermate):**
- **IProtocolService in Core, ProtocolService non in DI** — `IProtocolService` è definita in `Core/Interfaces/` (contratto: `SendCommandAsync`, `SendCommandAndWaitReplyAsync`, evento `AppLayerDecoded`). TelemetryService/BootService dipendono dall'interfaccia, non dal concreto. ProtocolService NON è registrato in DI perché dipende dalla port scelta a runtime — creato dal ConnectionManager in Fase 3. L'interfaccia abilita: (1) test unitari con mock di IProtocolService senza tirare su lo stack, (2) swap trasparente in Fase 4 quando Stem.Communication sostituisce lo stack legacy.
- **Riscrittura, non wrapping** — la logica in Services/ è codice nuovo, non wrapper sui vecchi file in `GUI.Windows/STEMProtocol/`. I file legacy restano in GUI.Windows/ senza essere chiamati dopo Fase 3, eliminati in Fase 4.
- **Nessun FormRef nei nuovi servizi** — i servizi nascono senza alcun riferimento a Form1 o UI. Il progresso/stato viene comunicato via eventi tipizzati. Il rewiring UI avviene in Fase 3.
- **ImmutableArray<byte> per payload** — `AppLayerDecodedEvent.Payload` usa `ImmutableArray<byte>` (BCL `System.Collections.Immutable`, incluso nel runtime .NET 8+, non è un NuGet esterno). Fornisce immutabilità vera + equality strutturale elemento per elemento.
- **Services referenzia solo Core** — nessun riferimento a Infrastructure. Il wiring concreto avviene nel composition root (`GUI.Windows/Program.cs`).

---

## Branch Map

```
main
 └─ refactor/protocol-abstractions                    ✅ Fase 1 (merged)
     └─ refactor/services-foundation                  ✅ Fase 2 Branch A (PR #24)
         └─ refactor/protocol-service                 ✅ Fase 2 Branch A2 (PR #25)
             └─ refactor/services-business            ✅ Fase 2 Branch B (PR #26)
                 └─ refactor/services-di-integration  ✅ Fase 2 Branch C (PR #27)
                     └─ refactor/protocol-interface   ✅ Fase 3 Branch 0 (PR #28)
                         └─ refactor/dictionary-cache ✅ Fase 3 Branch 1 (PR #29)
                             └─ refactor/tab-decoupling ✅ Fase 3 Branch 2 (PR #30)
                                 └─ refactor/form1-thin-shell ✅ Fase 3 Branch 3 (PR #31)
                                     └─ refactor/remove-ifs     ✅ Fase 3 Branch 4 (PR #32)
                                         └─ refactor/phase-4-switch-to-new-stack ✅ Fase 4 (in review)
                                             └─ refactor/phase-4b-app-reorganization ⏳ Fase 4b
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

**Obiettivo:** Popolare Services/ con implementazioni concrete + introdurre Infrastructure.Protocol per gli adapter HW. Spostare logica da GUI.Windows/ a Services/ / Infrastructure.Protocol/.

**Progresso al 2026-04-20:**

Branch `refactor/services-foundation` (merged → main, PR #24):
- ✅ Step 1 — Setup struttura progetti: Services `net10.0` puro + nuovo `Infrastructure.Protocol` dual TFM + rinomina `Infrastructure` → `Infrastructure.Persistence` (allineamento pattern Stem)
- ✅ Step 1 — `PacketDecoder` + `DictionarySnapshot` in `Services/Protocol/`
- ✅ Step 2 — Adapter HW `CanPort` (option A arbId LE prefix), `BlePort` e `SerialPort` (pass-through) in `Infrastructure.Protocol/Hardware/`
- ✅ `PCANManager` spostato da `GUI.Windows/` a `Infrastructure.Protocol/Hardware/` (driver autonomo)
- ✅ `Docs/PROTOCOL.md` — documentazione completa del protocollo STEM legacy

Branch `refactor/protocol-service` (merged → main 2026-04-20, PR #25, **Branch A** completata):
- ✅ `NetInfo` struct + `PacketReassembler` thread-safe (Services/Protocol/)
- ✅ `ChannelKind` enum in Core.Models + proprietà `Kind` su `ICommunicationPort`
- ✅ Step 6 — `ProtocolService` facade (encode TP + CRC16 + chunking + framing per canale; decode + reassembly + event; pattern request/reply con validator custom)
- ✅ Test: **317 net10.0-windows** (era 274) / **172 net10.0** (era 132) — +43 test (13 NetInfo + 13 PacketReassembler + 13 ProtocolService + 3 Kind adapter + 1 refactor PacketDecoder)

Branch `refactor/services-business` (merged → main 2026-04-20, PR #26, **Branch B** completata):
- ✅ Step 3 — `Services/Configuration/DeviceVariantConfigFactory` (parsing case-insensitive, default Generic, `CanonicalName` round-trip) + `GUI.Windows/appsettings.json` sezione `Device:Variant`
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
- ✅ `GUI.Windows/appsettings.json` sezione `Device.SenderId = 8`
- ✅ `GUI.Windows/Program.cs` wiring DI completo: `AddDictionaryProvider` + driver `IBleDriver`/`ISerialDriver` + `AddProtocolInfrastructure()` + `AddServices(config)`. Form1 invariato (consumer migration = Phase 3)
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

### 2.2 Riscrivere logica da GUI.Windows/STEMProtocol/ (codice nuovo, non wrapper)

I servizi in Services/ sono **riscritture pulite** della logica, non wrapper sui file originali. I file in `GUI.Windows/STEMProtocol/` restano intatti e continuano a essere usati dall'app fino a Fase 3 (rewiring). Dopo Fase 3 non sono più chiamati; eliminati in Fase 4.

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
PCANManager.cs   — driver PCAN-USB embedded (spostato da GUI.Windows/)
IPcanDriver.cs, IBleDriver.cs, ISerialDriver.cs — abstraction per testability + dependency inversion
BlePacketEventArgs.cs, SerialPacketEventArgs.cs — event args dei driver
```

`BLE_Manager.cs` e `SerialPort_Manager.cs` restano in `GUI.Windows/` (refs a `Form1.FormRef` e `MessageBox`, da rimuovere in Fase 3). Implementano le interfacce `IBleDriver` / `ISerialDriver` di Infrastructure.Protocol (dependency inversion).

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

### 3.0bis Branch 1 `refactor/dictionary-cache` ✅ Completata (PR #29, 2026-04-21)

Estrai `DictionaryCache` (cache centralizzata commands+addresses+variables, `LoadAsync`/`SelectByRecipientAsync`/`SelectByDeviceBoardAsync`, evento `DictionaryUpdated`, sincronizzazione automatica con `IPacketDecoder`) e `ConnectionManager` (factory `IProtocolService` runtime, gestisce `SwitchToAsync` con dispose+disconnect+connect, eventi `ActiveChannelChanged`/`StateChanged`) in `Services/Cache/`. Aggiunto `ChannelKind DefaultChannel` a `IDeviceVariantConfig` (TOPLIFT→Can, altre→Ble). Promosso `UpdateDictionary` al contratto `IPacketDecoder`. DI: `AddServices` registra entrambi i nuovi servizi; `AddProtocolInfrastructure` espone i 3 port anche come `IEnumerable<ICommunicationPort>`. Test: 47 nuovi (32 cross-platform + 18 Windows-only ConnectionManager + 5 wiring DI). Suite **268 net10.0** / **441 net10.0-windows**.

### 3.0ter Branch 2 `refactor/tab-decoupling` ✅ Completata (PR #30, 2026-04-21)

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

### 3.1 Branch 3 `refactor/form1-thin-shell` ✅ Completata (PR #31, 2026-04-21)

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

### 3.2 Branch 4 `refactor/remove-ifs` ⏳ In corso (2026-04-21)

**Obiettivo:** Sostituire i blocchi `#if TOPLIFT/EDEN/EGICON` con runtime check su `IDeviceVariantConfig` e rimuovere le build configuration device-specific.

**Strategia pragmatica:** switch su `_variantConfig.Variant` direttamente, con commenti che spiegano il perché di ogni ramo (NO_OVERENGINEERING — niente inflazione di flag booleani). Flag dedicati solo per dati non derivabili dalla variant: `WindowTitle` + `SmartBootDevices`.

#### 3.2.1 Esiti (al 2026-04-21)

- **11 blocchi `#if` rimossi** su 4 file (Form1.cs 8, SplashScreen 1, Boot_WF_Tab 1, Telemetry_WF_Tab 1) — zero `#if TOPLIFT/EDEN/EGICON` attivi nel codice
- **2 flag nuovi in `IDeviceVariantConfig`**: `WindowTitle: string`, `SmartBootDevices: IReadOnlyList<SmartBootDeviceEntry>`
- **1 tipo nuovo in Core**: `SmartBootDeviceEntry(uint Address, string Name, bool IsKeyboard)`
- **6 build configuration rimosse** da `GUI.Windows.csproj` e `Stem.Device.Manager.slnx` (TOPLIFT-A2-Debug/Release, EDEN-Debug/Release, EGICON-Debug/Release). Restano solo `Debug` e `Release`.
- **Iniezione DI**: `IDeviceVariantConfig` iniettato in Form1 (`GetRequiredService`), SplashScreen (ctor), Boot_Interface_Tab (ctor), Telemetry_Tab (ctor)
- **Test**: +8 (`DeviceVariantConfigTests` esteso: WindowTitle per 4 variant + SmartBootDevices TopLift/Eden/Generic/Egicon) + 2 (tab null-ctor variant) → suite **278 net10.0 / 458 net10.0-windows**
- **Docs aggiornati**: `Docs/PREPROCESSOR_DIRECTIVES.md` riscritto con mappa blocchi → sostituzioni, `CLAUDE.md` aggiornato (2 build config + test counts)

#### 3.2.2 Deferred / esclusi

- `Core/**` e `README.md` refs a `#if` (3 occorrenze): solo testo esplicativo nei commenti XML, non direttive di preprocessore attive — lasciati come documentazione storica
- Piping CI `bitbucket-pipelines.yml`: non aveva riferimenti device-specific (build solo `Release` cross-platform)
- Run check manuale (4 varianti × 2 build): richiede hardware device, fuori scope automatico; UI preserva 1:1 la logica legacy per ogni variant.

### 3.3 Comandi

```bash
# Branch 3 — form1 thin shell (in corso)
git checkout -b refactor/form1-thin-shell
# Commit 1: BLE_Manager event LogMessageEmitted
# Commit 2: Form1 internal self-reference cleanup (Form1.FormRef.X -> this.X)
# Commit 3: unificare handler connection status in helper
# Verificare: dotnet build Stem.Device.Manager.slnx
# Verificare: dotnet test Tests/Tests.csproj

# Branch 4 — remove-ifs
git checkout -b refactor/remove-ifs
# Estendere IDeviceVariantConfig con feature flag
# Sostituire #if TOPLIFT/EDEN/EGICON con runtime check (un commit per file)
# Rimuovere build configuration device-specific da GUI.Windows.csproj
# Aggiornare Docs/PREPROCESSOR_DIRECTIVES.md e CLAUDE.md
# Commit finale: "refactor(app): remove #if TOPLIFT/EDEN/EGICON, single Debug/Release config"
```

---

## Fase 4 — `refactor/phase-4-switch-to-new-stack` ✅ Completata (2026-04-21)

**Obiettivo:** Spostare Form1 + tab dai manager legacy (PacketManager, BootManager, TelemetryManager) al nuovo stack (IProtocolService + IBootService + ITelemetryService via ConnectionManager), ed eliminare lo stack protocollo embedded `GUI.Windows/STEMProtocol/` senza attendere Stem.Communication NuGet.

**Strategia pragmatica (decisione 2026-04-21):** invece di attendere Stem.Communication (almeno 1 mese di distanza), chiudiamo la Fase 4 usando lo stack nuovo già costruito in Fase 2. Quando Stem.Communication sarà pronto, la sua integrazione sarà uno swap locale dei wrapper `Infrastructure.Protocol/Legacy/*` — il resto dell'architettura resta invariato.

### 4.1 Estensione servizi di dominio

Il nuovo `TelemetryService` inizialmente copriva solo fast telemetry; esteso con:
- `ReadOneShotAsync`/`WriteOneShotAsync` — loop round-robin CMD_READ_VARIABLE/CMD_WRITE_VARIABLE
- Dizionario mutabile: `AddToDictionary`, `AddToDictionaryForWrite`, `RemoveFromDictionary`, `ResetDictionary`, `GetVariableName`
- Decodifica CMD_READ_VARIABLE reply (cmdHi=0x80, cmdLo=0x01) in big-endian

Nuovo `BootService` esteso con step separati per workflow Egicon:
- `StartBootAsync`/`EndBootAsync`/`RestartAsync` (single-shot)
- `UploadBlocksOnlyAsync` (solo blocchi, senza START/END/RESTART)

`TelemetryDataPoint` esteso con `TelemetrySource` (FastStream/ReadReply) + helper `NumericValue` che sceglie endianness corretta.

### 4.2 ConnectionManager come coordinatore runtime

`ConnectionManager` già esposto in Fase 3 è stato esteso per esporre i servizi di dominio:
- `CurrentBoot: IBootService?` + `CurrentTelemetry: ITelemetryService?` — ricreati ad ogni `SwitchToAsync` insieme al protocol.
- Forward events `AppLayerDecoded`, `TelemetryDataReceived`, `BootProgressChanged` — i consumer (Form1, tab) si iscrivono una volta sola al manager, il re-binding ai servizi nuovi è interno.

### 4.3 Migrazione Form1 e tab

- **Form1.SendPS_Async** (133 LOC legacy) → chiama `_connMgr.ActiveProtocol.SendCommandAsync(recipientId, command, payload)`. Rimossi `_CDL`/`_BLE_SDL`/`_SDL`/`RXpacketManager`.
- **Handler RX** (`onAppLayerPacketReady`/`onAppLayerDecoded`, ~385 LOC) → sottoscrizione unica a `ConnectionManager.AppLayerDecoded`, display semplificato (`DisplayDecodedPacket`).
- **Menu canale** (CAN/BLE/Serial + baudrate) → chiama `SwitchChannelAsync(ChannelKind)`. Baudrate PCAN runtime deferito (ora fix 250 kbps in `CanPort`).
- **Connection status** (3 handler separati) → unico `OnConnectionStateChanged` che riceve `ConnectionStateSnapshot` e aggiorna la label corrispondente.
- **Tab** (Boot_Interface, Boot_Smart, Telemetry, TopLiftTelemetry) → ctor injection `DictionaryCache` + `ConnectionManager` + (eventuale) `IDeviceVariantConfig`. Usano `_connMgr.CurrentBoot`/`CurrentTelemetry` con null-check ("Select communication channel first!").
- **FormRef static** (`public static Form1 FormRef`) eliminato. Zero FormRef residui.

### 4.4 Eliminazione legacy

- `GUI.Windows/STEMProtocol/` eliminata (7 file, 2590 LOC: STEM_protocol.cs, PacketManager.cs, CanDataLayer.cs, SerialDataLayer.cs, BootManager.cs, TelemetryManager.cs, SPRollingCode.cs).
- `GUI.Windows/CAN_WF_Tab.cs` eliminata (dead code, mai istanziata).
- `Tests/Unit/Protocol/TelemetryManagerTests.cs` e `Tests/Unit/Protocol/RollingCodeGeneratorTests.cs` eliminati (coprivano il legacy).
- `Tests/Unit/Tabs/TabDependencyInjectionTests.cs` aggiornato con i nuovi ctor.

### 4.5 Driver legacy in Infrastructure.Protocol/Legacy/

`BLE_Manager.cs` e `SerialPort_Manager.cs` spostati da `GUI.Windows/` a `Infrastructure.Protocol/Legacy/`:
- Namespace `Infrastructure.Protocol.Legacy`.
- Dipendenze WinForms eliminate: `MessageBox.Show` sostituito con event `ErrorOccurred(title, message)`. Form1 sottoscrive e mostra `MessageBox` lato UI.
- `Plugin.BLE` + `System.IO.Ports` inclusi nel csproj solo per TFM `net10.0-windows`; `Legacy/` escluso dal build cross-platform (CI Linux).

### 4.6 Esiti

- Form1.cs: **1121 → 731 LOC** (-35%)
- Legacy protocollo: **-2590 LOC** (STEMProtocol/ eliminata)
- Dead code: **-283 LOC** (CAN_WF_Tab)
- Codice totale netto: **-3420 LOC** in un commit
- FormRef residui: **0**
- Suite: **292 net10.0 / 470 net10.0-windows** (+14/+12 vs pre-Phase 4)

### 4.7 Deferred

- `CanPort.ChangeBaudrate` runtime: ora il bitrate PCAN è fissato alla creazione. Se serve nuovamente, esporre API sul port.
- Display RX fine (int/float/bool decoding): semplificato a uint LE/BE. Il legacy supportava più type; può essere riintrodotto ma non era critico.
- `Boot_Smart_Tab` line 300 (parità legacy `FormRef.RecipientId = X`): **rimossa** — non più necessaria, il legacy che la leggeva non esiste più.

---

## Fase 4b — `refactor/app-reorganization` ✅ Completata (2026-04-21)

**Obiettivo:** Rinominare il progetto `App` in `GUI.Windows` e riorganizzare la cartella in sottocartelle semantiche.

**Esito:**
- Progetto rinominato: `App/App.csproj` → `GUI.Windows/GUI.Windows.csproj`. Assembly output `GUI.Windows.dll`. RootNamespace `GUI.Windows`.
- Namespace `App` → `GUI.Windows` in Program.cs + Properties/*.Designer.cs. Refs `App.Properties.Resources.*` aggiornati nei Designer.cs. Resource stream `App.images.*` → `GUI.Windows.images.*`.
- `Stem.Device.Manager.slnx` e `Tests/Tests.csproj` aggiornati al nuovo path.

**Struttura finale `GUI.Windows/`:**
```
GUI.Windows/
├── GUI.Windows.csproj, Program.cs, appsettings.json
├── Forms/       — Form1.{cs,Designer.cs,resx}, SplashScreen.{cs,Designer.cs,resx}
├── Tabs/        — BLE_WF_Tab, Boot_WF_Tab, Boot_Smart_WF_Tab, Telemetry_WF_Tab, TopLift_Telemetry_WF_Tab
├── Controls/    — CircularProgressBar, custom-oxyplot-zoom-x
├── Properties/  — Resources/Settings designer
├── Resources/   — ico, xlsx, png
├── images/      — png tab telemetria TopLift
├── SP_Code_Generator.cs, Terminal.cs  (utility in root)
└── README.md
```

Branch eseguito dopo merge di `phase-4-switch-to-new-stack` per tenere quella PR pulita dal rumore di rename e sposta-file. Suite invariata: **292 net10.0 / 470 net10.0-windows**.

---

## Stabilization Gate — `spec-001-spark-ble-fw-stabilize` ✅ Completata (2026-04-29)

**Obiettivo:** Validate the new post-Phase 4 stack on the SPARK-UC reference bench (SN 2225998) through 7 success criteria (SC-001..SC-007), formalize the critical state machines in Lean 4, and codify the invariants as FsCheck property tests. Closes the stabilization debt that Phase 4 left open and gates Phase 5 (`Stem.Communication`).

**Esito** (full detail in [`specs/001-spark-ble-fw-stabilize/`](../specs/001-spark-ble-fw-stabilize/)):

- **User stories US1..US5** closed: close/relaunch crash-free (US1), UI state truthful (US2 — `ConnectionManager.TransitionTo` mutator + C1 biconditional), BLE session-stable across HMI upload (US3), multi-file batch correctness (US4 — FR-010 precondition + #74 end-of-batch `RESTART_MACHINE` fix), HMI time budget (US5 — `Docs/PerfRegression-Spec001.md` records the regression as not reproducing on bench).
- **Lean 4 formalization** in `Lean/Spec001/`: `BootStateMachine` (T1..T4), `BleLifecycle` (T5 state-protocol biconditional), `BatchComposition` (composition preservation theorem). `lake build` green, no `sorry`/`admit`.
- **FsCheck property tests**: Q1/Q2/Q3/I1 (`BootStateMachinePropertyTests`), C1/C3 (`ConnectionManagerPropertyTests`), Lean ↔ C# drift guard (`LeanDriftGuardTests`).
- **Observability (FR-009)**: structured logging scopes in `BootService` + `ConnectionManager`; discard-frame logging in `ProtocolService` (#76).
- **Bench log**: `Docs/BenchLog-Spec001.md` chronological per-run record for SC-001..SC-007.

**Phase 5 prerequisite:** suite green on both TFMs, `lake build` green, bench validation rows in `Docs/BenchLog-Spec001.md` for each SC. The stable `ICommunicationPort` contract + the now-formalized `BootStateMachine` / `BleLifecycle` mean the Phase 5 swap can be evaluated against a machine-checked spec.

---

## Fase 5 — Integrazione `Stem.Communication` NuGet (quando disponibile)

**Obiettivo:** Sostituire i driver in `Infrastructure.Protocol/Legacy/` con adapter `Stem.Communication`.

**Scope ridotto grazie al refactor precedente:**
- Il contratto `ICommunicationPort` è stabile (Fase 1). Basta sostituire le 3 implementazioni (`CanPort`/`BlePort`/`SerialPort`) con wrapper verso `Stem.Communication.StemClient`.
- Nessuna modifica a Core/Services/tab/Form1.
- Eliminare `Infrastructure.Protocol/Legacy/` e aggiungere `PackageReference Stem.Communication`.

**Valutazione parallela:** migrazione UI da WinForms a WPF (o altro). Questione separata, non blocca l'integrazione NuGet.

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

GUI.Windows/                     [net10.0-windows, WinForms]
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