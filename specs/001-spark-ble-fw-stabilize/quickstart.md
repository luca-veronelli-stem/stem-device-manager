# Quickstart: Bench Validation for Spec 001 (Spark BLE Firmware Stabilization)

**Feature**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md) | **Date**: 2026-04-23

This is the runbook a technician executes against the reference bench to validate each success criterion in the spec. Every SC is an observable bench outcome; the steps below produce the evidence.

## Reference bench

| Item | Value |
|---|---|
| Device model | **SPARK-UC** |
| Device serial | **2225998** |
| Host OS | Windows 11 Pro 10.0.26100 |
| Host app | `Stem.Device.Manager` built on branch under test (`Debug` or `Release`) |
| BLE adapter | host internal (no external dongle) |
| Reference single-file firmware | `WORKCODE_HMI_RSC.bin` (HMI v8.2) |
| Reference batch | in submission order: `WORKCODE_HMI_RSC.bin` (HMI v8.2) → `WEMOTOR1.bin` (Motor1 v4.0) → `WEMOTOR2.bin` (Motor2 v4.0) |
| Baseline commit for perf diff | `80bf9c6` (v2.15) on a second worktree |

Firmware binaries live under the team-shared `Docs/Firmwares/` folder (git-ignored per `.gitignore`).

## Prerequisites

- `Stem.Device.Manager` builds and tests pass on the branch under test: `dotnet build Stem.Device.Manager.slnx` and `dotnet test Tests/Tests.csproj` (both TFMs) are green.
- Device variant `Egicon` is active: `GUI.Windows/appsettings.json` has `"Device": { "Variant": "Egicon" }`.
- SPARK-UC is powered, in advertising range, and not currently connected to any other host.
- No Visual Studio instance is attached as a debugger during timed runs (it distorts the measurements).

## Known constraints

- **BLE idle-drop window (issue #70).** After the "Connesso al dispositivo" message appears, the BLE link drops within ~5–10 s if no traffic flows. This is a `Plugin.BLE`-on-Windows behavior — the link supervision-timeout fires because nothing keeps it alive. **Operator workaround until the `Stem.Communication` NuGet (Phase 5) lands**: click "Start upload" within ~5 s of connect. Once a firmware upload is in progress, the chunk traffic itself acts as keepalive and the link holds for the full upload, so SC-003 / SC-004 / SC-006 / SC-007 are unaffected. SC-001 / SC-002 are also unaffected (they don't rely on idle-hold).

## Runbook

Each section maps one-to-one to an SC in the spec.

### SC-001 — Close/relaunch is crash-free (10 cycles)

For *i* in 1..10:

1. Launch `Stem.Device.Manager.exe`.
2. Connect to SPARK-UC over BLE from the UI. Wait for "connected" indicator.
3. Disconnect. Wait for "disconnected".
4. Close the app window (normal close, not Task Manager kill).
5. Record in the bench log any error dialog, exception stack, or log-file entry containing `ObjectDisposedException` / "handle already disposed" / "stale".

**Pass criterion**: 0 occurrences across 10 cycles.

### SC-002 — UI connection state matches reality (10 disconnect events)

For *i* in 1..10:

1. Launch the app, connect to SPARK-UC. Confirm "connected".
2. **Physically power off** SPARK-UC (switch on the device, not the BLE adapter).
3. Start a stopwatch at the moment of the physical switch-off.
4. Watch the UI. Record the wall-clock time at which the indicator transitions to "disconnected".
5. Power SPARK-UC back on. Reconnect from the UI. Confirm "connected" is only displayed after the link is actually live (send a no-op read and confirm a reply).

**Pass criterion**: 10/10 transitions occur within ≤ 5 seconds of the physical event (FR-002).

### SC-003 — Session survives a full HMI upload (10 runs)

For *i* in 1..10:

1. Launch the app, connect to SPARK-UC.
2. Select the single-file HMI upload flow. Choose `WORKCODE_HMI_RSC.bin` (HMI v8.2).
3. Start the upload.
4. Wait for the upload to terminate. Record: did the BLE session drop mid-upload (visible as a "disconnected" UI transition or an error surfaced by the boot service), or did it complete through `RestartAsync`?

**Pass criterion**: 10/10 runs complete without a mid-upload disconnect.

### SC-004 — HMI upload mean wall-clock ≤ 14 min (5 timed runs)

For *i* in 1..5:

1. Prerequisite bench quiet: no Windows Update running, no other BLE traffic, laptop plugged in.
2. Connect to SPARK-UC.
3. **Start a wall-clock timer** the moment the user clicks "Start upload" in the UI.
4. **Stop the timer** the moment the boot service reports `Succeeded` (end of `RestartAsync`).
5. Record the elapsed time.

**Pass criterion**: mean of 5 runs ≤ 14 min; no single run > 16 min.

### SC-005 — Firmware-upload success rate ≥ 95 % (10 runs)

Fold into the SC-003 cycle — the same 10 runs provide success/failure data.

**Pass criterion**: ≥ 10/10 successes (100 %) under nominal bench conditions. If failures occur, capture logs with FR-009 structured scopes to attribute each failure to a phase (StartBoot / UploadBlocks / EndBoot / Restart); acceptable only if root cause is hardware-side and retries exhausted legitimately.

### SC-006 — Canonical three-file batch (10 runs)

For *i* in 1..10:

1. Connect to SPARK-UC.
2. Submit the canonical batch: `WORKCODE_HMI_RSC.bin` / HMI v8.2, `WEMOTOR1.bin` / Motor1 v4.0, `WEMOTOR2.bin` / Motor2 v4.0, in that order.
3. Observe that each file completes in order and the batch reports overall success.

**Pass criterion**: 10/10 batches succeed 3/3 files.

### SC-007 — Fault-injected batch abort (5 runs)

Prerequisite: a deliberately corrupted firmware file `WEMOTOR1.corrupt.bin` (same size as the original, one byte of payload flipped beyond the header so on-device verification rejects it after the retry budget).

For *i* in 1..5:

1. Submit the three-file batch with the corrupted `WEMOTOR1.corrupt.bin` in position 2.
2. Observe:
    - File 1 (HMI) completes.
    - File 2 fails after the retry budget is exhausted. The UI names `WEMOTOR1.corrupt.bin` (or equivalent: the `SparkBatchUpdateException.Area` is `Motor1` and `Cause` is informative).
    - File 3 (Motor2) is **not** attempted.
    - Batch overall result is "failed".

**Pass criterion**: 5/5 runs satisfy all four observations.

## Perf-regression bench procedure (R7 of `research.md`)

When the perf-regression fix lands, re-run the SC-004 procedure on both worktrees (commit `80bf9c6` baseline + the branch-under-test) and record the time delta. The fix is acceptable only when the branch-under-test mean is within 15 % of the baseline mean.

## Reporting

Log each run in `Docs/BenchLog-Spec001.md` (add file if missing, append entries chronologically). Each entry: date, run number, SC id, outcome, elapsed time if applicable, link to the logs captured by FR-009 instrumentation.
