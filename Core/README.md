# Core

> **Modelli dominio, enumerazioni e interfacce cross-platform per Stem.Device.Manager.**  
> **Ultimo aggiornamento:** 2026-04-16

---

## Panoramica

| Aspetto | Valore |
|---------|--------|
| **Tipo** | Class Library |
| **TFM** | `net10.0` (cross-platform) |
| **Dipendenze NuGet** | Zero |
| **Scopo** | Definisce i tipi puri del dominio — nessuna logica I/O, nessuna dipendenza GUI |

Core è il progetto più interno nel grafo dipendenze. Tutti gli altri progetti lo referenziano.
Non ha dipendenze esterne — garantisce che i modelli dominio siano portabili e testabili ovunque.

---

## Struttura

```
Core/
├── Core.csproj
├── Models/
│   ├── Variable.cs              Record: nome, addressHigh, addressLow, dataType
│   ├── Command.cs               Record: nome, codeHigh, codeLow
│   ├── ProtocolAddress.cs       Record: deviceName, boardName, address (hex)
│   └── DictionaryData.cs        Record: addresses + commands (immutabile)
└── Interfaces/
    └── IDictionaryProvider.cs   Astrazione: LoadProtocolDataAsync + LoadVariablesAsync
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
- `Infrastructure.Excel.ExcelDictionaryProvider` (fallback)
- `Infrastructure.Api.DictionaryApiProvider` (API Azure)
- `Infrastructure.FallbackDictionaryProvider` (decorator)

### Modelli Dizionario

| Record | Campi | Descrizione |
|--------|-------|-------------|
| `Variable` | Name, AddressHigh, AddressLow, DataType | Variabile del protocollo |
| `Command` | Name, CodeHigh, CodeLow | Comando del protocollo |
| `ProtocolAddress` | DeviceName, BoardName, Address | Indirizzo dispositivo |
| `DictionaryData` | Addresses, Commands | Contenitore indirizzi + comandi |

---

## Quick Start

```csharp
// Core non ha dipendenze — basta referenziarlo
var variable = new Variable("Firmware", "0", "0", "UInt16");
var command = new Command("Read", "0", "1");
var address = new ProtocolAddress("TopLift", "Azionamento", "0x00080381");
```

---

## Requisiti

- **.NET 10.0** (cross-platform)

---

## Links

- [README Soluzione](../README.md)
- [Infrastructure](../Infrastructure/README.md)
- [Tests](../Tests/README.md)
- [MIGRATION_API.md](../Docs/MIGRATION_API.md)
