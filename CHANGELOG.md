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

Modernizzazione: documentazione, standard, riorganizzazione progetto.

### Added

- `.copilot/` — Istruzioni Copilot con memoria a lungo termine, 5 agent files (CODING, TEST, DOCS, ISSUES, FORMAL)
- `Docs/Standards/` — Template standard (README, ISSUES, STANDARD, TEMPLATE_STANDARD)
- `README.md` — Documentazione root del progetto
- `CHANGELOG.md` — Questo file
- `LICENSE` — Licenza proprietaria
- `Stem.Device.Manager.slnx` — Solution file migrato a formato XML moderno (da `.sln`)

### Changed

- Rinominato progetto da `STEMPM` ad `App` (cartella `App/`, file `App.csproj`)
- Migrato solution file da `.sln` (legacy) a `.slnx` (XML moderno, ~58% riduzione righe)
- Build configurations ridotte da 10 a 9 (rimossa `STEMDM`)

### Removed

- Configurazione `STEMDM` — mai usata, nessun `#if STEMDM` nel codice, nessun `DefineConstants` nel `.csproj`
- `Stem.Device.Manager.sln` — sostituito da `.slnx`

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
- 9 build configurations: Debug, Release, TOPLIFT-A2-Debug/Release, EDEN-Debug/Release, EGICON-Debug/Release, BUTTONPANEL
- Configurazioni device via `#if` preprocessor (TOPLIFT, EDEN, EGICON, BUTTONPANEL)
- Titolo form dinamico per configurazione: "STEM Toplift A2 Manager", "STEM Eden XP Manager", "STEM Spark Manager", "STEM Button Panel Tester"
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

[Unreleased]: https://bitbucket.org/stem-fw/win10-stem-dev-man/branches/compare/test/copertura-iniziale..main
