using Core.Diagnostics;
using Microsoft.Extensions.Logging;

namespace GUI.Windows.Diagnostics;

/// <summary>
/// Debug-only shutdown audit sink (spec-001 T004, research.md R8). When
/// enabled, every disposal recorded through <see cref="ShutdownAuditHook"/>
/// produces one <see cref="LogLevel.Debug"/> line with the call-site stack
/// trace, so that the US1 close/relaunch investigation (T006) can correlate
/// "disposed at X" with "reused at Y".
/// </summary>
public static class ShutdownAudit
{
    /// <summary>
    /// Install the audit sink. Call from <c>Program.Main</c> guarded by
    /// <c>#if DEBUG</c>. Idempotent: a second call replaces the previous sink.
    /// </summary>
    public static void Enable(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ShutdownAuditHook.OnDispose = (owner, field) =>
        {
            logger.LogDebug(
                "[ShutdownAudit] {Owner} disposing {Field}\nStack:\n{Stack}",
                owner, field, Environment.StackTrace);
        };
    }

    /// <summary>Remove the audit sink (hook becomes a no-op again).</summary>
    public static void Disable() => ShutdownAuditHook.OnDispose = null;
}
