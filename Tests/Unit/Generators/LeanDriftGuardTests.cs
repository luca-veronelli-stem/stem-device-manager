using System.Text.RegularExpressions;

namespace Tests.Unit.Generators;

/// <summary>
/// Spec-001 T025 — Lean → C# drift guard. Parses the
/// <c>inductive Step</c> declaration in each formalized state machine and
/// asserts the constructor list matches the hand-ported C# array. If a Lean
/// constructor is added, removed, or renamed without an equivalent C#
/// update, this test fails — closing the loop on Constitution principle IV
/// (Lean spec gates the C# tests).
///
/// <para>Runs on <c>net10.0</c> only (no Windows-specific dependency). Pure
/// file parsing, no <c>lake</c> / Lean toolchain required at test time —
/// the assumption is that the repository's Lean source compiles cleanly
/// (verified locally during development; CI doesn't yet build Lean).</para>
/// </summary>
public class LeanDriftGuardTests
{
    [Fact]
    public void BootStateMachine_LeanInductiveStep_MatchesCSharpHandPort()
    {
        var ctors = ParseInductiveStepConstructors("Spec001/BootStateMachine.lean");
        Assert.Equal(
            BootTransitionGenerator.LeanStepConstructorNames,
            ctors);
    }

    [Fact]
    public void BleLifecycle_LeanInductiveStep_MatchesCSharpHandPort()
    {
        var ctors = ParseInductiveStepConstructors("Spec001/BleLifecycle.lean");
        Assert.Equal(
            BleLifecycleTransitionGenerator.LeanStepConstructorNames,
            ctors);
    }

    /// <summary>
    /// Returns the constructor names of the first <c>inductive Step ...</c>
    /// declaration in the given Lean file, in declaration order.
    /// </summary>
    private static IReadOnlyList<string> ParseInductiveStepConstructors(
        string relativeFromLeanRoot)
    {
        var leanFilePath = Path.Combine(LocateLeanRoot(), relativeFromLeanRoot);
        var lines = File.ReadAllLines(leanFilePath);

        var startIdx = FindLineIndex(lines, l => Regex.IsMatch(l, @"^\s*inductive\s+Step\b"));
        if (startIdx < 0)
            throw new InvalidOperationException(
                $"Could not find `inductive Step` declaration in {leanFilePath}");

        var ctorRegex = new Regex(@"^\s+\|\s+(\w+)");
        var ctors = new List<string>();
        for (var i = startIdx + 1; i < lines.Length; i++)
        {
            var line = lines[i];

            // End of inductive block: a non-whitespace, non-doc-comment line
            // starting at column 0 (e.g. the next `inductive`, `def`, `end`).
            if (line.Length > 0
                && !char.IsWhiteSpace(line[0])
                && !line.StartsWith("/-", StringComparison.Ordinal))
                break;

            var m = ctorRegex.Match(line);
            if (m.Success)
                ctors.Add(m.Groups[1].Value);
        }

        if (ctors.Count == 0)
            throw new InvalidOperationException(
                $"Found `inductive Step` in {leanFilePath} but parsed zero constructors");

        return ctors;
    }

    private static int FindLineIndex(string[] lines, Func<string, bool> predicate)
    {
        for (var i = 0; i < lines.Length; i++)
            if (predicate(lines[i]))
                return i;
        return -1;
    }

    /// <summary>
    /// Walks up from the test's <see cref="AppContext.BaseDirectory"/>
    /// looking for a directory that contains <c>Lean/lakefile.lean</c>. That
    /// directory is the repo root; the Lean root is its <c>Lean/</c>
    /// subdirectory.
    /// </summary>
    private static string LocateLeanRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
            && !File.Exists(Path.Combine(dir.FullName, "Lean", "lakefile.lean")))
        {
            dir = dir.Parent;
        }
        if (dir is null)
            throw new DirectoryNotFoundException(
                "Could not locate Lean/ root: walked from "
                + AppContext.BaseDirectory + " up to filesystem root, no "
                + "directory with Lean/lakefile.lean found.");
        return Path.Combine(dir.FullName, "Lean");
    }
}
