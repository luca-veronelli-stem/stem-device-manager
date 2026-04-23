namespace Core.Diagnostics;

/// <summary>
/// Minimal cross-layer hook used by the Debug-configuration shutdown audit
/// (spec-001 T004, research.md R8). Services and Infrastructure layers call
/// <see cref="Record"/> inside their Dispose methods; the composition root
/// (GUI.Windows) installs an <see cref="OnDispose"/> callback that attaches a
/// stack-trace and writes a log line.
/// </summary>
/// <remarks>
/// Intentionally a static with a nullable delegate so that Release builds and
/// any environment that does not wire up a sink incur zero overhead and no
/// allocations. No dependency on <c>Microsoft.Extensions.Logging</c> — Core
/// stays dependency-free.
/// </remarks>
public static class ShutdownAuditHook
{
    /// <summary>
    /// Callback invoked for each recorded disposal. Parameters: owner type
    /// name, disposed field description. Null (default) means no-op.
    /// </summary>
    public static Action<string, string>? OnDispose { get; set; }

    /// <summary>Record a disposal event. No-op when <see cref="OnDispose"/> is null.</summary>
    public static void Record(string ownerName, string fieldDescription)
        => OnDispose?.Invoke(ownerName, fieldDescription);
}
