# Perf-Regression Investigation — Spec 001 / US5

> **Status:** scaffold only. Bench measurements pending.
>
> **Spec:** [spec.md](../specs/001-spark-ble-fw-stabilize/spec.md) — US5, SC-004
> **Research:** [research.md](../specs/001-spark-ble-fw-stabilize/research.md) — R7 (H1–H4)
> **Bench runbook:** [quickstart.md](../specs/001-spark-ble-fw-stabilize/quickstart.md) — SC-004 procedure
> **Tracks:** spec-001 T019 (this doc) → T020 (root-cause fix) → T021 (≤ 16 min single-run regression test)

---

## 1. Goal

Quantify the ~2× regression observed in HMI v8.2 single-file upload time
between v2.15 (commit `80bf9c6`) and current `main`. Produce per-step
wall-clock deltas, then rank the four candidate hypotheses (H1–H4 from
research.md R7) by the evidence in those deltas. The result feeds T020
(the surgical fix) and T021 (the regression-guard test).

A "fix is acceptable" gate is defined in
[quickstart.md:113](../specs/001-spark-ble-fw-stabilize/quickstart.md):
branch-under-test mean within **15 %** of the v2.15 mean.

---

## 2. Bench setup

### Devices and firmware

- **DUT:** SPARK-UC SN **2225998** (canonical reference bench).
- **Firmware payload:** `WORKCODE_HMI_RSC.bin` (HMI v8.2). Single file,
  not a batch.
- **Host:** Luca's dev laptop, plugged in, no Windows Update active, no
  other BLE traffic on the host adapter.

### Two checked-out builds

```powershell
# Already created on this branch:
git worktree list
# C:/Users/lucav/source/repos/Stem.Device.Manager       <main HEAD>
# C:/Users/lucav/source/repos/Stem.Device.Manager-v215  80bf9c6 (detached HEAD)
```

- **Branch under test:** `main` (or whichever feature branch carries the
  T015–T018 work). Built and run from
  `C:/Users/lucav/source/repos/Stem.Device.Manager`.
- **v2.15 baseline:** detached HEAD at `80bf9c6` in
  `C:/Users/lucav/source/repos/Stem.Device.Manager-v215`. Pre-refactor
  WinForms layout (flat `*.cs` at repo root, `App.STEMProtocol.BootManager`
  is the upload entry point — no `Services/` or `Infrastructure.*`
  namespaces yet).

### Instrumentation

| Side             | Source             | Mechanism                                                                                                                                                                                            |
| ---------------- | ------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Branch under test| `BootService` (T003) | FR-009 structured `ILogger.BeginScope` with `{ Area, Step, Attempt, Recipient }`. One `LogInformation` per state transition, one `LogWarning` per retry. Emit per-step `Stopwatch` delta on entry/exit. |
| v2.15 baseline   | `BootManager.UploadFirmware` (`80bf9c6`) | Add an ad-hoc `Stopwatch` around the same four steps (`CMD_START_PROCEDURE` / block loop / `CMD_END_PROCEDURE` / restart). Write to a temp text file (`%TEMP%\sparkfwbench-v215.txt`) — the old stack has no structured logger. **Do not commit this instrumentation** to the v2.15 worktree; it is throw-away.|

Both builds run **3 uploads each**. Cold-start each upload (relaunch the
app, reconnect BLE) so the first-block latency is included in every
sample.

---

## 3. Procedure

For each build (`main` then v2.15, or interleave to balance any
laptop-thermal drift):

1. Launch the app in the appropriate worktree.
2. Connect to SPARK-UC.
3. Single-file HMI flow → `WORKCODE_HMI_RSC.bin`.
4. Start the upload. **Capture the full structured-log output** (or the
   `%TEMP%` file for v2.15) for the run.
5. When the boot service reports `Succeeded`, record the per-step
   wall-clock from the logs into the table below.
6. Repeat for runs 2 and 3.

A third data source — Visual Studio 2026 Performance Profiler on the
single hottest run from each build — is optional but recommended per
research.md R7 to triangulate any ambiguous timing delta against actual
CPU/wait attribution.

---

## 4. Hypotheses to rank (research.md R7)

