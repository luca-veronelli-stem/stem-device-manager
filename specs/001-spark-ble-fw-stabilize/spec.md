# Feature Specification: Spark BLE Batch Firmware Update — Stabilization and Regression Fixes

**Feature Branch**: `001-spark-ble-fw-stabilize`
**Created**: 2026-04-23
**Status**: Draft
**Input**: User description: "Spark BLE batch firmware update — stabilization and regression fixes. Baseline v2.15 behavior lives at commit 80bf9c6. Post-refactor (Phases 3–4) the BLE firmware-update path for the Egicon (→ Spark) variant exhibits defects that block feature work from resuming. This spec stabilizes that path."

## User Scenarios & Testing *(mandatory)*

### User Story 1 — Session lifecycle is crash-free (Priority: P1)

As a technician using the desktop app on a Windows bench laptop, I close the app at the end of a firmware session and relaunch it to start a new one; the new session starts cleanly without errors caused by resources left behind by the previous session.

**Why this priority**: Every other user story depends on being able to open the app and reach a working state. A crash at session boundary blocks the technician from even starting a firmware upload; it is the first failure mode observed on the refactored stack.

**Independent Test**: Open the app, connect to SPARK-UC SN 2225998 over BLE, disconnect, close the app, relaunch. Repeat 10 times. No error dialog, log entry, or exception referencing `ObjectDisposedException` or disposed/stale BLE handles appears in any of the 10 runs.

**Acceptance Scenarios**:

1. **Given** a fresh launch after a previous session ended normally (window closed, no upload in progress), **When** the technician launches the app and initiates a BLE connect to the reference device, **Then** the connect completes without exception and the app reaches the "ready to select firmware" state.
2. **Given** the app was killed while a BLE connection was active (window closed without explicit disconnect), **When** the technician relaunches the app, **Then** the next session does not inherit any stale handle state from the previous process and BLE connect behaves as in scenario 1.

---

### User Story 2 — UI connection state matches reality (Priority: P1)

As a technician on the bench, when the app's UI says "connected" I can trust that the BLE link is actually live, and when the link drops the UI reflects that within a bounded time so I do not waste a firmware-upload attempt on a dead connection.

**Why this priority**: A false "connected" indicator causes the technician to start an upload that silently fails, burning bench time. Trustworthy state is a precondition for every upload-related story below.

**Independent Test**: With the app showing "connected" to SPARK-UC, power the device off at the switch. Within a bounded time (to be captured as an FR), the UI transitions to "disconnected". Power the device back on and reconnect from the UI; the UI returns to "connected" only after the link is re-established. Repeat 10 times.

**Acceptance Scenarios**:

1. **Given** the UI indicator reads "connected", **When** the technician attempts a firmware command, **Then** the command is either accepted by a live device or fails with a clear "device unreachable" error — never a silent no-op.
2. **Given** the device is powered off while the app shows "connected", **When** the power-off event propagates through the BLE stack, **Then** the UI transitions to "disconnected" within a bounded time (the bound is defined in FR-002).

---

### User Story 3 — BLE session survives a full firmware upload (Priority: P1)

As a technician performing a single-file firmware upload, the BLE link stays up for the full duration of the upload so the upload completes without me having to restart it because of an unexpected mid-upload disconnect.

**Why this priority**: Mid-upload disconnects turn a 10-minute operation into a multi-hour retry loop and may leave the device in a half-flashed state. Session survival gates every upload-completion criterion below.

**Independent Test**: Connect to SPARK-UC over BLE, upload `WORKCODE_HMI_RSC.bin` (HMI v8.2). The link does not drop before `EndBootAsync` returns success. Repeat 10 times.

**Acceptance Scenarios**:

1. **Given** a live BLE connection and a well-formed firmware file, **When** the technician starts a single-file upload, **Then** the upload progresses through the full state sequence (start → upload blocks → end → restart) without a disconnect event, on 10/10 consecutive attempts on the reference device.
2. **Given** a transient link degradation shorter than the retry budget, **When** the upload encounters a retriable error, **Then** the boot protocol recovers via its bounded retry logic without surfacing the transient as a session drop.

---

### User Story 4 — Multi-file batch upload is all-success with abort-on-first-failure (Priority: P1)

As a technician, when I submit a batch of firmware files (for example the canonical three-file SPARK-UC set), the batch reports success only when every file has been uploaded and verified on-device; if any file fails after retries, the batch aborts immediately, skips the remaining files, and tells me which file failed so I can investigate without scrolling through a pass/fail matrix.

