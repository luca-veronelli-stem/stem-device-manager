import Mathlib

/-!
# Boot state machine (spec-001, T005 + T022)

Per-area firmware upload state machine. State, transitions, and the four
preservation theorems T1..T4:

- T1 (offset-total): every reachable `Uploading off tot _` has `off ≤ tot`.
- T2 (retry-bounded): every reachable `Uploading _ _ att` has `att ≤ RetryBudget`.
- T3 (terminal stability): `Succeeded` and `Failed` have no outgoing transitions.
- T4 (phase-preservation on abort): each `Failed phase _` arrival is reached
  from the source state corresponding to `phase`.

See `specs/001-spark-ble-fw-stabilize/data-model.md` for the source of truth;
every transition label in this file corresponds to one bullet in the
"Boot Step (per-area state machine) — Transitions" list there.
-/

namespace Spec001.BootStateMachine

/-- Phase tag attached to `Failed`, identifying which boot step raised
    the failure. One constructor per `IBootService` method. -/
inductive BootPhase where
  | StartBoot
  | UploadBlocks
  | EndBoot
  | Restart
  deriving DecidableEq, Repr

/-- Per-area boot state.

    `Uploading` carries `(offset, total, attempt)`:
    - `offset` — bytes already acknowledged by the device.
    - `total`  — total firmware length in bytes.
    - `attempt` — current retry counter for the in-flight chunk.

    `Failed` carries the phase that raised the failure and a free-form cause. -/
inductive BootState where
  | Idle
  | AwaitingStart
  | Uploading (offset : Nat) (total : Nat) (attempt : Nat)
  | AwaitingEnd
  | Restarting
  | Succeeded
  | Failed (phase : BootPhase) (cause : String)
  deriving Repr

/-- Retry budget (FR-007, research.md R10). Abstract here; the C# value
    is injected via `BootService`'s constructor. -/
abbrev RetryBudget : Nat := 3

/-- Single-step transition relation. Exact labels per data-model.md;
    each constructor corresponds to one transition bullet. -/
inductive Step : BootState → BootState → Prop where
  -- `Idle → AwaitingStart` on `StartBootAsync` invocation.
  | startBootInvoked :
      Step .Idle .AwaitingStart
  -- `AwaitingStart → Uploading 0 total 1` on ack received.
  | startBootAcked (total : Nat) :
      Step .AwaitingStart (.Uploading 0 total 1)
  -- `AwaitingStart → Failed StartBoot _` on retry exhaustion.
  | startBootExhausted (cause : String) :
      Step .AwaitingStart (.Failed .StartBoot cause)
  -- `Uploading off tot att → Uploading (off + chunk) tot att` on chunk ack,
  -- while `off + chunk ≤ tot`.
  | chunkAcked (off tot att chunk : Nat) (_h : off + chunk ≤ tot) :
      Step (.Uploading off tot att) (.Uploading (off + chunk) tot att)
  -- `Uploading off tot att → Uploading off tot (att + 1)` on retriable
  -- error, when `att < RetryBudget`.
  | chunkRetry (off tot att : Nat) (_h : att < RetryBudget) :
      Step (.Uploading off tot att) (.Uploading off tot (att + 1))
  -- `Uploading off tot RetryBudget → Failed UploadBlocks _` when the last
  -- allowed attempt fails.
  | chunkExhausted (off tot : Nat) (cause : String) :
      Step (.Uploading off tot RetryBudget) (.Failed .UploadBlocks cause)
  -- `Uploading tot tot _ → AwaitingEnd` when all chunks are acked.
  | uploadComplete (tot att : Nat) :
      Step (.Uploading tot tot att) .AwaitingEnd
  -- `AwaitingEnd → Restarting` on `EndBootAsync` ack.
  | endBootAcked :
      Step .AwaitingEnd .Restarting
  -- `AwaitingEnd → Failed EndBoot _` on retry exhaustion.
  | endBootExhausted (cause : String) :
      Step .AwaitingEnd (.Failed .EndBoot cause)
  -- `Restarting → Succeeded` on `RestartAsync` ack.
  | restartAcked :
      Step .Restarting .Succeeded
  -- `Restarting → Failed Restart _` on retry exhaustion.
  | restartExhausted (cause : String) :
      Step .Restarting (.Failed .Restart cause)

/-- Reflexive–transitive closure of `Step`. Used to define `Reachable`. -/
inductive Reaches : BootState → BootState → Prop where
  | refl (s : BootState) : Reaches s s
  | step {s t u : BootState} : Reaches s t → Step t u → Reaches s u

