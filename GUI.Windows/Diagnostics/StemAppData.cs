namespace GUI.Windows.Diagnostics;

/// <summary>
/// Per-user runtime data root for Stem Device Manager, per the STEM
/// <c>APP_DATA</c> standard (v1.9.0). All file output (logs, future caches,
/// future credentials) lives under <c>%LocalAppData%\Stem\DeviceManager\</c>
/// so the shipped single-file <c>.exe</c> doesn't litter its own directory
/// and the path stays writable regardless of where the technician dropped
/// the executable.
///
/// See <c>shared/standards/APP_DATA.md</c> in the
/// <see href="https://github.com/luca-veronelli-stem/standards">standards</see>
/// repo for the convention. v0.4.3 is the first reference adopter; no
/// migration helper is needed because the previous
/// <c>AppContext.BaseDirectory/logs/</c> data was per-process session logs
/// with no longterm value.
/// </summary>
internal static class StemAppData
{
    private const string CompanySegment = "Stem";
    private const string AppSegment = "DeviceManager";

    public static string GetAppRoot() =>
        EnsureDir(Path.Combine(LocalRoot, CompanySegment, AppSegment));

    public static string GetLogsDir() =>
        EnsureDir(Path.Combine(GetAppRoot(), "logs"));

    public static string GetCacheDir() =>
        EnsureDir(Path.Combine(GetAppRoot(), "cache"));

    public static string GetCredentialsDir() =>
        EnsureDir(Path.Combine(GetAppRoot(), "credentials"));

    public static string GetDbDir() =>
        EnsureDir(Path.Combine(GetAppRoot(), "db"));

    private static string LocalRoot =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string EnsureDir(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
