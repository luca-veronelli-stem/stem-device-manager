# Core

> **Modelli dominio, enumerazioni e interfacce cross-platform per Stem.Device.Manager.**  
> **Ultimo aggiornamento:** 2026-04-17

---

## Panoramica

| Aspetto | Valore |
|---------|--------|
| **Tipo** | Class Library |
| **TFM** | `net10.0` (cross-platform) |
| **Dipendenze NuGet** | Zero (usa solo BCL + shared framework `System.Collections.Immutable`) |
| **Scopo** | Definisce i tipi puri del dominio — nessuna logica I/O, nessuna dipendenza GUI |

Core è il progetto più interno nel grafo dipendenze. Tutti gli altri progetti lo referenziano.
Non ha dipendenze esterne — garantisce che i modelli dominio siano portabili e testabili ovunque.

---

## Struttura

```
Core/
├── Core.csproj
├── Models/
│   ├── Variable.cs                 Record: nome, addressHigh, addressLow, dataType
│   ├── Command.cs                  Record: nome, codeHigh, codeLow
│   ├── ProtocolAddress.cs          Record: deviceName, boardName, address (hex)
│   ├── DictionaryData.cs           Record: addresses + commands (immutabile)
│   ├── ConnectionState.cs          Enum: Disconnected, Connecting, Connected, Error
│   ├── DeviceVariant.cs            Enum: Generic, TopLift, Eden, Egicon
│   ├── DeviceVariantConfig.cs      Record + factory Create(DeviceVariant)
│   ├── RawPacket.cs                Record: payload + timestamp (pacchetto raw)
│   ├── AppLayerDecodedEvent.cs     Record: comando decodificato + mittente
│   ├── TelemetryDataPoint.cs       Record: variabile + raw value + timestamp
│   └── ImmutableArrayEquality.cs   Internal helper — equality strutturale ImmutableArray<byte>
└── Interfaces/
    ├── IDictionaryProvider.cs      Astrazione dati dizionario (già Fase 2)
    ├── ICommunicationPort.cs       Astrazione canale HW (CAN/BLE/Serial)
    ├── IPacketDecoder.cs           Decoder puro: RawPacket → AppLayerDecodedEvent
    ├── ITelemetryService.cs        Servizio telemetria veloce
    ├── IBootService.cs             Servizio upload firmware (+ enum BootState, record BootProgress)
    └── IDeviceVariantConfig.cs     Config read-only per variante device
```

---

## API / Componenti

### IDictionaryProvider

```csharp
public interface IDictionaryProvider
{
    Task<DictionaryData> LoadProtocolDataAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Variable>> LoadVariablesAsync(
        uint recipientId, CancellationToken ct = default);
}
```

Implementata da:
- `Infrastructure.Persistence.Excel.ExcelDictionaryProvider` (fallback)
- `Infrastructure.Persistence.Api.DictionaryApiProvider` (API Azure)
- `Infrastructure.Persistence.FallbackDictionaryProvider` (decorator)

### Interfacce Protocol Abstractions (Fase 1)

| Interfaccia | Metodi principali | Implementazione prevista |
|-------------|-------------------|--------------------------|
| `ICommunicationPort` | `ConnectAsync`, `DisconnectAsync`, `SendAsync`, event `PacketReceived`, event `StateChanged` | Fase 2: `Services/Hardware/{Can,Ble,Serial}Port` |
| `IPacketDecoder` | `Decode(RawPacket) → AppLayerDecodedEvent?` | Fase 2: `Services/Protocol/PacketDecoder` |
| `ITelemetryService` | `StartFastTelemetryAsync`, `StopTelemetryAsync`, `UpdateDictionary`, `UpdateSourceAddress`, event `DataReceived` | Fase 2: `Services/Telemetry/TelemetryService` |
| `IBootService` | `StartFirmwareUploadAsync(byte[], CancellationToken)`, event `ProgressChanged` | Fase 2: `Services/Boot/BootService` |
| `IDeviceVariantConfig` | `Variant`, `DefaultRecipientId`, `DeviceName`, `BoardName` (+ feature flag in Fase 3) | `DeviceVariantConfig.Create(DeviceVariant)` |

### Modelli Dizionario

| Record | Campi | Descrizione |
|--------|-------|-------------|
| `Variable` | Name, AddressHigh, AddressLow, DataType | Variabile del protocollo |
| `Command` | Name, CodeHigh, CodeLow | Comando del protocollo |
| `ProtocolAddress` | DeviceName, BoardName, Address | Indirizzo dispositivo |
| `DictionaryData` | Addresses, Commands | Contenitore indirizzi + comandi |

### Modelli Protocol Abstractions (Fase 1)

| Tipo | Natura | Descrizione |
|------|--------|-------------|
| `ConnectionState` | Enum | Stato di una `ICommunicationPort` |
| `DeviceVariant` | Enum | Variante device (sostituisce `#if TOPLIFT/EDEN/EGICON`) |
| `DeviceVariantConfig` | Record | Default per variant + factory totale |
| `RawPacket` | Record | Payload byte + timestamp, non ancora decodificato |
| `AppLayerDecodedEvent` | Record | Pacchetto decodificato (comando + variabile opzionale + payload + mittente) |
| `TelemetryDataPoint` | Record | Campione telemetria (variabile + raw value + timestamp) |
| `BootState` / `BootProgress` | Enum / record struct | Stato + progresso dell'upload firmware |

I record che contengono `ImmutableArray<byte>` (`RawPacket`, `AppLayerDecodedEvent`, `TelemetryDataPoint`) hanno override manuale di `Equals`/`GetHashCode` per fornire equality strutturale sul payload (via `ImmutableArrayEquality`).

---

## Quick Start

```csharp
// Modelli dizionario
var variable = new Variable("Firmware", "0", "0", "UInt16");
var command = new Command("Read", "0", "1");
var address = new ProtocolAddress("TopLift", "Azionamento", "0x00080381");

// Config device variant (Fase 1)
var topLiftCfg = DeviceVariantConfig.Create(DeviceVariant.TopLift);
Console.WriteLine(topLiftCfg.DefaultRecipientId); // 0x00080381

// Pacchetto raw
var packet = new RawPacket(
    ImmutableArray.Create<byte>(0xAA, 0xBB, 0xCC),
    DateTime.UtcNow);
```

---

## Formalizzazioni Lean 4

I tipi introdotti in Fase 1 sono formalizzati in [`Specs/Phase1/`](../Specs/Phase1/README.md) (Lean 4):

| File Lean | Corrispettivo C# |
|-----------|------------------|
| `ConnectionState.lean` | `Models/ConnectionState.cs` |
| `DeviceVariant.lean` | `Models/DeviceVariant.cs` |
| `DeviceVariantConfig.lean` | `Models/DeviceVariantConfig.cs` |
| `RawPacket.lean` | `Models/RawPacket.cs` |
| `AppLayerDecodedEvent.lean` | `Models/AppLayerDecodedEvent.cs` |
| `TelemetryDataPoint.lean` | `Models/TelemetryDataPoint.cs` |
| `Interfaces.lean` | `Interfaces/*.cs` |

---

## Requisiti

- **.NET 10.0** (cross-platform)

---

## Links

- [README Soluzione](../README.md)
- [Infrastructure.Persistence](../Infrastructure.Persistence/README.md)
- [Tests](../Tests/README.md)
- [Specs/Phase1](../Specs/Phase1/README.md)
- [REFACTOR_PLAN](../Docs/REFACTOR_PLAN.md)
- [PREPROCESSOR_DIRECTIVES](../Docs/PREPROCESSOR_DIRECTIVES.md)
