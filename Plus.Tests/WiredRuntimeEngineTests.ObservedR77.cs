using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

/// <summary>Observed on hhus build WIN63-202610131318, r77.</summary>
public partial class WiredRuntimeEngineTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PassingConditionsFlashWithTheirTriggerAndFailedOnesStayAtRest(bool passes)
    {
        // Date-range windows: in range, the click trigger, the condition and the message all flip; out of range, only the trigger does.
        var f = new Fixture();
        var trigger = f.Trigger();
        var condition = f.Add(new Box(WiredBoxCategory.Condition) { Body = _ => passes });
        var action = f.Action();

        f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter));

        Assert.Contains(trigger.Item, f.Flashed);
        Assert.Equal(passes, f.Flashed.Contains(condition.Item));
        Assert.Equal(passes ? 1 : 0, action.Calls);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 500)]
    [InlineData(54, 500)]
    [InlineData(499, 500)]
    [InlineData(500, 500)]
    [InlineData(501, 1000)]
    public void AlignedAuxiliaryWorkWaitsForTheNextStateUpdate(int delay, long dueAt)
    {
        var f = new Fixture();
        f.Trigger();
        var calls = 0;
        f.Action(ctx => f.Engine.ScheduleAux(ctx, delay, () => calls++, alignToStateUpdate: true));
        f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter));

        if (dueAt > 0) {
            f.Advance((int)dueAt - 1);
            Assert.Equal(0, calls);
            f.Advance(1);
        }

        Assert.Equal(1, calls);
    }

    [Fact]
    public void AlignedAuxiliaryWorkFollowsTheCurrentCycleNotTheStart()
    {
        var f = new Fixture { Now = 1230 };
        f.Trigger();
        var calls = 0;
        f.Action(ctx => f.Engine.ScheduleAux(ctx, 0, () => calls++, alignToStateUpdate: true));
        f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter));
        f.Advance(269);
        Assert.Equal(0, calls);
        f.Advance(1);
        Assert.Equal(1, calls);
    }
}
