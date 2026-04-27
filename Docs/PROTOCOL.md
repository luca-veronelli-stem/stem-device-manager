# Protocollo STEM

Documentazione del protocollo di comunicazione proprietario STEM, estratta dallo
stack attuale in `GUI.Windows/STEMProtocol/` (PacketManager 625 LOC, STEM_protocol 858
LOC, BootManager 371 LOC, TelemetryManager 422 LOC, CanDataLayer 142 LOC,
SerialDataLayer 150 LOC, SPRollingCode 19 LOC — totale 2587 LOC).

**Autore originale:** Umberto (non più raggiungibile). Implementazione C#:
Michele Pignedoli / Paperbat.

**Stato:** documentazione descrittiva del comportamento attuale, base per la
migrazione a `Stem.Communication` NuGet prevista in Fase 4.

---

## 1. Architettura a livelli

Tre livelli annidati, costruiti per composizione:

```
NetworkLayer     [NetInfo(2) + recipientId(4) + chunk] × N  ← wire
   ↓ unwraps
TransportLayer   [cryptFlag(1) | senderId_BE(4) | lPack_BE(2) | AL | CRC_BE(2)]
   ↓ unwraps
ApplicationLayer [cmdInit(1) | cmdOpt(1) | payload]
```

L'Application Layer è l'unità logica (un comando + un payload). Il Transport
Layer aggiunge identificazione del mittente e integrità (CRC16 Modbus). Il
Network Layer divide il pacchetto completo in chunk da inviare sul canale
fisico (CAN / BLE / Serial).

---

## 2. Application Layer (AL)

Struttura: `[cmdInit(1) | cmdOpt(1) | payload(N)]`

- `cmdInit` = byte alto del codice comando (codeHigh in dizionario)
- `cmdOpt` = byte basso del codice comando (codeLow in dizionario)
- `payload` = dati specifici del comando

Il codice a 16 bit `(cmdInit << 8) | cmdOpt` identifica univocamente l'operazione.

### 2.1 Codici comando noti

Estratti da `BootManager.cs` e `TelemetryManager.cs`:

| Comando | Codice (hex) | Scopo |
|---------|--------------|-------|
| `CMD_READ_VARIABLE` | `0x0001` | Lettura variabile singola (telemetria lenta) |
| `CMD_WRITE_VARIABLE` | `0x0002` | Scrittura variabile singola |
| `CMD_START_PROCEDURE` | `0x0005` | Avvio procedura bootloader |
| `CMD_END_PROCEDURE` | `0x0006` | Fine procedura bootloader |
| `CMD_PROGRAM_BLOCK` | `0x0007` | Blocco da 1024 B del firmware |
| `CMD_RESTART_MACHINE` | `0x000A` | Reboot dispositivo |
| `CMD_CONFIGURE_TELEMETRY` | `0x0015` | Configurazione telemetria veloce |
| `CMD_START_TELEMETRY` | `0x0016` | Avvio telemetria veloce |
| `CMD_STOP_TELEMETRY` | `0x0017` | Stop telemetria veloce |
| `CMD_TELEMETRY_DATA` | `0x0018` | Dato periodico della telemetria veloce |

Il dizionario applicativo (`GUI.Windows/Resources/Dizionari STEM.xlsx` + API) contiene
l'elenco completo con descrizioni.

### 2.2 Flag di risposta

Il bit più alto del `cmdInit` indica che il pacchetto è una **risposta** a un
comando inviato. Convenzione letta in `PacketDecoder.IsReadOrWriteVariable`:

- Request: `cmdInit` in `{0x00}` per `CMD_READ_VARIABLE` / `CMD_WRITE_VARIABLE`
- Response: `cmdInit` in `{0x80}` per le rispettive risposte (bit 7 acceso)

Estendibile agli altri comandi (es. `0x80 | 0x05` = risposta a `START_PROCEDURE`).

### 2.3 Variabili: indirizzo a 2 byte nel payload

Per `CMD_READ_VARIABLE` / `CMD_WRITE_VARIABLE` il payload inizia con l'indirizzo
della variabile:

```
byte 0 : addressHigh
byte 1 : addressLow
byte 2..N : (solo write) dato, lunghezza dipende dal DataType della variabile
```

`PacketDecoder.ResolveVariable` ricostruisce la variabile cercando nel
dizionario tramite `(addressHigh, addressLow)`.

---

## 3. Transport Layer (TL)

Struttura on-wire:

```
byte 0       : cryptFlag (sempre 0x00 in Fase 2 — crittografia non implementata)
byte 1..4    : senderId (4 byte, big-endian sul wire — vedi nota sotto)
byte 5..6    : lPack (2 byte, big-endian) — lunghezza dell'AL packet
byte 7..N    : ApplicationLayer packet
byte N+1..N+2: CRC16 Modbus (2 byte, big-endian)
```

`lPack` = `length(AL packet)` = `length(payload) + 2` (i 2 byte di cmdInit/cmdOpt).

### 3.1 Endianness del senderId

Il senderId viaggia sul wire in **big-endian** (MSB per primo): sia la TX
in `ProtocolService.BuildTransportPacket` sia la RX in `PacketDecoder` usano
la convention BE. Il firmware device fa lo stesso in spedizione.

```csharp
// TX (BuildTransportPacket):
packet[1] = (byte)((senderId >> 24) & 0xFF);
packet[2] = (byte)((senderId >> 16) & 0xFF);
packet[3] = (byte)((senderId >>  8) & 0xFF);
packet[4] = (byte)( senderId        & 0xFF);

// RX (PacketDecoder.ReadSenderIdBigEndian):
return ((uint)payload[1] << 24)
     | ((uint)payload[2] << 16)
     | ((uint)payload[3] << 8)
     | payload[4];
```

Il dizionario (API Azure / Excel) contiene gli indirizzi nel formato standard
(es. `ProtocolAddress.Address = "0x000A0441"` per EDEN Madre): `FindSender`
fa il lookup diretto contro il valore letto in BE.

**Nota storica:** nel legacy `TransportLayer.BuildTransportHeader` scriveva
con `Array.Reverse` sui byte già LE di `BitConverter.GetBytes` (quindi BE
sul wire), ma `NetworkLayer.SP_PacketReady` leggeva **gli stessi byte come
little-endian** — il senderId estratto risultava byte-swappato rispetto
all'originale (es. `0x000A0441` sul wire → `0x41040A00` letto). Funzionava
perché il dizionario dell'epoca conteneva gli address byte-swappati.
Con la migrazione del dizionario al formato standard (API + Excel rivisti),
il decoder è stato allineato a leggere BE — parità end-to-end ripristinata.

### 3.2 CRC16 Modbus

Algoritmo standard Modbus:

- **Init:** `0xFFFF`
- **Polynomial:** `0xA001` (reversed di `0x8005`)
- **Loop bit-by-bit:** per ogni byte XOR con `crc`, poi 8 shift-right; se il
  bit scartato era 1, XOR con `0xA001` dopo lo shift.

Implementazione attuale in `TransportLayer.Crc16`:

```csharp
ushort crc = 0xFFFF;
foreach (byte b in data)
{
    crc ^= b;
    for (int i = 0; i < 8; i++)
    {
        if ((crc & 0x0001) != 0) { crc >>= 1; crc ^= 0xA001; }
        else { crc >>= 1; }
    }
}
return crc;
```

Applicato su `[TL_header + AL_packet]` (esclusi i 2 byte di CRC stessi).
Output scritto on-wire **big-endian** (reverse di `BitConverter.GetBytes`).

**Validazione in ricezione:** **non eseguita** in Fase 2. `SP_PacketReady`
scarta semplicemente gli ultimi 2 byte (`ApplicationPacket.Take(length - 2)`)
e non confronta il CRC con uno calcolato. Il nostro `PacketDecoder` replica
lo stesso comportamento (known gap #1 della spec Fase 2, rivalutare in
Fase 4).

---

## 4. Network Layer (NL) e chunking

Il pacchetto TL (fino a ~2 KB per BLE/Serial, limitato dal buffer applicativo)
viene diviso in chunk che rispettano il limite del canale fisico. Ogni chunk è
prefissato da `NetInfo(2) + recipientId(4)` per CAN, o dallo stesso per
BLE/Serial ma con layout diverso di unpacking (vedi §6).

### 4.1 NetInfo (16 bit, little-endian sul wire)

Layout dei 2 byte (MSB = bit 15, LSB = bit 0):

```
bit 15..6 : remainingChunks (10 bit)
bit 5     : setLength (1 bit)
bit 4..2  : packetId       (3 bit)
bit 1..0  : version        (2 bit)
```

- **`remainingChunks`** = numero di chunk ancora da ricevere dopo questo
  (0 = ultimo chunk → pacchetto completo)
- **`setLength`** = 1 nel primo chunk di un pacchetto, 0 nei successivi
- **`packetId`** = rolling code 1..7, identifica il pacchetto applicativo in
  corso quando più pacchetti sono interleaved
- **`version`** = versione protocollo, attualmente `1`

Codifica in `NetworkLayer.BuildNetInfo`:

