using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private (RoomUser Bot, RoomUser Owner, WiredBotTargets Targets) FollowTrial(int ownerX, Func<double> roll)
    {
        FallbackModel(FollowRow);
        var owner = FallbackActor(ownerX, 2);
        var bot = FallbackBot(0, 2, 99);
        var targets = WiredBotTargets.For(_room);
        targets.StartRoll = roll;
        targets.Follow(bot, owner);

        return (bot, owner, targets);
    }

    private static bool BotWalks(RoomUser bot) => bot.Statusses.GetValueOrDefault("mv") is not null;

    private static Func<double> Rolls(params double[] values)
    {
        var index = 0;

        return () => values[Math.Min(index++, values.Length - 1)];
    }

    // A following bot far from a user who stands still starts with a chance of 0.38 per room tick.
    [Fact]
    public void ABotFarFromAStandingUserStaysWhereItIsWhileItsRollIsHigh()
    {
        var (bot, _, _) = FollowTrial(12, () => 0.9);

        for (var tick = 0; tick < 8; tick++) {
            ExecutorTick();
            Assert.False(BotWalks(bot), $"the bot started in tick {tick}");
        }
    }

    [Fact]
    public void ABotFarFromAStandingUserStartsWhenItsRollIsLow()
    {
        var (bot, _, _) = FollowTrial(12, () => 0.1);
        ExecutorTick();
        ExecutorTick();

        Assert.True(BotWalks(bot));
    }

    [Fact]
    public void ARollBetweenTheTwoChancesDoesNotStartABotFarFromAStandingUser()
    {
        var (bot, _, _) = FollowTrial(12, () => 0.5);

        for (var tick = 0; tick < 8; tick++) {
            ExecutorTick();
            Assert.False(BotWalks(bot), $"the bot started in tick {tick}");
        }
    }

    // While the user walks the chance is 0.57.
    [Fact]
    public async Task ARollBetweenTheTwoChancesStartsABotFarFromAWalkingUser()
    {
        var (bot, _, _) = FollowTrial(6, () => 0.5);
        await EdgeMove().Parse(_client, ClientPacket(15, 2));
        ExecutorTick();
        ExecutorTick();

        Assert.True(BotWalks(bot));
    }

    // A bot that decides in the tick of the user's last step takes its first step in the tick after it, which still has the walking chance.
    [Fact]
    public async Task TheBotsFirstStepInTheTickAfterTheUsersLastStepHasTheWalkingChance()
    {
        var (bot, _, _) = FollowTrial(6, () => 0.5);
        await EdgeMove().Parse(_client, ClientPacket(7, 2));
        ExecutorTick();
        ExecutorTick();

        Assert.True(BotWalks(bot));
    }

    // The tick after that the user stands.
    [Fact]
    public async Task TheBotsFirstStepTwoTicksAfterTheUsersLastStepHasTheStandingChance()
    {
        var (bot, _, _) = FollowTrial(6, Rolls(0.9, 0.5));
        await EdgeMove().Parse(_client, ClientPacket(7, 2));

        for (var tick = 0; tick < 8; tick++) {
            ExecutorTick();
            Assert.False(BotWalks(bot), $"the bot started in tick {tick}");
        }
    }

    // A room tick gets one roll, however often the bot is looked at in it.
    [Fact]
    public void ABotRollsOnceInARoomTick()
    {
        var rolls = 0;
        var (_, _, targets) = FollowTrial(12, () => { rolls++; return 0.9; });

        targets.Poll(_room, 1000);
        targets.Poll(_room, 1050);
        Assert.Equal(1, rolls);
        ExecutorTick();
        Assert.Equal(2, rolls);
        targets.Poll(_room, 2000);
        Assert.Equal(2, rolls);
    }
}
