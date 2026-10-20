using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public partial class WiredRuntimeEngineTests
{
    private static Item Disabler(Fixture f, bool on)
    {
        var item = f.Furni();
        item.Definition.InteractionType = InteractionType.WiredDisabler;
        item.ExtraData = new LegacyDataFormat { Data = on ? "1" : "0" };

        return item;
    }

    // Observed on AIR WIN63-202610201032-107964540: with the wired disabler on, no wired runs: a stack does not fire.
    [Fact]
    public void AWiredStackDoesNotFireWhileTheWiredDisablerIsOn()
    {
        var f = new Fixture();
        Disabler(f, on: true);
        f.Trigger();
        var action = f.Action();

        Assert.False(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));
        Assert.False(f.Engine.Enqueue(new WiredRuntimeEvent(WiredEventKind.Enter)));
        f.Resume();

        Assert.Equal(0, action.Calls);
    }

    // Observed on AIR WIN63-202610201032-107964540: a repeater stops with the disabler and ticks again once the disabler is off.
    [Fact]
    public void ARepeaterStopsWhileTheWiredDisablerIsOnAndResumesWhenItIsOff()
    {
        var f = new Fixture();
        var disabler = Disabler(f, on: false);
        f.Add(new Timer());
        var action = f.Action();
        f.Engine.OnFastCycle();
        f.Advance(49);
        f.Engine.OnCycle();
        Assert.True(action.Calls > 0);
        var before = action.Calls;

        disabler.LegacyDataString = "1";
        f.Advance(50);
        f.Engine.OnCycle();
        f.Engine.OnRoomTick();
        f.Engine.OnFastCycle();
        Assert.Equal(before, action.Calls);

        disabler.LegacyDataString = "0";
        f.Engine.OnFastCycle();
        f.Advance(49);
        f.Engine.OnCycle();
        Assert.True(action.Calls > before);
    }

    // Observed on AIR WIN63-202610201032-107964540: wired boxes can still be placed and saved while the disabler is on.
    [Fact]
    public void WiredBoxesCanStillBeEditedWhileTheWiredDisablerIsOn()
    {
        var f = new Fixture();
        Disabler(f, on: true);

        var trigger = f.Trigger();

        Assert.True(f.Engine.TryGet(trigger.Item.Id, out _));
        Assert.True(f.Engine.Mutate(() => true));
    }

    // Observed on AIR WIN63-202610201032-107964540: a wired toggle cannot switch the disabler back off.
    [Fact]
    public void AWiredToggleDoesNotSwitchTheWiredDisablerBackOff()
    {
        var f = new Fixture();
        var disabler = Disabler(f, on: true);
        var interactor = new Plus.HabboHotel.Items.Interactor.InteractorConfigurationSwitch();

        interactor.OnWiredTrigger(disabler);

        Assert.Equal("1", disabler.LegacyDataString);
    }

    [Fact]
    public void AWiredToggleSwitchesTheWiredDisablerOn()
    {
        var f = new Fixture();
        var disabler = Disabler(f, on: false);

        new Plus.HabboHotel.Items.Interactor.InteractorConfigurationSwitch().OnWiredTrigger(disabler);

        Assert.Equal("1", disabler.LegacyDataString);
    }
}
