using App.Core.Interfaces;
using App.Services;
using Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.Integration.DependencyInjection;

/// <summary>
/// Test di integrazione per il container DI.
/// Verifica che il wiring dei servizi funzioni come in Program.cs.
/// </summary>
public class ServiceRegistrationTests
{
    private static ServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddTransient<IButtonPanelTestService, ButtonPanelTestService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Resolve_ButtonPanelTestService_ReturnsInstance()
    {
        using var sp = BuildServiceProvider();

        var service = sp.GetRequiredService<IButtonPanelTestService>();

        Assert.NotNull(service);
        Assert.IsType<ButtonPanelTestService>(service);
    }

    [Fact]
    public void Resolve_ButtonPanelTestService_IsTransient()
    {
        using var sp = BuildServiceProvider();

        var first = sp.GetRequiredService<IButtonPanelTestService>();
        var second = sp.GetRequiredService<IButtonPanelTestService>();

        Assert.NotSame(first, second);
    }

    [Fact]
    public void Resolve_UnregisteredService_Throws()
    {
        using var sp = BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => sp.GetRequiredService<IButtonPanelTestTab>());
    }
}
