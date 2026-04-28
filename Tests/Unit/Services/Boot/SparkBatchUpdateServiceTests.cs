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
        // Submit in a deliberately scrambled order; orchestrator must reorder.
        var items = new List<SparkBatchItem>
        {
            new(SparkFirmwareArea.Rostrum,        Fw),
            new(SparkFirmwareArea.HmiApplication, Fw),
            new(SparkFirmwareArea.Motor1,         Fw),
            new(SparkFirmwareArea.BootloaderHmi,  Fw),
            new(SparkFirmwareArea.Motor2,         Fw),
            new(SparkFirmwareArea.HmiImages,      Fw),
        };

        await orchestrator.ExecuteAsync(items);

        // Each area = 3 calls: StartBoot, UploadBlocksOnly, EndBoot. RESTART_MACHINE
        // is hoisted to the batch end (issue #74) and not part of the per-area sequence.
        // The "Start" calls (one per area) reveal the order.
        var startedAreas = fake.Calls
            .Where(c => c.Kind == FakeBootCallKind.StartBoot)
            .Select(c => c.Recipient)
            .ToList();
        Assert.Equal(
            SparkAreas.All.Select(a => a.RecipientId).ToList(),
            startedAreas);
    }

    [Fact]
    public async Task ExecuteAsync_Subset_RunsOnlySelectedInCanonicalOrder()
    {
        var fake = new FakeBootService();
        var orchestrator = new SparkBatchUpdateService(fake);
        var items = new List<SparkBatchItem>
        {
            new(SparkFirmwareArea.Rostrum,   Fw),
            new(SparkFirmwareArea.HmiImages, Fw),
        };

        await orchestrator.ExecuteAsync(items);

        var startedAreas = fake.Calls
            .Where(c => c.Kind == FakeBootCallKind.StartBoot)
            .Select(c => c.Recipient)
            .ToList();
        Assert.Equal(
            new[]
            {
                SparkAreas.Get(SparkFirmwareArea.HmiImages).RecipientId,
                SparkAreas.Get(SparkFirmwareArea.Rostrum).RecipientId,
            },
            startedAreas);
    }

    [Fact]
    public async Task ExecuteAsync_StartBootFails_ThrowsAndStopsBatch()
    {
        var fake = new FakeBootService { StartBootFailsForRecipient = SparkAreas.Get(SparkFirmwareArea.Motor1).RecipientId };
        var orchestrator = new SparkBatchUpdateService(fake);
        var items = SparkAreas.All
            .Select(a => new SparkBatchItem(a.Area, Fw))
            .ToList();

        var ex = await Assert.ThrowsAsync<SparkBatchUpdateException>(
            () => orchestrator.ExecuteAsync(items));

        Assert.Equal(SparkFirmwareArea.Motor1, ex.Area.Area);
        Assert.Equal(SparkBatchPhase.StartBoot, ex.Phase);
        // Areas after Motor1 must not have been started.
        var started = fake.Calls
            .Where(c => c.Kind == FakeBootCallKind.StartBoot)
            .Select(c => c.Recipient)
            .ToList();
        Assert.DoesNotContain(SparkAreas.Get(SparkFirmwareArea.Motor2).RecipientId, started);
        Assert.DoesNotContain(SparkAreas.Get(SparkFirmwareArea.Rostrum).RecipientId, started);
    }

    [Fact]
    public async Task ExecuteAsync_UploadBlocksFails_ThrowsWithUploadBlocksPhase()
    {
        // Use Rostrum: distinct recipient so the fake can target precisely
        // (BootloaderHmi/HmiApplication/HmiImages currently share the same
        // recipient address — see SparkAreas.All).
        var fake = new FakeBootService { UploadBlocksFailsForRecipient = SparkAreas.Get(SparkFirmwareArea.Rostrum).RecipientId };
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
        var fake = new FakeBootService { EndBootFailsForRecipient = SparkAreas.Get(SparkFirmwareArea.Motor2).RecipientId };
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
        // Three areas with DISTINCT recipient ids, in canonical order:
        // Motor1 (Order=3) → Motor2 (Order=4) → Rostrum (Order=5).
        // The fake is configured to fail UploadBlocks for the SECOND area
        // (Motor2), so the first must complete fully and the third must never
        // be StartBoot-ed.
        var fake = new FakeBootService
        {
            UploadBlocksFailsForRecipient = SparkAreas.Get(SparkFirmwareArea.Motor2).RecipientId,
        };
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

        // Third area (Rostrum) must never have been StartBoot-ed.
        var rostrumRecipient = SparkAreas.Get(SparkFirmwareArea.Rostrum).RecipientId;
        Assert.DoesNotContain(
            fake.Calls,
            c => c.Kind == FakeBootCallKind.StartBoot && c.Recipient == rostrumRecipient);

        // First area (Motor1) must have the three per-area call kinds observed —
        // but NOT Restart, since RESTART_MACHINE is hoisted to the batch end
        // and the abort path skips it entirely (issue #74).
        var motor1Recipient = SparkAreas.Get(SparkFirmwareArea.Motor1).RecipientId;
        var motor1Kinds = fake.Calls
            .Where(c => c.Recipient == motor1Recipient)
            .Select(c => c.Kind)
            .ToList();
        Assert.Contains(FakeBootCallKind.StartBoot,        motor1Kinds);
        Assert.Contains(FakeBootCallKind.UploadBlocksOnly, motor1Kinds);
        Assert.Contains(FakeBootCallKind.EndBoot,          motor1Kinds);
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
    private sealed record FakeBootCall(FakeBootCallKind Kind, uint Recipient);

    private sealed class FakeBootService : IBootService
    {
        public List<FakeBootCall> Calls { get; } = new();
        public uint? StartBootFailsForRecipient { get; set; }
        public uint? UploadBlocksFailsForRecipient { get; set; }
        public uint? EndBootFailsForRecipient { get; set; }

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
            return Task.FromResult(StartBootFailsForRecipient != recipientId);
        }

        public Task<bool> EndBootAsync(uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.EndBoot, recipientId));
            return Task.FromResult(EndBootFailsForRecipient != recipientId);
        }

        public Task RestartAsync(uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.Restart, recipientId));
            return Task.CompletedTask;
        }

        public Task UploadBlocksOnlyAsync(
            byte[] firmware, uint recipientId, CancellationToken ct = default)
        {
            Calls.Add(new(FakeBootCallKind.UploadBlocksOnly, recipientId));
            ProgressChanged?.Invoke(this, new BootProgress(firmware.Length, firmware.Length));
            if (UploadBlocksFailsForRecipient == recipientId)
            {
                State = BootState.Failed;
                return Task.CompletedTask;
            }
            State = BootState.Completed;
            return Task.CompletedTask;
        }
    }
}
