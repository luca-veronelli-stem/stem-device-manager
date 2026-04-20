using Core.Interfaces;

namespace Core.Models;

/// <summary>
/// Configurazione di default per una <see cref="DeviceVariant"/>.
/// Rimpiazza i blocchi <c>#if TOPLIFT/EDEN/EGICON</c> di <c>Form1.LoadDictionaryDataAsync</c>
/// (vedi <c>Docs/PREPROCESSOR_DIRECTIVES.md</c> blocco #7).
///
/// In Fase 1 sono esposte solo le 4 proprietà base. I feature flag booleani
/// saranno aggiunti in Fase 3. Vedi <c>Specs/Phase1/DeviceVariantConfig.lean</c>.
/// </summary>
/// <param name="Variant">Variante del device.</param>
/// <param name="DefaultRecipientId">RecipientId fisso. 0 = risolvere per nome a runtime.</param>
/// <param name="DeviceName">Nome macchina nel dizionario (vuoto se non applicabile).</param>
/// <param name="BoardName">Nome scheda nel dizionario (vuoto se non applicabile).</param>
/// <param name="SenderId">Indirizzo STEM del mittente. Default <see cref="DefaultSenderId"/>.</param>
public sealed record DeviceVariantConfig(
    DeviceVariant Variant,
    uint DefaultRecipientId,
    string DeviceName,
    string BoardName,
    uint SenderId) : IDeviceVariantConfig
{
    /// <summary>
    /// SenderId di default usato dalla factory <see cref="Create(DeviceVariant)"/>.
    /// Storicamente hardcoded a 8 per tutte le varianti nel legacy.
    /// </summary>
    public const uint DefaultSenderId = 8u;

    /// <summary>
    /// Factory totale: per ogni <see cref="DeviceVariant"/> restituisce la config di default
    /// con <see cref="SenderId"/> = <see cref="DefaultSenderId"/>.
    /// Riflette il comportamento di <c>Form1.LoadDictionaryDataAsync</c>.
    /// </summary>
    public static DeviceVariantConfig Create(DeviceVariant variant)
        => Create(variant, DefaultSenderId);

    /// <summary>
    /// Factory totale con <see cref="SenderId"/> esplicito. Usata dalla DI per
    /// iniettare il valore letto da <c>appsettings.json</c> (<c>Device:SenderId</c>).
    /// </summary>
    public static DeviceVariantConfig Create(DeviceVariant variant, uint senderId) => variant switch
    {
        DeviceVariant.Generic => new DeviceVariantConfig(variant, 0u, "", "", senderId),
        DeviceVariant.TopLift => new DeviceVariantConfig(variant, 0x00080381u, "", "", senderId),
        DeviceVariant.Eden    => new DeviceVariantConfig(variant, 0u, "EDEN", "Madre", senderId),
        DeviceVariant.Egicon  => new DeviceVariantConfig(variant, 0u, "SPARK", "HMI", senderId),
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Variante non supportata.")
    };
}
