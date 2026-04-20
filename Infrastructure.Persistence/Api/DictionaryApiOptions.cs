namespace Infrastructure.Api;

/// <summary>
/// Opzioni di configurazione per DictionaryApiProvider.
/// Popolate da appsettings.json sezione "DictionaryApi".
/// </summary>
public class DictionaryApiOptions
{
    /// <summary>URL base dell'API (es. "https://stem-dictionaries.azurewebsites.net").</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API key per autenticazione (header X-Api-Key).</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Timeout per le chiamate HTTP in secondi. Default: 30.</summary>
    public int TimeoutSeconds { get; set; } = 30;
}
