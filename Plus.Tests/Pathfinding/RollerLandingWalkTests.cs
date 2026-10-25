using System.Collections.Concurrent;
using Plus.HabboHotel.GameClients;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void RollerSlideFiresWalksOnOneTickAfterTheSlide()
    {
        ExecutorRoller(10, 0, 1);
        ExecutorFloor(11, 1, 1);
        ExecutorRollerActor(0, 1, 0.5);
        var tick = 0;
        var events = RollerWalkEvents(() => tick);
        EnableExecutorRollers();
        var slides = RollerRunTicks(ref tick, 4);

        Assert.Equal(new[] { 1 }, slides);
        Assert.Equal(new[] { (2, WiredBoxType.TriggerWalkOnFurni, 11u) },
            events.Where(e => e.Kind == WiredBoxType.TriggerWalkOnFurni && e.Item == 11).ToArray());
    }

    [Fact]
    public void ChainedRollersFireWalksOnOncePerLandingAndWalksOffOncePerSlide()
    {
        ExecutorRoller(10, 0, 1);
        ExecutorRoller(11, 1, 1);
        ExecutorFloor(12, 2, 1);
        ExecutorRollerActor(0, 1, 0.5);
        var tick = 0;
        var events = RollerWalkEvents(() => tick);
        _room.GetRoomItemHandler().SetSpeed(3);
        _room.GetRoomItemHandler().GotRollers = true;
        var slides = RollerRunTicks(ref tick, 12);

        Assert.Equal(2, slides.Count);
        var first = slides[0];
        var second = slides[1];
        Assert.True(second > first + 1);
        Assert.Equal(new[] { (first, WiredBoxType.TriggerWalkOffFurni, 10u), (second, WiredBoxType.TriggerWalkOffFurni, 11u) },
            events.Where(e => e.Kind == WiredBoxType.TriggerWalkOffFurni).ToArray());
        Assert.Equal(new[] { (first + 1, WiredBoxType.TriggerWalkOnFurni, 11u), (second + 1, WiredBoxType.TriggerWalkOnFurni, 12u) },
            events.Where(e => e.Kind == WiredBoxType.TriggerWalkOnFurni).ToArray());
    }

    [Fact]
    public void RollerLandingFiresWalksOnBeforeAWalkOrderedInTheSlideTickBegins()
    {
        ExecutorRoller(10, 0, 1);
        ExecutorFloor(11, 1, 1);
        var actor = ExecutorRollerActor(0, 1, 0.5);
        var tick = 0;
        var started = false;
        var events = RollerWalkEvents(() => tick, (kind, item) =>
        {
            if (kind == WiredBoxType.TriggerWalkOffFurni && item == 10 && !started) {
                started = true;
                actor.MoveTo(2, 1);
            }
        });
        EnableExecutorRollers();
        RollerRunTicks(ref tick, 4);

        Assert.True(started);
        Assert.Equal(new[] { (2, WiredBoxType.TriggerWalkOnFurni), (2, WiredBoxType.TriggerWalkOffFurni) },
            events.Where(e => e.Item == 11).Select(e => (e.Tick, e.Kind)).ToArray());
    }

    [Fact]
    public void RollerLandingSkipsWalksOnWhenTheUserWasMovedDuringTheWait()
    {
        ExecutorRoller(10, 0, 1);
        ExecutorFloor(11, 1, 1);
        var actor = ExecutorRollerActor(0, 1, 0.5);
        var tick = 0;
        var events = RollerWalkEvents(() => tick);
        EnableExecutorRollers();
        ExecutorTick();
        _room.GetGameMap().Navigation!.ForcePlace(actor, 1, 1, 0, ForceResolution.ExactZ);
        tick = 2;
        ExecutorTick();
        ExecutorTick();

        Assert.DoesNotContain(events, e => e.Kind == WiredBoxType.TriggerWalkOnFurni);
    }

    [Fact]
    public void RollerFollowUpUserUpdatesStayAtOneAndTwoTicksAfterTheSlide()
    {
        ExecutorRoller(10, 0, 1);
        ExecutorFloor(11, 1, 1);
        var actor = ExecutorRollerActor(0, 1, 0.5);
        EnableExecutorRollers();
        var updates = new List<bool>();

        for (var i = 0; i < 4; i++) {
            ExecutorTick();
            updates.Add(ExecutorHasUpdate(actor));
        }

        Assert.Equal(new[] { false, true, true, false }, updates.ToArray());
    }

    [Fact]
    public void RollerWithABlockedTargetDoesNotSlideOrFireWalksOn()
    {
        ExecutorRoller(10, 0, 1);
        ExecutorFloor(11, 1, 1);
        var actor = ExecutorRollerActor(0, 1, 0.5);
        _room.GetGameMap().SetFloorStatus(1, 1, 0);
        var tick = 0;
        var events = RollerWalkEvents(() => tick);
        EnableExecutorRollers();
        var slides = RollerRunTicks(ref tick, 4);

        Assert.Empty(slides);
        Assert.Empty(events);
        Assert.Equal((0, 1), (actor.X, actor.Y));
    }

    private List<int> RollerRunTicks(ref int tick, int count)
    {
        var slides = new List<int>();

        for (tick = 1; tick <= count; tick++) {
            ExecutorTick();

            if (_client.Packets.Any(p => p.Header == ServerPacketHeader.SlideObjectBundleComposer)) {
                slides.Add(tick);
            }
        }

        return slides;
    }

    private List<(int Tick, WiredBoxType Kind, uint Item)> RollerWalkEvents(Func<int> tick, Action<WiredBoxType, uint>? hook = null)
    {
        var events = new List<(int, WiredBoxType, uint)>();
        uint id = 900;

        foreach (var kind in new[] { WiredBoxType.TriggerWalkOnFurni, WiredBoxType.TriggerWalkOffFurni }) {
            var item = Furni(id, InteractionType.WiredTrigger, kind);
            item.Definition.Height = 0;
            item.Definition.Width = item.Definition.Length = 1;
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, 3 - (int)(id++ - 900), 3, 0, true, false, false));
            Assert.True(_room.GetWired().AddBox(new RollerWalkObserver(_room, item, kind, landed =>
            {
                events.Add((tick(), kind, landed));
                hook?.Invoke(kind, landed);
            })));
        }

        return events;
    }

    private sealed class RollerWalkObserver(Room room, Item item, WiredBoxType kind, Action<uint> observe) : IWiredItem
    {
        public Room Instance { get; set; } = room;
        public Item Item { get; set; } = item;
        public WiredBoxType Type => kind;
        public ConcurrentDictionary<uint, Item> SetItems { get; set; } = new();
        public string StringData { get; set; } = "";
        public bool BoolData { get; set; }
        public string ItemsData { get; set; } = "";
        public void HandleSave(IIncomingPacket packet) => throw new NotSupportedException();
        public bool Execute(params object[] arguments)
        {
            observe(((Item)arguments[1]).Id);

            return false;
        }
    }
}
