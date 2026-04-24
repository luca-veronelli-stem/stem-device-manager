using FsCheck;
using FsCheck.Fluent;

namespace Tests.Unit.Generators;

/// <summary>
/// Hand-ported C# projection of the Lean <c>Spec001.BootStateMachine</c>
/// (<c>Lean/Spec001/BootStateMachine.lean</c>). Pure data + total transition
/// function used by the <c>BootStateMachinePropertyTests</c>.
///
/// <para>This port is intentionally confined to the Tests project: the Lean
/// model is the source of truth; production <c>BootService</c> uses the
/// coarser <c>Core.Interfaces.BootState</c> enum (Idle/Uploading/Completed/
/// Failed). T014 covers the Lean-level invariants via FsCheck; the mechanical
/// drift-guard between Lean constructor list and this file is a separate
/// ticket (T025).</para>
///
/// <para>Every transition constructor below comments its Lean counterpart
/// by name (<c>Spec001.BootStateMachine.Step.<i>case</i></c>).</para>
/// </summary>
public static class BootTransitionGenerator
{
    /// <summary>
    /// Retry budget — mirrors the Lean <c>abbrev RetryBudget : Nat := 3</c>.
    /// Kept in sync with <c>BootService.DefaultRetryBudget</c>; a drift between
    /// these two constants is caught by T025 (not by this file).
    /// </summary>
    public const int RetryBudget = 3;

    /// <summary>
    /// Upper bound on per-state total firmware length. Keeps the generator
    /// sample space small and deterministic; real firmware sizes are irrelevant
    /// to the invariants under test.
    /// </summary>
    public const int MaxTotalBytes = 4;

    /// <summary>
    /// Chunk size used by <see cref="EventKind.ChunkAcked"/>. Fixed to 1 so the
    /// generator can saturate the <c>off + chunk ≤ tot</c> precondition across
    /// the whole <c>[0, MaxTotalBytes]</c> range without special casing.
    /// </summary>
    public const int ChunkSize = 1;

    /// <summary>
    /// Phase tag mirroring Lean <c>BootPhase</c>. Used on <see cref="State.Failed"/>
    /// to record which Lean constructor produced the failure.
    /// </summary>
    public enum Phase { StartBoot, UploadBlocks, EndBoot, Restart }

    /// <summary>
    /// State tag mirroring Lean <c>BootState</c>. Offset/total/attempt live on
    /// <see cref="State"/> itself; a union-with-payload is overkill for 7 cases.
    /// </summary>
    public enum StateTag
    {
        Idle,
        AwaitingStart,
        Uploading,
        AwaitingEnd,
        Restarting,
        Succeeded,
        Failed,
    }

    /// <summary>
    /// Port of Lean <c>BootState</c>. Offset/Total/Attempt are only meaningful
    /// when <see cref="Tag"/> is <see cref="StateTag.Uploading"/>. FailedPhase
    /// is only meaningful when <see cref="Tag"/> is <see cref="StateTag.Failed"/>.
    /// </summary>
    public readonly record struct State(
        StateTag Tag,
        int Offset,
        int Total,
        int Attempt,
        Phase FailedPhase)
    {
        public static State Idle => new(StateTag.Idle, 0, 0, 0, Phase.StartBoot);

        public static State AwaitingStart => new(
            StateTag.AwaitingStart, 0, 0, 0, Phase.StartBoot);

        public static State Uploading(int offset, int total, int attempt) =>
            new(StateTag.Uploading, offset, total, attempt, Phase.StartBoot);

        public static State AwaitingEnd => new(
            StateTag.AwaitingEnd, 0, 0, 0, Phase.StartBoot);

        public static State Restarting => new(
            StateTag.Restarting, 0, 0, 0, Phase.StartBoot);

        public static State Succeeded => new(
            StateTag.Succeeded, 0, 0, 0, Phase.StartBoot);

        public static State Failed(Phase phase) =>
            new(StateTag.Failed, 0, 0, 0, phase);
    }

    /// <summary>
    /// Discriminator over the 11 Lean transition constructors. Each case maps
    /// 1:1 to a constructor of <c>Spec001.BootStateMachine.Step</c>. The
    /// generator draws from this enum and <see cref="TryApply"/> filters out
    /// events whose preconditions do not hold at the current state — this is
    /// the standard "small-step trace" generation pattern for FsCheck.
    /// </summary>
    public enum EventKind
    {
        // Lean: Step.startBootInvoked
        StartBootInvoked,
        // Lean: Step.startBootAcked
        StartBootAcked,
        // Lean: Step.startBootExhausted
        StartBootExhausted,
        // Lean: Step.chunkAcked
        ChunkAcked,
        // Lean: Step.chunkRetry
        ChunkRetry,
        // Lean: Step.chunkExhausted
        ChunkExhausted,
        // Lean: Step.uploadComplete
        UploadComplete,
        // Lean: Step.endBootAcked
        EndBootAcked,
        // Lean: Step.endBootExhausted
        EndBootExhausted,
        // Lean: Step.restartAcked
        RestartAcked,
        // Lean: Step.restartExhausted
        RestartExhausted,
    }

