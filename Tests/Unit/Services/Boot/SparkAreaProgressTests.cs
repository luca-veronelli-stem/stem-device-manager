using Core.Models;
using Services.Boot;

namespace Tests.Unit.Services.Boot;

/// <summary>
/// Tests for the page-counter math on <see cref="SparkAreaProgress"/>. A "page"
/// is one <c>CMD_PROGRAM_BLOCK</c> (<see cref="BootService.FirmwareBlockSize"/>
/// bytes), so <see cref="SparkAreaProgress.CurrentPage"/> is the count of
/// acked pages and <see cref="SparkAreaProgress.TotalPages"/> the page count of
/// the whole firmware. Both are <c>ceil(bytes / blockSize)</c>.
/// </summary>
public class SparkAreaProgressTests
{
    private const int Page = BootService.FirmwareBlockSize; // 1024

    [Theory]
    // total bytes 0 -> no pages at all.
    [InlineData(0, 0, 0, 0)]
    // single full page: 0 acked at start, 1 after the page lands.
    [InlineData(0, Page, 0, 1)]
    [InlineData(Page, Page, 1, 1)]
    // partial last page (1500 B -> 2 pages): the final ack lands offset == total.
    [InlineData(0, 1500, 0, 2)]
    [InlineData(Page, 1500, 1, 2)]
    [InlineData(1500, 1500, 2, 2)]
    // three exact pages.
    [InlineData(2 * Page, 3 * Page, 2, 3)]
    [InlineData(3 * Page, 3 * Page, 3, 3)]
    public void Pages_AreCeilingOfBytesOverBlockSize(
        int currentOffset, int totalLength, int expectedCurrentPage, int expectedTotalPages)
    {
        var progress = new SparkAreaProgress(
            SparkFirmwareArea.HmiApplication, currentOffset, totalLength);

        Assert.Equal(expectedCurrentPage, progress.CurrentPage);
        Assert.Equal(expectedTotalPages, progress.TotalPages);
    }
}