```csharp
int netInfo = (remainingChunks << 6) | (setLength << 5)
            | (packetId << 2) | version;
_netInfo = BitConverter.GetBytes((ushort)netInfo);  // LE
```

Decodifica in `PacketManager.ProcessPacket`:

```csharp
var remainingChunks = (netInfo >> 6) & 0x3FF;
var setLength      = (netInfo >> 5) & 0x01;
var packetId       = (netInfo >> 2) & 0x07;
var version        = netInfo & 0x03;
```

### 4.2 Rolling code `packetId`

Generato da `RollingCodeGenerator.GetIndex` (thread-safe via
`Interlocked.CompareExchange`):

```
1 → 2 → 3 → 4 → 5 → 6 → 7 → 1 → 2 → ...
```

Scopo: se due pacchetti applicativi sono in trasmissione contemporanea (es.
risposta inattesa durante un upload firmware), il receiver può buffer-arli
separatamente senza mescolare i chunk. `PacketManager.packetQueues` è un
`Dictionary<int, List<byte[]>>` indicizzato da `packetId`.

### 4.3 Chunk size per canale

Definito in `NetworkLayer.SetPacketChunkSize`:

| Canale | Chunk size (byte dati) | Wire frame |
|--------|------------------------|------------|
| CAN    | 6                      | 8 = NetInfo(2) + chunk(6) + arbId(29-bit, fuori dati) |
| BLE    | 98                     | 104 = NetInfo(2) + recipientId(4) + chunk(98) |
| Serial | 98                     | 104 = NetInfo(2) + recipientId(4) + chunk(98) |

Il CAN usa l'`ArbitrationId` extended del frame per il recipientId (non nei
dati). BLE/Serial includono il recipientId nei dati perché non hanno un
header hardware equivalente.

### 4.4 Riassembly

`PacketManager.ProcessPacket` (righe 76-145):

1. Estrae NetInfo dai primi 2 byte del chunk
2. Append di `chunk[2..]` a `packetQueues[packetId]` (lock-protected)
3. Se `remainingChunks == 0`:
   - Concatena tutti i chunk bufferizzati per quel `packetId`
   - Rimuove l'entry da `packetQueues`
   - Passa il payload unificato a `NetworkLayer` per decode
   - Filtro: `unifiedPacket.Length > 7` (scarta pacchetti troppo corti per
     essere TL validi)

**Known gap #2:** nessun timeout sui buffer incompleti. Un pacchetto che non
arriva mai (chunk perso) resta in memoria indefinitamente. Valutare TTL in
Fase 3.

---

## 5. Pipeline di invio (encode)

Esempio da `ProtocolManager.HandleSendCanCommandAsync`:

```csharp
// 1. Prepara payload applicativo
byte cmdInit = (byte)(command >> 8);
byte cmdOpt  = (byte)(command);
byte[] appPayload = {cmdInit, cmdOpt, ...userPayload};

// 2. Costruisci NetworkLayer (incapsula TL + AL)
var nl = new NetworkLayer(
    interfaceType: "can",
    version: 1,
    recipientId: targetAddress,
    data: new byte[] {
        cryptFlag,                          // byte 0
        senderIdLE_b0, b1, b2, b3,          // byte 1..4 (verrà byte-swapped dal TL)
        0, 0,                               // byte 5..6 (placeholder lPack, ricalcolato)
        cmdInit, cmdOpt,                    // byte 7..8
        ...userPayload                      // byte 9..
    });

// 3. Ottieni chunk pronti da spedire
var chunks = nl.NetworkPackets;  // List<Tuple<NetInfo, recipientId, chunkData>>

// 4. Invia chunk sul canale fisico
foreach (var (netInfo, rid, chunk) in chunks)
{
    if (interfaceType == "can")
    {
        // ArbitrationId = recipientId; Data = [NetInfo + chunk]
        await can.SendMessageAsync(rid, netInfo.Concat(chunk).ToArray(), isExtended: true);
    }
    else
    {
        // BLE/Serial: frame = [NetInfo + recipientId_LE + chunk]
        var frame = netInfo.Concat(BitConverter.GetBytes(rid)).Concat(chunk).ToArray();
        await bleOrSerial.SendMessageAsync(frame);
    }
}
```

---

## 6. Pipeline di ricezione (decode)

### 6.1 CAN (in `PacketManager.ProcessCANPacket`)

Ogni frame CAN ricevuto ha `ArbitrationId` + `Data` (fino a 8 byte).

1. Filtro per ArbitrationId: processa solo se `ArbId == self._id` oppure in
   modalità sniffer (`self._id == 0xFFFFFFFF`)