    /// <summary>
    /// A single Lean transition invocation: the event kind plus its payload.
    /// <see cref="Total"/> is used by <see cref="EventKind.StartBootAcked"/>
    /// to pick the upload size. <see cref="SessionAlive"/> is the I1 flag
    /// (not part of the Lean Step constructors — it rides alongside so that
    /// the I1 property can inject session-lost events).
    /// </summary>
    public readonly record struct Event(EventKind Kind, int Total, bool SessionAlive);

    /// <summary>
    /// Total transition function: returns the successor state if the Lean
    /// precondition holds at <paramref name="s"/> under event <paramref name="e"/>,
    /// otherwise returns <paramref name="s"/> unchanged. Staying put means
    /// the generator picked an illegal event for the current state; property
    /// tests treat that as a no-op, exactly as the Lean inductive relation
    /// would (no matching <c>Step</c> constructor ⇒ no transition).
    ///
    /// <para>The I1 session-lost short-circuit is layered on top: if
    /// <see cref="Event.SessionAlive"/> is <c>false</c> at any non-terminal
    /// state, the transition is overridden to
    /// <c>Failed (currentPhase)</c>. This is the C# analogue of the
    /// <c>BootSessionLostException</c> handling in <c>BootService</c>.</para>
    /// </summary>
    public static State Apply(State s, Event e)
    {
        // I1 override: a dead session fails the current phase immediately.
        // Terminal states (Succeeded, Failed) are unaffected — Q2 keeps them.
        if (!e.SessionAlive && !IsTerminal(s))
            return State.Failed(PhaseOf(s));

        return e.Kind switch
        {
            // Lean: Step.startBootInvoked  —  Idle → AwaitingStart
            EventKind.StartBootInvoked when s.Tag == StateTag.Idle
                => State.AwaitingStart,

            // Lean: Step.startBootAcked  —  AwaitingStart → Uploading 0 tot 1
            EventKind.StartBootAcked when s.Tag == StateTag.AwaitingStart
                => State.Uploading(0, ClampTotal(e.Total), 1),

            // Lean: Step.startBootExhausted  —  AwaitingStart → Failed StartBoot
            EventKind.StartBootExhausted when s.Tag == StateTag.AwaitingStart
                => State.Failed(Phase.StartBoot),

            // Lean: Step.chunkAcked  —  Uploading off tot att
            //                         → Uploading (off+chunk) tot att
            // Precondition: off + chunk ≤ tot (Lean _h).
            EventKind.ChunkAcked when s.Tag == StateTag.Uploading
                                   && s.Offset + ChunkSize <= s.Total
                => State.Uploading(s.Offset + ChunkSize, s.Total, s.Attempt),

            // Lean: Step.chunkRetry  —  Uploading off tot att
            //                        → Uploading off tot (att+1)
            // Precondition: att < RetryBudget (Lean _h).
            EventKind.ChunkRetry when s.Tag == StateTag.Uploading
                                   && s.Attempt < RetryBudget
                => State.Uploading(s.Offset, s.Total, s.Attempt + 1),

            // Lean: Step.chunkExhausted  —  Uploading off tot RetryBudget
            //                            → Failed UploadBlocks
            EventKind.ChunkExhausted when s.Tag == StateTag.Uploading
                                       && s.Attempt == RetryBudget
                => State.Failed(Phase.UploadBlocks),

            // Lean: Step.uploadComplete  —  Uploading tot tot _ → AwaitingEnd
            EventKind.UploadComplete when s.Tag == StateTag.Uploading
                                       && s.Offset == s.Total
                => State.AwaitingEnd,

            // Lean: Step.endBootAcked  —  AwaitingEnd → Restarting
            EventKind.EndBootAcked when s.Tag == StateTag.AwaitingEnd
                => State.Restarting,

            // Lean: Step.endBootExhausted  —  AwaitingEnd → Failed EndBoot
            EventKind.EndBootExhausted when s.Tag == StateTag.AwaitingEnd
                => State.Failed(Phase.EndBoot),

            // Lean: Step.restartAcked  —  Restarting → Succeeded
            EventKind.RestartAcked when s.Tag == StateTag.Restarting
                => State.Succeeded,

            // Lean: Step.restartExhausted  —  Restarting → Failed Restart
            EventKind.RestartExhausted when s.Tag == StateTag.Restarting
                => State.Failed(Phase.Restart),

            // No matching Lean constructor ⇒ no-op (preserves invariants).
            _ => s,
        };
    }

