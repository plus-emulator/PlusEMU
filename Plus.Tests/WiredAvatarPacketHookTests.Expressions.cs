using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public partial class WiredAvatarPacketHookTests
{
    private const int WaveExpression = 1;
    private const int KissExpression = 2;
    private const int LaughExpression = 3;
    private const int SleepExpression = 5;
    private const int RespectExpression = 7;
    private const int WaveCondition = 1;
    private const int AwakeCondition = 4;
    private const int SleepCondition = 5;
    private const int RespectCondition = 12;

    // Observed on AIR WIN63-202610201032-107964540: a wave still counts 4.76 s after the expression but not 5.27 s after it.
    // Respect counts 1.56 s after it but not 1.87 s after it.
    [Theory]
    [InlineData(WaveExpression, WaveCondition, 4760, 5270)]
    [InlineData(RespectExpression, RespectCondition, 1560, 1870)]
    public async Task PerformsActionConditionRemembersWaveAndRespectForTheirOwnWindow(int expression, int condition, int passesAt, int failsAt)
    {
        var clock = new ManualClock(new DateTimeOffset(2040, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var world = new World(condition, -1, clock);
        var speech = new WiredRuntimeEvent(WiredEventKind.Speech) { Actor = world.Actor };
        var start = clock.Now;
        bool PerformsAt(int milliseconds)
        {
            clock.Now = start.AddMilliseconds(milliseconds);
            world.Users.UpdateSignStatus(world.Actor);

            return WiredModernCondition.IsPerforming(world.Actor, new() { IntParams = [condition, 0, 0, 0, 1] }, speech);
        }

        Assert.False(PerformsAt(0));

        await new ActionEvent(world.Actions).Parse(world.Room, world.Client, Packet(expression));

        Assert.True(PerformsAt(450));
        Assert.True(PerformsAt(passesAt));
        Assert.False(PerformsAt(failsAt));
    }

    // Observed on AIR WIN63-202610201032-107964540: without Habbo Club, these expressions reach nobody, the sender included,
    // fire no Wired and leave a dancing, sleeping avatar as it was.
    [Theory]
    [InlineData(KissExpression, 2)]
    [InlineData(LaughExpression, 3)]
    [InlineData(4, WaveCondition)]
    [InlineData(6, WaveCondition)]
    [InlineData(67, WaveCondition)]
    public async Task NonClubExpressionsOutsideTheBasicSetAreIgnored(int expression, int editorAction)
    {
        var world = new World(editorAction);
        world.Actor.DanceId = 1;
        world.Actor.IsAsleep = true;

        await new ActionEvent(world.Actions).Parse(world.Room, world.Client, Packet(expression));
        world.FlushWired();

        Assert.Empty(world.SentPackets);
        Assert.Empty(world.Capture.Events);
        Assert.Equal(1, world.Actor.DanceId);
        Assert.True(world.Actor.IsAsleep);
    }

    // Observed on AIR WIN63-202610201032-107964540: without Habbo Club, wave, sleep, respect and expression 0 still reach the room.
    [Theory]
    [InlineData(0, WaveCondition)]
    [InlineData(WaveExpression, WaveCondition)]
    [InlineData(SleepExpression, SleepCondition)]
    [InlineData(RespectExpression, RespectCondition)]
    public async Task NonClubBasicExpressionsStillReachTheRoom(int expression, int editorAction)
    {
        var world = new World(editorAction);

        await new ActionEvent(world.Actions).Parse(world.Room, world.Client, Packet(expression));
        world.FlushWired();

        Assert.Contains(world.SentPackets, packet => Header(packet) == 1 && packet[^4..].SequenceEqual(IntBytes(expression)));
        Assert.Equal(expression == 0 ? 0 : 1, world.Capture.Events.Count);
    }

    [Theory]
    [InlineData(KissExpression, 2)]
    [InlineData(LaughExpression, 3)]
    public async Task ClubExpressionsStillReachTheRoom(int expression, int editorAction)
    {
        var world = new World(editorAction);
        GrantClub(world);

        await new ActionEvent(world.Actions).Parse(world.Room, world.Client, Packet(expression));
        world.FlushWired();

        Assert.Contains(world.SentPackets, packet => Header(packet) == 1 && packet[^4..].SequenceEqual(IntBytes(expression)));
        Assert.Single(world.Capture.Events);
    }

    // Observed on AIR WIN63-202610201032-107964540: without Habbo Club, dance 2, 3 and 4 send nothing and the avatar keeps dance 1.
    // Dance 0 still stops it.
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task NonClubDanceRequestsForClubDancesAreIgnored(int dance)
    {
        var world = new World(10, 1);
        var handler = new DanceEvent(world.Actions);
        await handler.Parse(world.Room, world.Client, Packet(1));
        world.FlushWired();
        world.SentPackets.Clear();

        await handler.Parse(world.Room, world.Client, Packet(dance));
        world.FlushWired();

        Assert.Empty(world.SentPackets);
        Assert.Equal(1, world.Actor.DanceId);
        Assert.Single(world.Capture.Events);

        await handler.Parse(world.Room, world.Client, Packet(0));

        var stop = Assert.Single(world.SentPackets);
        Assert.Equal(2, Header(stop));
        Assert.Equal(IntBytes(0), stop[^4..]);
        Assert.Equal(0, world.Actor.DanceId);
    }

    private static void GrantClub(World world) => world.Client.GetHabbo().Access =
        Plus.HabboHotel.Permissions.UserAccess.Create([], [new(Plus.HabboHotel.Permissions.PermissionKeys.ClubAccess, false)]);

    private static byte[] IntBytes(int value) => [(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value];

    [Theory]
    [InlineData(KissExpression, 2)]
    [InlineData(LaughExpression, 3)]
    public void PerformsActionConditionDoesNotRememberKissAndLaugh(int expression, int condition)
    {
        var clock = new ManualClock(new DateTimeOffset(2040, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var world = new World(condition, -1, clock);
        var speech = new WiredRuntimeEvent(WiredEventKind.Speech) { Actor = world.Actor };

        world.Actions.PerformAction(world.Room, world.Client, expression);
        clock.Now = clock.Now.AddMilliseconds(450);
        world.Users.UpdateSignStatus(world.Actor);

        Assert.False(WiredModernCondition.IsPerforming(world.Actor, new() { IntParams = [condition, 0, 0, 0, 1] }, speech));
    }

    // Observed on AIR WIN63-202610151745-318700621: Sleep passes 1.78 s after the sleep expression and Awake passes from 2.22 s on,
    // while the avatar still shows as asleep until its next action.
    [Fact]
    public void PerformsActionConditionSeesTheSleepExpressionForAShortTimeOnly()
    {
        var clock = new ManualClock(new DateTimeOffset(2040, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var world = new World(SleepCondition, -1, clock);
        var speech = new WiredRuntimeEvent(WiredEventKind.Speech) { Actor = world.Actor };
        bool Performs(int condition) => WiredModernCondition.IsPerforming(world.Actor, new() { IntParams = [condition, 0, 0, 0, 1] }, speech);

        world.Actions.PerformAction(world.Room, world.Client, SleepExpression);
        clock.Now = clock.Now.AddMilliseconds(1780);
        world.Users.UpdateSignStatus(world.Actor);
        Assert.True(Performs(SleepCondition));
        Assert.False(Performs(AwakeCondition));

        clock.Now = clock.Now.AddMilliseconds(440);
        world.Users.UpdateSignStatus(world.Actor);
        Assert.True(world.Actor.IsAsleep);
        Assert.False(Performs(SleepCondition));
        Assert.True(Performs(AwakeCondition));
    }
}