2. Frame valido: `Data` contiene `[NetInfo(2) + chunk(≤6)]` — passa a
   `ProcessPacket("can", Data)`

Nota: nel wire CAN il `recipientId` non è nei dati (sta nell'arbitration ID
del frame), quindi `ProcessPacket` riceve **direttamente** `[NetInfo + chunk]`.

### 6.2 BLE / Serial (in `PacketManager.ProcessBLE/SerialPacket`)

Frame BLE/Serial contiene `[NetInfo(2) + senderId_in_frame(4) + chunk(≤98) + trailer(4)]`.

Il trattamento prima di `ProcessPacket`:

```csharp
// 1. Rimuovi i 4 byte di senderId (dopo NetInfo):
//    copia byte[6..] → byte[2..]
Array.Copy(packet.Data, 6, packet.Data, 2, packet.Data.Length - 6);

// 2. Rimuovi gli ultimi 4 byte (trailer):
packet.Data = packet.Data.Take(packet.Data.Length - 4).ToArray();

// 3. Ora Data ha lo stesso layout del CAN: [NetInfo(2) + chunk]
ProcessPacket("ble"/"serial", packet.Data);
```

Dopo la normalizzazione, tutti e tre i canali convergono su
`ProcessPacket` con frame `[NetInfo(2) + chunk(N)]`.

### 6.3 Dal pacchetto unificato all'evento applicativo

`PacketManager.ProcessPacket` → accumula chunk → quando completo costruisce
`NetworkLayer(interface, version, snifferId, unifiedPacket)` → chiama
`NetworkLayer.SP_PacketReady`:

1. Estrae `Source_Address` dai byte 1..4 del TransportPacket (byte-swappato,
   vedi §3.1)
2. Trunca `ApplicationPacket` degli ultimi 2 byte (CRC, non validato)
3. Emette `SP_PacketReadyEvent(PacketReadyEventArgs)` con
   `Packet = ApplicationPacket`, `SourceAddress`, `DestinationAddress`

---

## 7. Canali fisici

### 7.1 CAN (PCAN)

- Driver: `Peak.Can.Basic` via `PCANManager`
  (`Infrastructure.Protocol/Hardware/PCANManager.cs`)
- Channel: `PCAN_USB` (handle `0x51`)
- Baudrate supportati: 100, 125, 250, 500 kbps (default 100)
- Arbitration ID: extended 29-bit, `IsExtended=true` sempre
- Frame size: 8 byte (di cui 2 NetInfo + 6 chunk)
- Auto-reconnect: loop in `Task.Run` che controlla ogni 1 s via
  `PCANBasic.GetStatus`

### 7.2 BLE

- Driver: `Plugin.BLE 3.2.0` via `BLE_Manager` (in `GUI.Windows/`)
- Chunk size: 98 byte dati
- Frame size: 104 byte (NetInfo + recipientId + chunk)

### 7.3 Serial

- Driver: `System.IO.Ports.SerialPort` via `SerialPortManager` (in `GUI.Windows/`)
- Chunk size: 98 byte dati
- Frame size: 104 byte

---

## 8. Boot / upload firmware (BootManager)

Sequenza per upload firmware via CAN (richiesta più pesante del protocollo):

1. `CMD_START_PROCEDURE` (0x0005), payload vuoto, `waitAnswer=true`
2. Loop blocchi da 1024 B (ultimo paddato a `0xFF`):
   `CMD_PROGRAM_BLOCK` (0x0007), payload
   `[fwType(2 LE) | pageNum(4) | pageSize(4) | 0x00000000 | block(1024)]`
   - Retry: 10 per blocco (hard-coded)
3. `CMD_END_PROCEDURE` (0x0006), payload vuoto — retry 5
4. `CMD_RESTART_MACHINE` (0x000A), payload vuoto — retry 2

`fwType` letto dai byte 14..15 del firmware (little-endian):

```csharp
fwType = (ushort)((firmwareData[15] << 8) | firmwareData[14]);
```

`FIRMWARE_BLOCK_SIZE = 1024` (costante). Timeout risposta: 4000 ms
hard-coded in `SendCANAndWaitForResponseAsync` (il parametro `timeoutMs=600`
è ignorato).

> **Multi-area batch deviation (SPARK).** The four-step sequence above describes a
> single-file upload. For a multi-area batch via `Services.Boot.SparkBatchUpdateService`,
> step 4 (`CMD_RESTART_MACHINE`) is hoisted out of the per-area sequence and fires
> **once at the end of the whole batch**, addressed to the HMI board recipient
> (`0x000702C1`). Per-area execution stops at step 3. The SPARK firmware shuts the
> device down on `RESTART_MACHINE`, so a per-area restart would prevent areas
> 2..N from running. On the abort path (any area fails), no `RESTART_MACHINE` fires
> at all — recovery from a half-flashed device is operator-driven. Issue #74.

