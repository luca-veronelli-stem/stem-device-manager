using GUI.Windows.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace Tests.Unit.Diagnostics;

/// <summary>
/// Cover the four supported resolution cases plus the precedence ordering
/// between them. Tests touch real env vars + real temp JSON files to
/// exercise the same provider types the runtime composition root uses.
/// </summary>
public class ApiKeySourceDetectorTests
{
    private const string EnvVarName = "DictionaryApi__ApiKey";

    [Fact]
    public void Detect_NoSource_ReturnsEmpty()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.json"), """{ "DictionaryApi": { "ApiKey": "" } }""");

        using (ClearEnvVar())
        {
            var config = BuildConfig(temp.Path);
            Assert.Equal(ApiKeySource.Empty, ApiKeySourceDetector.Detect(config));
        }
    }

    [Fact]
    public void Detect_AppSettingsOnly_ReturnsAppSettings()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.json"), """{ "DictionaryApi": { "ApiKey": "from-appsettings" } }""");

        using (ClearEnvVar())
        {
            var config = BuildConfig(temp.Path);
            Assert.Equal(ApiKeySource.AppSettings, ApiKeySourceDetector.Detect(config));
        }
    }

    [Fact]
    public void Detect_ProductionOverlayOnly_ReturnsProductionFile()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.json"), """{ "DictionaryApi": { "ApiKey": "" } }""");
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.Production.json"), """{ "DictionaryApi": { "ApiKey": "from-production" } }""");

        using (ClearEnvVar())
        {
            var config = BuildConfig(temp.Path);
            Assert.Equal(ApiKeySource.ProductionFile, ApiKeySourceDetector.Detect(config));
        }
    }

    [Fact]
    public void Detect_EnvVarOnly_ReturnsEnv()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.json"), """{ "DictionaryApi": { "ApiKey": "" } }""");

        using (SetEnvVar("from-env"))
        {
            var config = BuildConfig(temp.Path);
            Assert.Equal(ApiKeySource.Env, ApiKeySourceDetector.Detect(config));
        }
    }

    [Fact]
    public void Detect_EnvVarOverridesProductionFile_ReturnsEnv()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.json"), """{ "DictionaryApi": { "ApiKey": "" } }""");
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.Production.json"), """{ "DictionaryApi": { "ApiKey": "from-production" } }""");

        using (SetEnvVar("from-env"))
        {
            var config = BuildConfig(temp.Path);
            Assert.Equal(ApiKeySource.Env, ApiKeySourceDetector.Detect(config));
        }
    }

    [Fact]
    public void Detect_ProductionFileOverridesAppSettings_ReturnsProductionFile()
    {
        using var temp = new TempDir();
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.json"), """{ "DictionaryApi": { "ApiKey": "from-appsettings" } }""");
        File.WriteAllText(Path.Combine(temp.Path, "appsettings.Production.json"), """{ "DictionaryApi": { "ApiKey": "from-production" } }""");

        using (ClearEnvVar())
        {
            var config = BuildConfig(temp.Path);
            Assert.Equal(ApiKeySource.ProductionFile, ApiKeySourceDetector.Detect(config));
        }
    }

    [Fact]
    public void Detect_NonConfigurationRoot_ReturnsUnknown()
    {
        // IConfiguration that isn't an IConfigurationRoot can't expose providers
        // -- guard against the upstream contract changing.
        var section = new ConfigurationBuilder().Build().GetSection("DictionaryApi");
        Assert.Equal(ApiKeySource.Unknown, ApiKeySourceDetector.Detect(section));
    }

    private static IConfiguration BuildConfig(string basePath) =>
        new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Production.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

    private static EnvVarScope SetEnvVar(string value)
    {
        Environment.SetEnvironmentVariable(EnvVarName, value);
        return new EnvVarScope();
    }

    private static EnvVarScope ClearEnvVar()
    {
        Environment.SetEnvironmentVariable(EnvVarName, null);
        return new EnvVarScope();
    }

    private sealed class EnvVarScope : IDisposable
    {
        public void Dispose() => Environment.SetEnvironmentVariable(EnvVarName, null);
    }

    private sealed class TempDir : IDisposable
    {
        public TempDir() => Path = Directory.CreateTempSubdirectory("apidetector-test-").FullName;

        public string Path { get; }

        public void Dispose()
        {
            try { Directory.Delete(Path, recursive: true); } catch { /* best-effort */ }
        }
    }
}
