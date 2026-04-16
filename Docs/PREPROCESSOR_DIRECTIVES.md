# PREPROCESSOR_DIRECTIVES.md — Blocchi `#if` in Form1.cs

**Creato:** 2026-04-16  
**Ultimo aggiornamento:** 2026-04-16  
**Scopo:** Documentare i blocchi `#if` rimasti dopo la migrazione a IDictionaryProvider e la rimozione di ButtonPanel.
I blocchi relativi al caricamento Excel e alla funzionalità ButtonPanel sono stati eliminati (unificati da IDictionaryProvider).
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

### 2. Titolo finestra (riga ~156)

```csharp
#if TOPLIFT
    Text = "STEM Toplift A2 Manager " + Software_Version;
#elif EDEN
    Text = "STEM Eden XP Manager " + Software_Version;
#elif EGICON
    Text = "STEM Spark Manager " + Software_Version;
#else
    this.Text += Software_Version;
#endif
```

**Scopo:** Titolo della finestra principale specifico per device.  
**Refactoring futuro:** Tabella di mapping `symbol → titolo` in configurazione.

---

### 3. `BootTabRef` (riga ~221)

```csharp
#if TOPLIFT

#elif EDEN

#else
    tabControl.TabPages.Add(BootTabRef);
#endif
```

**Scopo:** La tab del bootloader classico è visibile solo su configurazioni non-TOPLIFT e non-EDEN.  
**Refactoring futuro:** Tabella di capacità device.

---

### 4. `BootSmartDevices` (riga ~244)

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

### 5. Configurazione UI per TOPLIFT (riga ~302)

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

### 6. `comboBoxBoard_SelectedIndexChanged` — aggiornamento tab (riga ~474)

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

### 7. `LoadDictionaryDataAsync` — caricamento variabili iniziale (riga ~519)

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

### 8. `AppLayerDecoded` — visualizzazione dati (riga ~839)

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
| (nessuno) | `Debug/Release` | Configurazione generica |

---

## Eliminati durante la modernizzazione

I seguenti blocchi `#if` sono stati **eliminati** perché unificati da `IDictionaryProvider` o rimossi con la funzionalità ButtonPanel:

| Periodo | Contenuto eliminato | Perché |
|--------|-------------------|--------|
| Branch 2 (refactor/source-swap-idictionary-provider) | 6 blocchi `#if TOPLIFT/#else` caricamento Excel | `LoadProtocolDataAsync`/`LoadVariablesAsync` unificano il caricamento per tutti i device |
| Refactor ButtonPanel (main branch) | `#if BUTTONPANEL` titolo, rimozione tab, rimozione dizionari | Modulo ButtonPanel completamente rimosso dal repository |
| Refactor ButtonPanel (main branch) | `#if BUTTONPANEL` caricamento variabili smart-devices | Integrazione con ButtonPanelTestService rimossa |

---

## Strategia di refactoring (Fase 3)

I blocchi `#if` rimanenti (1-8) saranno progressivamente eliminati mediante:

1. **Device Profile Manager** — Configurazione centralizzata (file JSON o API) con metadati device:
   - CommunicationPort (can/ble)
   - Titolo applicazione
   - Tab visibili
   - Dispositivi per bootloader smart
   - RecipientId di default

2. **Factory pattern** — Creazione UI dinamica in base al profilo device

3. **Feature flags** — Migrazione da `#if` a `FeatureFlags` in configurazione (più flessibile e testabile)

4. **Interfacce comuni** — `ITelemetryTab`, `IBootTab` per rendere l'UI modulare e device-agnostica