/-- A state is reachable from the initial `Idle`. -/
def Reachable (s : BootState) : Prop := Reaches .Idle s

/-! ### T022 — preservation theorems (US3 / FR-007) -/

/-- Single inductive invariant carrying T1 (offset-total) and T2 (retry-bounded)
    for `Uploading` states; trivially `True` everywhere else. -/
def UploadInvariant : BootState → Prop
  | .Uploading off tot att => off ≤ tot ∧ att ≤ RetryBudget
  | _ => True

private theorem invariant_idle : UploadInvariant .Idle := trivial

private theorem invariant_step {s t : BootState}
    (h_inv : UploadInvariant s) (h_step : Step s t) : UploadInvariant t := by
  cases h_step with
  | startBootInvoked => trivial
  | startBootAcked total =>
      exact ⟨Nat.zero_le _, by decide⟩
  | startBootExhausted _ => trivial
  | chunkAcked off tot att chunk h =>
      obtain ⟨_, h_att⟩ := h_inv
      exact ⟨h, h_att⟩
  | chunkRetry off tot att h =>
      obtain ⟨h_off, _⟩ := h_inv
      exact ⟨h_off, h⟩
  | chunkExhausted _ _ _ => trivial
  | uploadComplete _ _ => trivial
  | endBootAcked => trivial
  | endBootExhausted _ => trivial
  | restartAcked => trivial
  | restartExhausted _ => trivial

private theorem invariant_reaches {s t : BootState}
    (h_reach : Reaches s t) (h_inv : UploadInvariant s) : UploadInvariant t := by
  induction h_reach with
  | refl => exact h_inv
  | step _ h_step ih => exact invariant_step ih h_step

private theorem reachable_invariant {s : BootState}
    (h : Reachable s) : UploadInvariant s :=
  invariant_reaches h invariant_idle

/-- **T1 (offset-total).** Every reachable `Uploading` state has its offset
    bounded by the total firmware length. -/
theorem reachable_uploading_offset_le_total {off tot att : Nat}
    (h : Reachable (.Uploading off tot att)) : off ≤ tot :=
  (reachable_invariant h).1

/-- **T2 (retry-bounded, FR-007).** Every reachable `Uploading` state has its
    attempt counter bounded by `RetryBudget`. -/
theorem reachable_uploading_attempt_le_budget {off tot att : Nat}
    (h : Reachable (.Uploading off tot att)) : att ≤ RetryBudget :=
  (reachable_invariant h).2

/-- **T3 (terminal stability) — `Succeeded`.** No transition leaves
    `Succeeded`. Direct case analysis on `Step`: no constructor has
    `Succeeded` as its source. -/
theorem succeeded_no_step : ∀ t, ¬ Step .Succeeded t := by
  intro t h; cases h

/-- **T3 (terminal stability) — `Failed`.** No transition leaves `Failed`. -/
theorem failed_no_step (phase : BootPhase) (cause : String) :
    ∀ t, ¬ Step (.Failed phase cause) t := by
  intro t h; cases h

/-- **T4 (phase-preservation on abort).** A `Failed StartBoot _` is only
    reached from `AwaitingStart`. -/
theorem failed_startBoot_source {s : BootState} {cause : String}
    (h : Step s (.Failed .StartBoot cause)) : s = .AwaitingStart := by
  cases h
  rfl

/-- **T4 (phase-preservation on abort).** A `Failed UploadBlocks _` is only
    reached from `Uploading _ _ RetryBudget` (the post-budget exhaustion). -/
theorem failed_uploadBlocks_source {s : BootState} {cause : String}
    (h : Step s (.Failed .UploadBlocks cause)) :
    ∃ off tot, s = .Uploading off tot RetryBudget := by
  cases h with
  | chunkExhausted off tot _ => exact ⟨off, tot, rfl⟩

/-- **T4 (phase-preservation on abort).** A `Failed EndBoot _` is only
    reached from `AwaitingEnd`. -/
theorem failed_endBoot_source {s : BootState} {cause : String}
    (h : Step s (.Failed .EndBoot cause)) : s = .AwaitingEnd := by
  cases h
  rfl

/-- **T4 (phase-preservation on abort).** A `Failed Restart _` is only
    reached from `Restarting`. -/
theorem failed_restart_source {s : BootState} {cause : String}
    (h : Step s (.Failed .Restart cause)) : s = .Restarting := by
  cases h
  rfl

end Spec001.BootStateMachine
