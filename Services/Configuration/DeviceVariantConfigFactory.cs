using Core.Interfaces;
using Core.Models;

namespace Services.Configuration;

/// <summary>
/// Factory per <see cref="IDeviceVariantConfig"/> da stringa di configurazione.
/// Usata dall'host DI per materializzare la variante letta da
/// <c>appsettings.json</c> (chiavi <c>Device:Variant</c> e <c>Device:SenderId</c>).
///
/// Parsing <b>totale</b>: input sconosciuti, null o vuoti → <see cref="DeviceVariant.Generic"/>.
/// Case-insensitive e trim-tolerant.
///
/// Formalizzazione Lean (memoria progetto, §4): <c>parseVariant</c> e
/// <c>fromConfigString</c> in <c>Stem.Services.Configuration</c>.
/// </summary>
public static class DeviceVariantConfigFactory
{
    /// <summary>
    /// Materializza la config dalla stringa di configurazione, con
    /// <see cref="DeviceVariantConfig.DefaultSenderId"/> come SenderId.
    /// </summary>
    public static IDeviceVariantConfig FromString(string? configValue)
        => DeviceVariantConfig.Create(ParseVariant(configValue));

    /// <summary>
    /// Materializza la config con variante e SenderId espliciti. Usata dall'host
    /// DI quando il SenderId è letto da <c>appsettings.json</c>.
    /// </summary>
    public static IDeviceVariantConfig FromString(string? configValue, uint senderId)
        => DeviceVariantConfig.Create(ParseVariant(configValue), senderId);

    /// <summary>
    /// Parsing totale della variante da stringa. Case-insensitive.
    /// Input sconosciuto, null o vuoto → <see cref="DeviceVariant.Generic"/>.
    /// </summary>
    public static DeviceVariant ParseVariant(string? configValue)
    {
        if (string.IsNullOrWhiteSpace(configValue)) return DeviceVariant.Generic;
        return configValue.Trim().ToLowerInvariant() switch
        {
            "toplift" => DeviceVariant.TopLift,
            "eden"    => DeviceVariant.Eden,
            "egicon"  => DeviceVariant.Egicon,
            "generic" => DeviceVariant.Generic,
            _         => DeviceVariant.Generic
        };
    }

    /// <summary>
    /// Nome canonico della variante (round-trip con <see cref="ParseVariant"/>).
    /// </summary>
    public static string CanonicalName(DeviceVariant variant) => variant switch
    {
        DeviceVariant.Generic => "Generic",
        DeviceVariant.TopLift => "TopLift",
        DeviceVariant.Eden    => "Eden",
        DeviceVariant.Egicon  => "Egicon",
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Variante non supportata.")
    };
}
