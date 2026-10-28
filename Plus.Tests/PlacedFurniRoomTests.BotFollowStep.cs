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
        var starts = await FollowStarts(seed, botFirst: false, targetX: 12);

        Assert.NotEmpty(starts.Starts);
        Assert.All(starts.Starts, start => Assert.True(Next(start.Goal, start.After), $"goal {start.Goal} is not next to the user's tile {start.After}\n{starts.Log}"));
    }

    // A bot that is processed before its walking user sees the tile the user still stands on.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task ABotProcessedBeforeAWalkingUserAimsNextToTheTileTheUserStandsOn(int seed)
    {
        var starts = await FollowStarts(seed, botFirst: true, targetX: 12);

        Assert.NotEmpty(starts.Starts);
        Assert.All(starts.Starts, start => Assert.True(Next(start.Goal, start.Before), $"goal {start.Goal} is not next to the user's tile {start.Before}\n{starts.Log}"));
    }

    // On the user's last step of a walk the bot sees the tile the user steps onto, whatever the processing order.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task ABotAimsNextToTheTileTheUserArrivesOnWhateverTheProcessingOrder(int seed)
    {
        var starts = await FollowStarts(seed, botFirst: true, targetX: 3);

        Assert.NotEmpty(starts.Starts);
        Assert.All(starts.Starts, start => Assert.True(start.After == (3, 2), $"the bot did not start on the user's last step\n{starts.Log}"));
        Assert.All(starts.Starts, start => Assert.True(Next(start.Goal, start.After), $"goal {start.Goal} is not next to the user's tile {start.After}\n{starts.Log}"));
    }

    private static bool Next((int X, int Y) goal, (int X, int Y) tile) => Math.Max(Math.Abs(goal.X - tile.X), Math.Abs(goal.Y - tile.Y)) == 1;

    private async Task<(List<((int X, int Y) Goal, (int X, int Y) Before, (int X, int Y) After)> Starts, string Log)> FollowStarts(int seed, bool botFirst, int targetX)
    {
        FallbackModel(FollowRow);
        var owner = FallbackActor(1, 2);
        var bot = FallbackBot(0, 2, 99);
        var targets = WiredBotTargets.For(_room);
        targets.Random = new Random(seed);
        targets.BotFirst = () => botFirst;
        targets.Follow(bot, owner);
        ExecutorTick();
        await EdgeMove().Parse(_client, ClientPacket(targetX, 2));
        var log = new List<string>();
        var starts = new List<((int X, int Y) Goal, (int X, int Y) Before, (int X, int Y) After)>();
        var wasMoving = false;
        var before = (owner.X, owner.Y);

        for (var tick = 0; tick < 12; tick++) {
            ExecutorTick();
            var botMoving = bot.Statusses.GetValueOrDefault("mv") is not null;
            var after = owner.Statusses.GetValueOrDefault("mv") is { } mv ? (int.Parse(mv.Split(',')[0]), int.Parse(mv.Split(',')[1])) : (owner.X, owner.Y);
            log.Add($"t{tick} owner {before}->{after} bot({bot.X},{bot.Y}) mv={bot.Statusses.GetValueOrDefault("mv")} goal=({bot.GoalX},{bot.GoalY})");

            if (botMoving && !wasMoving) {
                starts.Add(((bot.GoalX, bot.GoalY), before, after));
            }

            wasMoving = botMoving;
            before = after;
        }

        return (starts, string.Join("\n", log));
    }
}
