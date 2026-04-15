using System.Reflection;
using Core.Interfaces;
using Infrastructure.Api;
using Infrastructure.Excel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

/// <summary>
/// Extension methods per registrare IDictionaryProvider nel container DI.
/// Se DictionaryApi è configurato → API con fallback Excel.
/// Altrimenti → solo Excel.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra IDictionaryProvider in base alla configurazione.
    /// </summary>
    public static IServiceCollection AddDictionaryProvider(
        this IServiceCollection services, IConfiguration configuration)
    {
        // Registra sempre ExcelDictionaryProvider (usato come fallback o standalone)
        services.AddSingleton(sp =>
        {
            var asm = Assembly.GetAssembly(typeof(ExcelDictionaryProvider))!;
            var stream = asm.GetManifestResourceStream(
                "Infrastructure.Excel.Dizionari STEM.xlsx")
                ?? throw new FileNotFoundException(
                    "Embedded resource 'Dizionari STEM.xlsx' not found in Infrastructure");
            return new ExcelDictionaryProvider(stream);
        });

        var section = configuration.GetSection("DictionaryApi");
        var baseUrl = section["BaseUrl"];
        var apiKey = section["ApiKey"];

        if (!string.IsNullOrWhiteSpace(baseUrl) && !string.IsNullOrWhiteSpace(apiKey))
        {
            // API configurata → registra HttpClient + DictionaryApiProvider + Fallback
            var options = new DictionaryApiOptions();
            section.Bind(options);

            services.AddHttpClient<DictionaryApiProvider>(client =>
            {
                client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
            });

            services.AddSingleton<IDictionaryProvider>(sp =>
            {
                var api = sp.GetRequiredService<DictionaryApiProvider>();
                var excel = sp.GetRequiredService<ExcelDictionaryProvider>();
                return new FallbackDictionaryProvider(api, excel);
            });
        }
        else
        {
            // API non configurata → solo Excel
            services.AddSingleton<IDictionaryProvider>(sp =>
                sp.GetRequiredService<ExcelDictionaryProvider>());
        }

        return services;
    }
}