---

## 9. Telemetria veloce (TelemetryManager)

1. `CMD_CONFIGURE_TELEMETRY` (0x0015), payload
   `[type(4) | destAddr(4) | instance(1) | periodMs(2) | boardAddr(4) | varAddrs(2*N)]`
2. `CMD_START_TELEMETRY` (0x0016), payload `[instance(1)]`
3. Ricezione asincrona di `CMD_TELEMETRY_DATA` (0x0018):
   - Header 6 byte `[0x00, 0x18, 0x00, 0x00, 0x00, 0x00]`
   - Valori concatenati, ogni campo secondo il `DataType` della variabile:
     - `uint8_t` → 1 byte
     - `uint16_t` → 2 byte little-endian
     - `uint32_t` → 4 byte little-endian
4. `CMD_STOP_TELEMETRY` (0x0017), payload `[instance(1)]`

---

## 10. Known quirks e gap

Sintesi (vedi anche i known gap nella spec Lean `project_refactor_phase2_lean_spec.md`):

| # | Quirk / gap | Dettaglio |
|---|-------------|-----------|
| 1 | SenderId byte-swappato | TL encode in BE, riceve-decode in LE → byte-swap. Compensato dal dizionario `ProtocolAddress` con la stessa convenzione. |
| 2 | CRC16 non validato in RX | `SP_PacketReady` scarta solo gli ultimi 2 byte. Rivalutare in Fase 4. |
| 3 | Nessun timeout sui buffer chunk | Chunk persi → memoria non liberata. TTL da valutare in Fase 3. |
| 4 | Timeout wait-answer hard-coded | `SendCANAndWait…` usa 4000 ms (CAN) / 5000 ms (BLE/Serial); parametro `timeoutMs` ignorato. |
| 5 | Rolling code 1..7 | 7 slot simultanei max per packetId. Con traffico intenso potrebbe collidere. Accettabile per il traffico tipico. |
| 6 | `PacketManager` filtra per self-address | Solo CAN; BLE/Serial non hanno questo filtro, il NL sniffa tutto quello che riceve. |

---

## 11. Riferimenti nel codice

| Concetto | File | Riga/Sezione |
|----------|------|--------------|
| Application Layer | `GUI.Windows/STEMProtocol/STEM_protocol.cs` | 54-122 |
| Transport Layer + CRC16 | `GUI.Windows/STEMProtocol/STEM_protocol.cs` | 124-256 |
| Network Layer + chunking + NetInfo | `GUI.Windows/STEMProtocol/STEM_protocol.cs` | 258-492 |
| Riassembly | `GUI.Windows/STEMProtocol/PacketManager.cs` | 76-145 (`ProcessPacket`) |
| Rolling code | `GUI.Windows/STEMProtocol/SPRollingCode.cs` | 1-19 |
| Pipeline invio CAN | `GUI.Windows/STEMProtocol/STEM_protocol.cs` | 542-625 (`HandleSendCanCommandAsync`) |
| Ricezione CAN + filtro | `GUI.Windows/STEMProtocol/PacketManager.cs` | 275-297 (`ProcessCANPacket`) |
| Ricezione BLE/Serial + normalizzazione | `GUI.Windows/STEMProtocol/PacketManager.cs` | 428-446 / 589-607 |
| Driver PCAN | `Infrastructure.Protocol/Hardware/PCANManager.cs` | — |
| Adapter CanPort (nuovo) | `Infrastructure.Protocol/Hardware/CanPort.cs` | — |
| Decoder pacchetto applicativo (nuovo) | `Services/Protocol/PacketDecoder.cs` | — |

---

## 12. Roadmap migrazione

- **Fase 2 (in corso):** estrazione `PacketDecoder` + adapter
  `CanPort/BlePort/SerialPort` in `Infrastructure.Protocol/Hardware/`,
  preservando il comportamento attuale bit-per-bit (inclusi i quirk).
- **Fase 4:** sostituzione dello stack `STEMProtocol/` con il NuGet
  `Stem.Communication`. Servirà un adapter di compatibilità che:
  - Mappi i codici comando `ushort` → `Stem.Communication.CommandId`
  - Riallinei il byte-swap del senderId (quirk #1)
  - Attivi la validazione CRC (gap #2)
  - Gestisca il timeout dinamico dei buffer di riassembly (gap #3)
