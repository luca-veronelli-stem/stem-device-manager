using Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Integration.DependencyInjection;

/// <summary>
/// Test di integrazione per il container DI.
/// Verifica che il wiring dei servizi funzioni come in Program.cs.
/// </summary>
public class ServiceRegistrationTests
{
    private static ServiceProvider BuildServiceProvider(
        Dictionary<string, string?>? configOverrides = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(configOverrides ?? [])
            .Build();

        var services = new ServiceCollection();
        global::Infrastructure.DependencyInjection.AddDictionaryProvider(services, config);
        return services.BuildServiceProvider();
    }

    // --- IDictionaryProvider: no API config → Excel ---

    [Fact]
    public void Resolve_DictionaryProvider_NoApiConfig_ReturnsExcelBacked()
    {
        using var sp = BuildServiceProvider();

        var provider = sp.GetRequiredService<IDictionaryProvider>();

        Assert.NotNull(provider);
        Assert.IsType<global::Infrastructure.Excel.ExcelDictionaryProvider>(provider);
    }

    [Fact]
    public void Resolve_DictionaryProvider_IsSingleton()
    {
        using var sp = BuildServiceProvider();

        var first = sp.GetRequiredService<IDictionaryProvider>();
        var second = sp.GetRequiredService<IDictionaryProvider>();

        Assert.Same(first, second);
    }

    // --- IDictionaryProvider: API config → Fallback(API, Excel) ---

    [Fact]
    public void Resolve_DictionaryProvider_WithApiConfig_ReturnsFallback()
    {
        var config = new Dictionary<string, string?>
        {
            ["DictionaryApi:BaseUrl"] = "https://test.example.com",
            ["DictionaryApi:ApiKey"] = "test-key"
        };
        using var sp = BuildServiceProvider(config);

        var provider = sp.GetRequiredService<IDictionaryProvider>();

        Assert.IsType<global::Infrastructure.FallbackDictionaryProvider>(provider);
    }

    [Fact]
    public void Resolve_DictionaryProvider_EmptyApiKey_ReturnsExcel()
    {
        var config = new Dictionary<string, string?>
        {
            ["DictionaryApi:BaseUrl"] = "https://test.example.com",
            ["DictionaryApi:ApiKey"] = ""
        };
        using var sp = BuildServiceProvider(config);

        var provider = sp.GetRequiredService<IDictionaryProvider>();

        Assert.IsType<global::Infrastructure.Excel.ExcelDictionaryProvider>(provider);
    }

    [Fact]
    public void Resolve_DictionaryProvider_EmptyBaseUrl_ReturnsExcel()
    {
        var config = new Dictionary<string, string?>
        {
            ["DictionaryApi:BaseUrl"] = "",
            ["DictionaryApi:ApiKey"] = "some-key"
        };
        using var sp = BuildServiceProvider(config);

        var provider = sp.GetRequiredService<IDictionaryProvider>();

        Assert.IsType<global::Infrastructure.Excel.ExcelDictionaryProvider>(provider);
    }

    [Fact]
    public void Resolve_DictionaryProvider_WithApiConfig_IsSingleton()
    {
        var config = new Dictionary<string, string?>
        {
            ["DictionaryApi:BaseUrl"] = "https://test.example.com",
            ["DictionaryApi:ApiKey"] = "test-key"
        };
        using var sp = BuildServiceProvider(config);

        var first = sp.GetRequiredService<IDictionaryProvider>();
        var second = sp.GetRequiredService<IDictionaryProvider>();

        Assert.Same(first, second);
    }
}
