using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: At Given Time with 0 pulses fires on the first room tick after a timer reset, 428 ms
// after the reset in the capture, and the box with 1 pulse one pulse later. It did not fire when it was saved.
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void AtGivenTimeWithZeroPulsesFiresOnTheFirstRoomTickAfterEachTimerReset()
    {
        var (room, _, items) = World();
        long now = 0;
        var engine = new WiredStackEngine(() => now, box => items.TryGetValue(box.Item.Id, out var item) && ReferenceEquals(item, box.Item), _ => true, _ => { }, _ => { });
        var zero = Timer(room, engine, items, 1, 0);
        var one = Timer(room, engine, items, 2, 1);

        foreach (var t in new long[] { 0, 200, 500, 1000, 1400 }) {
            Assert.Null(zero.Poll(t));
        }

        now = 1198;
        engine.ResetTimers(items.Values.ToArray());
        Assert.Null(zero.Poll(1499));
        Assert.NotNull(zero.Poll(1500));
        Assert.Null(zero.Poll(1550));
        Assert.Null(one.Poll(1999));
        Assert.NotNull(one.Poll(2000));

        now = 7231;
        engine.ResetTimers(items.Values.ToArray());
        Assert.Null(zero.Poll(7499));
        Assert.NotNull(zero.Poll(7500));
    }

    private static WiredModernTimedTrigger Timer(Plus.HabboHotel.Rooms.Room room, WiredStackEngine engine,
        System.Collections.Concurrent.ConcurrentDictionary<uint, Plus.HabboHotel.Items.Item> items, uint id, int units)
    {
        var item = MakeItem(id, "wf_trg_at_given_time");
        item.SetState((int)id, 0, 0);
        items[item.Id] = item;
        var box = new WiredModernTimedTrigger(room, item, Descriptor("wf_trg_at_given_time"));
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(box, new() { IntParams = [units] }, out var config, out _));
        box.ApplyConfiguration(config);
        Assert.True(engine.Add(box));

        return box;
    }
}
