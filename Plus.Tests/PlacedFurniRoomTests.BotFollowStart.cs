using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const string FollowRowBlocked = "0000000000000000\r00000000000xxx00\r0000000000000000\r0000000000000000\r0000000000000000";

    private (RoomUser Bot, RoomUser Owner, WiredBotTargets Targets) FollowTrial(int ownerX, Func<double> roll, int ownerY = 2, string heightmap = FollowRow)
    {
        FallbackModel(heightmap);
        var owner = FallbackActor(ownerX, ownerY);
        var bot = FallbackBot(0, 2, 99);
        var targets = WiredBotTargets.For(_room);
        targets.StartRoll = roll;
        targets.Follow(bot, owner);

        return (bot, owner, targets);
    }

    private static bool BotWalks(RoomUser bot) => bot.Statusses.GetValueOrDefault("mv") is not null;

    // A following bot that could start picks one of the 9 tiles of the 3 x 3 square around the user per room tick.
    // It starts when that tile is free, so the chance is the number of free tiles around the user divided by 9.
    [Theory]
    [InlineData(12, 2, FollowRow, 0.85)]
    [InlineData(15, 2, FollowRow, 0.5)]
    [InlineData(15, 4, FollowRow, 0.3)]
    [InlineData(12, 2, FollowRowBlocked, 0.5)]
    public void ABotFarFromAStandingUserStartsWhenItsRollIsUnderTheShareOfFreeTilesAroundTheUser(int ownerX, int ownerY, string heightmap, double roll)
    {
        var (bot, _, _) = FollowTrial(ownerX, () => roll, ownerY, heightmap);
        ExecutorTick();
        ExecutorTick();

        Assert.True(BotWalks(bot));
    }

    [Theory]
    [InlineData(12, 2, FollowRow, 0.9)]
    [InlineData(15, 2, FollowRow, 0.6)]
    [InlineData(15, 4, FollowRow, 0.35)]
    [InlineData(12, 2, FollowRowBlocked, 0.6)]
    public void ABotFarFromAStandingUserStaysWhereItIsWhileItsRollIsOverTheShareOfFreeTilesAroundTheUser(int ownerX, int ownerY, string heightmap, double roll)
    {
        var (bot, _, _) = FollowTrial(ownerX, () => roll, ownerY, heightmap);

        for (var tick = 0; tick < 8; tick++) {
            ExecutorTick();
            Assert.False(BotWalks(bot), $"the bot started in tick {tick}");
        }
    }

    // The two tiles a walking user is on in that tick, the one it steps off and the one it steps onto, are not free.
    // In open floor that leaves 7 of 9 while the user walks and 8 of 9 once it stands.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ABotFollowingAWalkingUserHasSevenFreeTilesOfNineAndEightOnceTheUserStands(bool botFirst)
    {
        var (bot, owner, targets) = FollowTrial(2, () => 0.8);
        targets.BotFirst = () => botFirst;
        await EdgeMove().Parse(_client, ClientPacket(14, 2));
        var log = new List<string>();
        var started = false;

        for (var tick = 0; tick < 20 && !started; tick++) {
            ExecutorTick();
            var ownerWalks = owner.Statusses.GetValueOrDefault("mv") is not null;
            log.Add($"t{tick} owner ({owner.X},{owner.Y}) walks={ownerWalks} bot ({bot.X},{bot.Y}) walks={BotWalks(bot)}");
            started = BotWalks(bot);
            Assert.False(started && ownerWalks, string.Join("\n", log));
        }

        Assert.True(started, string.Join("\n", log));
    }

    // A room tick gets one roll, however often the bot is looked at in it.
    [Fact]
    public void ABotRollsOnceInARoomTick()
    {
        var rolls = 0;
        var (_, _, targets) = FollowTrial(12, () => { rolls++; return 0.95; });

        targets.Poll(_room, 1000);
        targets.Poll(_room, 1050);
        Assert.Equal(1, rolls);
        ExecutorTick();
        Assert.Equal(2, rolls);
        targets.Poll(_room, 2000);
        Assert.Equal(2, rolls);
    }
}
