# Services

> **Logica applicativa pura (cross-platform): protocol decode, telemetry, boot, configuration.**  
> **Ultimo aggiornamento:** 2026-04-20

---

## Panoramica

| Aspetto | Valore |
|---------|--------|
| **Tipo** | Class Library |
| **TFM** | `net10.0` (cross-platform, zero dipendenze Windows) |
| **Dipendenze** | Core |
| **Scopo** | Logica pura del dominio applicativo: nessun I/O, nessun driver, nessuna UI |

Services contiene la logica di business del protocollo STEM senza legami
hardware o framework. Le implementazioni concrete qui dipendono solo da Core e
ricevono tutte le risorse esterne via ctor injection (`IDictionaryProvider`,
`ICommunicationPort`, ecc.).

Il progetto è **net10.0 puro**: i test girano anche in CI Linux e sono parte
della baseline cross-platform della soluzione.

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **PacketDecoder** | ✅ | `IPacketDecoder` puro: `RawPacket → AppLayerDecodedEvent?` |
| **DictionarySnapshot** | ✅ | Snapshot immutabile + lookup comandi/variabili/indirizzi |
| **NetInfo** | ✅ | Parser/encoder dei 2 byte header del Network Layer |
| **PacketReassembler** | ✅ | Riassembly multi-chunk per packetId (rolling code 1..7) |
| **ProtocolService** | ✅ | Facade encode/chunking/reassembly + request/reply pattern |
| **TelemetryService** | ⏳ | Config + start/stop telemetria veloce (Step 4 Fase 2) |
| **BootService** | ⏳ | State machine upload firmware (Step 5 Fase 2) |
| **DeviceVariantConfigFactory** | ⏳ | Factory da `appsettings.json` (Step 3 Fase 2) |

Legenda: ✅ implementato nel branch `refactor/services-layer`; ⏳ previsto dal
piano Fase 2.

---

## Struttura

```
Services/
├── Services.csproj
└── Protocol/
    ├── PacketDecoder.cs          Decoder puro di pacchetti applicativi STEM
    ├── DictionarySnapshot.cs     Snapshot immutabile dizionario (lookup cmd/var/addr)
    ├── NetInfo.cs                Parser/encoder header a 16 bit del Network Layer
    ├── PacketReassembler.cs      Riassembly multi-chunk thread-safe per packetId
    └── ProtocolService.cs        Facade: encode TP + CRC + chunk; decode + reassembly + event
```

In corso di popolamento (Fase 2, rimanente):

```
Services/
├── Telemetry/
│   └── TelemetryService.cs        ⏳ Step 4 — branch refactor/services-business
├── Boot/
│   └── BootService.cs             ⏳ Step 5 — branch refactor/services-business
├── Configuration/
│   └── DeviceVariantConfigFactory.cs  ⏳ Step 3 — branch refactor/services-business
└── DependencyInjection.cs         ⏳ Step 7 — branch refactor/services-di-integration
```

---

## API / Componenti

### PacketDecoder

Implementa `Core.Interfaces.IPacketDecoder`. Decoder puro, zero stato esterno:

```csharp
var decoder = new PacketDecoder(commands, variables, addresses);
var evt = decoder.Decode(rawPacket);  // AppLayerDecodedEvent? (null se invalido)
decoder.UpdateDictionary(newCommands, newVariables, newAddresses);  // atomic swap
```

**Thread-safety:** lo snapshot del dizionario è sostituito atomicamente via
`Volatile.Read`/`Volatile.Write`. Una `Decode` in corso vede sempre uno
snapshot coerente (vecchio o nuovo, mai intermedio). Verificato con test di
concorrenza in `Tests/Unit/Services/Protocol/PacketDecoderTests`.

**Struttura payload attesa** (post-riassembly, vedi [PROTOCOL.md](../Docs/PROTOCOL.md)):

```
byte 0      : cryptFlag
byte 1..4   : senderId (byte-swappato per quirk storico, vedi PROTOCOL.md §3.1)
byte 5..6   : lPack
byte 7..8   : cmdInit, cmdOpt (comando a 16 bit)
byte 9..N   : payload applicativo
byte N+1,N+2: CRC16 Modbus (scartato, non validato in Fase 2 — gap #1)
```

### ProtocolService

Facade che combina `ICommunicationPort` + `PacketReassembler` + `IPacketDecoder`
per esporre un'API orientata al comando:

