using Infrastructure.Protocol.Hardware;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Protocol;

/// <summary>
/// Extension methods per registrare gli adapter hardware del protocollo STEM
/// nel container DI dell'host (App).
///
/// <para><b>Cosa registra:</b></para>
/// <list type="bullet">
/// <item><description><see cref="PCANManager"/> come <see cref="IPcanDriver"/> singleton (driver PCAN-USB embedded in questo progetto).</description></item>
/// <item><description><see cref="CanPort"/>, <see cref="BlePort"/>, <see cref="SerialPort"/> come singleton del tipo concreto.</description></item>
/// </list>
///
/// <para><b>Cosa NON registra (responsabilità dell'host):</b></para>
/// <list type="bullet">
/// <item><description><see cref="IBleDriver"/> — implementazione vive in <c>App/BLE_Manager</c> (ha riferimenti a <c>Form1.FormRef</c>, da rimuovere in Phase 3).</description></item>
/// <item><description><see cref="ISerialDriver"/> — implementazione vive in <c>App/SerialPortManager</c>.</description></item>
/// </list>
///
/// <para><b>Scelta architetturale (REFACTOR_PLAN Branch C, dubbio 1 opzione c):</b>
/// gli adapter sono registrati come tipi concreti, non come <see cref="Core.Interfaces.ICommunicationPort"/>.
/// La scelta del canale attivo è runtime e sarà gestita in Phase 3 da un
/// <c>ConnectionManager</c> che riceverà tutti e tre gli adapter nel ctor.</para>
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra gli adapter hardware. L'host deve aver già registrato
    /// <see cref="IBleDriver"/> e <see cref="ISerialDriver"/> prima di chiamare
    /// questo metodo, altrimenti la risoluzione di <see cref="BlePort"/> e
    /// <see cref="SerialPort"/> fallirà a runtime.
    /// </summary>
    public static IServiceCollection AddProtocolInfrastructure(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IPcanDriver, PCANManager>();
        services.AddSingleton<CanPort>();
        services.AddSingleton<BlePort>();
        services.AddSingleton<SerialPort>();

        return services;
    }
}
