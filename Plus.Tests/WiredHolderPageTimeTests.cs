using Plus.Communication.Packets.Outgoing.WiredVariables;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public class WiredHolderPageTimeTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 28, 14, 25, 20, TimeSpan.Zero);
    private static readonly DateTimeOffset Updated = new(2026, 10, 29, 1, 2, 3, TimeSpan.Zero);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HolderPageWritesCreationAndUpdateTimeAsDayMonthYearWithDashes(bool exact)
    {
        var holder = new WiredVariableStoredHolder(new(10, WiredVariableTarget.User, 7), "Alice", new(5, Created, Updated));
        var packet = new HabbiconTestSupport.RecordingPacket();

        new WiredVariableHoldersPageComposer("custom:10", new(1, 1, 50, [holder]), -1, 0, exact).Compose(packet);

        var texts = packet.Writes.OfType<string>().ToArray();
        Assert.Equal(["custom:10", "Alice", "28-10-2026 14:25:20", "29-10-2026 01:02:03"], texts);
    }

    [Fact]
    public void HolderWithoutTimesWritesEmptyTexts()
    {
        var holder = new WiredVariableStoredHolder(new(10, WiredVariableTarget.User, 7), "Alice", new(5, null, null));
        var packet = new HabbiconTestSupport.RecordingPacket();

        new WiredVariableHoldersPageComposer("custom:10", new(1, 1, 50, [holder]), -1, 0).Compose(packet);

        Assert.Equal(["custom:10", "Alice", "", ""], packet.Writes.OfType<string>().ToArray());
    }
}
