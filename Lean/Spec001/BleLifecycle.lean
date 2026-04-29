import Mathlib

/-!
# BLE session lifecycle (spec-001, T005 + T023)

State, transitions, and the core invariant T5 (US2):

> `∀ s, Reachable s → s.2.isSome ↔ s.1 = Connected`

Mirrors `ConnectionManager.ConnectionState` + `ConnectionManager.ActiveProtocol`
in the C# side, where the C# invariant is `ActiveProtocol = null iff State ≠
Connected`. See `specs/001-spark-ble-fw-stabilize/data-model.md`.

The transitions that leave `Connected` (user-initiated disconnect, unsolicited
drop) drop the handle atomically — that atomicity is what makes the
biconditional hold by construction, and is the structural fix US2 wires up
on the C# side via `ConnectionManager.TransitionTo`.
-/

namespace Spec001.BleLifecycle

/-- Opaque application-level protocol handle created on `Connect` and
    dropped on `Disconnect`. The Lean model only cares about its *presence*;
    the concrete contents (framing, CRC, decoder) are domain detail that
    lives in the C# `ProtocolService`. -/
structure ProtocolHandle where
  /-- Phantom field to keep `ProtocolHandle` inhabitable in proofs without
      forcing every generator to synthesize a meaningful value. -/
  tag : Unit := ()

inductive BleLifecycleState where
  | Disconnected
  | Connecting
  | Connected
  | Disconnecting
  deriving DecidableEq, Repr

/-- Application-visible state: lifecycle stage + protocol handle slot.

    The core invariant (T5, proved in T023) is the biconditional
    `s.2.isSome ↔ s.1 = Connected`. -/
abbrev AppState : Type := BleLifecycleState × Option ProtocolHandle

/-- Single-step transition relation. Every transition that enters or
    leaves `Connected` also sets the handle slot atomically, preventing
    the "phantom connected" state that US2 exists to eliminate. -/
inductive Step : AppState → AppState → Prop where
  -- `Disconnected → Connecting` on user-initiated connect.
  | userConnect :
      Step (.Disconnected, none) (.Connecting, none)
  -- `Connecting → Connected` on BLE stack `DeviceConnected` + handle creation.
  | deviceConnected (h : ProtocolHandle) :
      Step (.Connecting, none) (.Connected, some h)
  -- `Connecting → Disconnected` on connect failure.
  | connectFailed :
      Step (.Connecting, none) (.Disconnected, none)
  -- `Connected → Disconnecting` on user-initiated disconnect. The handle is
  -- dropped atomically on leaving `Connected` so the T5 biconditional is
  -- preserved at every step (the C# `ConnectionManager.TransitionTo` does
  -- the same — `ActiveProtocol = null iff State ≠ Connected`).
  | userDisconnect (h : ProtocolHandle) :
      Step (.Connected, some h) (.Disconnecting, none)
  -- `Disconnecting → Disconnected` on `DeviceDisconnected`. The handle is
  -- already `none` from `userDisconnect`.
  | deviceDisconnected :
      Step (.Disconnecting, none) (.Disconnected, none)
  -- `Connected → Disconnected` on unsolicited drop (the US2/US3 failure
  -- mode). Handle MUST be dropped atomically — this is what T5 enforces.
  | unsolicitedDrop (h : ProtocolHandle) :
      Step (.Connected, some h) (.Disconnected, none)

/-- Reflexive–transitive closure of `Step`. -/
inductive Reaches : AppState → AppState → Prop where
  | refl (s : AppState) : Reaches s s
  | step {s t u : AppState} : Reaches s t → Step t u → Reaches s u

/-- A state is reachable from the initial `(Disconnected, none)`. -/
def Reachable (s : AppState) : Prop := Reaches (.Disconnected, none) s

/-! ### T023 — protocol-state biconditional (US2 / SC-002) -/

/-- The biconditional itself, expressed as a predicate on `AppState` so it
    can be carried by the induction over `Reaches`. -/
def StateProtocolAgreement (s : AppState) : Prop :=
  s.2.isSome ↔ s.1 = .Connected

private theorem agreement_initial : StateProtocolAgreement (.Disconnected, none) := by
  simp [StateProtocolAgreement]

private theorem agreement_step {s t : AppState}
    (h_inv : StateProtocolAgreement s) (h_step : Step s t) :
    StateProtocolAgreement t := by
  cases h_step <;> simp [StateProtocolAgreement]

private theorem agreement_reaches {s t : AppState}
    (h_reach : Reaches s t) (h_inv : StateProtocolAgreement s) :
    StateProtocolAgreement t := by
  induction h_reach with
  | refl => exact h_inv
  | step _ h_step ih => exact agreement_step ih h_step

/-- **T5 (protocol-state agreement, US2 / SC-002).** For every reachable
    `(state, handle)` pair, the handle is present iff the lifecycle stage
    is `Connected`. This is the structural fact the C# fix for US2 enforces
    via a single mutator `ConnectionManager.TransitionTo`. -/
theorem reachable_state_protocol_agreement {s : AppState}
    (h : Reachable s) : s.2.isSome ↔ s.1 = .Connected :=
  agreement_reaches h agreement_initial

end Spec001.BleLifecycle
