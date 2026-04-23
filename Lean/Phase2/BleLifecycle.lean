import Mathlib

/-!
# BLE session lifecycle (spec-001, Phase 2, T005)

Types and transitions only — the core invariant T5 (protocol-state
biconditional, `activeProtocol.isSome ↔ state = Connected`) is deferred
to T023.

Mirrors `ConnectionManager.ConnectionState` + `ConnectionManager.ActiveProtocol`
in the C# side. See `specs/001-spark-ble-fw-stabilize/data-model.md`.
-/

namespace Phase2.BleLifecycle

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
  -- `Connected → Disconnecting` on user-initiated disconnect.
  | userDisconnect (h : ProtocolHandle) :
      Step (.Connected, some h) (.Disconnecting, some h)
  -- `Disconnecting → Disconnected` on `DeviceDisconnected` + handle drop.
  | deviceDisconnected (h : ProtocolHandle) :
      Step (.Disconnecting, some h) (.Disconnected, none)
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

end Phase2.BleLifecycle
