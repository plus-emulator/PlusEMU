using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rewards;
using Plus.HabboHotel.Rooms;
using Plus.Core.Settings;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A visitor with rights can move, rotate and use a Place Furni copy but never pick it up. A visitor without rights can do none of these.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TemporaryCopyMovesRotatesAndSwitchesOnlyForAVisitorWithRightsAndIsNeverPickedUpByAVisitor(bool rights)
    {
        var item = TemporaryCopy();
        Visitor(rights);
        var placement = new RoomItemPlacementService(Proxy<ISettingsManager>((_, _) => "500"),
            Proxy<IAchievementManager>((_, _) => null), Proxy<IRewardTrackManager>((_, _) => null), Proxy<IQuestManager>((_, _) => null),
            TestLogging.For<RoomItemPlacementService>());

        placement.Move(_room, _client, item.Id, 2, 1, 0);
        Assert.Equal(rights ? (2, 1) : (1, 1), (item.GetX, item.GetY));
        placement.Move(_room, _client, item.Id, item.GetX, item.GetY, 2);
        Assert.Equal(rights ? 2 : 0, item.Rotation);

        new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null))
            .Use(_room, _client, new(item.Id, 0));
        Assert.Equal(rights ? "1" : "0", item.LegacyDataString);

        Assert.False(new RoomItemPickupService(TestGameClientManager.Empty, Proxy<IQuestManager>((_, _) => null),
            new RoomItemPickupStore(_database)).TryPickUp(_client, item.Id));
        Assert.Same(item, _room.GetRoomItemHandler().GetItem(item.Id));
        Assert.True(_room.GetRoomItemHandler().OwnsTemporary(item));
    }

    // The box owner picks up a Place Furni copy. The copy is removed, and nothing reaches the inventory or the database.
    [Fact]
    public void BoxOwnerPicksUpTemporaryCopyWithoutInventoryOrStorage()
    {
        var item = TemporaryCopy();
        var store = new CountingPickupStore();
        _client.GetHabbo().Id = 7;

        Assert.True(new RoomItemPickupService(TestGameClientManager.Empty, Proxy<IQuestManager>((_, _) => null), store)
            .TryPickUp(_client, item.Id));
        Assert.Null(_room.GetRoomItemHandler().GetItem(item.Id));
        Assert.False(_room.GetRoomItemHandler().OwnsTemporary(item));
        Assert.Equal(0, store.Calls);
    }

    // @level is the profile's account level, the same level User Level compares.
    [Fact]
    public void LevelVariableReadsTheUserLevel()
    {
        var avatar = Viewer();
        var habbo = _client.GetHabbo();
        habbo.HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0);
        var holder = WiredVariableRuntimeFrames.UserHolder(avatar);
        var frame = new WiredVariableFrame(RoomId, [holder]);
        var builtins = new RoomWiredBuiltinVariables(_room);
        WiredVariableValue? Read() => builtins.Read(new(holder.Target, "internal:@level"), holder, frame);
        Assert.True(RoomWiredBuiltinVariables.HasNumericValue(new(holder.Target, "internal:@level")));
        Assert.Equal(1, Read()?.Value);
        habbo.HabboStats.AchievementPoints = 5454;
        Assert.Equal(Plus.HabboHotel.Items.Wired.Modern.Conditions.WiredUserLevel.Of(avatar), Read()?.Value);
        Assert.NotEqual(5454, Read()?.Value);
    }

    // A visitor without rights sees the wired boxes unless the room hides them.
    [Fact]
    public void VisitorWithoutRightsReceivesWiredBoxesInTheObjectList()
    {
        var wired = Furni(52, InteractionType.WiredTrigger, WiredBoxType.TriggerWalkOnFurni);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, wired, 1, 2, 0, true, false, false));
        Visitor(false);
        Assert.False(_room.CheckRights(_client));
        Assert.Contains(wired, _room.VisibleFloorItems);
        _room.HideWired = true;
        Assert.DoesNotContain(wired, _room.VisibleFloorItems);
    }

    private Item TemporaryCopy()
    {
        var definition = Furni(0, InteractionType.None, WiredBoxType.None).Definition;
        definition.Modes = 2;
        definition.Length = 1;
        definition.Width = 1;
        var item = _room.GetRoomItemHandler().PlaceTemporaryFloorItem(definition, 7, 1, 1, 0, null, "0")!;
        Assert.NotNull(item);

        return item;
    }

    private sealed class CountingPickupStore : IRoomItemPickupStore
    {
        public int Calls;

        public bool PickUp(RoomItemPickup request)
        {
            Calls++;

            return true;
        }
    }

    private void Visitor(bool rights)
    {
        var habbo = _client.GetHabbo();
        habbo.Id = 9;
        habbo.Username = "visitor";
        _room.UsersWithRights = rights ? [9] : [];
    }
}
