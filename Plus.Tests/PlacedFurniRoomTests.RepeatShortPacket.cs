using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A Repeat Short stack that fires on the same room tick as a Repeat Every stack sends its own ObjectsDataUpdate after the Repeat Every one, whatever the item ids.
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(10, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(10, true)]
    public void ARepeatShortAndARepeatEveryOnOneRoomTickSendTwoDataUpdatesEveryFirst(int shortUnits, bool shortPlacedFirst)
    {
        var (clock, manager) = GridRoom();
        uint everyStack = shortPlacedFirst ? 140u : 40u, shortStack = shortPlacedFirst ? 40u : 140u;
        var everyTarget = ToggleAt(shortPlacedFirst ? 121u : 20u, 2, 0);
        var shortTarget = ToggleAt(shortPlacedFirst ? 20u : 121u, 2, 2);
        GridStack(everyStack, "wf_trg_periodically", new() { IntParams = [1] }, 1, everyTarget, 0);
        GridStack(shortStack, "wf_trg_period_short", new() { IntParams = [shortUnits] }, 3, shortTarget, 0);
        ExecutorActor(1, 0);

        var passes = RunRoom(manager, clock, clock.Ms + 3000, period: 25);

        var shared = passes.Where(pass => Touches(pass, everyTarget) && Touches(pass, shortTarget)).ToArray();
        Assert.True(shared.Length >= 4, $"{shared.Length} shared passes");
        Assert.All(shared, pass =>
        {
            var holding = DataUpdates(pass, everyTarget, shortTarget);
            Assert.True(holding.Length == 2, $"pass at {pass.At}: {holding.Length} ObjectsDataUpdate with the targets");
            Assert.Equal(new[] { everyStack, everyStack + 1, everyTarget.Id }.Order(), holding[0].Order());
            Assert.Equal(new[] { shortStack, shortStack + 1, shortTarget.Id }.Order(), holding[1].Order());
        });
    }

    [Fact]
    public void ARepeatLongAndARepeatEveryOnOneRoomTickShareOneDataUpdate()
    {
        var (clock, manager) = GridRoom();
        var everyTarget = ToggleAt(20, 2, 0);
        var longTarget = ToggleAt(121, 2, 2);
        GridStack(40, "wf_trg_periodically", new() { IntParams = [1] }, 1, everyTarget, 0);
        GridStack(140, "wf_trg_period_long", new() { IntParams = [1] }, 3, longTarget, 0);
        ExecutorActor(1, 0);

        var passes = RunRoom(manager, clock, clock.Ms + 12_000, period: 25);

        var shared = passes.Where(pass => Touches(pass, everyTarget) && Touches(pass, longTarget)).ToArray();
        Assert.NotEmpty(shared);
        Assert.All(shared, pass => Assert.Single(DataUpdates(pass, everyTarget, longTarget)));
    }

    [Fact]
    public void ARepeatShortAloneSendsOneDataUpdatePerFiring()
    {
        var (clock, manager) = GridRoom();
        var shortTarget = ToggleAt(121, 2, 2);
        GridStack(140, "wf_trg_period_short", new() { IntParams = [2] }, 3, shortTarget, 0);
        ExecutorActor(1, 0);

        var passes = RunRoom(manager, clock, clock.Ms + 3000, period: 25);

        var fired = passes.Where(pass => Touches(pass, shortTarget)).ToArray();
        Assert.True(fired.Length >= 20, $"{fired.Length} firings");
        Assert.All(fired, pass => Assert.Single(DataUpdates(pass, shortTarget)));
    }

    private static uint[][] DataUpdates(GridPass pass, params Plus.HabboHotel.Items.Item[] targets) =>
        [.. pass.Packets.Where(packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer).Select(DataUpdateIds)
            .Where(ids => targets.Any(target => ids.Contains(target.Id)))];
}
