# REFACTOR_ANALYSIS.md — Analisi per Fase 3: Disaccoppiamento Form1 e migrazione a IDictionaryProvider

**Creato:** 2026-04-14  
**Stato:** Step 3.5 — Formalize (in corso)

---

## 1. Mappa accoppiamento ExcelHandler

### Tipi ExcelHandler e dove sono usati

| Tipo | Campi | Equivalente Core.Models |
|------|-------|------------------------|
| `ExcelHandler.RowData` | Macchina, Scheda, Indirizzo | `ProtocolAddress` (DeviceName, BoardName, Address) |
| `ExcelHandler.CommandData` | Name, CmdH, CmdL | `Command` (Name, CodeHigh, CodeLow) |
| `ExcelHandler.VariableData` | Name, AddrH, AddrL, DataType | `Variable` (Name, AddressHigh, AddressLow, DataType) |

### Consumer per file

| File | Tipo usato | Come | Punti di contatto |
|------|-----------|------|-------------------|
| **Form1.cs** | RowData, CommandData, VariableData | Campi istanza (`List<T>`), `using static ExcelHandler` | ~20 |
| **Form1.cs → AppLayerDecoderEventArgs** | CommandData, VariableData | Proprietà dell'EventArgs (attraversa eventi) | 3 |
| **TelemetryManager.cs** | VariableData | Campo `List<VariableData>`, parametri metodi, iterazione nel protocollo | 6 |
| **Telemetry_WF_Tab.cs** | VariableData | Campo `List<VariableData>`, metodo `UpdateDictionary()` | 2 |
| **TopLift_Telemetry_WF_Tab.cs** | VariableData | Campo `List<VariableData>`, metodo `UpdateDictionary()` | 2 |
| **Boot_Smart_WF_Tab.cs** | VariableData | Campo `List<VariableData>`, metodo `UpdateDictionary()` | 2 |

### Flusso dati attuale

```
Form1 constructor:
  hExcel = new ExcelHandler(stream)
  hExcel.EstraiDatiProtocollo(IndirizziProtocollo, Comandi, Dizionario)
  hExcel.EstraiDizionario(RecipientId, Dizionario)
    → TLTTabRef.UpdateDictionary(Dizionario)      // passa List<VariableData>
    → BootSmartTabRef.UpdateDictionary(Dizionario) // passa List<VariableData>
    → TelemetryTabRef.UpdateDictionary(Dizionario) // passa List<VariableData>

Form1.comboBoxBoard_SelectedIndexChanged:
  RecipientId = (parsed from IndirizziProtocollo)
  hExcel.EstraiDizionario(RecipientId, Dizionario)
    → Tab.UpdateDictionary(Dizionario)

Form1.onAppLayerPacketReady:
  Cerca in IndirizziProtocollo per risolvere nomi macchina/scheda da indirizzo
  Cerca in Comandi per decodificare comando da payload
  Cerca in Dizionario per decodificare variabile (se "Leggi variabile logica risposta")
  → Emette evento AppLayerCommandDecoded con CommandData + VariableData

TelemetryManager:
  Riceve List<VariableData> tramite AddToDictionary()
  Usa AddrH/AddrL per costruire payload protocollo (Convert.ToByte hex)
  Usa DataType per interpretare dimensione dati ricevuti
  Usa Name per visualizzazione
```

---

## 2. Accoppiamento critico: VariableData nel protocollo

`VariableData` NON è solo un DTO per visualizzazione — **è usato attivamente nel protocollo**:
- `AddrH`/`AddrL` → convertiti in byte e messi nel payload (`Convert.ToByte(data.AddrH, 16)`)
- `DataType` → determina quanti byte leggere dalla risposta (`uint8_t`=1, `uint16_t`=2, `uint32_t`=4)
- Questo significa che il tipo di dato **deve essere compatibile col livello protocollo**

