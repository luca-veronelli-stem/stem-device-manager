using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;

namespace GUI.Windows.Diagnostics;

/// <summary>
/// Identifies which configuration provider supplied the live value of
/// <c>DictionaryApi:ApiKey</c> — used at startup to log a diagnostic line
/// so technicians can see <i>why</i> the app picked one auth route over
/// another. The key value itself is never returned, never logged.
///
/// Inspects <see cref="IConfigurationRoot.Providers"/> in reverse order
/// (highest precedence first, matching the runtime lookup order) and
/// classifies the first provider that yields a non-empty value.
/// </summary>
public static class ApiKeySourceDetector
{
    private const string Key = "DictionaryApi:ApiKey";

    public static ApiKeySource Detect(IConfiguration configuration)
    {
        if (configuration is not IConfigurationRoot root)
            return ApiKeySource.Unknown;

        foreach (var provider in root.Providers.Reverse())
        {
            if (provider.TryGet(Key, out var value) && !string.IsNullOrWhiteSpace(value))
                return Classify(provider);
        }
        return ApiKeySource.Empty;
    }

    private static ApiKeySource Classify(IConfigurationProvider provider) => provider switch
    {
        EnvironmentVariablesConfigurationProvider => ApiKeySource.Env,
        JsonConfigurationProvider json => IsProductionOverlay(json)
            ? ApiKeySource.ProductionFile
            : ApiKeySource.AppSettings,
        _ => ApiKeySource.Unknown,
    };

    private static bool IsProductionOverlay(JsonConfigurationProvider json) =>
        Path.GetFileName(json.Source.Path ?? string.Empty)
            .Equals("appsettings.Production.json", StringComparison.OrdinalIgnoreCase);
}

public enum ApiKeySource
{
    /// <summary>No provider supplied a non-empty value — the app will use the Excel-only DI path.</summary>
    Empty,

    /// <summary>The committed <c>appsettings.json</c> next to the exe. Should not happen in v0.4.x+ (key is rotated out of the committed file).</summary>
    AppSettings,

    /// <summary>The gitignored <c>appsettings.Production.json</c> overlay sitting next to the exe.</summary>
    ProductionFile,

    /// <summary>The <c>DictionaryApi__ApiKey</c> environment variable.</summary>
    Env,

    /// <summary>A provider type the detector does not recognise. Configuration was supplied; manual inspection needed.</summary>
    Unknown,
}
