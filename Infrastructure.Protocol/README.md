# Infrastructure.Protocol

> **Adapter hardware CAN/BLE/Serial che implementano `ICommunicationPort`, wrappano i driver nativi.**  
> **Ultimo aggiornamento:** 2026-04-20

---

## Panoramica

| Aspetto | Valore |
|---------|--------|
| **Tipo** | Class Library |
| **TFM** | `net10.0;net10.0-windows10.0.19041.0` (dual) |
| **Dipendenze** | Core, Peak.PCANBasic.NET |
| **Scopo** | Implementazioni di `ICommunicationPort` + driver PCAN embedded |

Infrastructure.Protocol contiene gli adapter hardware che portano il protocollo
STEM sui canali fisici. Ciascun adapter è pensato come un bridge sottile tra il
driver nativo (Peak PCAN, Plugin.BLE, System.IO.Ports) e l'astrazione uniforme
`ICommunicationPort` definita in Core.

Il progetto segue il pattern Stem `Infrastructure.<Concern>` (vedi
`Stem.Production.Tracker` e `Stem.ButtonPanel.Tester`) e affianca
[Infrastructure.Persistence](../Infrastructure.Persistence/README.md).

---

## Caratteristiche

| Feature | Stato | Descrizione |
|---------|-------|-------------|
| **CanPort** | ✅ | Adapter CAN su PCAN (`IPcanDriver`) — convention arbId LE prefix |
| **BlePort** | ✅ | Adapter BLE su Nordic UART (`IBleDriver`) — convention pass-through |
| **SerialPort** | ✅ | Adapter seriale su COM (`ISerialDriver`) — convention pass-through |
| **PCANManager** | ✅ | Driver PCAN-USB embedded (auto-reconnect, baud rate runtime) |
| **DI registration** | ⏳ | In Step 7 di Fase 2 (`AddProtocolInfrastructure()` extension) |

---

## Struttura

```
Infrastructure.Protocol/
├── Infrastructure.Protocol.csproj
└── Hardware/
    ├── CanPort.cs                   ICommunicationPort via IPcanDriver
    ├── BlePort.cs                   ICommunicationPort via IBleDriver
    ├── SerialPort.cs                ICommunicationPort via ISerialDriver
    ├── PCANManager.cs               Driver PCAN-USB + CANPacketEventArgs
    ├── IPcanDriver.cs               Abstraction su PCANManager per testability
    ├── IBleDriver.cs                Abstraction su BLEManager (App) per testability
    ├── ISerialDriver.cs             Abstraction su SerialPortManager (App) per testability
    ├── BlePacketEventArgs.cs        Event args BLE (ricezione)
    └── SerialPacketEventArgs.cs     Event args seriale (ricezione)
```

**Nota progetto/implementazione:** `PCANManager` è autonomo e risiede qui.
`BLEManager` e `SerialPortManager` invece restano in `App/` perché hanno
refs a `Form1.FormRef` e `MessageBox` — saranno estratti in Fase 3. Per
consentire il wiring oggi, entrambi implementano le interfacce
`IBleDriver` / `ISerialDriver` definite qui (dependency inversion).

---

## API / Componenti

### Convention payload

Per mantenere `ICommunicationPort.SendAsync(ReadOnlyMemory<byte>)` uniforme tra
canali con semantica hardware diversa, i tre adapter adottano convention
esplicite:

| Adapter | TX payload | RX `RawPacket.Payload` |
|---------|------------|------------------------|
| `CanPort` | `[arbitrationId_LE(4) + data(≤8)]` | `[arbitrationId_LE(4) + data(≤8)]` |
| `BlePort` | pass-through byte as-is | pass-through byte as-is |
| `SerialPort` | pass-through byte as-is | pass-through byte as-is |

Il CAN deve trasportare l'arbitration ID (metadata hardware del frame) in-band
perché `ICommunicationPort` non ha un campo dedicato. BLE e seriale invece non
hanno metadata separate, quindi il payload applicativo (costruito da
`ProtocolService` con `[NetInfo + recipientId + chunk]`) passa integro.

Vedi [Docs/PROTOCOL.md](../Docs/PROTOCOL.md) per il dettaglio del framing STEM
e l'endianness del `recipientId`.

### State machine degli adapter

Tutti e tre gli adapter condividono la stessa state machine:

