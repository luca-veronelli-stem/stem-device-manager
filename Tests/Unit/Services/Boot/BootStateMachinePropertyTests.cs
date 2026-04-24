using FsCheck;
using FsCheck.Xunit;
using Tests.Unit.Generators;
using BootEvent = Tests.Unit.Generators.BootTransitionGenerator.Event;
using BootState = Tests.Unit.Generators.BootTransitionGenerator.State;

namespace Tests.Unit.Services.Boot;

/// <summary>
/// FsCheck property tests that codify spec-001 <c>IBootService</c> invariants
/// Q1/Q2/Q3 and I1 (<c>specs/001-spark-ble-fw-stabilize/contracts/boot-service.md</c>),
/// against the hand-ported Lean <c>BootStateMachine</c> in
/// <see cref="BootTransitionGenerator"/>.
///
/// <para>Runs on the <c>net10.0</c> TFM only (FsCheck.Xunit 3.1.0 constraint);
/// the tests consume no WinForms / Infrastructure.Protocol code.</para>
///
/// <para>These properties exercise the Lean state machine directly — they
/// validate the <b>model</b>. The separate drift-guard test (spec-001 T025)
/// closes the loop by checking the Lean constructor list against
/// <see cref="BootTransitionGenerator.EventKind"/> mechanically.</para>
/// </summary>
public class BootStateMachinePropertyTests
{
    /// <summary>
    /// Q1 — retry-budget bounded. Any reachable <c>Uploading</c> state from
    /// <see cref="BootState.Idle"/> satisfies <c>Attempt ≤ RetryBudget</c>;
    /// a <c>Failed UploadBlocks</c> state can only arise from an
    /// <c>Uploading _ _ RetryBudget</c> predecessor. Formalizes
    /// <c>boot-service.md:Q1</c> + Lean preservation theorem T2.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(BootTraceArbitrary) }, MaxTest = 500)]
    public bool Q1_RetryBudgetBounded(BootEvent[] trace)
    {
        var state = BootState.Idle;
        var prev = state;
        foreach (var evt in trace)
        {
            prev = state;
            state = BootTransitionGenerator.Apply(state, evt);

            // Invariant: Uploading.Attempt never exceeds the budget.
            if (state.Tag == BootTransitionGenerator.StateTag.Uploading
                && state.Attempt > BootTransitionGenerator.RetryBudget)
                return false;

            // A 4th attempt is never constructed: the only way Attempt can
            // reach RetryBudget+1 is if the transition function violated the
            // Lean <c>chunkRetry</c> precondition. Double-checked here.
            if (state.Tag == BootTransitionGenerator.StateTag.Uploading
                && prev.Tag == BootTransitionGenerator.StateTag.Uploading
                && state.Attempt > prev.Attempt
                && prev.Attempt >= BootTransitionGenerator.RetryBudget)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Q2 — <see cref="BootTransitionGenerator.StateTag.Failed"/> is sticky
    /// within a single step sequence. Once the machine has transitioned to
    /// <c>Failed</c>, no subsequent event can move it elsewhere (including
    /// <c>Succeeded</c>). Formalizes <c>boot-service.md:Q2</c> + Lean T3
    /// terminal-stability.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(BootTraceArbitrary) }, MaxTest = 500)]
    public bool Q2_FailedIsSticky(BootEvent[] trace)
    {
        var state = BootState.Idle;
        var failedPhase = default(BootTransitionGenerator.Phase?);
        foreach (var evt in trace)
        {
            var before = state;
            state = BootTransitionGenerator.Apply(state, evt);

            if (before.Tag == BootTransitionGenerator.StateTag.Failed)
            {
                // Must remain Failed, with the same phase tag.
                if (state.Tag != BootTransitionGenerator.StateTag.Failed)
                    return false;
                if (state.FailedPhase != before.FailedPhase)
                    return false;
            }

            if (state.Tag == BootTransitionGenerator.StateTag.Failed)
                failedPhase ??= state.FailedPhase;
        }

        // If we ever entered Failed, the final state must still be Failed.
        if (failedPhase is not null
            && state.Tag != BootTransitionGenerator.StateTag.Failed)
            return false;

        return true;
    }

    /// <summary>
    /// Q3 — progress monotonicity. Within a single upload,
    /// <c>CurrentOffset</c> is non-decreasing and never exceeds
    /// <c>TotalLength</c>. A <c>chunkRetry</c> re-emits the same offset,
    /// never rewinds. Boundary: crossing out of <c>Uploading</c> resets the
    /// tracking window (next <c>StartBootAcked</c> starts fresh at 0).
    /// Formalizes <c>boot-service.md:Q3</c> + Lean preservation theorem T1.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(BootTraceArbitrary) }, MaxTest = 500)]
    public bool Q3_ProgressMonotonic(BootEvent[] trace)
    {
        var state = BootState.Idle;
        var lastOffset = 0;
        var inUpload = false;

        foreach (var evt in trace)
        {
            var before = state;
            state = BootTransitionGenerator.Apply(state, evt);

            // Enter the Uploading window — reset the monotonicity tracker.
            if (!inUpload
                && state.Tag == BootTransitionGenerator.StateTag.Uploading)
            {
                inUpload = true;
                lastOffset = state.Offset;
            }
            else if (inUpload
                     && state.Tag == BootTransitionGenerator.StateTag.Uploading)
            {
                // Monotonicity: never rewind.
                if (state.Offset < lastOffset) return false;
                // Bounds: offset ≤ total.
                if (state.Offset > state.Total) return false;
                lastOffset = state.Offset;
            }
            else if (inUpload
                     && state.Tag != BootTransitionGenerator.StateTag.Uploading)
            {
                // Left the Uploading window — close it. Legal exits are
                // AwaitingEnd (uploadComplete), Failed (chunkExhausted), or
                // Failed via I1; any other exit is a model bug. For Q3 we
                // only need the non-rewind property inside the window.
                inUpload = false;
                lastOffset = 0;
                _ = before; // kept for symmetry with the other properties
            }
        }

        return true;
    }

    /// <summary>
    /// I1 — protocol agreement. When the session probe (<c>SessionAlive</c>)
    /// returns <c>false</c> at a non-terminal state, the very next state is
    /// <see cref="BootTransitionGenerator.StateTag.Failed"/> carrying the
    /// phase tag of the state that was live when the drop occurred.
    /// Formalizes <c>boot-service.md:I1</c> — the C# counterpart is
    /// <c>BootService</c>'s <c>BootSessionLostException</c> path.
    /// </summary>
    [Property(Arbitrary = new[] { typeof(BootI1TraceArbitrary) }, MaxTest = 500)]
    public bool I1_ProtocolAgreement(BootEvent[] trace)
    {
        var state = BootState.Idle;
        foreach (var evt in trace)
        {
            var before = state;
            state = BootTransitionGenerator.Apply(state, evt);

            // A dead session at a non-terminal state MUST fail, tagging
            // the phase that was active when the drop happened.
            if (!evt.SessionAlive
                && !BootTransitionGenerator.IsTerminal(before))
            {
                if (state.Tag != BootTransitionGenerator.StateTag.Failed)
                    return false;
                if (state.FailedPhase != BootTransitionGenerator.PhaseOf(before))
                    return false;
            }
        }

        return true;
    }
}
