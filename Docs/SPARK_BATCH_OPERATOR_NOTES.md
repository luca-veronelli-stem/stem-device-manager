# SPARK Batch Firmware Update — Operator Notes

Practical notes for whoever runs the SPARK firmware update from the **SPARK Firmware Update** tab.
For the exact protocol commands see [`SPARK_BATCH_COMMANDS.md`](SPARK_BATCH_COMMANDS.md); for the
full official procedure (USB, Bluetooth module, calibration) see
[`SPARK_UPDATE_FW.md`](SPARK_UPDATE_FW.md).

Bench-validated 2026-06-15 / 2026-06-17 on Egicon SN 2225998 (two consecutive clean full-batch runs).

---

## 1. What this tab flashes — and what it does NOT

Over BLE, in one batch: **HMI application, HMI Images, Motor 1, Motor 2, Rostrum.**

It does **not** flash:
- **The Bluetooth module** (`BGM220…` file) — that's FASE 1, done with Silicon Labs'
  **Simplicity Connect** mobile app (`.gbl`/`.bin`), not this tool. The file carries no STEM
  work-code header and is not a batch area.
- **Bootloader HMI** — separate step (see `SPARK_UPDATE_FW.md`).

---

## 2. Pick the right file — DO NOT trust the filename ⚠️

Firmware filenames are **ambiguous**: a cloud download can produce two files that differ only by a
`(1)` suffix (Windows dedup), and that suffix does **not** tell you which board. The authoritative
identifier is the **work-code string in the file header** (ASCII, ~bytes 10–17). The tool logs it on
file selection: `SPARK batch file: <area> … header=…`.

| Work-code | Board / area | Example file (bundle `Spark_08.03`) | Size |
|---|---|---|---|
| `WCODE_HMI` | HMI application | `DIS0016201_1_08.02.00.01.bin` | 664 576 |
| `IMAGES_HMI` | HMI Images | `DIS0016201_1_08.02.00.01 (1).bin` | 672 800 |
| `WCODE_E2` | **Motor 1 (right)** | `DIS0016204_1_04.01.00.00.bin` | 47 104 |
| `WCODE_E3` | **Motor 2 (left)** | `DIS0016204_1_04.01.00.00 (1).bin` | 47 104 |
| `WCODE_E1` | Rostrum | `DIS0016202_1_03.01.00.00.bin` | 24 576 |

The two **motor** files are the trap: same name except `(1)`, both 47 104 B — only the work-code
(`E2` vs `E3`) distinguishes them. The HMI app/images pair has the same trap. **Always confirm by
work-code, not by filename or by the `(1)`.** (E1 = Rostrum, E2 = Motor 1, E3 = Motor 2.)

---

## 3. What's normal during a flash (don't be alarmed)

All of the following are expected and were present on every successful run:

- **A single `FF` byte / `Discarded short Ble frame: 1 bytes … Bytes: FF`** right after the very
  first `START`. It's the device entering bootloader mode **once**; harmless. Appears only on the
  first area, never again in the batch.
- **The first `START` takes ~3.5 s** (later areas ~0.1 s). That's the one-time
  application→bootloader transition; the device then stays in the bootloader for the whole batch.
- **One or two early `Retrying ProgramBlock`** at the **start of each area**. That's the first page
  waiting out the device-side flash erase (the bootloader only acks once the flash is ready) — not
  an error. See `SPARK_BATCH_COMMANDS.md` §4.1.
- **`SPARK settle: waiting 5.0s …` lines** — the deliberate commit waits (see §5). One per area
  before `END`, plus one before the final reboot.
- **`result 0x00`** on every page acknowledgement = success.

A genuine problem looks like an `[ERRO] SPARK area failed: … at phase …` line and a popup.

---

## 4. How long it takes

~1 second per 1 KB block, plus the settles.

- Full 5-area batch with the **~672 KB test** HMI Images: **~25–27 min**.
- The **full ~5.5 MB production** HMI Images alone is ~5 358 blocks ≈ **~90 min** — plan accordingly
  if you flash the real images.

---

## 5. Why the reboot waits at the end (the commit settle)

After `END_PROCEDURE` the target board **finalizes** its freshly-written image (closes the flash
session, writes the validity marker, switches the boot vector) — this takes a few seconds. The tool
waits **5 s after `END` before the reboot**, plus 5 s after each area's last block, so the commit
completes.

**This wait is load-bearing.** Without it the batch rebooted the ECU mid-finalize and **bricked it
to `v0.0.0.0`**. That was the bug; the settle is the fix. So the batch is intentionally a little
slower at each boundary — that's the safety window, not a hang.

---

## 6. If a flash fails

- The batch **stops at the failing area and does NOT reboot** — a half-flashed device must not be
  auto-rebooted, so it's left in a known state for recovery.
- A bricked / unfinished ECU reports **`v0.0.0.0`** and is unresponsive.
- **Recover via USB**: re-flash the affected board from the USB procedure
  (`WORKCODE.hex` / `WEMOTORx.hex` / `WEROSTRO.hex`, see `SPARK_UPDATE_FW.md` FASE 2–5). USB uses the
  HMI's own programmer and is the reliable fallback.

---

## 7. Running alongside another app that needs the CAN bus

By default the tool opens the **PCAN-USB (CAN)** bus at boot. To leave the bus free for another
process, disable CAN auto-start:

- `appsettings.json` → `"Can": { "AutoStart": false }`, **or**
- environment variable `Can__AutoStart=false` at launch.

With auto-start off, the CAN bus is untouched until you explicitly select the CAN channel in the
app. **SPARK updates are BLE**, so this does not affect them — confirmed by a full 27-minute batch
run with CAN auto-start disabled and the bus left free the whole time.

---

## 8. Quick checklist

1. Connect to SPARK over BLE; confirm with a version read (App Layer tab).
2. In the SPARK Firmware Update tab, assign each file to its area **by work-code** (§2).
3. Start the batch. Expect the §3 artifacts; watch for `SPARK area done` per area.
4. Let it run to `SPARK batch end: all selected areas completed` + the final reboot — **do not close
   the app mid-batch.**
5. After reboot, verify every board's version on the device (all 3 ECUs + HMI).
6. On any failure: don't reboot manually; recover the affected board via USB (§6).