```
Disconnected ──ConnectAsync──► Connecting ──driver ok────► Connected
      ▲                           │                          │
      │                           │ driver fail              │
      │                           ▼                          │ DisconnectAsync / driver drop
      └───── DisconnectAsync      Error ◄──driver error──────┘
```

- `ConnectAsync` è best-effort di sincronizzazione con lo stato corrente del
  driver (polling 20 × 100 ms). In caso di timeout transita a `Error` e lancia
  `InvalidOperationException`.
- `SendAsync` rifiuta con `InvalidOperationException` se lo stato non è
  `Connected`.
- `Dispose` è idempotente: la prima chiamata disiscrive gli event handler e,
  se necessario, disconnette il driver; la seconda è no-op.

### Interfacce driver

| Interfaccia | Metodi principali | Implementazione |
|-------------|-------------------|-----------------|
| `IPcanDriver` | `IsConnected`, `PacketReceived`, `ConnectionStatusChanged`, `SendMessageAsync(canId, data, isExtended)`, `Disconnect()` | `PCANManager` (qui) |
| `IBleDriver` | `IsConnected`, `PacketReceived`, `ConnectionStatusChanged`, `SendMessageAsync(data)`, `DisconnectAsync()` | `App.BLEManager` |
| `ISerialDriver` | `IsConnected`, `PacketReceived`, `ConnectionStatusChanged`, `SendMessageAsync(data)`, `Disconnect()` | `App.SerialPortManager` |

### Name conflict `SerialPort`

`Infrastructure.Protocol.Hardware.SerialPort` collide con
`System.IO.Ports.SerialPort`. Nei file che usano entrambi i tipi, disambiguare
con alias `using` o namespace completo:

```csharp
using SysSerialPort = System.IO.Ports.SerialPort;
using Infrastructure.Protocol.Hardware;  // brings in SerialPort adapter
```

---

## Quick Start

```csharp
// Costruisci il driver nativo (lifecycle owned dall'App, già esistente)
var pcan = new PCANManager(TPCANBaudrate.PCAN_BAUD_250K);

// Wrappa in un adapter ICommunicationPort
using var port = new CanPort(pcan);

port.PacketReceived += (_, rawPacket) =>
{
    // Primi 4 byte = arbitrationId (LE), resto = dati CAN
    var arbId = BinaryPrimitives.ReadUInt32LittleEndian(
        rawPacket.Payload.AsSpan()[..4]);
    var data = rawPacket.Payload.AsSpan()[4..];
    // ...
};

await port.ConnectAsync();

// Invia: primi 4 byte = arbitrationId LE, resto = dati CAN (max 8 byte)
byte[] frame = [
    0x81, 0x03, 0x08, 0x00,     // arbId 0x00080381 LE
    0xAA, 0xBB, 0xCC            // dati
];
await port.SendAsync(frame);
```

---

## Esecuzione / Testing

I test unitari vivono in `Tests/Unit/Infrastructure/Protocol/`:

```bash
dotnet test Tests/Tests.csproj --filter "FullyQualifiedName~Unit.Infrastructure.Protocol"
```

Ogni adapter ha un fake driver manuale (`FakePcanDriver`, `FakeBleDriver`,
`FakeSerialDriver`) che consente di simulare eventi e verificare il forwarding
senza dipendere da hardware reale. I test sono esclusi dal target net10.0
(CI Linux) perché `Peak.PCANBasic.NET` richiede runtime Windows.

---

## Requisiti

- **.NET 10.0** (Windows 10+ per runtime CAN/BLE; compila anche cross-platform)
- **PCAN USB driver** (solo per esecuzione con hardware CAN reale)

### Dipendenze

| Package | Versione | Uso |
|---------|----------|-----|
| Peak.PCANBasic.NET | 5.0.1.1131 | Driver PCAN-USB |

`Plugin.BLE` e `System.IO.Ports` sono referenziati indirettamente via
`BLEManager` / `SerialPortManager` (che vivono in `App/`).

---

## Issue Correlate

→ [ISSUES.md](../ISSUES.md) (da creare)

---

## Links

- [README Soluzione](../README.md)
- [Core](../Core/README.md)
- [Infrastructure.Persistence](../Infrastructure.Persistence/README.md)
- [Services](../Services/README.md)
- [Tests](../Tests/README.md)
- [PROTOCOL.md](../Docs/PROTOCOL.md)
- [REFACTOR_PLAN](../Docs/REFACTOR_PLAN.md)