```csharp
var svc = new ProtocolService(port, decoder, senderId: 0x12345678u);

// Fire-and-forget
await svc.SendCommandAsync(recipientId: 0x00080381u, command, payload: []);

// Con attesa risposta (custom validator)
bool replied = await svc.SendCommandAndWaitReplyAsync(
    recipientId: 0x00080381u,
    command: readCmd,
    payload: [0x12, 0x34],
    replyValidator: evt => evt.Command.Name == "ReadVariableReply",
    timeout: TimeSpan.FromSeconds(2));

// Evento per ogni pacchetto decodificato
svc.AppLayerDecoded += (_, evt) => { /* ... */ };
```

**Framing per canale** (deriva da `port.Kind`):
- CAN (chunk 6) → `[arbId_LE(4) + NetInfo(2) + chunk(≤6)]` (convention A CanPort)
- BLE/Serial (chunk 98) → `[NetInfo(2) + recipientId_LE(4) + chunk(≤98)]` pass-through

**Rolling packetId:** 1..7 incrementale (stesso comportamento legacy). CRC16
Modbus su TP (quirk senderId byte-swap preservato — vedi
[PROTOCOL.md](../Docs/PROTOCOL.md) §3.1).

### NetInfo

Struct immutable (`readonly record struct`) per i 2 byte del Network Layer:

```csharp
var info = NetInfo.Parse(lo, hi);
// info.RemainingChunks, info.SetLength, info.PacketId, info.Version
var (lo, hi) = new NetInfo(0, true, 3, 1).ToBytes();
```

### PacketReassembler

Thread-safe, buffer per packetId. API:

```csharp
var reassembler = new PacketReassembler();
byte[]? complete = reassembler.Accept(chunkWithNetInfo);
// null se remainingChunks > 0; payload completo se remainingChunks == 0
```

### DictionarySnapshot

Record immutabile con `ImmutableArray<Command>` + `ImmutableArray<Variable>` +
`ImmutableArray<ProtocolAddress>`. Espone:

| Metodo | Lookup |
|--------|--------|
| `FindCommand(codeHigh, codeLow)` | Per byte di codice (hex case-insensitive, entry invalidi ignorati) |
| `FindVariable(addressHigh, addressLow)` | Per indirizzo variabile (hex case-insensitive, entry invalidi/vuoti ignorati) |
| `FindSender(senderId)` | Per indirizzo mittente a 32 bit |

`DictionarySnapshot.Empty` è disponibile per inizializzazioni.

---

## Quick Start

```csharp
using System.Collections.Immutable;
using Core.Models;
using Services.Protocol;

var commands = new[] { new Command("ReadVariable", "00", "01") };
var variables = new[] { new Variable("Speed", "12", "34", "uint16_t") };

var decoder = new PacketDecoder(commands, variables, []);

// Pacchetto riassemblato (esempio fittizio)
var payload = ImmutableArray.Create<byte>(
    0x00,                                    // cryptFlag
    0x01, 0x02, 0x03, 0x04,                  // senderId
    0x00, 0x00,                              // lPack
    0x00, 0x01,                              // cmd = ReadVariable
    0x12, 0x34,                              // variable address (Speed)
    0xCC, 0xCD                               // CRC (ignorato)
);

var evt = decoder.Decode(new RawPacket(payload, DateTime.UtcNow));
// evt.Command.Name == "ReadVariable"
// evt.Variable.Name == "Speed"
```

---

## Esecuzione / Testing

```bash
# Tutti i test Services (cross-platform, girano in CI Linux)
dotnet test Tests/Tests.csproj --framework net10.0 --filter "FullyQualifiedName~Unit.Services"

# Solo PacketDecoder
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~PacketDecoderTests"
```

Test attuali: 19 su `PacketDecoder`, 21 su `DictionarySnapshot` (include
theory con hex case-insensitive e scenari di concorrenza).

---

## Requisiti

- **.NET 10.0** (cross-platform, nessuna dipendenza Windows/WinForms)

### Dipendenze

Nessun pacchetto NuGet esterno. Solo `Core.csproj` via project reference.

---

## Issue Correlate

→ [ISSUES.md](../ISSUES.md) (da creare)

---

## Links

- [README Soluzione](../README.md)
- [Core](../Core/README.md)
- [Infrastructure.Persistence](../Infrastructure.Persistence/README.md)
- [Infrastructure.Protocol](../Infrastructure.Protocol/README.md)
- [Tests](../Tests/README.md)
- [PROTOCOL.md](../Docs/PROTOCOL.md)
- [REFACTOR_PLAN](../Docs/REFACTOR_PLAN.md)
