# Investigation — senderId byte order on the wire (RX may be inverted vs firmware)

**Tracking issue:** [#97](https://github.com/luca-veronelli-stem/stem-device-manager/issues/97)
**Started:** 2026-05-21
**Status:** Hypothesis, not confirmed. Needs a real-device packet capture to clinch.

---

## Hypothesis

`Services/Protocol/PacketDecoder.cs:119-125` reads the Transport-layer senderId as big-endian on the wire:

```csharp
private static uint ReadSenderIdBigEndian(ImmutableArray<byte> payload)
{
    return ((uint)payload[1] << 24)
         | ((uint)payload[2] << 16)
         | ((uint)payload[3] << 8)
         | payload[4];
}
```

v2.15 (`80bf9c6` — `STEMProtocol/STEM_protocol.cs:466`) reads the same bytes with the opposite shift order — i.e. **little-endian** on the wire:

```csharp
uint Source_Address = (TransportPacket[4] << 24)
                    | (TransportPacket[3] << 16)
                    | (TransportPacket[2] << 8)
                    | TransportPacket[1];
```

The v2.15 receive path also looks the result up against the Excel `Dizionari STEM.xlsx` column G — verified in this investigation to contain **standard-form** addresses (`0x00080381`, `0x000A0441`, etc.), not byte-swapped ones. For v2.15 to have ever matched the dictionary against real device packets, `TransportPacket[1]` must be the LSB of the senderId on the wire, i.e. the firmware writes **little-endian**. The new code inverts that and computes the byte-swapped value (e.g. `0x81030800` for what should be `0x00080381`), so `DictionarySnapshot.FindSender` returns `null` and `AppLayerDecodedEvent.SenderDevice` / `SenderBoard` are empty strings.

## Why the doc's historical explanation is suspect

`Docs/PROTOCOL.md` §3.1 claims the legacy stack had byte-swapped addresses in the dictionary and the refactor "aligned the decoder to BE because the dictionary was migrated to the standard format." Two facts contradict the premise:

1. The Excel `Dizionari STEM.xlsx` blob hash is identical between `80bf9c6` and HEAD (`8cedb73e398f7f4e25663384c462c5649e0c8c63`). The dictionary was never migrated.
2. The Excel addresses at `80bf9c6` are already standard-form (`0x00080381`).

So the legacy stack must have been reading LE on the wire to match standard addresses in the dict. The doc rewrite is part of the fix, not separate from it.

## Why this is "investigation," not a one-line patch

The "wrong" answer can be patched in one line:

```csharp
private static uint ReadSenderIdOnWire(ImmutableArray<byte> payload)
{
    return ((uint)payload[4] << 24)
         | ((uint)payload[3] << 16)
         | ((uint)payload[2] << 8)
         | payload[1];
}
```

But I don't have a real packet to verify the premise. The unit test `PacketDecoderTests.Decode_SenderIdRisolto_PopolaDeviceEBoard` is self-consistent (the test fixture writes BE, the decoder reads BE) and cannot catch this mismatch.

## What's needed to clinch it

Capture one raw RX frame from a known device (e.g. TopLift-A2 Madre `0x00080381`). Bytes `[1..4]` of the Transport-layer packet tell us the wire order directly:

- Wire bytes `[0x81, 0x03, 0x08, 0x00]` → firmware emits LE → `PacketDecoder` is wrong.
- Wire bytes `[0x00, 0x08, 0x03, 0x81]` → firmware emits BE → `PacketDecoder` is right, and empty sender names (if observed) have a different cause.

Easiest capture path: temporary log line in `ProtocolService.OnPortPacketReceived` dumping `merged.Skip(1).Take(4)` as hex for the first few packets, then revert.

## Scope of impact (if confirmed)

- `AppLayerDecodedEvent.SenderDevice` / `SenderBoard` will be empty strings. UI shows the raw hex address as fallback. Display-only.
- `AppLayerDecodedEvent.SenderId` (uint) is wrong by byte-swap. Any consumer that gates on it semantically is broken.
- Does **not** break `CMD_READ_VARIABLE` resolution — that uses `[VariableHighIndex=9]`/`[VariableLowIndex=10]` (the AL payload's address pair), which is independent of the TP senderId.
- Does **not** break TX — `ProtocolService.BuildTransportPacket` writes the PC's own senderId BE (unchanged from v2.15); the firmware does whatever it does with it.

## Independent symptoms surfaced from the same code reading

- `TelemetryService.UpdateSourceAddress` writes the *configured* source address BE into the telemetry-configure payload at `[11..14]` — both v2.15 and HEAD do this identically, so it's not a regression.
- For BLE/Serial frames, the recipientId is written LE into bytes `[2..5]` of the wire frame — both versions do this identically too.

## Not the cause of

- The telemetry / read-reply data being silently dropped — that's the DataType vocabulary issue, see [#96](https://github.com/luca-veronelli-stem/stem-device-manager/issues/96).
- The Boot Interface tab being missing — that's a UI removal, see [#95](https://github.com/luca-veronelli-stem/stem-device-manager/issues/95).

## Next bench actions

- [ ] Capture one TP-layer packet on bench, confirm wire byte order at offsets `[1..4]`.
- [ ] If LE confirmed: rename `ReadSenderIdBigEndian` → `ReadSenderIdOnWire`, swap the shifts, update `Docs/PROTOCOL.md` §3.1 and the doc-comment in `PacketDecoder.cs` to match the corrected understanding.
- [ ] Rewrite the unit test to assert against real captured wire bytes (or against the documented LE convention), not against a self-built fixture.

## Changelog of this investigation

| Date | Note |
|---|---|
| 2026-05-21 | Filed [#97](https://github.com/luca-veronelli-stem/stem-device-manager/issues/97); cross-checked legacy receive path + Excel column G; doc §3.1 narrative ruled out as accurate. |