**Why this priority**: The "silent partial success" failure mode is the most dangerous for a firmware workflow because the technician believes the device is fully updated. All-or-nothing semantics (with clear reporting on failure) is the v2.15 baseline behavior and must be preserved.

**Independent Test**: Submit the canonical three-file batch (`WORKCODE_HMI_RSC.bin` / HMI v8.2, `WEMOTOR1.bin` / Motor1 v4.0, `WEMOTOR2.bin` / Motor2 v4.0) to SPARK-UC #2225998 over BLE. On a nominal run, all three files complete and the batch reports success. On a fault-injected run (e.g. one file corrupted), the batch aborts on that file after the retry budget is exhausted, the remaining files are not attempted, and the UI names the failing file.

**Acceptance Scenarios**:

1. **Given** the canonical three-file batch and a nominal bench setup, **When** the technician submits the batch, **Then** every file completes in submission order and the batch reports success only after on-device verification of the final file.
2. **Given** a batch where the second file will fail (deterministically, e.g. corrupted payload) and retries are exhausted, **When** the batch executes, **Then** file 1 completes, file 2 aborts with the failing filename reported, file 3 is not attempted, and the batch overall result is "failed".

---

### User Story 5 — HMI firmware update completes in v2.15 time budget (Priority: P2)

As a technician, an HMI firmware upload on SPARK-UC completes in the same time envelope as it did on the pre-refactor v2.15 build so that the modernization effort does not regress an operation I perform several times a day.

**Why this priority**: Correctness (P1) is first; performance is P2 because a slow upload is painful but not blocking. Still, a ~2× regression (observed: 25–30 min on the refactored stack versus 12–14 min on v2.15) is unacceptable over time and is scoped into this spec rather than deferred.

**Independent Test**: Upload `WORKCODE_HMI_RSC.bin` (HMI v8.2) to SPARK-UC #2225998 over BLE, five consecutive times. The mean wall-clock time from user-initiated start to `EndBootAsync` success is ≤ 14 minutes.

**Acceptance Scenarios**:

1. **Given** a nominal BLE session and the reference HMI firmware file, **When** the technician runs the upload, **Then** wall-clock time for the full single-file upload is within the v2.15 envelope as defined in SC-004.
2. **Given** five consecutive upload runs on the reference bench, **When** times are averaged, **Then** the mean does not exceed 14 minutes and no individual run exceeds 16 minutes (20 % tolerance on a single outlier).

---

### Edge Cases

- Device powered off mid-upload after the retry budget is exhausted: the batch aborts, the app surfaces the failure without crashing, and a subsequent connect attempt on the re-powered device succeeds.
- BLE adapter removed (USB unplug) while connected: connection state transitions to "disconnected" within the bounded time and the app does not crash.
- App killed (Task Manager / power loss) during an active upload: on the next launch the app does not inherit any stale in-memory state from the previous process; the device may be in a half-flashed state, which the technician resolves by restarting the batch (out of scope for automation in this spec).
- Two instances of the app launched concurrently by accident: defined behavior is out of scope; document that only one instance should own a BLE session, but do not add enforcement in this spec.
- Firmware file missing from disk or unreadable at batch start: batch fails before any on-device state is changed, with the missing filename reported.
- Corrupted firmware file that passes file-system read but fails on-device verification: treated as a per-file failure; triggers the retry budget, then the abort path in US4.
- Retry budget exhausted on the first boot step (device never acks `StartBoot`): the batch aborts with the affected filename reported; no subsequent files are attempted.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST release all BLE and hardware-driver resources on app shutdown such that a subsequent launch of the app does not raise `ObjectDisposedException` or surface stale-handle errors when initiating a new BLE session.
- **FR-002**: System MUST transition the user-visible "connected" indicator to "disconnected" within a bounded time after the BLE link is lost, where the bound is ≤ 5 seconds from the physical link-loss event on the reference bench.
- **FR-003**: System MUST maintain a BLE session for the full duration of a firmware upload under nominal bench conditions, recovering from transient errors within the bounded retry budget without surfacing them as session drops.
- **FR-004**: System MUST complete a single-file HMI v8.2 firmware upload on the reference device within a mean wall-clock time of 14 minutes over 5 runs, and no single run exceeds 16 minutes.
- **FR-005**: System MUST process a multi-file batch upload sequentially in the order submitted; the batch overall result is "success" if and only if every file's per-file upload completes and is verified on-device.
- **FR-006**: On per-file failure after the retry budget is exhausted, the system MUST (a) abort the batch, (b) skip remaining files without attempting them, and (c) report the failing filename to the user.
- **FR-007**: The retry logic within the boot protocol MUST have a bounded retry count per boot step such that no step can retry indefinitely; the bound is a configurable policy value owned by the boot service.
- **FR-008**: Firmware-upload success rate on the reference device under nominal bench conditions MUST be ≥ 95 % across 10 consecutive attempts.
- **FR-009**: Firmware-upload state transitions MUST be traceable: every state change in the boot state machine and every retry is recorded in the application log with enough context (filename, step, retry count) to post-mortem a failed run.
- **FR-010**: When the batch fails due to a missing or unreadable firmware file at batch start, the system MUST report the specific filename and MUST NOT modify any on-device state (no partial flash).