### Implicazione per il refactor
Il tipo `Core.Models.Variable` ha gli stessi campi (Name, AddressHigh, AddressLow, DataType) come stringhe.
La conversione `ExcelHandler.VariableData → Core.Models.Variable` è **1:1 campo per campo** (confermato nei cross-reference test).
Quindi sostituire il tipo è meccanicamente semplice, ma va fatto **ovunque contemporaneamente** nel flusso dati.

---

## 3. Confini di sostituzione

### Livello 1: Tipi (più semplice)
Sostituire `ExcelHandler.VariableData` con `Core.Models.Variable` ovunque.
Sostituire `ExcelHandler.RowData` con `Core.Models.ProtocolAddress` ovunque.
Sostituire `ExcelHandler.CommandData` con `Core.Models.Command` ovunque.

**Quantità:** ~35 punti nel codice produzione (Form1 + Tab + TelemetryManager)

### Livello 2: Sorgente dati (medio)
Sostituire `hExcel.EstraiDatiProtocollo(...)` con `IDictionaryProvider.LoadProtocolDataAsync(...)`
Sostituire `hExcel.EstraiDizionario(...)` con `IDictionaryProvider.LoadVariablesAsync(...)`

**Complessità:** Sync → Async. Form1 constructor è sync.
I metodi ExcelHandler mutano liste passate per riferimento. IDictionaryProvider restituisce IReadOnlyList.

### Livello 3: Eliminazione ExcelHandler (finale)
Rimuovere ExcelHandler.cs, rimuovere `using static ExcelHandler`.

---

## 4. Strategia possibile: "Big Bang" vs "Strangler Fig"

### Big Bang
- Sostituisci tutti i tipi e la sorgente dati in un singolo branch
- Pro: un solo passaggio, nessun codice duplicato
- Contro: molte righe toccate (~35+), rischio regressione alto

### Strangler Fig (progressivo)
1. **Branch A:** Aliasare i tipi — creare `using VariableData = Core.Models.Variable` etc.
   - Problema: nomi proprietà diversi (Macchina vs DeviceName, CmdH vs CodeHigh)
2. **Branch A (alternativo):** Aggiungere operatori di conversione implicita
   - Problema: overengineering
3. **Branch A (pragmatico):** Rinominare proprietà in Core.Models per matchare ExcelHandler?
   - Problema: contamina i modelli puliti

### Proposta: "Type Swap" in 2 branch
1. **Branch 1:** Sostituisci i tipi (VariableData → Variable, etc.) in TUTTO il flusso
   - Tocca ~35 punti ma è meccanico (rename proprietà)
   - ExcelHandler.EstraiDizionario resta, ma i risultati vengono convertiti in Core.Models
   - Oppure: ExcelHandler diventa un wrapper che chiama IDictionaryProvider sync
2. **Branch 2:** Sostituisci la sorgente (ExcelHandler → IDictionaryProvider)
   - A questo punto i tipi sono già quelli giusti
   - Basta cambiare chi popola le liste

---

## 5. Libreria NuGet protocollo (futuro)

L'utente ha menzionato una libreria NuGet per il protocollo STEM.
Implicazioni per il refactor:
- Il protocollo attuale (`ProtocolManager`, `NetworkLayer`, `PacketManager`, `TelemetryManager`)
  verrà eventualmente sostituito dalla libreria
- `TelemetryManager` è il consumer più accoppiato a `VariableData` nel protocollo
- Se la libreria definirà i propri tipi, ci sarà un SECONDO swap di tipi
- **Conclusione:** vale la pena creare un'astrazione intermedia tra "dati dizionario" e "protocollo"?
  Oppure è overengineering visto che la libreria non è pronta?

---

## 6. Dominio formale (mancante)

