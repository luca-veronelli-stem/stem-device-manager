# PREPROCESSOR_DIRECTIVES.md — Blocchi `#if` in Form1.cs

**Creato:** 2026-04-16  
**Branch:** refactor/source-swap-idictionary-provider  
**Scopo:** Documentare i blocchi `#if` rimasti dopo la migrazione a IDictionaryProvider.
I blocchi relativi al caricamento Excel sono stati eliminati (unificati da IDictionaryProvider).
Questi blocchi rimasti sono candidati per il refactoring della Fase 3.

---

## Blocchi attivi in Form1.cs

### 1. `CommunicationPort` (riga ~21)

```csharp
#if TOPLIFT
    public string CommunicationPort = "can";
#else
    public string CommunicationPort = "ble";
#endif
```

**Scopo:** Canale hardware di default per la comunicazione (CAN per TOPLIFT, BLE per gli altri).  
**Refactoring futuro:** Configurazione da `appsettings.json` o da file di device-profile.

---

### 2. Titolo finestra (riga ~158)

```csharp
#if BUTTONPANEL
    Text = "STEM Button Panel Tester ...";
#else
#if TOPLIFT
    Text = "STEM Toplift A2 Manager ...";
#elif EDEN
    Text = "STEM Eden XP Manager ...";
#elif EGICON
    Text = "STEM Spark Manager ...";
#else
    this.Text += Software_Version;
#endif
#endif
```

**Scopo:** Titolo della finestra principale specifico per device.  
**Refactoring futuro:** Tabella di mapping `symbol → titolo` in configurazione.

---

### 3. `BootTabRef` (riga ~223)

```csharp
#if TOPLIFT
    // non aggiungere BootTabRef
#elif EDEN
    // non aggiungere BootTabRef
#else
    tabControl.TabPages.Add(BootTabRef);
#endif
```

**Scopo:** La tab del bootloader classico è visibile solo su configurazioni non-TOPLIFT e non-EDEN.  
**Refactoring futuro:** Tabella di capacità device.

---

### 4. `BootSmartDevices` (riga ~246)

```csharp
#if TOPLIFT
    // TOPLIFT devices: Keyboard 1/2/3 + Motherboard
#elif EDEN
    // EDEN devices: Keyboard 1/2 + Motherboard
#else
    // lista vuota
#endif
```

**Scopo:** Lista dispositivi per il bootloader smart, specifica per device.  
**Refactoring futuro:** Provider di device-profile (es. API o JSON).

---

### 5. Configurazione UI per TOPLIFT (riga ~307)

```csharp
#if TOPLIFT
    // Nasconde terminal, rimuove tab protocol/BLE, nasconde status BLE
    // Aggiunge TopLiftTelemetry_Tab e BootSmartTab
#elif EGICON
    tabControl.SelectedTab = BLETabRef;
#else
    tabControl.SelectedTab = BLETabRef;
    tabControl.TabPages.Add(TelemetryTabRef);
#endif
```

**Scopo:** Layout iniziale dei tab specifico per device.  
**Refactoring futuro:** Configurazione layout via tabella di capacità device (Fase 3).

---

### 6. `#if BUTTONPANEL` — rimozione tab (riga ~349)

```csharp
#if BUTTONPANEL
    tabControl.TabPages.Remove(tabPageUART);
    tabControl.TabPages.Remove(tabPageCodeGen);
    tabControl.TabPages.Remove(TelemetryTabRef);
    tabControl.TabPages.Remove(BLETabRef);
    tabControl.TabPages.Remove(BootSmartTabRef);
    tabControl.TabPages.Remove(BootTabRef);
#endif
```

**Scopo:** Modalità solo-pulsantiere: rimuove tutti i tab non pertinenti.  
**Refactoring futuro:** Profilo "ButtonPanel" come configurazione dichiarativa.

---

### 7. `comboBoxBoard_SelectedIndexChanged` — aggiornamento tab (riga ~479)

```csharp
#if TOPLIFT
    TLTTabRef.UpdateDictionary(Dizionario);
    TLTTabRef.telemetryManager.UpdateSourceAddress(RecipientId);
#else
    if (TelemetryTabRef != null) { ... }
#endif
```

**Scopo:** Il cambio scheda aggiorna la tab di telemetria corretta (TLT per TOPLIFT, Telemetry per gli altri).  
**Refactoring futuro:** Interfaccia comune `ITelemetryTab` con `UpdateDictionary` e `UpdateSourceAddress`.

---

### 8. `LoadDictionaryDataAsync` — caricamento variabili iniziale (riga ~524)

```csharp
#if TOPLIFT
    // RecipientId fisso 0x00080381, carica variabili, aggiorna TLT + BootSmart
#elif EDEN
    // Pre-selezione EDEN/Madre, carica variabili, aggiorna Telemetry
#elif EGICON
    // Pre-selezione SPARK/HMI, carica variabili, aggiorna Telemetry
#else
    TelemetryTabRef.UpdateDictionary(Dizionario); // lista vuota
#endif
```

**Scopo:** Ogni device ha un RecipientId/board di default diverso al primo avvio.  
**Refactoring futuro:** Profilo device con `DefaultRecipientId` e `DefaultDevice/Board` in configurazione o device-profile.

---

### 9. `AppLayerDecoded` — visualizzazione dati (riga ~844)

```csharp
#if TOPLIFT
    // (nessun output nel richTextBox per TOPLIFT)
#else
    // Visualizza comando decodificato + variabile
#endif
```

**Scopo:** TOPLIFT non usa il pannello di decodifica applayer generico.  
**Refactoring futuro:** Rendere la visualizzazione configurabile o spostare in tab-specific.

---

## Simboli di preprocessore attivi

| Simbolo | Configurazione build | Scopo |
|---------|---------------------|-------|
| `TOPLIFT` | `TOPLIFT-A2-Debug/Release` | Lettino TOPLIFT A2 |
| `EDEN` | `EDEN-Debug/Release` | Lettino EDEN XP |
| `EGICON` | `EGICON-Debug/Release` | Display EGICON (SPARK) |
| `BUTTONPANEL` | `BUTTONPANEL` | Solo collaudo pulsantiere |
| (nessuno) | `Debug/Release` | Configurazione generica |

---

## Eliminati in questo branch (refactor/source-swap-idictionary-provider)

I seguenti blocchi `#if` sono stati **eliminati** perché unificati da `IDictionaryProvider`:

| Posizione originale | Contenuto | Perché eliminato |
|--------------------|-----------|-----------------|
| Riga ~356-403 | `#if TOPLIFT` caricamento Excel embedded vs file esterno | `LoadProtocolDataAsync` è uguale per tutti |
| Riga ~396-403 | `#if TOPLIFT isStreamBased = true #else false` | `isStreamBased` rimosso |
| Riga ~436 | `hExcel.EstraiDizionario(RecipientId, Dizionario, ExcelfilePath)` (`#if EDEN`) | Sostituito da `LoadVariablesAsync` |
| Riga ~475 | `hExcel.EstraiDizionario(RecipientId, Dizionario, ExcelfilePath)` (`#elif EGICON`) | Sostituito da `LoadVariablesAsync` |
| Riga ~484 | `hExcel.EstraiDizionario(RecipientId, Dizionario)` (`#if TOPLIFT` in board change) | Sostituito da `LoadVariablesAsync` |
| Riga ~488 | `hExcel.EstraiDizionario(RecipientId, Dizionario, ExcelfilePath)` (`#else` in board change) | Sostituito da `LoadVariablesAsync` |
