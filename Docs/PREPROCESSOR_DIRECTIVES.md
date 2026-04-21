# PREPROCESSOR_DIRECTIVES.md — Blocchi `#if` in Form1.cs

**Creato:** 2026-04-16
**Ultimo aggiornamento:** 2026-04-21
**Scopo:** Documentare la migrazione dai simboli di preprocessore `#if TOPLIFT/EDEN/EGICON` a configurazione runtime via `IDeviceVariantConfig`.

---

## Stato (2026-04-21)

**Tutti i blocchi `#if TOPLIFT/EDEN/EGICON` sono stati rimossi.** Branch `refactor/remove-ifs` ha convertito le 11 occorrenze nei 4 file interessati (Form1.cs, SplashScreen.cs, Boot_WF_Tab.cs, Telemetry_WF_Tab.cs) in runtime check su `IDeviceVariantConfig.Variant` o flag dedicati (`WindowTitle`, `SmartBootDevices`, `DefaultChannel`, `DefaultRecipientId`, `DeviceName`, `BoardName`).

Le 6 configurazioni di build device-specific (`TOPLIFT-A2-Debug/Release`, `EDEN-Debug/Release`, `EGICON-Debug/Release`) sono state rimosse da `GUI.Windows.csproj` e `Stem.Device.Manager.slnx`. Restano solo `Debug` e `Release`.

La variante viene letta da `appsettings.json` (`Device:Variant`) via `Services.Configuration.DeviceVariantConfigFactory` e iniettata come `IDeviceVariantConfig` dal composition root.

---

## Mappa dei blocchi rimossi

Ogni blocco è stato sostituito come indicato:

| # | File:Riga (prima) | Contenuto | Sostituzione |
|---|-------------------|-----------|--------------|
| 1 | Form1.cs:21 | `CommunicationPort` default (can vs ble) | `_variantConfig.DefaultChannel` → mapping a string nel ctor |
| 2 | Form1.cs:159 | Titolo finestra | `_variantConfig.WindowTitle` + `Software_Version` |
| 3 | Form1.cs:223 | `tabControl.TabPages.Add(BootTabRef)` condizionato | `if (Variant != TopLift && Variant != Eden)` |
| 4 | Form1.cs:246 | Lista `BootSmartDevices` hardcoded | `_variantConfig.SmartBootDevices` mappata a `DeviceInfo` |
| 5 | Form1.cs:304 | Layout tab iniziale (TOPLIFT nasconde UI, aggiunge TLT+Smart; Egicon/Generic BLE + Telemetry) | `if (Variant == TopLift) { ... } else { ... }` con sotto-branch per `Egicon` |
| 6 | Form1.cs:448 | Update telemetry manager (TLT vs Telemetry) | `if (Variant == TopLift) TLTTabRef.*; else if (TelemetryTabRef != null) Telemetry*` |
| 7 | Form1.cs:513 | Pre-selezione recipient/device/board iniziale | `if (DefaultRecipientId != 0) ... else if (DeviceName+BoardName) lookup` |
| 8 | Form1.cs:824 | Skip pannello applayer decodificato su TOPLIFT | `if (Variant == TopLift) return;` all'inizio di `AppLayerDecoded` |
| — | SplashScreen.cs:12 | Label splash per variante | switch su `Variant` nel ctor, riusa pattern di blocco 2 |
| — | Boot_WF_Tab.cs:111 | Pulsanti UI Egicon vs altri | `if (Variant == Egicon) { 4 pulsanti } else { btnAuto }` |
| — | Telemetry_WF_Tab.cs:263 | `AddToDictionary(MachineDictionary[1])` extra su TOPLIFT | `if (Variant == TopLift)` in `ButtonStart_Click` |

---

## Flag aggiunti a `IDeviceVariantConfig`

| Flag | Tipo | Derivazione | Usato in |
|------|------|-------------|----------|
| `WindowTitle` | `string` | `WindowTitleFor(variant)` | Form1 Text, SplashScreen label |
| `SmartBootDevices` | `IReadOnlyList<SmartBootDeviceEntry>` | `SmartBootDevicesFor(variant)` | Form1 `BootSmartTabRef.PopulateDevices` |

I flag esistenti già in `IDeviceVariantConfig` (da Fase 1 e Fase 3 Branch 1) riusati:

- `Variant` — switch runtime in 7 blocchi
- `DefaultRecipientId` — blocco 7 (TOPLIFT: 0x00080381)
- `DeviceName` / `BoardName` — blocco 7 (EDEN: "EDEN"/"Madre", EGICON: "SPARK"/"HMI")
- `DefaultChannel` — blocco 1 (`ChannelKind.Can` vs `.Ble`)

---

## Nuovo tipo in Core

`Core/Models/SmartBootDeviceEntry.cs` — record `(uint Address, string Name, bool IsKeyboard)`. Consumato da `Form1.BootSmartTabRef.PopulateDevices`, mappato al legacy `App.DeviceInfo`.

---

## Debito tecnico residuo

### `GUI.Windows/BLE_Manager.cs`

Riferimento a `Form1.FormRef` **eliminato** in Branch `refactor/form1-thin-shell` (2026-04-21): sostituito dall'evento `LogMessageEmitted` (`Action<string>`) a cui Form1 si sottoscrive. Il driver è ora autonomo rispetto a Form1 (il file resta in `GUI.Windows/` per contesto storico; spostamento a `Infrastructure.Protocol/` possibile in Phase 4).

### `GUI.Windows/SerialPort_Manager.cs`

Al 2026-04-21 nessun riferimento a `Form1.FormRef`. Candidato per lo spostamento in `Infrastructure.Protocol/Hardware/` insieme a `BLE_Manager` quando lo stack legacy viene rimosso.

### Driver in `Infrastructure.Protocol/Hardware/`

- `PCANManager` autonomo da Branch `refactor/services-foundation`.
