using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Interactor;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// A hopper or teleporter skips the door checks only for the room it sends the user to, and only for that entry.
public partial class PlacedFurniRoomTests
{
    private const uint HopperId = 500;

    [Fact]
    public void TheHoppingFlagDoesNotOpenALockedRoom()
    {
        var habbo = LockedRoomVisitor();
        habbo.IsHopping = true;
        habbo.HopperId = HopperId;

        habbo.PrepareRoom(RoomId, "");

        AssertRefusedAtTheDoor(habbo);
    }

    [Fact]
    public void AFailedHopLeavesTheNextEntryToTheDoorChecks()
    {
        var habbo = LockedRoomVisitor();
        Hop(RoomId + 1);
        Assert.Null(habbo.CurrentRoom);

        _client.Sent.Clear();
        habbo.PrepareRoom(RoomId, "");
        AssertRefusedAtTheDoor(habbo);

        habbo.PrepareRoom(RoomId, "secret");
        Assert.Same(_room, habbo.CurrentRoom);
        var avatar = Assert.IsType<RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id));
        Assert.Equal((0, 0), (avatar.X, avatar.Y));
    }

    [Fact]
    public void AHopIntoItsLockedDestinationSkipsTheDoor()
    {
        var habbo = LockedRoomVisitor();

        var hopper = Hop(RoomId);

        Assert.Same(_room, habbo.CurrentRoom);
        Assert.DoesNotContain(ServerPacketHeader.GenericErrorComposer, _client.Sent);
        var avatar = Assert.IsType<RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id));
        Assert.Equal((hopper.GetX, hopper.GetY), (avatar.X, avatar.Y));
    }

    [Fact]
    public void LeavingAfterAHopEndsTheWayPastTheDoor()
    {
        var habbo = LockedRoomVisitor();
        Hop(RoomId);
        Assert.Same(_room, habbo.CurrentRoom);
        _room.GetRoomUserManager().RemoveUserFromRoom(_client, true);

        _client.Sent.Clear();
        habbo.PrepareRoom(RoomId, "");

        AssertRefusedAtTheDoor(habbo);
    }

    [Fact]
    public void AFailedTeleportLeavesTheNextEntryToTheDoorChecks()
    {
        var habbo = LockedRoomVisitor();
        habbo.IsTeleporting = true;
        habbo.TeleportingRoomId = RoomId;
        _room.UsersMax = 0;
        habbo.PrepareRoom(RoomId, "");
        Assert.Null(habbo.CurrentRoom);
        _room.UsersMax = 10;

        _client.Sent.Clear();
        habbo.PrepareRoom(RoomId, "");

        AssertRefusedAtTheDoor(habbo);
    }

    [Fact]
    public void ATeleportIntoItsLockedDestinationSkipsTheDoor()
    {
        var habbo = LockedRoomVisitor();
        habbo.IsTeleporting = true;
        habbo.TeleportingRoomId = RoomId;

        habbo.PrepareRoom(RoomId, "");

        Assert.Same(_room, habbo.CurrentRoom);
        Assert.DoesNotContain(ServerPacketHeader.GenericErrorComposer, _client.Sent);
    }

    [Fact]
    public void AHopOpensTheDoorForOneEntryOnly()
    {
        var habbo = LockedRoomVisitor();
        Hop(RoomId);
        Assert.Same(_room, habbo.CurrentRoom);

        _client.Sent.Clear();
        habbo.PrepareRoom(RoomId, "");

        AssertRefusedAtTheDoor(habbo);
    }

    [Fact]
    public void ATeleportArrivalWithoutAStepDoesNotHideALaterHop()
    {
        var habbo = LockedRoomVisitor();
        habbo.IsTeleporting = true;
        habbo.TeleportingRoomId = RoomId;
        habbo.PrepareRoom(RoomId, "");
        Assert.True(habbo.IsTeleporting);
        _room.GetRoomUserManager().RemoveUserFromRoom(_client, false);

        Hop(RoomId);

        Assert.DoesNotContain(ServerPacketHeader.GenericErrorComposer, _client.Sent);
        Assert.Contains(ServerPacketHeader.RoomReadyComposer, _client.Sent);
        Assert.Same(_room, habbo.CurrentRoom);
        Assert.NotNull(_room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id));
    }

    private Habbo LockedRoomVisitor()
    {
        _room.OwnerId = 99;
        _room.Access = RoomAccess.Password;
        _room.Password = "secret";
        _room.UsersMax = 10;
        _room.SetBans(new BansComponent(_room, new RecordingModerationBans(), TimeProvider.System, []));
        var rooms = Proxy<IRoomManager>((method, args) =>
        {
            Assert.Equal(nameof(IRoomManager.TryLoadRoom), method);

            if ((uint)args[0]! != RoomId) {
                return false;
            }

            args[1] = _room;

            return true;
        });
        var habbo = Entrant(Entry());
        habbo.SetHotelServices(TestGameClientManager.Empty, rooms, roomEntry: Entry());
        habbo.SetRoomVisitRecorder(Proxy<IRoomVisitRecorder>((_, _) => null), TestItemRuntime.Achievements);

        return habbo;
    }

    // The user stands in a hopper of this room that leads to the given room.
    private Item Hop(uint destination)
    {
        var travel = new HopTravel(destination);
        var interactors = new ItemInteractorFactory(travel, TestItemRuntime.Profiles, TestItemRuntime.Quests,
            TestItemRuntime.Rewards, TestItemRuntime.Achievements, TestItemRuntime.Redemptions);
        Set("_roomItemHandling", new RoomItemHandling(_room, TestRoomItemStore.Instance, TestRoomItemMetadataStore.Instance,
            TestGameClientManager.Empty, TestLanguageManager.RoomItems, interactors, travel, TestItemRuntime.Rewards));
        var hopper = Furni(HopperId, InteractionType.Hopper, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, hopper, 1, 1, 0, true, false, false));
        var habbo = _client.GetHabbo();
        habbo.CurrentRoom = _room;
        var actor = ExecutorActor(1, 1);
        actor.TeleDelay = 0;
        hopper.InteractingUser = habbo.Id;
        _client.Sent.Clear();

        hopper.ProcessUpdates();

        return hopper;
    }

    private void AssertRefusedAtTheDoor(Habbo habbo)
    {
        Assert.Contains(ServerPacketHeader.GenericErrorComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.RoomReadyComposer, _client.Sent);
        Assert.Null(habbo.CurrentRoom);
        Assert.Null(_room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id));
    }

    private sealed class HopTravel(uint destination) : IItemTravelStore
    {
        public uint FindOtherHopperRoom(uint roomId) => destination;
        public uint FindHopper(uint roomId) => roomId == RoomId ? HopperId : 0;
        public uint FindLinkedTeleporter(uint itemId) => 0;
        public uint FindItemRoom(uint itemId) => 0;
        public void RegisterHopper(uint itemId, uint roomId) { }
        public void RemoveHopper(uint itemId, uint roomId) { }
    }
}
