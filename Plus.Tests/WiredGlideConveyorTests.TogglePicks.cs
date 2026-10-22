using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint PickTile = 61, PickSay = 62, PickToggle = 63, PickClock = 64, PickCounter = 65, PickLamp = 66, PickSnapshot = 67, PickMissing = 999;

    private static (Fixture Fixture, RoomUser Owner, List<(uint Header, byte[] Payload)> Frames) PickFixture()
    {
        var f = new Fixture(
        [
            (PickTile, 5, 12, 0.0, 0, "wf_colortile", null),
            (PickSay, 4, 9, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "tg" })),
            (PickToggle, 4, 9, 0.65, 0, "wf_act_toggle_state", null),
            (PickCounter, 8, 12, 0.0, 0, "wf_upcounter1", null),
            (PickClock, 6, 9, 0.0, 0, "wf_act_control_clock", null),
            (PickSnapshot, 7, 9, 0.0, 0, "wf_act_match_to_sshot", null)
        ], live: true);
        f.MovementContext().Room.GetWired().AttachRoomItem(f.Items[PickCounter]);
        f.Room.OwnerId = 1;
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";
        f.Items[PickTile].ExtraData = new LegacyDataFormat { Data = "0" };
        var lamp = new Item
        {
            Id = PickLamp,
            RoomId = 14,
            ExtraData = new LegacyDataFormat { Data = "0" },
            WallCoordinates = ":w=0,3 l=8,40 l",
            Definition = new()
            {
                Type = ItemType.Wall,
                Modes = 3,
                ItemName = "cine_light1",
                PublicName = "cine_light1",
                InteractionName = "default",
                InteractionType = InteractionType.None,
                AdjustableHeights = [],
                VendingIds = []
            }
        };
        typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(lamp, f.Room);
        ((ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_wallItems", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(f.Room.GetRoomItemHandler())!)[PickLamp] = lamp;
        var owner = f.Walker(1, 6, 10, 6, 11);

        return (f, owner, CaptureFrames(owner));
    }

    private static uint[] Picks(Fixture f, uint box) => [.. ((IWiredConfiguredItem)f.Box(box)).Configuration.SelectedItems];

    // The client sends an action save as: item, ints, text, picks, delay, furni sources, user sources, variables, secondary picks.
    private static void SavePicks(Fixture f, RoomUser owner, List<(uint Header, byte[] Payload)> frames, uint box, int[] ints, params int[] picks)
    {
        var service = new WiredConfigurationService(new SavedConfigurations([]), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        frames.Clear();
        object[] fields = [(int)box, ints.Length, .. ints.Cast<object>(), "", picks.Length, .. picks.Cast<object>(), 0, 1, 100, 0, 0, 0];
        f.Owned(() => new SaveWiredEffectConfigEvent(service).Parse(owner.GetClient()!, EditorTestSupport.Incoming(fields)).GetAwaiter().GetResult());
    }

    private static string? SaveRefusal(List<(uint Header, byte[] Payload)> frames)
    {
        var refusal = frames.LastOrDefault(frame => frame.Header == ServerPacketHeader.WiredValidationErrorComposer);

        return refusal.Payload == null ? null : System.Text.Encoding.UTF8.GetString(refusal.Payload, 2, System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(refusal.Payload));
    }

    // Observed on AIR WIN63-202610201032-107964540: Toggle State saved with a picked floor tile, the positive id of a wall item and ids
    // of no furni in the room saves; the reopened box holds only the tile, and firing it toggles the tile but not the wall item.
    [Fact]
    public void ToggleStateSaveDropsPicksThatNameNoFurniOfTheirKind()
    {
        var (f, owner, frames) = PickFixture();

        SavePicks(f, owner, frames, PickToggle, [0], (int)PickTile, (int)PickLamp, (int)PickMissing, -(int)PickMissing);

        Assert.Null(SaveRefusal(frames));
        Assert.Contains(frames, frame => frame.Header == ServerPacketHeader.HideWiredConfigComposer);
        Assert.Equal([PickTile], Picks(f, PickToggle).ToArray());
        f.Owned(() => owner.OnChat(0, "tg", false));
        f.Advance(1000);
        Assert.Equal("1", f.Items[PickTile].LegacyDataString);
        Assert.Equal("0", f.Room.GetRoomItemHandler().GetItem(PickLamp)!.LegacyDataString);
    }

    // Observed on AIR WIN63-202610201032-107964540: a wall item picked with its positive id saves with no pick and firing leaves it as it
    // is; picked with its negative id, as the client sends a wall pick, firing toggles it.
    [Fact]
    public void ToggleStateTogglesAWallItemOnlyWhenItIsPickedAsAWallItem()
    {
        var (f, owner, frames) = PickFixture();
        var lamp = f.Room.GetRoomItemHandler().GetItem(PickLamp)!;

        SavePicks(f, owner, frames, PickToggle, [0], (int)PickLamp);
        Assert.Null(SaveRefusal(frames));
        Assert.Empty(Picks(f, PickToggle));
        f.Owned(() => owner.OnChat(0, "tg", false));
        f.Advance(1000);
        Assert.Equal("0", lamp.LegacyDataString);

        SavePicks(f, owner, frames, PickToggle, [0], -(int)PickLamp);
        Assert.Null(SaveRefusal(frames));
        Assert.Equal([PickLamp], Picks(f, PickToggle));
        f.Owned(() => owner.OnChat(0, "tg", false));
        f.Advance(1000);
        Assert.Equal("1", lamp.LegacyDataString);
    }

    // Observed on AIR WIN63-202610201032-107964540: a box that only takes counters still refuses a save where any pick is not a counter,
    // including the positive id of a wall item or of no furni, and keeps its previous picks.
    [Theory]
    [InlineData((int)PickMissing)]
    [InlineData((int)PickLamp)]
    public void CounterBoxStillRefusesAPickThatIsNoCounter(int pick)
    {
        var (f, owner, frames) = PickFixture();
        SavePicks(f, owner, frames, PickClock, [0], (int)PickCounter);
        Assert.Null(SaveRefusal(frames));

        SavePicks(f, owner, frames, PickClock, [0], (int)PickCounter, pick);

        Assert.Equal("wiredfurni.error.require_counter_furni", SaveRefusal(frames));
        Assert.Equal([PickCounter], Picks(f, PickClock).ToArray());
    }

    // A wall item sent with a positive id is dropped from the picks, so the box keeps no snapshot of it either.
    [Fact]
    public void SnapshotBoxKeepsNoSnapshotOfADroppedWallPick()
    {
        var (f, owner, frames) = PickFixture();

        SavePicks(f, owner, frames, PickSnapshot, [0, 0, 1, 1], (int)PickTile, (int)PickLamp);

        Assert.Null(SaveRefusal(frames));
        var saved = ((IWiredConfiguredItem)f.Box(PickSnapshot)).Configuration.Origin!.Native;
        Assert.Equal([PickTile], saved.PrimaryItems.Select(reference => reference.ItemId).ToArray());
        Assert.Equal([PickTile], saved.SavedState.Snapshots.Select(snapshot => snapshot.ItemId).ToArray());
    }
}
