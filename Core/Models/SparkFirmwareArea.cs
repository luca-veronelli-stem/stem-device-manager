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
    /// All SPARK areas in canonical execution order. Every area uploads to the
    /// HMI recipient (0x000702C1): the HMI is the update orchestrator for the
    /// whole machine by design (fw team, <c>Docs/SPARK_UPDATE_FW.md</c>) and
    /// stages ECU firmware in its external flash for later delivery. Direct
    /// per-ECU addressing gets no reply over BLE (bench 2026-06-11); the
    /// original device-team ECU addresses were Motor1 0x00070301, Motor2
    /// 0x00070302, Rostrum 0x00070341.
    ///
    /// HMI application and HMI Images updates are bench-proven end-to-end over
    /// BLE (2026-06-10/11/12). The ECU areas (Motor1/Motor2/Rostrum) are
    /// currently NON-FUNCTIONAL: a confirmed bug in the HMI bootloader (wrong
    /// 32-byte offset in the images-bootloader path, fw team 2026-06-12) makes
    /// ECU staging fail and corrupts a previously-written images area in the
    /// same session. Blocked on a fixed bootloader release; nothing host-side
    /// to change.
    /// </summary>
    public static readonly IReadOnlyList<SparkAreaDefinition> All =
    [
        new(SparkFirmwareArea.BootloaderHmi,  "Bootloader HMI",  0x000702C1u, Order: 0),
        new(SparkFirmwareArea.HmiApplication, "HMI application", 0x000702C1u, Order: 1),
        new(SparkFirmwareArea.HmiImages,      "HMI Images",      0x000702C1u, Order: 2),
        new(SparkFirmwareArea.Motor1,         "Motor 1 (right)", 0x000702C1u, Order: 3),
        new(SparkFirmwareArea.Motor2,         "Motor 2 (left)",  0x000702C1u, Order: 4),
        new(SparkFirmwareArea.Rostrum,        "Rostrum",         0x000702C1u, Order: 5),
    ];

    public static SparkAreaDefinition Get(SparkFirmwareArea area)
        => All.First(a => a.Area == area);
}
