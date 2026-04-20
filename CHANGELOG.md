# Changelog

Tutte le modifiche rilevanti a questo progetto sono documentate in questo file.

Il formato si basa su [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

---

### Legenda

- **Added**: Nuove funzionalità
- **Changed**: Modifiche a funzionalità esistenti
- **Deprecated**: Funzionalità che verranno rimosse
- **Removed**: Funzionalità rimosse
- **Fixed**: Bug corretti
- **Security**: Vulnerabilità corrette

---

## [Unreleased]

Modernizzazione: documentazione, standard, riorganizzazione progetto, test coverage,
architettura multi-progetto, migrazione dizionari Excel → API Azure,
Fase 3 disaccoppiamento Form1 (Branch 1+2 completati), rimozione funzionalità ButtonPanel e ExcelHandler,
refactor architetturale (REFACTOR_PLAN) con Fase 1 — protocol abstractions in Core —
e Fase 2 (in corso) — services layer + HW adapter + rinomina a pattern Stem.

### Added

- **REFACTOR_PLAN Fase 2 — Branch C `refactor/services-di-integration`** (in corso, Step 7 completato — chiude Fase 2):
  - `Core/Interfaces/IDeviceVariantConfig.SenderId` — nuova proprietà uint (indirizzo STEM del mittente, default `8` per parità legacy)
  - `Core/Models/DeviceVariantConfig` — record esteso con `SenderId`; `DefaultSenderId = 8` costante; nuovo overload `Create(variant, senderId)`; il single-arg delega al default
  - `Services/Configuration/DeviceVariantConfigFactory.FromString(variant, senderId)` — overload per host DI che legge SenderId da appsettings
  - `Services/DependencyInjection.cs` — `AddServices(IConfiguration)`: registra `IDeviceVariantConfig` (da `Device:Variant` + `Device:SenderId`) e `IPacketDecoder` vuoto (UpdateDictionary chiamato dal consumer post-load Azure)
  - `Infrastructure.Protocol/DependencyInjection.cs` — `AddProtocolInfrastructure()`: registra `PCANManager` come `IPcanDriver`, e `CanPort`/`BlePort`/`SerialPort` come singleton concreti (NON come `ICommunicationPort`: scelta runtime gestita in Phase 3 da `ConnectionManager`)
  - `Services.csproj` + `Infrastructure.Protocol.csproj` — aggiunti PackageRef `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.5` (e `Configuration.Abstractions` per Services)
  - `App/appsettings.json` — `Device.SenderId = 8`
  - `App/Program.cs` — wiring DI completo: `AddDictionaryProvider` + driver BLE/Serial registrati come `IBleDriver`/`ISerialDriver` + `AddProtocolInfrastructure()` + `AddServices(config)`. Nessun consumer cambiato (Form1 continua a usare `STEMProtocol/` legacy — rimozione in Phase 3)
  - **Servizi NON registrati per scelta architetturale**: `ProtocolService`, `ITelemetryService`, `IBootService` dipendono dalla port runtime (CAN/BLE/Serial scelta a menu) — creati dal `ConnectionManager` in Phase 3
  - `Docs/PREPROCESSOR_DIRECTIVES.md` — documentato debito Phase 3: `BLE_Manager.FormRef` da rimpiazzare con evento o `ILogger`
  - Test: **+8 test** (6 DeviceVariantConfig SenderId + 2 DeviceVariantConfigFactory) → suite **236 net10.0** / **381 net10.0-windows**
- **REFACTOR_PLAN Fase 2 — Branch B `refactor/services-business`** (merged in main 2026-04-20, PR #26, Step 3-5 completati):
  - `Services/Configuration/DeviceVariantConfigFactory.cs` — factory totale: parsing case-insensitive da stringa di configurazione (default Generic per null/vuoto/sconosciuto), helper `CanonicalName` per round-trip
  - `App/appsettings.json` — sezione `Device:Variant` (default `"Generic"`)
  - `Services/Telemetry/TelemetryService.cs` — implementa `ITelemetryService` usando `ProtocolService` come facade (zero ref a `ICommunicationPort`/`IPacketDecoder` diretti, zero ref a Form1):
    - protocollo CMD_CONFIGURE_TELEMETRY (0x0015) / CMD_START_TELEMETRY (0x0016) / CMD_STOP_TELEMETRY (0x0017)
    - decode CMD_TELEMETRY_DATA (0x0018) per uint8/uint16/uint32 LE — variabili a DataType ignoto saltate
    - state machine stopped/running con `Lock`, `UpdateDictionary` atomico, pacchetti a telemetria spenta ignorati
  - `Services/Boot/BootService.cs` — implementa `IBootService` usando `ProtocolService.SendCommandAndWaitReplyAsync` per il pattern request/reply:
    - sequenza upload: CMD_START_PROCEDURE → loop blocchi 1024B (paddati 0xFF) di CMD_PROGRAM_BLOCK con 10 retry → CMD_END_PROCEDURE con 5 retry → CMD_RESTART_MACHINE x 2
    - reply matching tramite convenzione "CodeHigh=80 → risposta"
    - fwType estratto da byte 14-15 little-endian del firmware
    - state machine Idle → Uploading → (Completed | Failed) con `Lock`; `BootProtocolException` interna assorbita (parità legacy: stato Failed osservabile, niente rethrow)
    - `CancellationToken` rispettato fra blocchi
    - ctor interno con `responseTimeout` + `restartInterval` configurabili (per test veloci)
  - `Core/Interfaces/IBootService` — aggiunto parametro `uint recipientId` a `StartFirmwareUploadAsync` (target device)
  - `Services/Protocol/ProtocolService.SenderId` — getter pubblico per accesso al senderId interno (usato da TelemetryService per payload CONFIGURE)
  - `Tests/Unit/Services/Protocol/FakeCommunicationPort` — hook `OnSent` opzionale per auto-reply nei test BootService
  - Test: **+56 test** (25 DeviceVariantConfigFactory + 18 TelemetryService + 13 BootService), tutti cross-platform → suite **228 net10.0** / **373 net10.0-windows**
- **REFACTOR_PLAN Fase 2 — Branch A `refactor/protocol-service`** (merged in main 2026-04-20, PR #25, Step 6 completato):
  - `Core/Models/ChannelKind.cs` — enum Can/Ble/Serial per discriminare il framing atteso dal protocollo
  - `Core/Interfaces/ICommunicationPort` — aggiunta proprietà `ChannelKind Kind { get; }` (i 3 adapter esistenti la espongono)
  - `Services/Protocol/NetInfo.cs` — struct immutable (readonly record struct) per parsing/encoding dei 2 byte Network Layer (remainingChunks 10 bit, setLength 1 bit, packetId 3 bit, version 2 bit)
  - `Services/Protocol/PacketReassembler.cs` — riassembly multi-chunk thread-safe per packetId con `Reset()` e `PendingPacketCount` per diagnostica
  - `Services/Protocol/ProtocolService.cs` — facade del protocollo: encode command → TP con CRC16 Modbus → chunking per canale → wire frame; decode + reassembly + evento `AppLayerDecoded`; pattern `SendCommandAndWaitReplyAsync` con validator custom e timeout
  - Mantenuto quirk storico senderId byte-swap (parità con legacy TransportLayer, vedi PROTOCOL.md §3.1)
  - Test: +49 test (13 NetInfo + 13 PacketReassembler + 13 ProtocolService + 3 Kind adapter + 7 altri), tutti cross-platform tranne Kind per adapter (Windows-only)
- **REFACTOR_PLAN Fase 2 — Branch `refactor/services-foundation`** (merged in main 2026-04-20, PR #24, Step 1-2 completati):
  - **Rinomina** progetto `Infrastructure` → `Infrastructure.Persistence` per allineamento al pattern Stem (`Infrastructure.<Concern>` — vedi `Stem.Production.Tracker`, `Stem.ButtonPanel.Tester`)
  - **Nuovo progetto** `Infrastructure.Protocol` (dual TFM `net10.0;net10.0-windows10.0.19041.0`) — adapter hardware CAN/BLE/Serial:
    - `Hardware/CanPort.cs` — implementa `ICommunicationPort` con convention A (arbitration ID LE prefisso nei payload) wrappando `PCANManager` via `IPcanDriver`
    - `Hardware/BlePort.cs` — implementa `ICommunicationPort` con convention pass-through, wrappa `App.BLEManager` via `IBleDriver`
    - `Hardware/SerialPort.cs` — implementa `ICommunicationPort` con convention pass-through, wrappa `App.SerialPortManager` via `ISerialDriver`
    - `Hardware/IPcanDriver.cs` / `IBleDriver.cs` / `ISerialDriver.cs` — abstraction per testability + dependency inversion (`BLEManager` e `SerialPortManager` restano in `App/` per Form1/MessageBox refs)
    - `Hardware/BlePacketEventArgs.cs` / `SerialPacketEventArgs.cs` — event args centralizzati
    - `Hardware/PCANManager.cs` — driver PCAN-USB spostato da `App/` (auto-reconnect, baud rate runtime)
  - **`Services/` ora popolato** (target `net10.0` puro, cross-platform):
    - `Services/Protocol/PacketDecoder.cs` — `IPacketDecoder` puro con thread-safe `UpdateDictionary` (Volatile.Read/Write su snapshot)
    - `Services/Protocol/DictionarySnapshot.cs` — snapshot immutabile con lookup comandi/variabili/indirizzi (hex case-insensitive)
  - `Docs/PROTOCOL.md` — nuova documentazione completa del protocollo STEM (3 livelli AL/TL/NL, NetInfo bit layout, CRC16 Modbus, chunking per canale, 10 comandi noti, 6 known quirks, pipeline send/receive per canale, roadmap migrazione Fase 4)
  - Test coverage: **274 test** totali (era 209), **132 cross-platform** (era 86): +27 PacketDecoder, +21 DictionarySnapshot, +22 CanPort, +21 BlePort, +21 SerialPort, +7 PacketEventArgs, +18 gap-fill (Dispose idempotency, connect timeout, state cycle, null validation)
  - `CanPort` / `BlePort` / `SerialPort` tests usano fake driver manuali (`FakePcanDriver`, `FakeBleDriver`, `FakeSerialDriver`) in `Tests/Unit/Infrastructure/Protocol/`
  - README progetti: `Infrastructure.Protocol/README.md`, `Services/README.md`; `Infrastructure.Persistence/README.md` aggiornato post-rename
- **REFACTOR_PLAN Fase 1 — Protocol abstractions in Core** (branch `refactor/protocol-abstractions`):
  - `Core/Interfaces/` — 5 nuove interfacce: `ICommunicationPort` (astrazione CAN/BLE/Serial), `IPacketDecoder` (decoder puro `RawPacket → AppLayerDecodedEvent`), `ITelemetryService`, `IBootService` (+ enum `BootState`, record `BootProgress`), `IDeviceVariantConfig`
  - `Core/Models/` — 6 nuovi modelli: `ConnectionState` (enum), `DeviceVariant` (enum), `DeviceVariantConfig` (record + factory totale `Create(DeviceVariant)`), `RawPacket`, `AppLayerDecodedEvent`, `TelemetryDataPoint`
  - `Core/Models/ImmutableArrayEquality.cs` — helper interno per equality strutturale di `ImmutableArray<byte>` (necessario perché `ImmutableArray<T>.Equals` è reference-based)
  - `Specs/Phase1/` — 7 file Lean 4 (formalizzazione dei tipi + teoremi di correttezza della factory `DeviceVariantConfig.Create`)
  - `Tests/Unit/Core/Models/` — 6 nuovi file test (33 test): `ConnectionStateTests`, `DeviceVariantTests`, `DeviceVariantConfigTests`, `RawPacketTests`, `AppLayerDecodedEventTests`, `TelemetryDataPointTests`
  - `Docs/PREPROCESSOR_DIRECTIVES.md` — sezione "Nota Fase 1 — IDeviceVariantConfig (TODO per Fase 3)" che elenca i feature flag booleani da aggiungere in Fase 3 per eliminare i blocchi `#if`
- `.copilot/` — Istruzioni Copilot con memoria a lungo termine, 5 agent files (CODING, TEST, DOCS, ISSUES, FORMAL)
- `Docs/Standards/` — Template standard (README, ISSUES, STANDARD, TEMPLATE_STANDARD)
- `README.md` — Documentazione root del progetto
- `CHANGELOG.md` — Questo file
- `LICENSE` — Licenza proprietaria
- `Stem.Device.Manager.slnx` — Solution file migrato a formato XML moderno (da `.sln`)
- `Tests/` — Progetto test xUnit con 176 test (xUnit 2.5.3)
  - **Unit test** (68): Core Models, Infrastructure providers (API/Excel/Fallback), Terminal, Protocol, CodeGenerator, CircularProgressBar
  - **Integration test** (34): DI wiring con IDictionaryProvider, CodeGenerator E2E, Form1 IDictionaryProvider integration
  - **Manual mocks**: MockHttpMessageHandler, MockDictionaryProvider
- `App/README.md` — Documentazione progetto App
- `Tests/README.md` — Documentazione progetto Tests
- `Core/README.md` — Documentazione progetto Core
- `Infrastructure/README.md` — Documentazione progetto Infrastructure
- `Docs/PREPROCESSOR_DIRECTIVES.md` — Documentazione blocchi `#if` nel codice, strategia Fase 3
- `InternalsVisibleTo("Tests")` in `App.csproj` per testare tipi `internal`
- **Architettura multi-progetto** — Migrazione da monolite a 4 progetti separati:
  - `Core/` (net10.0) — Modelli dominio, interfacce (`IDictionaryProvider`)
  - `Infrastructure/` (net10.0) — Provider dati (API Azure + Excel + Fallback decorator)
  - `Services/` (net10.0-windows) — Pronto per logica business futura
  - `App/` (net10.0-windows) — Windows Forms, DI configurato con `IConfiguration`
- **Migrazione dizionari Excel → API Azure** (Fase 2 completata):
  - `Core/Models/` — 4 record dominio: `Variable`, `Command`, `ProtocolAddress`, `DictionaryData`
  - `Core/Interfaces/IDictionaryProvider` — Astrazione async con `CancellationToken`
  - `Infrastructure/Excel/ExcelDictionaryProvider` — Legge da Excel embedded, ora unica implementazione Excel
  - `Infrastructure/Api/DictionaryApiProvider` — Chiama API REST Stem.Dictionaries.Manager
  - `Infrastructure/Api/Dtos/` — 5 DTO deserializzazione (struttura JSON reale dell'API)
  - `Infrastructure/Api/DictionaryApiOptions` — Configurazione: BaseUrl, ApiKey, TimeoutSeconds
  - `Infrastructure/FallbackDictionaryProvider` — Decorator: API → catch HttpRequestException → Excel
  - `Infrastructure/DependencyInjection.cs` — Extension method `AddDictionaryProvider(IConfiguration)`
  - `App/appsettings.json` — Sezione `DictionaryApi` (vuota = Excel, popolata = API con fallback)
  - `Docs/MIGRATION_API.md` — Piano migrazione completo con 6 branch documentati
- **Fase 3 Branch 1: `refactor/type-swap-core-models`** — Sostituzione tipi ExcelHandler con Core.Models
  - `App/Form1.cs`, `App/TelemetryManager.cs`, `App/*_WF_Tab.cs` — Rinominate proprietà (es. `AddrH→AddressHigh`)
- **Fase 3 Branch 2: `refactor/source-swap-idictionary-provider`** — Sostituzione ExcelHandler come sorgente dati
  - `App/Form1.cs` — Iniettato `IDictionaryProvider`, estratto `LoadDictionaryDataAsync(CancellationToken)`,
    `comboBoxBoard_SelectedIndexChanged` reso `async void`
  - Eliminati 6 blocchi `#if TOPLIFT/#else` relativi al caricamento
  - `Docs/PREPROCESSOR_DIRECTIVES.md` — Documentati 9 blocchi `#if` rimasti con strategia di refactoring
  - `Tests/Integration/Form1/` — 9 test di integrazione + `MockDictionaryProvider` per il contratto IDictionaryProvider

### Changed

- **Migrazione .NET 8 → .NET 10** — TFM `net10.0-windows10.0.19041.0` per App e Tests
- `bitbucket-pipelines.yml` — image `sdk:10.0`, build via `.slnx` (supportato da SDK 10)
- Rinominato progetto da `STEMPM` ad `App` (cartella `App/`, file `App.csproj`)
- Migrato solution file da `.sln` (legacy) a `.slnx` (XML moderno, ~58% riduzione righe)
- Build configurations ridotte da 10 a 8 (rimossa `STEMDM`, rimossa `BUTTONPANEL`)
- `bitbucket-pipelines.yml` — aggiunto step Test dopo Build
- `README.md` — aggiornato badge test (0 → 176), struttura soluzione multi-progetto, caratteristiche, build config
- `App/Program.cs` — Aggiunto `IConfiguration` (appsettings.json + env vars) e `AddDictionaryProvider()`
- `App/App.csproj` — Aggiunto `ProjectReference` a Core e Infrastructure, pacchetti Configuration, rimosso BUTTONPANEL config
- Namespace `App.Core.*` → `Core.*` per modelli e interfacce spostati in Core/
- `App/README.md` — Rimosso ButtonPanel e ExcelHandler da struttura e build config, LOC 56k → 54k
- `Core/README.md` — Rimosso ButtonPanel da modelli e interfacce
- `Tests/README.md` — Aggiornato test count 272 → 176, rimosso ButtonPanel test suite, rimosso ExcelHandler test suite
- `Docs/PREPROCESSOR_DIRECTIVES.md` — Rimosso BUTTONPANEL da simboli attivi, documentata rimozione nella sezione "Eliminati"
- `CLAUDE.md` — Rimosso ButtonPanel da componenti chiave, aggiornato numero configurazioni build 9 → 8
- `README.md` (root) — Rimosso "Dizionari Excel embedded", aggiornato a "Dizionari Azure API"

### Removed

- Configurazione `STEMDM` — mai usata, nessun `#if STEMDM` nel codice
- **Configurazione build `BUTTONPANEL`** — Funzionalità test pulsantiere rimossa interamente
- **Modulo ButtonPanel completo** — Rimozione in 6 fasi:
  - Fase 1: Test ButtonPanel (7 file, 34 test unit, 8 test integration)
  - Fase 2: Disconnessione da Form1 e Program.cs (metodi, blocchi `#if BUTTONPANEL`, registrazione DI)
  - Fase 3: Interfaccia e servizi (IButtonPanelTestService, ButtonPanelTestService, MVP presenter/view)
  - Fase 4: Modelli core (ButtonPanel, ButtonPanelTestResult, ButtonPanelEnums, ButtonIndicator)
  - Fase 5: Configurazione build (rimosso da `<Configurations>` in App.csproj)
  - Fase 6: Risorse e documentazione (cartella images/ButtonPanels/, aggiornamento README)
- **ExcelHandler.cs rimosso completamente** — Migrato a Infrastructure.ExcelDictionaryProvider
  - Rimozione logica da Form1, tab WinForms e TelemetryManager
  - Eliminazione di ExcelHandler.cs, ExcelHandler test (8 unit + 1 integration)
  - Semplificazione: nessun più ExcelfilePath, isStreamBased, MessageBox per errori Excel
  - IDictionaryProvider unificato: form usa infrastructure provider
- `Core/Models/ButtonPanel.cs` — Factory e maschere bit
- `Core/Models/ButtonPanelTestResult.cs` — DTO risultato test
- `Core/Models/ButtonIndicator.cs` — Indicatore visuale stato pulsante
- `Core/Enums/ButtonPanelEnums.cs` — 6 enum (ButtonPanelType, TestType, IndicatorState, pulsanti)
- `Core/Interfaces/IButtonPanelTestService.cs` — Contratto servizio collaudo
- `App/Services/ButtonPanelTestService.cs` — Implementazione servizio (~250 LOC)
- `App/GUI/Presenters/ButtonPanelTestPresenter.cs` — Presenter MVP (157 LOC)
- `App/GUI/Views/ButtonPanelTestTabControl.cs` — UserControl WinForms (579 LOC)
- `App/GUI/Views/ButtonPanelTestTabControl.Designer.cs` — Auto-generated WinForms
- `App/Core/Interfaces/IButtonPanelTestTab.cs` — Contratto vista
- `App/images/ButtonPanels/` — Cartella con 4 immagini JPG (risorse pulsantiere)
- Test ButtonPanel:
  - `Tests/Unit/Core/Models/ButtonPanelTests.cs` (10 test)
  - `Tests/Unit/Core/Models/ButtonPanelTestResultTests.cs` (3 test)
  - `Tests/Unit/Core/Models/ButtonIndicatorTests.cs` (2 test)
  - `Tests/Unit/Core/Enums/ButtonPanelEnumsTests.cs` (8 test)
  - `Tests/Integration/Presenter/ButtonPanelTestPresenterTests.cs` (8+ test)
  - `Tests/Integration/Presenter/Mocks/MockButtonPanelTestTab.cs`
  - `Tests/Integration/Presenter/Mocks/MockButtonPanelTestService.cs`
- `Stem.Device.Manager.sln` — sostituito da `.slnx`

### Fixed

- Blocchi `#if BUTTONPANEL` rimossi da `Form1.cs` (righe ~156, ~344)
- Blocchi `#if BUTTONPANEL` rimossi da `SplashScreen.cs` (riga ~12)
- ExcelHandler decoupled da Form1: nessun più chiamate dirette a `hExcel.EstraiDizionario()`

---

### Statistiche rimozione ButtonPanel e ExcelHandler

- **File eliminati**: 27 (19 ButtonPanel + 8 ExcelHandler-related)
- **File modificati**: 15 (Form1, SplashScreen, Program, App.csproj, ServiceRegistrationTests, + 6 README, CLAUDE.md, CHANGELOG, etc.)
- **Test rimossi**: 142 (34 ButtonPanel + 8 ExcelHandler)
- **Test rimanenti**: 176 ✅ (tutti passanti)
- **Linee di codice rimosse**: ~1.500
- **LOC App**: 56k → 54k
- **Build configurations**: 9 → 8
- **Codice semplificato**: Form1 no longer references ExcelHandler, IDictionaryProvider unico entry point

---

## [0.2.15] - 2026-04-14

Stato dell'arte del progetto legacy pre-modernizzazione. ~330 commit, ~56k LOC, 0 test automatizzati.

### Protocollo STEM

- Layer stack proprietario completo: Application Layer, Network Layer, Transport Layer, Protocol Manager
- Supporto multi-canale: CAN (PCAN USB), BLE (Plugin.BLE), Seriale (System.IO.Ports)
- `PacketManager` per gestione pacchetti multi-canale con ricomposizione chunk e eventi
- Sniffer application layer per debug comunicazione
- `SPRollingCode` per sicurezza rolling code
- CAN baudrate selezionabile (100 kbps / 250 kbps)

### Bootloader

- `BootManager` classico: aggiornamento firmware via CAN/BLE/Serial con barra progresso
- `Boot_Smart_Tab`: bootloader smart con gestione canale hardware dinamica
- Supporto filtro numero pagina, invio blocchi da 1024 byte
- Lettura tipo firmware da file per invio corretto alla scheda
- Start/End procedure, restart machine, program block

### Telemetria

- Telemetria lenta: lettura singole variabili via comando Read Variable (0x0001)
- Telemetria veloce: configurazione (0x0015), start (0x0016), stop (0x0017), ricezione dati (0x0018)
- Grafici in tempo reale con OxyPlot (zoom custom asse X)
- Tab telemetria standard + tab telemetria TopLift dedicata (~42k LOC)

### Dizionari Excel

- `ExcelHandler`: lettura dizionari da file Excel embedded (`Dizionari STEM.xlsx` ~187k)
- Supporto stream-based (risorsa embedded via `MemoryStream`)
- 3 tipi dati: `RowData` (indirizzi protocollo), `CommandData` (comandi), `VariableData` (variabili)
- Lettura fogli: indirizzi macchina/scheda, lista comandi, dizionario variabili per device

### Test Pulsantiere (Banco Collaudo)

- Architettura pulita MVP: `IButtonPanelTestService` → `ButtonPanelTestPresenter` → `ButtonPanelTestTabControl`
- Dependency Injection con `Microsoft.Extensions.DependencyInjection`
- 4 tipi pulsantiera: DIS0023789, DIS0025205, DIS0026166, DIS0026182
- 4 tipi collaudo: Completo, Pulsanti, LED, Buzzer
- `ButtonPanel` factory method con maschere bit per ogni tipo
- Test pulsanti: attesa pressione con timeout 10s, verifica maschera bit
- Test LED: accensione verde/rosso con conferma operatore
- Test buzzer: attivazione con conferma operatore
- `CancellationToken` per interruzione collaudo
- Download risultati collaudo
- Indicatori visivi per stato pulsante (Idle, Waiting, Success, Failed)
- Messaggi colorati in RichTextBox per feedback visivo
- Gestione errori in vista, presenter e servizio (PR #9)

### Code Generator

- `SP_Code_Generator`: generazione file `sp_config.h` con configurazioni protocollo
- Lista configurazioni: CAN channels, serial buffers, router, device table, logs, ecc.

### GUI (Windows Forms)

- Form principale con TabControl multi-pagina
- Tab BLE Scanner con scansione dispositivi e connessione
- Tab CAN con gestione TX/RX e stato connessione PCAN
- Tab Bootloader (classico + smart)
- Tab Telemetria (standard + TopLift dedicata)
- Tab Test Pulsantiere (MVP)
- SplashScreen all'avvio
- Barra di stato con stato connessione PCAN
- 10 build configurations: Debug, Release, TOPLIFT-A2-Debug/Release, EDEN-Debug/Release, EGICON-Debug/Release, STEMDM, BUTTONPANEL
- Configurazioni device via `#if` preprocessor (TOPLIFT, EDEN, EGICON, BUTTONPANEL)
- Titolo form dinamico per configurazione
- Selezione canale comunicazione CAN/BLE/Serial via menu
- Dimensione minima form impostata
- Logger basico (`Terminal`) con StringBuilder

### Utilità

- `CircularProgressBar`: custom control per barra progresso circolare
- `SerialPortManager`: scansione e gestione porte seriali
- `PCAN_Manager`: gestione hardware PCAN USB
- `BLE_Manager`: gestione Bluetooth Low Energy (escluso da compilazione)

### Dipendenze

- ClosedXML 0.105.0, DocumentFormat.OpenXml 3.5.1 (Excel)
- OxyPlot.WindowsForms 2.2.0 (grafici)
- Peak.PCANBasic.NET 5.0.1 (CAN)
- Plugin.BLE 3.2.0 (Bluetooth)
- System.IO.Ports 10.0.5 (seriale)
- Microsoft.Extensions.DependencyInjection 10.0.5
- Microsoft.Windows.SDK.BuildTools 10.0.28000.1721

### Note

- Progetto monolitico: singolo `App.csproj` (ex `STEMPM.csproj`), nessuna separazione in librerie
- `Form1.cs` è un God Object (~55k LOC con Designer) — contiene GUI + logica protocollo + telemetria
- 0 test automatizzati
- 0 documentazione formale (README vuoti fino a v2.15)
- Target: `net8.0-windows10.0.19041.0`, `win-x64`
- Autore originale: Michele Pignedoli
- Modernizzazione iniziata da: Luca Veronelli (branch `test/copertura-iniziale`)

---

## Storico Versioni Precedenti (da commit messages)

| Versione | SemVer | Milestone |
|----------|--------|-----------|
| 1.0 | 0.1.0 | Protocollo STEM impacchettato, sniffer completato |
| 1.1 | 0.1.1 | Riordino per gestione risposte |
| 1.2 | 0.1.2 | Primo bootloader funzionante |
| 1.3 | 0.1.3 | Fix pack ID, accelerazione boot |
| 1.4 | 0.1.4 | Aggiornamento scheda madre TopLift |
| 1.5 | 0.1.5 | Gestione tastiere + accelerazione boot |
| 1.6 | 0.1.6 | Risposta pacchetti funzionante |
| 1.7 | 0.1.7 | Filtro pagina bootloader |
| 1.8 | 0.1.8 | Barra stato connessione PCAN, dizionario variabili logiche |
| 2.1 | 0.2.1 | Telemetria funzionante |
| 2.7 | 0.2.7 | BLE with/without response |
| 2.8 | 0.2.8 | Lettura tipo firmware da file |
| 2.10 | 0.2.10 | Semplificazione per distributori internazionali |
| 2.11 | 0.2.11 | Versioni Complete/Spark/Eden/TopLiftA2, decodifica fault |
| 2.14 | 0.2.14 | Seriale + CAN baudrate selezionabile (100/250 kbps) |
| 2.15 | 0.2.15 | Fix telemetria lenta + attivazione telemetria veloce |

---

## Storico URL versioni

[Unreleased]: https://bitbucket.org/stem-fw/stem-device-manager/branches
