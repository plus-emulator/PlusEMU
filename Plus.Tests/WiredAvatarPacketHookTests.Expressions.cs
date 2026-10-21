using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public partial class WiredAvatarPacketHookTests
{
    private const int WaveExpression = 1;
    private const int KissExpression = 2;
    private const int LaughExpression = 3;
    private const int RespectExpression = 7;
    private const int WaveCondition = 1;
    private const int RespectCondition = 12;

    // Observed on AIR WIN63-202610201032-107964540: the condition "user performs action" passes for Respect and Wave for a short
    // while after the expression, long after it started. A kiss and a laugh do not pass.
    [Theory]
    [InlineData(WaveExpression, WaveCondition)]
    [InlineData(RespectExpression, RespectCondition)]
    public void PerformsActionConditionRemembersWaveAndRespectForAShortTime(int expression, int condition)
    {
        var clock = new ManualClock(new DateTimeOffset(2040, 1, 2, 3, 4, 5, TimeSpan.Zero));
        var world = new World(condition, -1, clock);
        var speech = new WiredRuntimeEvent(WiredEventKind.Speech) { Actor = world.Actor };
        bool Performs() => WiredModernCondition.IsPerforming(world.Actor, new() { IntParams = [condition, 0, 0, 0, 1] }, speech);

        Assert.False(Performs());

        world.Actions.PerformAction(world.Room, world.Client, expression);
        clock.Now = clock.Now.AddMilliseconds(450);
        world.Users.UpdateSignStatus(world.Actor);
        Assert.True(Performs());

        clock.Now = clock.Now.AddMilliseconds(2550);
        world.Users.UpdateSignStatus(world.Actor);
        Assert.False(Performs());
    }

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
}
