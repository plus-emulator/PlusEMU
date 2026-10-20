using System.Drawing;
using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>Room Initialized runs at the end of the real room load, once every component has loaded its part of the room.</summary>
public partial class ModernWiredRuntimeTests
{
    // Observed on AIR WIN63-202610201032-107964540: the Room Initialized stacks run during the load, and the room's bots are already
    // there, so a bot action in such a stack finds its bot.
    [Fact]
    public void RoomInitializedStackFindsABotThatLoadsWithTheRoom()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_teleport", intParams: [100, 100], text: "Bobby");
        Assert.True(f.Engine.Remove(101));
        OctoberBox<WiredModernTrigger>(f, 120, "wf_trg_room_initializes", 0, 0, native => native);
        MakeTargetWalkable(f);
        RoomUser? bot = null;

        InitiateRoom(f, new BotComponent(() => bot = AddNamedBot(f, 9, "Bobby", 2, 2)));
        f.Advance(TeleportFixture.TeleportLandsAfterMilliseconds + 50);

        Assert.NotNull(bot);
        Assert.Equal(new Point(1, 1), bot.Coordinate);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void AFailingRoomInitializedDispatchDoesNotStopTheRoomFromLoading()
    {
        using var f = new TeleportFixture();
        typeof(Room).GetField("_wiredLogger", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Room, TestLogging.Logger);
        var loaded = false;

        // Once the furniture is loaded the wired engine is gone, so the Room Initialized dispatch throws.
        InitiateRoom(f, new BotComponent(() => loaded = true), () =>
            typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Room.GetWired(), null));

        Assert.True(loaded);
    }

    private static void InitiateRoom(TeleportFixture f, IRoomComponent bots, Action? breakWired = null)
    {
        // The load puts the fixture's furniture back into an empty room, as a real load does.
        var loader = new FixtureFurniture(breakWired == null ? f.Items.Values.ToArray() : [], breakWired);
        f.Items.Clear();
        typeof(Room).GetField("_components", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(f.Room, new IRoomComponent[] { new RoomDataComponent(loader), bots });
        f.Room.Initiate();
    }

    private sealed class FixtureFurniture(IReadOnlyList<Item> items, Action? onLoad) : IRoomFurnitureLoader
    {
        public IReadOnlyList<Item> Load(uint roomId)
        {
            onLoad?.Invoke();

            return items;
        }
    }

    // Stands for the bot component, which loads after the room data (order 300).
    private sealed class BotComponent(Action load) : IRoomComponent
    {
        public int Order => 300;
        public void Initiate(Room room) { }
        public void Initiated() => load();
    }
}