Il progetto Dictionaries.Manager ha una spec Lean 4 completa.
Questo progetto non ne ha una. Elementi da formalizzare:
- **RecipientId** → uint, indirizzo protocollo hex della board target
- **Dizionario** → lista variabili filtrate per RecipientId
- **Comandi** → lista comandi protocollo (globali, non per-board)
- **Indirizzi protocollo** → mappa device/board → indirizzo hex
- **Flusso selezione:** Macchina → Scheda → Indirizzo → Dizionario variabili
- **Flusso protocollo:** Variable.AddrH/AddrL → payload byte[2..3], Variable.DataType → dimensione risposta
- **Flusso telemetria:** Lista variabili → configurazione telemetria veloce → interpretazione dati binari

---

## 7. Dubbi aperti (per Step 2: Confront)

### RISPOSTE UTENTE (Confront session):

1. **Nomi proprietà:** → **Adattare Form1 ai nuovi nomi**. Disaccoppiare dove si può.

2. **Sync vs Async:** → **Investigare se si può switchare ad async.**
   **Risultato indagine:** Form1 constructor è sync (righe 147-519). Non si può rendere
   async un costruttore. Ma Program.cs crea Form1 con `new Form1(serviceProvider)` e poi
   `Application.Run(mainForm)`. Inoltre c'è un `mainForm.Load += (s,e) => splash.Close()`.
   **Opzioni concrete:**
   - **A)** Estrarre il caricamento dati dal constructor → metodo `async Task LoadDictionaryDataAsync()`
     chiamato nell'evento `Load` o `Shown` (async void event handler, accettabile in WinForms)
   - **B)** Nel constructor fare `provider.LoadProtocolDataAsync().GetAwaiter().GetResult()`
     (blocca il thread UI ma succede durante splash screen, utente non se ne accorge)
   - **Raccomandazione:** Opzione A è più pulita e futura-proof per la libreria NuGet protocollo.

3. **Mutabilità liste:** → **Sì, convertire ai nuovi tipi** (IReadOnlyList<T>).

4. **Libreria NuGet protocollo:** → **Non fare nulla ora.** Documentare l'esistenza in README.
   La libreria un giorno fornirà i metodi per la comunicazione.

5. **Scope:** → **Disaccoppiare TUTTO quello che si può.** Sfruttare il disaccoppiamento
   solo per Dictionary adesso, ma in futuro gli elementi disaccoppiati serviranno
   per migliorare altre cose. Architettura con best practices.

6. **Formalizzazione:** → **Formalizzare tutto quello che si può.**

7. **`#if TOPLIFT` / `#if EDEN`:** → **Non è un obiettivo del branch.**
   I blocchi `#if` che collassano naturalmente durante la sostituzione tipi/sorgente
   vengono eliminati. Gli altri rimangono invariati e vengono documentati in un
   file dedicato (`Docs/PREPROCESSOR_DIRECTIVES.md`) per refactoring futuro.

8. **AppLayerDecoderEventArgs:** → **Se va fatto, va fatto.** Cambiare i tipi nell'EventArgs.

---

## Appendice: Form1 — Campi ExcelHandler

```csharp
// Form1.cs righe 81-88
string ExcelfilePath = "Dizionari STEM.xlsx";
List<ExcelHandler.RowData> IndirizziProtocollo;
List<ExcelHandler.CommandData> Comandi;
List<ExcelHandler.VariableData> Dizionario;
ExcelHandler hExcel;
private bool isStreamBased;
```

## Appendice: Form1 — Preprocessor directives e caricamento Excel

17 blocchi `#if` in Form1.cs. I principali per il dizionario:

```
Riga 358-405: Caricamento Excel
  #if TOPLIFT → embedded stream, RecipientId fisso 0x00080381, passa a TLT+BootSmart
  #else       → file esterno, niente RecipientId iniziale, passa a Telemetry

Riga 426-504: Pre-selezione device
  #if EDEN    → macchina="EDEN", scheda="Madre", EstraiDizionario dal file
  #elif EGICON → macchina="SPARK", scheda="HMI", EstraiDizionario dal file

Riga 640-651: Cambio board
  #if TOPLIFT → EstraiDizionario da stream, aggiorna TLT
  #else       → EstraiDizionario da file, aggiorna Telemetry
```

