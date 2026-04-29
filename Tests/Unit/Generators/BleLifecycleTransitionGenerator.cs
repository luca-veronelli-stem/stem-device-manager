namespace Tests.Unit.Generators;

/// <summary>
/// Hand-ported view of the Lean <c>Spec001.BleLifecycle.Step</c>
/// constructor list (<c>Lean/Spec001/BleLifecycle.lean</c>). Unlike
/// <see cref="BootTransitionGenerator"/>, the BLE lifecycle does not have
/// a C# trace-generator on top — <c>ConnectionManagerPropertyTests</c>
/// exercises the real <c>ConnectionManager</c> directly through fake
/// <c>ICommunicationPort</c>. This file's only role is to provide the
/// canonical constructor list that spec-001 T025
/// (<see cref="LeanDriftGuardTests"/>) compares against the Lean source.
/// </summary>
public static class BleLifecycleTransitionGenerator
{
    /// <summary>
    /// Lean-side constructor names of <c>Spec001.BleLifecycle.Step</c>, in
    /// declaration order. Drift guard against
    /// <c>Lean/Spec001/BleLifecycle.lean</c>.
    /// </summary>
    public static readonly IReadOnlyList<string> LeanStepConstructorNames =
    [
        "userConnect",
        "deviceConnected",
        "connectFailed",
        "userDisconnect",
        "deviceDisconnected",
        "unsolicitedDrop",
    ];
}
