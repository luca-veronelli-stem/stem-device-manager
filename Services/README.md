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
| **ProtocolService** | ⏳ | Facade encode/chunking/reassembly (Step 6 Fase 2) |
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
    └── DictionarySnapshot.cs     Snapshot immutabile dizionario (lookup cmd/var/addr)
```

In corso di popolamento (Fase 2):

```
Services/
├── Protocol/
│   ├── PacketDecoder.cs           ✅
│   ├── DictionarySnapshot.cs      ✅
│   ├── ProtocolService.cs         ⏳ Step 6 — facade encode/chunking
│   └── PacketReassembler.cs       ⏳ Step 6 — stateful multi-chunk
├── Telemetry/
│   └── TelemetryService.cs        ⏳ Step 4
├── Boot/
│   └── BootService.cs             ⏳ Step 5
├── Configuration/
│   └── DeviceVariantConfigFactory.cs  ⏳ Step 3
└── DependencyInjection.cs         ⏳ Step 7 — AddServices() extension
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