**Implicazione:** Il refactor a IDictionaryProvider UNIFICA i due percorsi
(stream vs file) — `LoadVariablesAsync(recipientId)` è uguale per tutti.
Le `#if` si semplificheranno naturalmente.

---

## 8. Strategy — Piano di esecuzione (Step 3)

### Due branch sequenziali

#### Branch 1: `refactor/type-swap-core-models`
**Obiettivo:** sostituire i tipi interni di ExcelHandler con i modelli Core ovunque nel codice produzione.
ExcelHandler continua ad essere usato come sorgente dati, ma smette di esporre i propri tipi all'esterno.

**Approccio:** modificare ExcelHandler per restituire direttamente `Core.Models.*` invece dei propri inner class.
Questo elimina la necessità di conversioni nei consumer e rende ExcelHandler un adapter però ora sono sul main.

| File | Modifica |
|------|----------|
| `App/ExcelHandler.cs` | Restituire `List<ProtocolAddress>`, `List<Command>`, `List<Variable>` invece di `RowData/CommandData/VariableData`. Eliminare le inner class. |
| `App/Form1.cs` | Cambiare dichiarazioni campi + `AppLayerDecoderEventArgs`. Rename proprietà: `Macchina→DeviceName`, `CmdH→CodeHigh`, `AddrH→AddressHigh` ecc. |
| `App/TelemetryManager.cs` | Cambiare tipo campo `TelemetryDictionary` e ogni uso di `AddrH/AddrL/DataType`. |
| `App/Telemetry_WF_Tab.cs` | Cambiare tipo `UpdateDictionary(List<Variable>)`. |
| `App/TopLift_Telemetry_WF_Tab.cs` | Cambiare tipo `UpdateDictionary(List<Variable>)`. |
| `App/Boot_Smart_WF_Tab.cs` | Cambiare tipo `UpdateDictionary(List<Variable>)`. |

**Test:** unit test che verificano la conversione proprietà (es. `AddrH="80"` → `AddressHigh="80"`).

#### Branch 2: `refactor/source-swap-idictionary-provider`
**Obiettivo:** sostituire ExcelHandler come sorgente dati con `IDictionaryProvider`.
A questo punto i tipi sono già quelli giusti — basta cambiare chi popola le liste.

| File | Modifica |
|------|----------|
| `App/Form1.cs` | Iniettare `IDictionaryProvider` via constructor. Estrarre caricamento dal constructor → `async Task LoadDictionaryDataAsync(CancellationToken ct)`. Chiamarlo nell'evento `Load` (async void, accettabile in WinForms). Eliminare `hExcel`, `ExcelfilePath`, `isStreamBased`. |
| `App/Form1.cs` | Sostituire `comboBoxBoard_SelectedIndexChanged` sync → async, chiamare `LoadVariablesAsync`. |
| `App/Form1.cs` | Eliminare i blocchi `#if TOPLIFT / #else` relativi al caricamento (vengono unificati da `IDictionaryProvider`). Documentare i `#if` rimasti. |

**Output aggiuntivo:** `Docs/PREPROCESSOR_DIRECTIVES.md` con i blocchi `#if` non eliminati.

**Test:** integration test che verificano il caricamento tramite `IDictionaryProvider` con mock.

---

## 9. Formalize — Specifica Lean 4 (Step 3.5)

### Lean 4 Specification v1 — STEM Device Manager Dictionary Domain

