import Mathlib

/-!
# Boot state machine (spec-001, Phase 2, T005)

Per-area firmware upload state machine. Types and transitions only — the
preservation theorems T1..T4 are deferred to T022.

See `specs/001-spark-ble-fw-stabilize/data-model.md` for the source of truth;
every transition label in this file corresponds to one bullet in the
"Boot Step (per-area state machine) — Transitions" list there.
-/

namespace Phase2.BootStateMachine

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

end Phase2.BootStateMachine
