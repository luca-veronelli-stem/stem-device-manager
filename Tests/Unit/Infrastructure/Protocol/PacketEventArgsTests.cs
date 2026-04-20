using Infrastructure.Protocol.Hardware;

namespace Tests.Unit.Infrastructure.Protocol;

/// <summary>
/// Test per gli event args dei driver HW (<see cref="BlePacketEventArgs"/>,
/// <see cref="SerialPacketEventArgs"/>, <see cref="CANPacketEventArgs"/>).
/// Verifica assegnazione proprietà e validazione argomenti.
/// </summary>
public class PacketEventArgsTests
{
    // --- BlePacketEventArgs ---

    [Fact]
    public void BleArgs_Ctor_AssignsProperties()
    {
        var data = new byte[] { 0xAA, 0xBB };
        var ts = new DateTime(2026, 4, 20, 10, 0, 0, DateTimeKind.Utc);

        var args = new BlePacketEventArgs(data, ts);

        Assert.Same(data, args.Data);
        Assert.Equal(ts, args.Timestamp);
    }

    [Fact]
    public void BleArgs_Ctor_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new BlePacketEventArgs(null!, DateTime.UtcNow));
    }

    [Fact]
    public void BleArgs_Ctor_EmptyData_Accepted()
    {
        // Array vuoto è un input valido (es. notifiche BLE senza payload).
        var args = new BlePacketEventArgs([], DateTime.UtcNow);

        Assert.Empty(args.Data);
    }

    // --- SerialPacketEventArgs ---

    [Fact]
    public void SerialArgs_Ctor_AssignsProperties()
    {
        var data = new byte[] { 0x01, 0x02, 0x03 };
        var ts = new DateTime(2026, 4, 20, 11, 0, 0, DateTimeKind.Utc);

        var args = new SerialPacketEventArgs(data, ts);

        Assert.Same(data, args.Data);
        Assert.Equal(ts, args.Timestamp);
    }

    [Fact]
    public void SerialArgs_Ctor_NullData_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new SerialPacketEventArgs(null!, DateTime.UtcNow));
    }

    [Fact]
    public void SerialArgs_Ctor_EmptyData_Accepted()
    {
        var args = new SerialPacketEventArgs([], DateTime.UtcNow);

        Assert.Empty(args.Data);
    }

    // --- CANPacketEventArgs (legacy, no null check) ---

    [Fact]
    public void CanArgs_Ctor_AssignsProperties()
    {
        var data = new byte[] { 0x11, 0x22, 0x33 };
        var ts = new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc);

        var args = new CANPacketEventArgs(0xCAFEBABE, data, ts);

        Assert.Equal(0xCAFEBABEu, args.ArbitrationId);
        Assert.Same(data, args.Data);
        Assert.Equal(ts, args.Timestamp);
    }
}