    /// <summary>
    /// <c>true</c> for <see cref="StateTag.Succeeded"/> and <see cref="StateTag.Failed"/>
    /// — Lean preservation theorem T3 (no outgoing transitions).
    /// </summary>
    public static bool IsTerminal(State s) =>
        s.Tag is StateTag.Succeeded or StateTag.Failed;

    /// <summary>
    /// Maps a non-terminal state to the <see cref="Phase"/> tag it would carry
    /// if it failed right now. Used by the I1 session-lost override.
    /// </summary>
    public static Phase PhaseOf(State s) => s.Tag switch
    {
        StateTag.Idle or StateTag.AwaitingStart => Phase.StartBoot,
        StateTag.Uploading => Phase.UploadBlocks,
        StateTag.AwaitingEnd => Phase.EndBoot,
        StateTag.Restarting => Phase.Restart,
        _ => Phase.StartBoot, // unreachable when IsTerminal is already false
    };

    private static int ClampTotal(int total) =>
        Math.Clamp(total, 0, MaxTotalBytes);

    // --- FsCheck generators -------------------------------------------------

    /// <summary>
    /// Draws a single <see cref="EventKind"/>. All 11 constructors are
    /// equally likely; <see cref="Apply"/> filters out illegal picks — the
    /// alternative (state-aware weighting) would bias away from the retry /
    /// exhaustion transitions that Q1 and Q2 are designed to hit.
    /// </summary>
    public static Gen<EventKind> GenEventKind() =>
        Gen.Elements(Enum.GetValues<EventKind>());

    /// <summary>
    /// Draws a <see cref="Event"/> with <see cref="Event.SessionAlive"/> fixed
    /// to <c>true</c>. Used by Q1/Q2/Q3 where I1 is out of scope.
    /// </summary>
    public static Gen<Event> GenAliveEvent() =>
        from k in GenEventKind()
        from t in Gen.Choose(0, MaxTotalBytes)
        select new Event(k, t, SessionAlive: true);

    /// <summary>
    /// Draws a <see cref="Event"/> with <see cref="Event.SessionAlive"/> drawn
    /// independently (biased ~10 % dead). Used by I1.
    /// </summary>
    public static Gen<Event> GenI1Event() =>
        from k in GenEventKind()
        from t in Gen.Choose(0, MaxTotalBytes)
        from alive in Gen.Frequency<bool>(
            (9, Gen.Constant(true)),
            (1, Gen.Constant(false)))
        select new Event(k, t, alive);

    /// <summary>
    /// Draws a finite trace (length capped at <paramref name="maxLength"/>,
    /// default 24). Caller-visible wrapper over
    /// <see cref="GenAliveEvent"/>.
    /// </summary>
    public static Gen<Event[]> GenAliveTrace(int maxLength = 24) =>
        from n in Gen.Choose(0, maxLength)
        from events in Gen.ArrayOf<Event>(GenAliveEvent(), n)
        select events;

    /// <summary>
    /// Draws a finite trace with per-event session-alive flags. Used by I1.
    /// </summary>
    public static Gen<Event[]> GenI1Trace(int maxLength = 24) =>
        from n in Gen.Choose(0, maxLength)
        from events in Gen.ArrayOf<Event>(GenI1Event(), n)
        select events;
}

/// <summary>
/// FsCheck <see cref="Arbitrary{T}"/> registrations for
/// <see cref="BootTransitionGenerator.Event"/> traces. Registered per-test
/// via <c>[Property(Arbitrary = new[] { typeof(BootTraceArbitrary) })]</c>.
/// </summary>
public static class BootTraceArbitrary
{
    public static Arbitrary<BootTransitionGenerator.Event[]> AliveTrace() =>
        Arb.From(BootTransitionGenerator.GenAliveTrace());
}

/// <summary>
/// FsCheck <see cref="Arbitrary{T}"/> registration for I1 traces (with
/// session-alive flags drawn per event).
/// </summary>
public static class BootI1TraceArbitrary
{
    public static Arbitrary<BootTransitionGenerator.Event[]> I1Trace() =>
        Arb.From(BootTransitionGenerator.GenI1Trace());
}
