import Mathlib
import Spec001.BootStateMachine

/-!
# Batch firmware-update composition (spec-001, T005)

Types and transitions only — the composition preservation theorem
(batch-succeeded ⇒ every area completed; batch-failed ⇒ failing area
is one of the submitted areas) is deferred to T024.

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

end Spec001.BatchComposition
