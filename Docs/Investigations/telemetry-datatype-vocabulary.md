# Investigation — telemetry / read-reply silently dropped (API DataType vocabulary mismatch)

**Tracking issue:** [#96](https://github.com/luca-veronelli-stem/stem-device-manager/issues/96)
**Started:** 2026-05-21
**Status:** **Confirmed by 2026-05-21 bench run.** `TelemetryService.LogWarning` on `width == 0` (landed in [#101](https://github.com/luca-veronelli-stem/stem-device-manager/pull/101)) fired continuously for fast-stream variables with `dataType` ∈ {`UInt8`, `UInt16`, `UInt32`}, matching the hypothesis exactly. Awaiting normalization-approach decision.

---

## Hypothesis

`Services/Telemetry/TelemetryService.cs:411` only recognizes the Excel-style C type strings:

```csharp
private static int DataTypeWidth(string? dataType) => dataType?.Trim() switch
{
    "uint8_t" => 1,
    "uint16_t" => 2,
    "uint32_t" => 4,
    _ => 0
};
```

The Azure dictionary API (`stem-dictionaries-manager`) emits canonical .NET names instead:

| address | API DataType | Excel DataType |
|---|---|---|
| `0x0000` | `UInt16` | `uint16_t ` |
| `0x0003` | `UInt32` | `uint32_t ` |
| `0x0006` | `Bitmapped[2]` | `due word uint16_t bitmapped` |
| `0x8006` | `UInt8` | `uint8_t` |

When the live dictionary comes from `DictionaryApiProvider` (the default — `GUI.Windows/appsettings.json` has both `BaseUrl` and `ApiKey` populated), every variable lookup hits the `_ => 0` branch, then:

- `HandleReadReply` (line 326): `if (width == 0) return;` — every `CMD_READ_VARIABLE` reply silently dropped.
- `EmitFastStreamDataPoints` (line 343): `if (width == 0) continue;` — every fast-stream variable skipped, no `DataReceived` events fire.

End-to-end effect: user sees no telemetry values, no read-reply data, no error.

## Why this regressed

v2.15 (`80bf9c6`) read variables only from the Excel via `ExcelHandler`, which returned the C-style strings the legacy `TelemetryManager.cs:193-206` already handled. The refactor introduced `DictionaryApiProvider` and `FallbackDictionaryProvider`, defaulting to API-first, but did not align the type-name vocabulary the consumer expects.

## Evidence collected so far

- **2026-05-21** — API vs Excel diff on TopLift-A2 Madre (`0x00080381`): 32 of the common variables have mismatched DataType strings.
- API endpoint queried: `GET /api/dictionaries/10/resolved` (TopLift-A2 Madre).
- The DataType field is consumed only by `DataTypeWidth`. No other call site uses the string.
- **2026-05-21 bench (Optimus-XP/Madre, `0x000A0441`)** — first run after [#101](https://github.com/luca-veronelli-stem/stem-device-manager/pull/101) added a `LogWarning` on `width == 0`. The warning fired every fast-stream tick during a telemetry session, e.g.:

  ```
  14:32:05.228 [WARN] Services.Telemetry.TelemetryService: Fast-stream variable skipped: 'Firmware macchina' has unrecognized dataType 'UInt16'
  14:32:05.228 [WARN] Services.Telemetry.TelemetryService: Fast-stream variable skipped: 'Stato EVA' has unrecognized dataType 'UInt8'
  ```

  Distinct `dataType` values observed in one session: `UInt8`, `UInt16`, `UInt32`. End-to-end behaviour matches the hypothesis: no `DataReceived` events fire for affected variables; UI stays blank for them. Hypothesis is no longer speculative.

## Open questions before patching

1. **Where to normalize.**
   - At the API boundary (`DictionaryApiProvider` maps API names → C-style names before producing `Variable`). Pro: keeps `Services/` ignorant of the API quirk. Con: hides the canonical names the rest of the .NET ecosystem expects.
   - At the consumer (`DataTypeWidth` accepts both vocabularies). Pro: most local change. Con: every future width-aware call site must remember to accept both.
   - Upstream (`stem-dictionaries-manager` emits C-style names natively). Pro: single source of truth. Con: breaks any other client already coded against the canonical names.
2. **`Bitmapped[2]` / "due word uint16_t bitmapped".** Neither the legacy app nor the new app's `DataTypeWidth` switch handles this label. Did v2.15 silently drop those reads too, or is the bitmapped variable consumed via a different code path?
3. **API-only DataTypes.** Are there other API-emitted strings beyond `UInt8/16/32` and `Bitmapped[2]` that need handling? The sample so far is one board.

## Next bench actions

- [ ] Enumerate every distinct `dataType` string the API returns across all boards. (3 seen on Optimus-XP: `UInt8`/`UInt16`/`UInt32`; still need a full sweep + the `Bitmapped[2]` confirmation.)
- [ ] Capture one telemetry session with `DictionaryApi__ApiKey=""` env override (forces Excel fallback) — confirm telemetry decodes correctly under Excel-only.
- [x] Capture the same session with API enabled — confirm zero decoded values. **Done 2026-05-21 (Optimus-XP/Madre).**
- [ ] Pick a normalization approach with the dictionaries-manager owner.

## Not the cause of

- The Boot Interface tab being missing — that's a UI removal, see [#95](https://github.com/luca-veronelli-stem/stem-device-manager/issues/95).
- The senderId endianness question — independent hypothesis, see [#97](https://github.com/luca-veronelli-stem/stem-device-manager/issues/97).

## Changelog of this investigation

| Date | Note |
|---|---|
| 2026-05-21 | Filed [#96](https://github.com/luca-veronelli-stem/stem-device-manager/issues/96); initial findings from code reading + API/Excel diff. |
| 2026-05-21 | Bench-confirmed on Optimus-XP/Madre. `TelemetryService.LogWarning` (added in [#101](https://github.com/luca-veronelli-stem/stem-device-manager/pull/101)) fired continuously for `UInt8`/`UInt16`/`UInt32`. Status moved from hypothesis to confirmed. |
