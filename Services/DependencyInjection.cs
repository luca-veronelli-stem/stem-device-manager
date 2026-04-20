using Core.Interfaces;
using Core.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Services.Cache;
using Services.Configuration;
using Services.Protocol;

namespace Services;

/// <summary>
/// Extension methods per registrare i servizi del layer <c>Services</c> nel
/// container DI dell'host (App).
///
/// <para><b>Cosa registra:</b></para>
/// <list type="bullet">
/// <item><description><see cref="IDeviceVariantConfig"/> — risolto da <c>Device:Variant</c> + <c>Device:SenderId</c> in <see cref="IConfiguration"/>.</description></item>
/// <item><description><see cref="IPacketDecoder"/> — istanza vuota; il consumer deve chiamare <c>UpdateDictionary</c> dopo aver caricato i dati da <see cref="IDictionaryProvider"/>.</description></item>
/// </list>
///
/// <para><b>Cosa NON registra (per scelta architetturale, vedi REFACTOR_PLAN
/// Branch C, dubbio 1 opzione c):</b></para>
/// <list type="bullet">
/// <item><description><see cref="ProtocolService"/> — dipende da una <see cref="ICommunicationPort"/> scelta a runtime (CAN/BLE/Serial), creato dal <see cref="ConnectionManager"/>.</description></item>
/// <item><description><see cref="ITelemetryService"/> e <see cref="IBootService"/> — dipendono da <see cref="IProtocolService"/>, creati insieme ad esso dal consumer.</description></item>
/// </list>
///
/// <para><b>Fase 3:</b> aggiunge <see cref="DictionaryCache"/> e
/// <see cref="ConnectionManager"/> al container. <c>ConnectionManager</c>
/// riceve via DI l'enumerabile di <see cref="ICommunicationPort"/> (registrate
/// da <c>AddProtocolInfrastructure()</c>).</para>
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra i servizi pure-logic in base alla <see cref="IConfiguration"/>
    /// dell'host. Idempotente: chiamare due volte non duplica le registrazioni
    /// (il container considera l'ultima registrazione vincente per
    /// <c>AddSingleton</c>).
    /// </summary>
    public static IServiceCollection AddServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var deviceSection = configuration.GetSection("Device");
        var variant = deviceSection["Variant"];
        uint senderId = uint.TryParse(deviceSection["SenderId"], out var parsedSenderId)
            ? parsedSenderId
            : DeviceVariantConfig.DefaultSenderId;

        services.AddSingleton<IDeviceVariantConfig>(_ =>
            DeviceVariantConfigFactory.FromString(variant, senderId));

        // PacketDecoder vuoto: UpdateDictionary verrà chiamato dalla DictionaryCache
        // dopo IDictionaryProvider.LoadProtocolDataAsync(). Pattern consolidato dalla
        // Fase 1 — il dizionario arriva async da Azure, non sincronizzabile in DI.
        services.AddSingleton<IPacketDecoder>(_ => new PacketDecoder([], [], []));

        // Cache del dizionario + gestore del canale attivo (Fase 3).
        services.AddSingleton<DictionaryCache>();
        services.AddSingleton<ConnectionManager>();

        return services;
    }
}
