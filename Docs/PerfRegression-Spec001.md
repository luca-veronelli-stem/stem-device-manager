# Perf-Regression Investigation — Spec 001 / US5

> **Status:** investigation closed without identified root cause. Regression
> appears resolved between the original measurement (~26 min) and the
> 2026-04-27 bench (3 runs, all 13–15 min, within SC-004 budget). T020
> (root-cause fix) **skipped** — nothing to fix. T021 (≤ 16 min single-run
> guard test) is the safety net against recurrence.
>
> **Spec:** [spec.md](../specs/001-spark-ble-fw-stabilize/spec.md) — US5, SC-004
> **Research:** [research.md](../specs/001-spark-ble-fw-stabilize/research.md) — R7 (H1–H4)
> **Bench runbook:** [quickstart.md](../specs/001-spark-ble-fw-stabilize/quickstart.md) — SC-004 procedure
> **Tracks:** spec-001 T019 (this doc) → ~~T020~~ → T021 (≤ 16 min single-run regression test, issue #31)

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

**Not captured.** The bench session on 2026-04-27 measured aggregate wall-clock
only (3 runs, all in the 13–15 min band against SPARK-UC SN 2225998 with
`WORKCODE_HMI_RSC.bin` HMI v8.2). Per-step breakdown was not recorded because
once the aggregate landed inside the SC-004 budget the investigation pivoted
to T021 (guard) instead of root-cause attribution.

If the regression resurfaces and SC-004 fails again, this is the table to
fill from FR-009 structured-scope log scrape on the failing run + a
matching baseline from the v2.15 worktree (recreate it with
`git worktree add ../Stem.Device.Manager-v215 80bf9c6` — see §8).

### 5.0 Aggregate observation (2026-04-27)

| Build  | Runs | Wall-clock band | SC-004 status                                |
| ------ | ---- | --------------- | -------------------------------------------- |
| `main` | 3    | 13–15 min       | Within budget (mean ≤ 14 min, no run > 16 min) |

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

## 6. Hypothesis ranking — not performed

Per-step deltas were never captured (see §5.1) so the H1–H4 evidence
matrix in §4 cannot be applied. The four hypotheses remain the right
checklist if the regression resurfaces; the matrix is preserved as-is
for future use.

The merges between the original 26-min measurement and the 2026-04-27
re-measurement were:

| PR | Subject | Plausible runtime impact |
| --- | ------- | ------------------------ |
| #63 | `FallbackDictionaryProvider` falls back to Excel on HttpClient timeout | Negligible (dictionary path only, not boot) |
| #64 | T013 BootService retry-logic audit (P3/Q1/I1) | Possible — changed cancellation/session-loss semantics around `SendWithRetry` |
| #65 | Skip dispose-recreate when switching to already-active channel | **Most-likely candidate** — if a spurious channel switch was happening mid-upload before this PR, the dispose-recreate cost is exactly the kind of hidden ~2× overhead consistent with the original symptom |
| #66 | T014 FsCheck property tests for `BootStateMachine` | Test-only, no runtime impact |
| #67 | CI mirror-to-Bitbucket workflow | CI-only, no runtime impact |

#65 is the load-bearing guess. **Not verified** by bisection; the bench
cost (3+ uploads × 13–15 min each at minimum to disambiguate from
variance) was judged not worth it given the regression is gone and
T021 is the safety net.

---

## 7. Conclusion

The ~2× regression that motivated US5 / SC-004 is **not reproducing on
`main` as of 2026-04-27**. Three uploads on the canonical bench
(SPARK-UC SN 2225998, `WORKCODE_HMI_RSC.bin` HMI v8.2) all completed
inside the 13–15 min band, satisfying SC-004 (mean ≤ 14 min, no single
run > 16 min) without any targeted intervention.

**Root cause:** unidentified. Most-likely candidate is PR #65
(skip dispose-recreate for already-active channel) but this is a guess,
not a verified attribution. Other plausible explanations — bench
environment differences, Plugin.BLE / Windows BLE stack behaviour
changes, or simple BLE-link variance (RF environment can swing
upload time 30–50 % run-to-run on its own) — cannot be ruled out
without a controlled bisection that was judged not worth the bench cost.

**T020 (root-cause fix) is skipped.** Nothing to fix.

**T021 (≤ 16 min single-run regression test, issue #31)** is the safety
net. With deterministic auto-reply fakes the assertion is trivially
true (a fake-driven upload completes in seconds), so the test is
functionally a tripwire against any future refactor that introduces
artificial delays in the boot orchestration. Real-bench enforcement
remains the SC-004 procedure in `quickstart.md`.

**If the regression returns:** §4 (hypothesis matrix), §5.1 (per-step
table), and §8 (worktree recreation) provide the playbook.

---

## 8. Cleanup after the investigation

The investigation closed without populating §5 / §6, so the v2.15
worktree is no longer load-bearing. Remove it:

```powershell
git worktree remove ../Stem.Device.Manager-v215
```

If the regression resurfaces and §5.1 needs to be filled, recreate
with:

```powershell
git worktree add ../Stem.Device.Manager-v215 80bf9c6
```

The pre-refactor flat WinForms layout (no `Services/` or
`Infrastructure.*` namespaces) is what the `BootManager.UploadFirmware`
baseline lives in.
