using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// A Wired counter's Start, Stop and Reset fire wf_trg_stuff_state once each; an adjust and the per-second ticks never do.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void WiredCounterStartStopAndResetChangeItsStateButAdjustsAndTicksDoNot()
    {
        var counter = Toggle(32, place: false);
        counter.Definition.InteractionName = counter.Definition.ItemName = "wf_upcounter1";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, counter, 3, 3, 0, true, false, false));
        _room.GetWired().AttachRoomItem(counter);
        WatchState(counter);
        var clocks = (WiredCounterController)typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent)
            .GetField("_counters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_room.GetWired())!;

        const long start = 1L << 40;

        void Cycle()
        {
            _room.GetWired().OnFastCycle();
            _room.GetWired().OnCycle();
        }

        Assert.True(clocks.Adjust(counter, 2, 0, 60, WiredClockOrigin.ModernWired));
        Cycle();
        Assert.Equal("30", counter.LegacyDataString);
        Assert.Equal(0, StateLines().Changed);

        Assert.True(clocks.Control(counter, 0, start, WiredClockOrigin.ModernWired));
        Cycle();
        Assert.Equal("30", counter.LegacyDataString);
        Assert.Equal(1, StateLines().Changed);

        for (var now = start + 500; now <= start + 4000; now += 500) {
            clocks.Poll(now);
        }

        Cycle();
        Assert.Equal("34", counter.LegacyDataString);
        Assert.Equal(1, StateLines().Changed);

        Assert.True(clocks.Control(counter, 1, start + 4000, WiredClockOrigin.ModernWired));
        Cycle();
        Assert.Equal(2, StateLines().Changed);

        Assert.True(clocks.Control(counter, 2, start + 4000, WiredClockOrigin.ModernWired));
        Cycle();
        Assert.Equal("0", counter.LegacyDataString);
        Assert.Equal(3, StateLines().Changed);
    }
}