| ID  | Hypothesis                                                                                                  | Where to look in the deltas                                                                                                                                                  |
| --- | ----------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| H1  | BLE MTU re-negotiation per chunk on the refactored path.                                                    | If per-block ack latency (table §5.2) is ~2× v2.15's, with the gap roughly constant per block, H1 is favoured. Cross-check with `BLEManager.SendMessageAsync` MTU log lines. |
| H2  | Extra `ConfigureAwait(false)` serialisations in the refactored call chain.                                  | If the gap is small per block but the *aggregate* wall-clock between block N+1 and block N drifts upward over time, H2 is favoured. Profiler will show thread-pool waits.   |
| H3  | CRC verification round-trip added during refactor.                                                          | If the per-block ack latency on `main` shows a fixed bimodal cost (one extra ~few-ms spike per block), H3 is favoured. Look for a CRC-verify log line that v2.15 lacked.    |
| H4  | Logging I/O in the hot path (FR-009 `LogInformation` per block).                                            | If the gap shrinks materially when the run is repeated with the log sink raised to `Warning` (drop the per-block `LogDebug` lines), H4 is favoured.                          |

H4 has the cheapest test: re-run one upload with the `Logging` section
in `appsettings.json` set to `Warning` and compare the wall-clock against
the same build's `Information` baseline. If the delta vanishes, H4 is
the cause; if it doesn't, H4 is ruled out and H1–H3 split the evidence.

---

## 5. Data tables

### 5.1 Per-step wall-clock (seconds; 3 runs per build)

| Step                | v2.15 r1 | v2.15 r2 | v2.15 r3 | v2.15 mean | main r1 | main r2 | main r3 | main mean | Δ (main − v2.15) | Δ %   |
| ------------------- | -------- | -------- | -------- | ---------- | ------- | ------- | ------- | --------- | ---------------- | ----- |
| `StartProcedure`    | _t.b.f._ |          |          |            |         |         |         |           |                  |       |
| `ProgramBlock` loop | _t.b.f._ |          |          |            |         |         |         |           |                  |       |
| `EndProcedure`      | _t.b.f._ |          |          |            |         |         |         |           |                  |       |
| `RestartMachine` ×2 | _t.b.f._ |          |          |            |         |         |         |           |                  |       |
| **Total**           |          |          |          |            |         |         |         |           |                  |       |

### 5.2 Per-chunk ack latency (milliseconds; aggregate across the 3 runs)

| Build  | Blocks (n) | Mean ack latency | p50 | p95 | Max |
| ------ | ---------- | ---------------- | --- | --- | --- |
| v2.15  | _t.b.f._   |                  |     |     |     |
| main   | _t.b.f._   |                  |     |     |     |
| Δ      |            |                  |     |     |     |

Compute the percentiles from the FR-009 log scopes (`Step = ProgramBlock`,
elapsed between consecutive entries with the same `Recipient`). For v2.15
extract from the ad-hoc `%TEMP%` file with a one-shot script (the file is
flat text, no need to keep the script after analysis).

### 5.3 Visual Studio Performance Profiler — top 5 hot frames per build

| Build  | Frame                              | Inclusive %  | Notes                |
| ------ | ---------------------------------- | ------------ | -------------------- |
| v2.15  | _t.b.f._                           |              |                      |
| main   | _t.b.f._                           |              |                      |

---

## 6. Hypothesis ranking (fill after §5 is populated)

Rank by strength of evidence. One sentence per row pointing at the §5
data that argues for or against the hypothesis. The top of the list is
the proposed root cause that T020 fixes.

| Rank | Hypothesis | Evidence                |
| ---- | ---------- | ----------------------- |
| 1    | _t.b.f._   |                         |
| 2    | _t.b.f._   |                         |
| 3    | _t.b.f._   |                         |
| 4    | _t.b.f._   |                         |

---

## 7. Conclusion and handoff to T020

_T.b.f. once §5 and §6 are filled in._ Expected output of this section:

- Identified root cause (one of H1–H4, or a fifth hypothesis surfaced by
  the profiler).
- Concrete code location to fix in T020 (most likely candidates per
  research.md R7: `Services/Boot/BootService.cs` chunk loop /
  `Infrastructure.Protocol/Hardware/BlePort.cs` MTU handling /
  `Infrastructure.Protocol/Legacy/BLEManager.cs` notification vs
  indication wiring).
- Predicted wall-clock improvement on `main` after the T020 fix and the
  T021 regression-guard threshold (≤ 16 min single-run, mean ≤ 14 min /
  spec SC-004).

---

## 8. Cleanup after the investigation

```powershell
# Remove the v2.15 worktree once T020 is merged and the deltas are no longer needed.
git worktree remove ../Stem.Device.Manager-v215
```

The throw-away `Stopwatch` instrumentation in the v2.15 worktree is
discarded with the worktree itself.
