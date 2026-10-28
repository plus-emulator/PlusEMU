using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const string FollowRow = "0000000000000000\r0000000000000000\r0000000000000000\r0000000000000000\r0000000000000000";

    // A following bot picks its goal next to the tile the user is announced to step onto in the same room tick as the bot's first step.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task ABotFollowingAWalkingUserAimsNextToTheTileTheUserStepsOntoThatTick(int seed)
    {
        FallbackModel(FollowRow);
        var owner = FallbackActor(1, 2);
        var bot = FallbackBot(0, 2, 99);
        var targets = WiredBotTargets.For(_room);
        targets.Random = new Random(seed);
        targets.Follow(bot, owner);
        ExecutorTick();
        await EdgeMove().Parse(_client, ClientPacket(12, 2));
        var log = new List<string>();
        var wasMoving = false;
        var checkedStarts = 0;

        for (var tick = 0; tick < 12; tick++) {
            ExecutorTick();
            var botMoving = bot.Statusses.GetValueOrDefault("mv") is not null;
            var ownerTile = owner.Statusses.GetValueOrDefault("mv") is { } mv ? mv.Split(',') : [owner.X.ToString(), owner.Y.ToString()];
            log.Add($"t{tick} owner->({ownerTile[0]},{ownerTile[1]}) bot({bot.X},{bot.Y}) mv={bot.Statusses.GetValueOrDefault("mv")} goal=({bot.GoalX},{bot.GoalY})");

            if (botMoving && !wasMoving) {
                checkedStarts++;
                var distance = Math.Max(Math.Abs(bot.GoalX - int.Parse(ownerTile[0])), Math.Abs(bot.GoalY - int.Parse(ownerTile[1])));
                Assert.True(distance == 1, $"goal ({bot.GoalX},{bot.GoalY}) is not next to the user's tile ({ownerTile[0]},{ownerTile[1]})\n" + string.Join("\n", log));
            }

            wasMoving = botMoving;
        }

        Assert.True(checkedStarts > 0, string.Join("\n", log));
    }
}
