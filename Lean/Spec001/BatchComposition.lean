import Mathlib
import Spec001.BootStateMachine

/-!
# Batch firmware-update composition (spec-001, T005 + T024)

State machine + composition preservation theorem (T024): a successful batch
completes every submitted file (length-equality on the `done` list); a failed
batch reports an area that was actually submitted.

Mirrors `SparkBatchUpdateService` in the C# side. See
`specs/001-spark-ble-fw-stabilize/data-model.md`.
-/

namespace Spec001.BatchComposition

open Spec001.BootStateMachine (BootPhase)

/-- On-device target area. The concrete set (HMI / Motor1 / Motor2 /
    Scrolling) is a domain detail; the Lean model only needs area values
    to be comparable. -/
inductive FirmwareArea where
  | Hmi
  | Motor1
  | Motor2
  | Scrolling
  deriving DecidableEq, Repr

/-- A firmware file staged in a batch. The `nonEmpty` proof witnesses
    FR-010's "Firmware.Length > 0" precondition at the type level — a
    `FirmwareFile` cannot be constructed with an empty byte array. -/
structure FirmwareFile where
  area     : FirmwareArea
  bytes    : ByteArray
  nonEmpty : bytes.size > 0

/-- Batch state.

    - `InProgress completed remaining` — `completed` is the (reverse-order)
      list of areas that have finished successfully; `remaining` is the
      yet-to-run tail.
    - `Succeeded completed` — every file in the input succeeded.
    - `Failed area phase cause` — the file targeting `area` failed in
      boot-machine `phase` with the given cause; all subsequent files
      are skipped. -/
inductive BatchState where
  | InProgress (completed : List FirmwareArea) (remaining : List FirmwareFile)
  | Succeeded  (completed : List FirmwareArea)
  | Failed     (failingArea : FirmwareArea) (phase : BootPhase) (cause : String)

/-- Single-step transition relation. `ExecuteOne`/`FinalOne`/`AbortOne`
    per data-model.md. -/
inductive Step : BatchState → BatchState → Prop where
  -- `InProgress done (f :: rest) → InProgress (f.area :: done) rest` when
  -- the per-area boot machine reaches `Succeeded` and `rest` is non-empty.
  -- Gated by `rest ≠ []` so that `FinalOne` is the only transition from a
  -- 1-element `InProgress`.
  | executeOne (done : List FirmwareArea) (f : FirmwareFile)
               (rest : List FirmwareFile) (_h : rest ≠ []) :
      Step (.InProgress done (f :: rest)) (.InProgress (f.area :: done) rest)
  -- `InProgress done [f] → Succeeded (f.area :: done)` when the last file
  -- succeeds.
  | finalOne (done : List FirmwareArea) (f : FirmwareFile) :
      Step (.InProgress done [f]) (.Succeeded (f.area :: done))
  -- `InProgress done (f :: rest) → Failed f.area phase cause` when the
  -- per-area boot machine reaches `Failed phase cause`.
  | abortOne (done : List FirmwareArea) (f : FirmwareFile)
             (rest : List FirmwareFile) (phase : BootPhase) (cause : String) :
      Step (.InProgress done (f :: rest)) (.Failed f.area phase cause)

/-- Reflexive–transitive closure of `Step`. -/
inductive Reaches : BatchState → BatchState → Prop where
  | refl (s : BatchState) : Reaches s s
  | step {s t u : BatchState} : Reaches s t → Step t u → Reaches s u

/-- A batch state is reachable from a given initial file list. -/
def ReachableFrom (initial : List FirmwareFile) (s : BatchState) : Prop :=
  Reaches (.InProgress [] initial) s

/-! ### T024 — batch composition preservation theorem (US4 / SC-006) -/

/-- Inductive invariant on `BatchState` parameterised by the initial input list.

    For an `InProgress` state, the `completed` and `remaining` slots together
    cover `initial`: there is a *prefix* of `initial` whose areas are exactly
    `completed` (in reverse order, since `completed` is built by cons), and
    whose suffix is `remaining`.

    The two terminal forms record the conclusions we want to publish: at
    `Succeeded` we keep the length equality; at `Failed` we keep the
    failing-area-membership fact. -/
def Invariant (initial : List FirmwareFile) : BatchState → Prop
  | .InProgress completed remaining =>
      ∃ done : List FirmwareFile,
        initial = done ++ remaining ∧
        completed = (done.map (·.area)).reverse
  | .Succeeded completed =>
      completed.length = initial.length
  | .Failed area _ _ =>
      area ∈ initial.map (·.area)

private theorem invariant_initial (initial : List FirmwareFile) :
    Invariant initial (.InProgress [] initial) :=
  ⟨[], by simp, by simp⟩

private theorem invariant_step (initial : List FirmwareFile) {s t : BatchState}
    (h_inv : Invariant initial s) (h_step : Step s t) : Invariant initial t := by
  cases h_step with
  | executeOne done f rest _ =>
      obtain ⟨pre, h_init, h_done⟩ := h_inv
      refine ⟨pre ++ [f], ?_, ?_⟩
      · rw [h_init]; simp
      · simp [h_done]
  | finalOne done f =>
      obtain ⟨pre, h_init, h_done⟩ := h_inv
      show (f.area :: done).length = initial.length
      rw [h_init, h_done]
      simp [Nat.add_comm]
  | abortOne done f rest phase cause =>
      obtain ⟨pre, h_init, _⟩ := h_inv
      show f.area ∈ initial.map (·.area)
      rw [h_init]
      simp

private theorem invariant_reaches (initial : List FirmwareFile) {s t : BatchState}
    (h_reach : Reaches s t) (h_inv : Invariant initial s) : Invariant initial t := by
  induction h_reach with
  | refl => exact h_inv
  | step _ h_step ih => exact invariant_step initial ih h_step

private theorem reachable_invariant (initial : List FirmwareFile) {s : BatchState}
    (h : ReachableFrom initial s) : Invariant initial s :=
  invariant_reaches initial h (invariant_initial initial)

/-- **T024 (composition preservation, US4 / SC-006).**

    For every reachable batch state:
    - if it is `Succeeded done`, then `done.length = initial.length` (every
      submitted file was completed);
    - if it is `Failed area phase cause`, then `area ∈ initial.map (·.area)`
      (the failing area was actually submitted, not invented). -/
theorem batch_composition (initial : List FirmwareFile) (s : BatchState)
    (h : ReachableFrom initial s) :
    (∀ done, s = .Succeeded done → done.length = initial.length) ∧
    (∀ a phase cause, s = .Failed a phase cause → a ∈ initial.map (·.area)) := by
  have h_inv := reachable_invariant initial h
  refine ⟨?_, ?_⟩
  · rintro done rfl; exact h_inv
  · rintro a phase cause rfl; exact h_inv

/-- Convenience corollary: a successful batch reaches a `Succeeded` state
    whose `completed` list has the same length as the input. -/
theorem batch_succeeded_complete (initial : List FirmwareFile)
    (done : List FirmwareArea)
    (h : ReachableFrom initial (.Succeeded done)) :
    done.length = initial.length :=
  (batch_composition initial _ h).1 done rfl

/-- Convenience corollary: a failed batch reports an area that was actually
    in the input. -/
theorem batch_failed_area_in_initial (initial : List FirmwareFile)
    (a : FirmwareArea) (phase : BootPhase) (cause : String)
    (h : ReachableFrom initial (.Failed a phase cause)) :
    a ∈ initial.map (·.area) :=
  (batch_composition initial _ h).2 a phase cause rfl

end Spec001.BatchComposition