```lean
-- ============================================================================
-- STEM DEVICE MANAGER — Dictionary Domain — Lean 4 Specification v1
-- v1: SESSION_006 (Fase 3 — disaccoppiamento Form1, migrazione IDictionaryProvider)
-- ============================================================================

-- === INDIRIZZI PROTOCOLLO ===

-- Indirizzo di una board nel protocollo STEM.
-- Address è una stringa hex a 8 cifre (es. "00080381").
structure ProtocolAddress where
  deviceName : String      -- nome macchina (es. "TOPLIFT A2")
  boardName  : String      -- nome scheda (es. "Madre")
  address    : String      -- indirizzo hex completo (es. "00080381")

-- RecipientId: indirizzo board target come UInt32.
-- Derivato da ProtocolAddress.address via parsing hex.
-- Usato come chiave per filtrare le variabili di una board specifica.
def RecipientId := UInt32

-- BR-D001: Unicità indirizzo nell'elenco
def hasUniqueAddresses (addrs : List ProtocolAddress) : Bool :=
  let keys := addrs.map (·.address)
  keys.length == keys.eraseDups.length

-- Risoluzione RecipientId da macchina+scheda
def resolveRecipientId
    (deviceName boardName : String)
    (addrs : List ProtocolAddress) : Option RecipientId :=
  match addrs.find? (fun a => a.deviceName == deviceName && a.boardName == boardName) with
  | none   => none
  | some a => some (UInt32.ofNat (Nat.fromDigits 16 (a.address.toList.map Char.digitToNat)))

-- === COMANDI ===

-- Comando protocollo STEM.
-- CodeHigh e CodeLow sono stringhe hex a 2 cifre (es. "00", "01").
structure Command where
  name     : String   -- nome leggibile (es. "Leggi variabile logica")
  codeHigh : String   -- byte alto comando hex (es. "00")
  codeLow  : String   -- byte basso comando hex (es. "01")

-- Ricerca comando per codici hex ricevuti dal protocollo
def findCommand (codeH codeL : UInt8) (commands : List Command) : Option Command :=
  commands.find? (fun c =>
    c.codeHigh == (Nat.toDigits 16 codeH.toNat |>.toString) &&
    c.codeLow  == (Nat.toDigits 16 codeL.toNat |>.toString))

-- === VARIABILI DIZIONARIO ===

-- Variabile dizionario STEM.
-- AddressHigh e AddressLow sono stringhe hex a 2 cifre (es. "80", "01").
-- DataType è una stringa che determina la dimensione della risposta protocollo.
structure Variable where
  name        : String   -- nome variabile (es. "Velocità motore")
  addressHigh : String   -- byte alto indirizzo hex (es. "80")
  addressLow  : String   -- byte basso indirizzo hex (es. "01")
  dataType    : String   -- tipo dato ("uint8_t" | "uint16_t" | "uint32_t" | "float" | ...)

-- BR-D002: Unicità indirizzo nel dizionario
def hasUniqueVariableAddresses (vars : List Variable) : Bool :=
  let keys := vars.map (fun v => (v.addressHigh, v.addressLow))
  keys.length == keys.eraseDups.length

-- Encoding per payload protocollo (telemetria veloce)
-- AddrH/AddrL come stringhe hex → byte da inserire nel payload
def Variable.addrHighByte (v : Variable) : UInt8 :=
  UInt8.ofNat (Nat.fromDigits 16 (v.addressHigh.toList.map Char.digitToNat))

def Variable.addrLowByte (v : Variable) : UInt8 :=
  UInt8.ofNat (Nat.fromDigits 16 (v.addressLow.toList.map Char.digitToNat))

-- BR-D003: Dimensione risposta in byte determinata dal DataType
-- Usata da TelemetryManager per leggere il numero corretto di byte dalla risposta
inductive DataTypeSize where
  | one   -- uint8_t
  | two   -- uint16_t
  | four  -- uint32_t, float

def Variable.responseByteCount (v : Variable) : DataTypeSize :=
  match v.dataType with
  | "uint8_t"  => DataTypeSize.one
  | "uint16_t" => DataTypeSize.two
  | "uint32_t" => DataTypeSize.four
  | "float"    => DataTypeSize.four
  | _          => DataTypeSize.one   -- fallback conservativo

-- Ricerca variabile per indirizzo ricevuto dal protocollo
def findVariable (addrH addrL : UInt8) (vars : List Variable) : Option Variable :=
  vars.find? (fun v =>
    v.addrHighByte == addrH && v.addrLowByte == addrL)

-- === DATI PROTOCOLLO (DictionaryData) ===

-- Bundle di dati restituito da IDictionaryProvider.LoadProtocolDataAsync.
structure DictionaryData where
  addresses : List ProtocolAddress   -- mappa device/board → indirizzo
  commands  : List Command           -- comandi globali (non per-board)

-- === FLUSSO SELEZIONE DIZIONARIO ===

-- F1: Selezione macchina → elenco schede disponibili
def boardsForDevice (deviceName : String) (addrs : List ProtocolAddress) : List String :=
  addrs.filter (fun a => a.deviceName == deviceName) |>.map (·.boardName)

-- F2: Selezione scheda → RecipientId → caricamento variabili
-- (loadVariables : RecipientId → IO (List Variable)) rappresenta IDictionaryProvider.LoadVariablesAsync

-- F3: Ricezione pacchetto → decodifica
-- dato un payload protocollo con codeH/codeL byte 0/1 e addrH/addrL byte 2/3:
-- 1. findCommand codeH codeL commands → Command corrente
-- 2. findVariable addrH addrL variables → Variable corrente (se "leggi variabile logica risposta")
-- 3. Variable.responseByteCount → numero byte da leggere dalla risposta
```

