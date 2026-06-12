using Core.Interfaces;
using Core.Models;
using Services.Boot;

namespace Tests.Unit.Services.Boot;

/// <summary>
/// Tests for <see cref="SparkBatchUpdateService"/>. Uses a manual fake
/// <see cref="IBootService"/> (no protocol/HW), focused on the orchestration
/// logic: canonical order, stop-on-first-failure, phase identification.
/// </summary>
public class SparkBatchUpdateServiceTests
{
    private static readonly byte[] Fw = new byte[16];

    [Fact]
    public async Task ExecuteAsync_NullItems_Throws()
    {
        var orchestrator = new SparkBatchUpdateService(new FakeBootService());
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => orchestrator.ExecuteAsync(null!));
    }

    [Fact]
    public async Task ExecuteAsync_EmptyItems_DoesNothing()
    {
        var fake = new FakeBootService();
        var orchestrator = new SparkBatchUpdateService(fake);

        await orchestrator.ExecuteAsync([]);

        Assert.Empty(fake.Calls);
    }

    [Fact]
    public async Task ExecuteAsync_AllAreas_RunsInCanonicalOrder()
    {
        var fake = new FakeBootService();
        var orchestrator = new SparkBatchUpdateService(fake);
        // All areas share the HMI recipient, so order is observed through the
        // firmware payload each area uploads: one distinct byte per area.
        var fwByArea = SparkAreas.All.ToDictionary(
            a => a.Area, a => new byte[] { (byte)a.Order });
        // Submit in a deliberately scrambled order; orchestrator must reorder.
        var items = new List<SparkBatchItem>
        {
            new(SparkFirmwareArea.Rostrum,        fwByArea[SparkFirmwareArea.Rostrum]),
            new(SparkFirmwareArea.HmiApplication, fwByArea[SparkFirmwareArea.HmiApplication]),
            new(SparkFirmwareArea.Motor1,         fwByArea[SparkFirmwareArea.Motor1]),
            new(SparkFirmwareArea.BootloaderHmi,  fwByArea[SparkFirmwareArea.BootloaderHmi]),
            new(SparkFirmwareArea.Motor2,         fwByArea[SparkFirmwareArea.Motor2]),
            new(SparkFirmwareArea.HmiImages,      fwByArea[SparkFirmwareArea.HmiImages]),
        };

        await orchestrator.ExecuteAsync(items);

        // Each area = 3 calls: StartBoot, UploadBlocksOnly, EndBoot. RESTART_MACHINE
        // is hoisted to the batch end (issue #74) and not part of the per-area sequence.
        // The uploaded payloads (one per area) reveal the order.
        var uploadedAreaTags = fake.Calls
            .Where(c => c.Kind == FakeBootCallKind.UploadBlocksOnly)
            .Select(c => c.Firmware![0])
            .ToList();
        Assert.Equal(
            SparkAreas.All.Select(a => (byte)a.Order).ToList(),
            uploadedAreaTags);
    }

    [Fact]
    public async Task ExecuteAsync_Subset_RunsOnlySelectedInCanonicalOrder()
    {
        var fake = new FakeBootService();
        var orchestrator = new SparkBatchUpdateService(fake);
        byte[] imagesFw = [1];
        byte[] rostrumFw = [2];
        var items = new List<SparkBatchItem>
        {
            new(SparkFirmwareArea.Rostrum,   rostrumFw),
            new(SparkFirmwareArea.HmiImages, imagesFw),
        };

        await orchestrator.ExecuteAsync(items);

        var uploadedFw = fake.Calls
            .Where(c => c.Kind == FakeBootCallKind.UploadBlocksOnly)
            .Select(c => c.Firmware)
            .ToList();
        Assert.Equal(new[] { imagesFw, rostrumFw }, uploadedFw);
    }

    [Fact]
    public async Task ExecuteAsync_AllAreas_AddressesEveryCallToHmiRecipient()
    {
        // Confirmed routing design (fw team + bench 2026-06-11): the HMI
        // orchestrates the whole machine update, so every device call —
        // ECU areas included — is addressed to the HMI recipient. Direct
        // per-ECU addressing gets no reply over BLE.
        const uint HmiRecipient = 0x000702C1u;
        var fake = new FakeBootService();
        var orchestrator = new SparkBatchUpdateService(fake);
        var items = SparkAreas.All
            .Select(a => new SparkBatchItem(a.Area, Fw))
            .ToList();

        await orchestrator.ExecuteAsync(items);

        Assert.All(fake.Calls, c => Assert.Equal(HmiRecipient, c.Recipient));
    }

    [Fact]
    public async Task ExecuteAsync_StartBootFails_ThrowsAndStopsBatch()
    {
        // All areas share the HMI recipient, so the fake targets the 4th
        // StartBoot call = Motor1 in canonical order (BootloaderHmi,
        // HmiApplication, HmiImages, Motor1, Motor2, Rostrum).
        var fake = new FakeBootService { StartBootFailsAtCall = 4 };
        var orchestrator = new SparkBatchUpdateService(fake);
        var items = SparkAreas.All
            .Select(a => new SparkBatchItem(a.Area, Fw))
            .ToList();

        var ex = await Assert.ThrowsAsync<SparkBatchUpdateException>(
            () => orchestrator.ExecuteAsync(items));

        Assert.Equal(SparkFirmwareArea.Motor1, ex.Area.Area);
        Assert.Equal(SparkBatchPhase.StartBoot, ex.Phase);
        // Areas after Motor1 must not have been started: exactly 4 StartBoot
        // calls (the failing one included), none for Motor2/Rostrum.
        Assert.Equal(4, fake.Calls.Count(c => c.Kind == FakeBootCallKind.StartBoot));
    }

    [Fact]
    public async Task ExecuteAsync_UploadBlocksFails_ThrowsWithUploadBlocksPhase()
    {
        // 6th UploadBlocksOnly call = Rostrum, last area in canonical order.
        var fake = new FakeBootService { UploadBlocksFailsAtCall = 6 };
        var orchestrator = new SparkBatchUpdateService(fake);
        var items = SparkAreas.All
            .Select(a => new SparkBatchItem(a.Area, Fw))
            .ToList();

        var ex = await Assert.ThrowsAsync<SparkBatchUpdateException>(
            () => orchestrator.ExecuteAsync(items));

        Assert.Equal(SparkFirmwareArea.Rostrum, ex.Area.Area);
        Assert.Equal(SparkBatchPhase.UploadBlocks, ex.Phase);
    }

    [Fact]
    public async Task ExecuteAsync_EndBootFails_ThrowsWithEndBootPhase()
    {
        // 5th EndBoot call = Motor2 in canonical order.
        var fake = new FakeBootService { EndBootFailsAtCall = 5 };
        var orchestrator = new SparkBatchUpdateService(fake);
        var items = SparkAreas.All
            .Select(a => new SparkBatchItem(a.Area, Fw))
            .ToList();

        var ex = await Assert.ThrowsAsync<SparkBatchUpdateException>(
            () => orchestrator.ExecuteAsync(items));

        Assert.Equal(SparkFirmwareArea.Motor2, ex.Area.Area);
        Assert.Equal(SparkBatchPhase.EndBoot, ex.Phase);
    }

    [Fact]
    public async Task Execute_SecondAreaFailsAfterRetries_AbortsAndNamesArea()
    {
        // Three areas in canonical order: Motor1 (Order=3) → Motor2 (Order=4)
        // → Rostrum (Order=5). The fake fails the SECOND UploadBlocks call
        // (Motor2), so the first area must complete fully and the third must
        // never be StartBoot-ed.
        var fake = new FakeBootService { UploadBlocksFailsAtCall = 2 };
        var orchestrator = new SparkBatchUpdateService(fake);
        var items = new List<SparkBatchItem>
        {
            new(SparkFirmwareArea.Motor1, Fw),
            new(SparkFirmwareArea.Motor2, Fw),
            new(SparkFirmwareArea.Rostrum, Fw),
        };

        var ex = await Assert.ThrowsAsync<SparkBatchUpdateException>(
            () => orchestrator.ExecuteAsync(items));

        Assert.Equal(SparkFirmwareArea.Motor2, ex.Area.Area);
        Assert.Equal(SparkBatchPhase.UploadBlocks, ex.Phase);

        // Third area (Rostrum) must never have been StartBoot-ed: only the
        // first two areas opened a boot session.
        Assert.Equal(2, fake.Calls.Count(c => c.Kind == FakeBootCallKind.StartBoot));

        // First area (Motor1) completed its three per-area steps in order —
        // but NOT Restart, since RESTART_MACHINE is hoisted to the batch end
        // and the abort path skips it entirely (issue #74).
        Assert.Equal(
            new[]
            {
                FakeBootCallKind.StartBoot,
                FakeBootCallKind.UploadBlocksOnly,
                FakeBootCallKind.EndBoot,
            },
            fake.Calls.Take(3).Select(c => c.Kind).ToArray());
        // Issue #74: on the abort path, no RESTART_MACHINE fires anywhere
        // — half-flashed devices must not be auto-rebooted.
        Assert.DoesNotContain(FakeBootCallKind.Restart, fake.Calls.Select(c => c.Kind));
    }

    [Fact]
    public async Task ExecuteAsync_NominalBatch_FiresSingleRestartAtEndAddressedToHmi()
    {
        // Issue #74: SPARK firmware requires RESTART_MACHINE only at the end of the
        // batch, addressed to the HMI board. The orchestrator must hoist the per-area
        // restart out of RunAreaAsync and emit exactly one CMD_RESTART_MACHINE
        // (recipient = HMI) after the last area completes.
        var fake = new FakeBootService();
        var orchestrator = new SparkBatchUpdateService(fake);
        var items = new List<SparkBatchItem>
        {
            new(SparkFirmwareArea.HmiApplication, Fw),
            new(SparkFirmwareArea.Motor1,         Fw),
            new(SparkFirmwareArea.Rostrum,        Fw),
        };
        var hmiRecipient = SparkAreas.Get(SparkFirmwareArea.HmiApplication).RecipientId;

        await orchestrator.ExecuteAsync(items);

        var restartCalls = fake.Calls.Where(c => c.Kind == FakeBootCallKind.Restart).ToList();
        Assert.Single(restartCalls);
        Assert.Equal(hmiRecipient, restartCalls[0].Recipient);

        // Restart must be the LAST call: no further per-area work after it.
        Assert.Equal(FakeBootCallKind.Restart, fake.Calls[^1].Kind);
    }

    [Fact]
    public async Task Execute_EmptyFirmwareAtStart_ThrowsBeforeAnyDeviceCall()
    {
        // First item has zero-length firmware → precondition must reject
        // BEFORE any device call, regardless of the rest of the batch.
        var fake = new FakeBootService();
        var orchestrator = new SparkBatchUpdateService(fake);
        var offendingArea = SparkFirmwareArea.Motor1;
        var items = new List<SparkBatchItem>
        {
            new(offendingArea,               Array.Empty<byte>()),
            new(SparkFirmwareArea.Motor2,    Fw),
            new(SparkFirmwareArea.Rostrum,   Fw),
        };

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => orchestrator.ExecuteAsync(items));

        Assert.Empty(fake.Calls);
        Assert.Contains(SparkAreas.Get(offendingArea).DisplayName, ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_RaisesAreaStartedAndCompletedEvents()
    {
        var fake = new FakeBootService();
        var orchestrator = new SparkBatchUpdateService(fake);
        var startedAreas = new List<SparkFirmwareArea>();
        var completedAreas = new List<SparkFirmwareArea>();
        orchestrator.AreaStarted += (_, a) => startedAreas.Add(a.Area);
        orchestrator.AreaCompleted += (_, a) => completedAreas.Add(a.Area);
        var items = new List<SparkBatchItem>
        {
            new(SparkFirmwareArea.HmiApplication, Fw),
            new(SparkFirmwareArea.Motor1,         Fw),
        };

        await orchestrator.ExecuteAsync(items);

        Assert.Equal(
            new[] { SparkFirmwareArea.HmiApplication, SparkFirmwareArea.Motor1 },
            startedAreas);
        Assert.Equal(startedAreas, completedAreas);
    }

    [Fact]
    public async Task ExecuteAsync_AreaProgress_TaggedWithCurrentArea()
    {
        var fake = new FakeBootService();
        var orchestrator = new SparkBatchUpdateService(fake);
        var observed = new List<SparkAreaProgress>();
        orchestrator.AreaProgress += (_, p) => observed.Add(p);
        // Fake will emit a single progress event during UploadBlocksOnlyAsync.
        var items = new List<SparkBatchItem>
        {
            new(SparkFirmwareArea.Motor1, Fw),
        };

        await orchestrator.ExecuteAsync(items);

        Assert.NotEmpty(observed);
        Assert.All(observed, p => Assert.Equal(SparkFirmwareArea.Motor1, p.Area));
    }

    // --- Manual fake (no mocking library) ---

    private enum FakeBootCallKind { StartBoot, UploadBlocksOnly, EndBoot, Restart }
    private sealed record FakeBootCall(
        FakeBootCallKind Kind, uint Recipient, byte[]? Firmware = null);

    private sealed class FakeBootService : IBootService
    {
        public List<FakeBootCall> Calls { get; } = new();

        // All SPARK areas share the HMI recipient, so failures are targeted by
        // the 1-based ordinal of the call among calls of the same kind. Areas
        // run in canonical order, so ordinal N = Nth area in the sorted batch.
        public int? StartBootFailsAtCall { get; set; }
        public int? UploadBlocksFailsAtCall { get; set; }
        public int? EndBootFailsAtCall { get; set; }

        public BootState State { get; private set; } = BootState.Idle;
        public double Progress => 0;
        public event EventHandler<BootProgress>? ProgressChanged;

        public Task StartFirmwareUploadAsync(
            byte[] firmware, uint recipientId, CancellationToken ct = default)
            => throw new NotSupportedException(
                "SparkBatchUpdateService should never call StartFirmwareUploadAsync.");

        public Task<bool> StartBootAsync(uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.StartBoot, recipientId));
            return Task.FromResult(
                StartBootFailsAtCall != OrdinalOf(FakeBootCallKind.StartBoot));
        }

        public Task<bool> EndBootAsync(uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.EndBoot, recipientId));
            return Task.FromResult(
                EndBootFailsAtCall != OrdinalOf(FakeBootCallKind.EndBoot));
        }

        public Task RestartAsync(uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.Restart, recipientId));
            return Task.CompletedTask;
        }

        public Task UploadBlocksOnlyAsync(
            byte[] firmware, uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.UploadBlocksOnly, recipientId, firmware));
            ProgressChanged?.Invoke(this, new BootProgress(firmware.Length, firmware.Length));
            if (UploadBlocksFailsAtCall == OrdinalOf(FakeBootCallKind.UploadBlocksOnly))
            {
                State = BootState.Failed;
                return Task.CompletedTask;
            }
            State = BootState.Completed;
            return Task.CompletedTask;
        }

        /// <summary>1-based ordinal of the call just recorded for <paramref name="kind"/>.</summary>
        private int OrdinalOf(FakeBootCallKind kind)
            => Calls.Count(c => c.Kind == kind);
    }
}
