namespace Core.Models;

/// <summary>
/// Firmware areas of the SPARK device addressable by the STEM batch update flow.
/// Bluetooth is intentionally excluded: it is updated via Silicon Labs'
/// "Simplicity Connect BLE mobile app" (PHASE 1 of <c>Docs/SPARK_UPDATE_FW.md</c>),
/// not from this application.
/// </summary>
public enum SparkFirmwareArea
{
    BootloaderHmi,
    HmiApplication,
    HmiImages,
    Motor1,
    Motor2,
    Rostrum
}

/// <summary>
/// Static metadata for a single SPARK firmware area: how to display it,
/// which STEM recipient receives the upload, and the position of the area
/// in the canonical execution sequence (lower = runs first).
///
/// The execution order mirrors the sequence in <c>Docs/SPARK_UPDATE_FW.md</c>:
/// Bootloader HMI → HMI application → HMI Images → Motor1 → Motor2 → Rostrum.
/// </summary>
public sealed record SparkAreaDefinition(
    SparkFirmwareArea Area,
    string DisplayName,
    uint RecipientId,
    int Order);

/// <summary>
/// Canonical table of SPARK firmware areas. Source of truth for the UI
/// (row order, display labels) and for the batch orchestrator (execution
/// order, recipient resolution).
/// </summary>
public static class SparkAreas
{
    /// <summary>
    /// All SPARK areas in canonical execution order. Recipient IDs are the
    /// STEM addresses provided by the device team; they may be revised once
    /// the routing strategy through the HMI is confirmed.
    /// </summary>
    public static readonly IReadOnlyList<SparkAreaDefinition> All =
    [
        new(SparkFirmwareArea.BootloaderHmi,  "Bootloader HMI",  0x000702C1u, Order: 0),
        new(SparkFirmwareArea.HmiApplication, "HMI application", 0x000702C1u, Order: 1),
        new(SparkFirmwareArea.HmiImages,      "HMI Images",      0x000702C1u, Order: 2),
        new(SparkFirmwareArea.Motor1,         "Motor 1 (right)", 0x00070301u, Order: 3),
        new(SparkFirmwareArea.Motor2,         "Motor 2 (left)",  0x00070302u, Order: 4),
        new(SparkFirmwareArea.Rostrum,        "Rostrum",         0x00070341u, Order: 5),
    ];

    public static SparkAreaDefinition Get(SparkFirmwareArea area)
        => All.First(a => a.Area == area);
}