### Traduzione italiana

1. **ProtocolAddress** — indirizzo di una board nel protocollo STEM. `Address` è la stringa hex completa (8 cifre) che identifica la board nella rete.
2. **RecipientId** — alias `UInt32`, rappresenta l'indirizzo della board target per il filtraggio del dizionario.
3. **Command** — comando protocollo con codice a 2 byte (hex string). Globale, non legato a una board specifica.
4. **Variable** — variabile dizionario con indirizzo a 2 byte (hex string) e tipo dato. Il `DataType` determina quanti byte leggere dalla risposta protocollo (BR-D003).
5. **DictionaryData** — bundle restituito da `LoadProtocolDataAsync`: indirizzi protocollo + comandi globali.
6. **Flusso F1-F2**: selezione macchina → schede → `RecipientId` → `LoadVariablesAsync(recipientId)`.
7. **Flusso F3**: decodifica pacchetto in arrivo tramite lookup su `commands` e `variables`.

### Regole di business

| ID | Regola | Vincolo |
|----|--------|---------|
| BR-D001 | Unicità indirizzi nell'elenco protocollo | Nessun duplicato su `ProtocolAddress.address` |
| BR-D002 | Unicità indirizzi nel dizionario variabili | Nessun duplicato su `(AddressHigh, AddressLow)` per RecipientId |
| BR-D003 | Dimensione risposta da DataType | `uint8_t`=1, `uint16_t`=2, `uint32_t`/`float`=4 byte, default=1 |

---

## Appendice: Form1 — EventArgs con tipi ExcelHandler

```csharp
// Form1.cs righe 115-131
public class AppLayerDecoderEventArgs : EventArgs
{
    public byte[] Payload { get; }
    public ExcelHandler.CommandData CurrentCommand { get; }
    public string MachineName { get; }
    public string MachineNameRecipient { get; }
    public ExcelHandler.VariableData CurrentVariable { get; }
}
```

## Appendice: TelemetryManager — Uso VariableData nel protocollo

```csharp
// TelemetryManager.cs righe 258-262 (costruzione payload telemetria veloce)
for (int i = 0; i < TelemetryDictionary.Count; i++)
{
    Payload[15 + (i * 2)] = Convert.ToByte(TelemetryDictionary[i].AddrH, 16);
    Payload[16 + (i * 2)] = Convert.ToByte(TelemetryDictionary[i].AddrL, 16);
}

// TelemetryManager.cs righe 141-144 (decodifica risposta telemetria lenta)
foreach (ExcelHandler.VariableData data in TelemetryDictionary)
{
    if ((int.Parse(data.AddrH, NumberStyles.HexNumber) == payload[2]) &&
        (int.Parse(data.AddrL, NumberStyles.HexNumber) == payload[3]))
```