### Key Entities

- **Firmware File**: A single binary firmware artifact on the technician's disk, identified by its on-disk filename (e.g. `WORKCODE_HMI_RSC.bin`) and a human-readable target (e.g. "HMI v8.2"). Includes the on-device target component the file flashes (HMI, Motor1, Motor2, bootloader, …).
- **Batch Upload Job**: An ordered list of firmware files submitted as a single unit. Holds an overall status (`pending` / `in-progress` / `succeeded` / `failed`) plus a per-file status. When any per-file status transitions to `failed`, the job transitions to `failed` and remaining files are skipped.
- **BLE Session**: A live BLE connection to a single device. Holds identity (device name / address / serial), negotiated parameters (MTU, …), connection state (`disconnected` / `connecting` / `connected` / `disconnecting`), and timestamps for the last state transitions.
- **Boot Step**: One discrete phase in the boot state machine (start-boot handshake, block upload, end-boot handshake, restart). Holds a retry counter bounded by FR-007.
- **Reference Bench Configuration**: The canonical hardware + firmware setup used to validate every measurable success criterion of this spec. Device SPARK-UC SN 2225998; single-file perf reference HMI v8.2 (`WORKCODE_HMI_RSC.bin`); canonical three-file batch (in order): `WORKCODE_HMI_RSC.bin` / HMI v8.2, `WEMOTOR1.bin` / Motor1 v4.0, `WEMOTOR2.bin` / Motor2 v4.0. Baseline behavior at commit `80bf9c6` (v2.15).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Zero `ObjectDisposedException` or stale-handle errors observed across 10 consecutive close-and-relaunch cycles of the app on the reference bench.
- **SC-002**: UI connection state matches the physical BLE link in 10/10 manually-induced disconnect events (device power-off) within ≤ 5 seconds of the physical event.
- **SC-003**: BLE session survives 10/10 full single-file HMI uploads on the reference device without a mid-upload disconnect.
- **SC-004**: HMI v8.2 single-file upload mean wall-clock time ≤ 14 minutes over 5 runs on SPARK-UC #2225998; no individual run exceeds 16 minutes.
- **SC-005**: Firmware-upload success rate ≥ 95 % across 10 consecutive single-file runs on the reference device under nominal bench conditions.
- **SC-006**: Canonical three-file batch (`WORKCODE_HMI_RSC.bin`, `WEMOTOR1.bin`, `WEMOTOR2.bin`) succeeds 3/3 files on ≥ 10 consecutive batch runs on SPARK-UC #2225998.
- **SC-007**: On a fault-injected per-file failure (one deliberately corrupted file in position *k* of a batch of size *N* ≥ 2), the batch aborts after the retry budget is exhausted on file *k*, files in positions > *k* are not attempted, and the UI names file *k* as the cause — 5/5 fault-injected runs.

## Assumptions

- Bench hardware is in nominal condition for all measurements; observed defects are software-side. A hardware issue on the reference SPARK-UC would invalidate the numeric success criteria and must be ruled out before the spec is accepted as met.
- All files in the canonical three-file batch transfer over BLE via the same firmware-upload pipeline (no CAN or Serial fallback). If any Motor firmware is found to require a different bus on the reference device, that discovery reopens the spec scope.
- The BLE driver in play for this spec is the legacy Plugin.BLE-backed driver currently in the refactored stack; the `Stem.Communication` NuGet migration is Phase 5 work and out of scope here.
- The performance regression root cause is not pre-decided. It will be identified by comparing runtime behavior against the v2.15 baseline at commit `80bf9c6`. The fix is in scope; the specific code change is a planning-phase decision (`/speckit.plan`).
- All four user categories (field technician, R&D engineer, end-customer operator, dev team) observe the same behavior; this spec does not differentiate permissions or feature visibility by user category.
- Only the Egicon (→ Spark) variant is exercised and validated by this spec. Fixes may transparently benefit other variants via shared code but this is not validated.
- "Nominal bench conditions" means: reference device powered and in range, no other BLE-intensive workload on the host, host laptop not sleeping, no Windows Update interruption during a measurement run.
