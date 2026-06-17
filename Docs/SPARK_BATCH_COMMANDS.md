# SPARK Batch Firmware Update — Command Sequence (excluding bootloader)

The exact STEM-protocol commands the application sends to flash a SPARK over BLE
through the **SPARK Firmware Update** tab (`GUI.Windows/Tabs/Spark_FirmwareUpdate_WF_Tab.cs`
→ `Services/Boot/SparkBatchUpdateService.cs` → `Services/Boot/BootService.cs`).

**Scope:** every area **except** "Bootloader HMI" — i.e. HMI application, HMI Images,
Motor 1, Motor 2, Rostrum. Bench-validated on Egicon SN 2225998, bundle 8.2.0.1
(2026-06-15 / 2026-06-17).

> All wire-level layering (Transport CRC16, Network chunking, NetInfo, senderId
> endianness) is in [`PROTOCOL.md`](PROTOCOL.md). This document is the boot/batch
> command layer on top of it. For operator-facing guidance (file identification,
> what's normal during a flash, recovery), see
> [`SPARK_BATCH_OPERATOR_NOTES.md`](SPARK_BATCH_OPERATOR_NOTES.md).

### Boot-entry observations (expected, benign)

- **One `0xFF` byte on the first `START` only.** The first `CMD_START_PROCEDURE` transitions the
  device from its application into the bootloader; that transition takes **~3.5 s** and the device
  emits a single `0xFF` notification (logged as `Discarded short Ble frame: 1 bytes … FF` — a valid
  STEM frame needs ≥6 bytes, so the decoder drops it). Subsequent areas' `START`s are answered in
  ~0.1 s with **no** `0xFF`, because the device stays in the bootloader for the whole batch (only the
  final `RESTART_MACHINE` leaves it). So the `0xFF` is a one-shot boot-entry marker, not a reply.
- **`result 0x00`** on every `0x8007` page ack = success (§3.1).

---

## 1. Areas, order, recipient

The batch runs the selected areas in canonical order (`Core/Models/SparkFirmwareArea.cs`,
`SparkAreas.All`). **Every area is addressed to the HMI recipient `0x000702C1`** — the HMI
is the machine's update orchestrator and stages/forwards each image to its target board.

| Order | Area | File (bundle 8.2.0.1) | Recipient | fwType¹ | Size (B) | Blocks² |
|---|---|---|---|---|---|---|
| 1 | HMI application | `WORKCODE_HMI_RSC.bin` | `0x000702C1` | `0x5F45` | 664 576 | 649 |
| 2 | HMI Images | `IMAGES_HMI_RSC.bin` | `0x000702C1` | `0x5345` | 5 486 112 | 5 358 |
| 3 | Motor 1 (right) | `WEMOTOR1.bin` | `0x000702C1` | `0x5F45` | 47 104 | 46 |
| 4 | Motor 2 (left) | `WEMOTOR2.bin` | `0x000702C1` | `0x5F45` | 47 104 | 46 |
| 5 | Rostrum | `WEROSTRO.bin` | `0x000702C1` | `0x5F45` | 24 576 | 24 |

¹ `fwType` is read from **firmware-file bytes 14–15** (`fwType = (file[15] << 8) | file[14]`),
not assigned by area; values above are what the bench files carry. The file header also
carries an ASCII work-code identity around bytes 10–17 (e.g. Rostrum = `WCODE_E1`) that the
HMI uses to route the image to the correct board.
² `Blocks = ceil(Size / 1024)`.

---

## 2. Application-layer commands used

Codes are 16-bit `(cmdInit << 8) | cmdOpt`. A reply sets bit 7 of `cmdInit` (i.e. `0x80 | code`).

| Command | Code | App payload (request) | Reply code | Source |
|---|---|---|---|---|
| `CMD_START_PROCEDURE` | `0x0005` | *(empty)* | `0x8005` | `BootService.StartBootAsync` |
| `CMD_PROGRAM_BLOCK` | `0x0007` | 14-byte header + 1024-byte block (§3) | `0x8007` | `BootService.SendAllBlocks` |
| `CMD_END_PROCEDURE` | `0x0006` | *(empty)* | `0x8006` | `BootService.EndBootAsync` |
| `CMD_RESTART_MACHINE` | `0x000A` | *(empty)* | `0x800A` | `BootService.RestartAsync` |

Acceptance rule: `BootService.MatchesReply` accepts a reply when `cmdInit == 0x80` **and**
`cmdOpt` equals the request's low byte. The reply payload is **not** inspected (see §3.1).

---

## 3. `CMD_PROGRAM_BLOCK` payload layout

Built by `BootService.BuildProgramBlockPayload`. 14-byte header followed by one 1024-byte
firmware block:

```
offset  0..1   fwType        big-endian   = (file[15]<<8)|file[14]   e.g. 5F 45
offset  2..5   pageNum       big-endian   0,1,2,…  (one per block, from 0)
offset  6..9   pageSize      big-endian   always 0x00000400 (1024)
offset 10..13  reserved                   0x00 0x00 0x00 0x00
offset 14..    block(1024)                firmware bytes; LAST block 0xFF-padded to 1024
```

- `pageSize` is **always 1024**, even for the final (short) block — the tail is `0xFF`-padded.
- Total app-layer payload per block = `2 + 4 + 4 + 4 + 1024` = **1038 bytes** → chunked into
  ~11 BLE frames of ≤98 data bytes each (`PROTOCOL.md §4.3`).

### 3.1 `0x8007` reply ("Aggiorna pagina bootloader risposta")

15 bytes, echoing the header plus a trailing result region:

```
fwType(2) | pageNum(4) | pageSize(4 = 00 00 04 00) | result(5 bytes)
```

On every acknowledged block observed on the bench the result region was **all `0x00`**
(success). The host does **not** read this byte today — a block counts as done the moment a
`0x8007` reply with the matching low byte returns. Hardening this is a separate follow-up.

---

## 4. Per-area sequence

`SparkBatchUpdateService.RunAreaAsync`:

```
CMD_START_PROCEDURE        → 0x000702C1     (await 0x8005)
CMD_PROGRAM_BLOCK × Blocks → 0x000702C1     (each awaits its 0x8007; pageNum 0…Blocks-1)
‹settle: wait 5 s›                          (after last block, before END — no wire traffic)
CMD_END_PROCEDURE          → 0x000702C1     (await 0x8006)
```

There is **no per-area `RESTART_MACHINE`** — restart is hoisted to the end of the whole batch
(§5). Retry budget: first block 60 attempts (rides out the device-side flash erase, §4.1), all
other commands 3 attempts; 4000 ms reply timeout (`BootService` defaults).

### 4.1 Time to wait between `START_PROCEDURE` and the first page

After `CMD_START_PROCEDURE` is acknowledged (`0x8005`), the target board **erases the flash area**
it is about to program. The erase takes time (from a few hundred ms up to several seconds,
depending on area size), and the bootloader does **not** acknowledge the first `CMD_PROGRAM_BLOCK`
until the erase has finished.

The host does **not** insert a fixed delay here. Page 0 is sent immediately and **retried with a
much larger budget** than the rest, so the wait is *adaptive* — it lasts exactly as long as the
erase, no more:

| Block | Retry budget | Per-attempt reply timeout | Max wait |
|---|---|---|---|
| Page 0 (first of **each area**) | 60 attempts (`firstBlockRetryBudget`) | 4000 ms | ~240 s |
| Pages 1…N | 3 attempts (`retryBudget`) | 4000 ms | ~12 s |

Each unacknowledged attempt re-sends page 0 and waits up to 4 s for a reply; the loop repeats (up
to ~240 s) until the bootloader finishes erasing and acks. The moment it acks, programming runs at
full speed — pages 1…N use the short 3-attempt budget, because slowness past the erase is not
expected and a dead link should fail fast.

This is why the logs show **one or two early `Retrying ProgramBlock` lines (page 0/1) in every
run** — that is page 0 being re-sent while the flash erases, **not** an error. The first-block
budget resets for **each area** (`pageNum` restarts at 0 per area), so every area rides out its own
erase. The ~240 s ceiling is ~2× the worst measured area-erase time — enough margin to never abort
a healthy erase, without hanging forever on a truly dead link.

> A multi-second `START → first page` gap seen in a **manual** (human-paced) flash is incidental —
> the operator clicking "Upload" after the Start dialog — and is **not** a required delay. The retry
> budget is the real mechanism; sending page 0 immediately is correct because it is retried until
> the erase completes. (Contrast with the `END → RESTART` settle in §6, which *is* a required wait
> because nothing else gates it.)

---

## 5. Whole-batch sequence (excluding bootloader)

`SparkBatchUpdateService.ExecuteAsync` — areas in canonical order, single restart at the end:

```
── Area 1  HMI application ──────────────────────────────
   START_PROCEDURE              → 0x000702C1
   PROGRAM_BLOCK × 649          → 0x000702C1   (pageNum 0…648)
   ‹settle 5 s›
   END_PROCEDURE                → 0x000702C1
── Area 2  HMI Images ───────────────────────────────────
   START_PROCEDURE              → 0x000702C1
   PROGRAM_BLOCK × 5358         → 0x000702C1   (pageNum 0…5357)
   ‹settle 5 s›
   END_PROCEDURE                → 0x000702C1
── Area 3  Motor 1 ──────────────────────────────────────
   START_PROCEDURE              → 0x000702C1
   PROGRAM_BLOCK × 46           → 0x000702C1
   ‹settle 5 s›
   END_PROCEDURE                → 0x000702C1
── Area 4  Motor 2 ──────────────────────────────────────
   START_PROCEDURE              → 0x000702C1
   PROGRAM_BLOCK × 46           → 0x000702C1
   ‹settle 5 s›
   END_PROCEDURE                → 0x000702C1
── Area 5  Rostrum ──────────────────────────────────────
   START_PROCEDURE              → 0x000702C1
   PROGRAM_BLOCK × 24           → 0x000702C1
   ‹settle 5 s›
   END_PROCEDURE                → 0x000702C1
── End of batch ─────────────────────────────────────────
   ‹settle 5 s›
   RESTART_MACHINE              → 0x000702C1   (single, await 0x800A)
```

`pageNum` **resets to 0 at the start of each area**. Restart is sent **once**, only after the
last area's `END_PROCEDURE` succeeds.

**Abort path:** if any area fails (no reply after the retry budget), the batch throws and
**no `RESTART_MACHINE` is sent** — a half-flashed device is not auto-rebooted; recovery is
operator-driven.

---

## 6. The commit settle (required — bench finding 2026-06-15)

The two `‹settle 5 s›` waits are **not** cosmetic and are the difference between a working and a
bricking ECU flash. `CMD_END_PROCEDURE` makes the target board finalize the freshly-written
image (close the flash session, write the validity marker, switch the boot vector); that takes
a few seconds. Firing `RESTART_MACHINE` immediately after the `0x8006` reply reboots the board
**mid-finalize**, leaving the image invalid → the ECU comes up at `v0.0.0.0`.

- A manual flash that worked left **~3.4 s** between the `END` reply and `RESTART`.
- The unguarded batch fired them **~3 ms** apart and bricked the ECU.

`SparkBatchUpdateService` reinstates the window: `_postBlocksSettle` (after the last block,
before `END`) and `_postEndSettle` (after `END`, before `RESTART`), both default **5 s** and
constructor-configurable. They emit no wire traffic. A device-readiness poll would be the
longer-term replacement for fixed delays.

---

## 7. How each command goes on the wire (summary)

Per `PROTOCOL.md`. For BLE each application command is wrapped:

```
Application Layer : [ cmdInit(1) | cmdOpt(1) | payload(N) ]
Transport  Layer  : [ 0x00 | senderId(4 BE)=0x00000008 | lPack(2 BE) | AL | CRC16(2 BE) ]
Network    Layer  : [ NetInfo(2 LE) | recipientId(4 LE)=C1 02 07 00 | chunk(≤98) ] × ceil(len/98)
```

- **senderId** = `Device:SenderId` from `appsettings.json` (default `8`), big-endian on the wire.
- **recipientId** = `0x000702C1`, little-endian in the BLE frame → bytes `C1 02 07 00`.
- **CRC16** = Modbus (init `0xFFFF`, poly `0xA001`) over `[TL header + AL]`, big-endian; **not**
  validated on receive today.
- **NetInfo** carries `remainingChunks | setLength | packetId(1..7) | version`; the last chunk
  has `remainingChunks == 0`.

---

## 8. Concrete example — Rostrum area (bench log app-20260615-174322, with the settle fix)

```
SPARK area start: Rostrum (24576 bytes) → recipient 000702C1
TX START_PROCEDURE → 000702C1            →  RX 8005
TX PROGRAM_BLOCK × 24 → 000702C1         →  RX 8007 × 24   (pageNum 0…23, all result=0x00)
SPARK settle: waiting 5.0s after last block, before END
TX END_PROCEDURE → 000702C1              →  RX 8006
SPARK settle: waiting 5.0s after END, before RESTART (ECU commit window)
TX RESTART_MACHINE → 000702C1            →  RX 800A   (~5 s after the 8006 reply)
SPARK batch end: all selected areas completed
```

Sample frames (Rostrum, BLE; `PROGRAM_BLOCK` chunk 1 of 11 shown):

```
TX START_PROCEDURE   : <NetInfo> C1 02 07 00 | 00 00 00 00 00 08 00 02 00 05 <crc>
                                   └recipient┘   └─ TL: crypt|sender|lPack ─┘ └AL 00 05┘
RX 8005              : … from 000702C1, payload=00
TX PROGRAM_BLOCK     : … 00 07 | 5F 45 | 00 00 00 00 | 00 00 04 00 | 00 00 00 00 | <1024 block…>
                              cmd   fwType   pageNum=0   pageSize=1024  reserved
RX 8007              : … from 000702C1, payload=5F45 00000000 00000400 00 00 00 00 00
```
