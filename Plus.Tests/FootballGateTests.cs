using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Plus.Communication.Packets.Incoming.Rooms.Furni;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.FootballGate;
using Plus.HabboHotel.Items.Interactor;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// AIR WIN63-202610151745-318700621: outgoing 2545 is id, gender, figure (package_66/class_3476.as:15-30); the gate's stuff data is
// "<male look>,<female look>" (FurnitureClothingChangeLogic.as:51-66).
public sealed class FootballGateTests
{
    private const string MaleKit = "hd-99999-99999.ch-3109-92-1408.lg-3116-82-1408.sh-3115-1408-1408";
    private const string FemaleKit = "hd-99999-99999.ch-3112-1408-1408.lg-3116-71-1408.sh-3115-1408-1408";

    [Fact]
    public async Task SavePacketIsReadAsItemIdGenderThenFigure()
    {
        var (client, _) = HabbiconTestSupport.Client(new Habbo { Id = 1 });
        var gates = new RecordingGates();
        var packet = HabbiconTestSupport.Incoming(7, "F", FemaleKit);

        await new SetClothingChangeDataEvent(gates).Parse(client, packet);

        Assert.Equal(new FootballGateLookRequest(7, "F", FemaleKit), gates.Request);
        Assert.False(packet.HasDataRemaining());
    }

    [Fact]
    public void EachGenderIsSavedInItsOwnFieldAndKeepsTheOther()
    {
        var (room, client, _) = Context("M", "hr-100-61.hd-180-1.ch-210-66.lg-270-82.sh-290-80");
        var gate = AddGate(room, "");
        var store = new RecordingStore();
        var service = new FootballGateService(store);

        service.SaveLook(client, new(gate.Id, "M", MaleKit));
        Assert.Equal($"{MaleKit},", gate.LegacyDataString);

        service.SaveLook(client, new(gate.Id, "f", FemaleKit));
        Assert.Equal($"{MaleKit},{FemaleKit}", gate.LegacyDataString);
        Assert.Equal($"{MaleKit},{FemaleKit}", store.Data);

        service.SaveLook(client, new(gate.Id, "M", "ch-215-66"));
        Assert.Equal($"ch-215-66,{FemaleKit}", gate.LegacyDataString);
        Assert.Equal(3, store.Writes);
    }

    [Fact]
    public void SaveIsRefusedWithoutRightsOrWithABadGenderOrLook()
    {
        var (room, client, _) = Context("M", "hd-180-1");
        var gate = AddGate(room, "a,b");
        var store = new RecordingStore();
        var service = new FootballGateService(store);

        service.SaveLook(client, new(gate.Id, "X", MaleKit));
        service.SaveLook(client, new(gate.Id, "M", "not,a.look"));
        room.OwnerName = "someone-else";
        service.SaveLook(client, new(gate.Id, "M", MaleKit));

        Assert.Equal(0, store.Writes);
        Assert.Equal("a,b", gate.LegacyDataString);
    }

    [Theory]
    [InlineData("M", "hr-100-61.hd-180-1.ch-210-66.lg-270-82.sh-290-80.ha-1002-70",
        "hr-100-61.hd-180-1.ch-3109-92-1408.lg-3116-82-1408.sh-3115-1408-1408.ha-1002-70")]
    [InlineData("F", "hr-515-33.hd-600-1.ch-635-70.lg-716-66-62.ea-1406-62",
        "hr-515-33.hd-600-1.ch-3112-1408-1408.lg-3116-71-1408.ea-1406-62.sh-3115-1408-1408")]
    public void WalkingThroughPutsOnTheGendersKitAndWalkingBackRestoresTheOwnLook(string gender, string own, string kitted)
    {
        var (room, client, sent) = Context(gender, own);
        var gate = AddGate(room, FootballGateLooks.Serialize(MaleKit, FemaleKit));
        var user = AddRoomUser(room, client);
        var interactor = new InteractorFootballGate(gate);

        interactor.OnWalkOn(user);

        Assert.Equal(kitted, user.LookFor(client.GetHabbo()));
        Assert.Equal(own, client.GetHabbo().Look);
        Assert.Equal(kitted, SelfLook(sent));

        sent.Clear();
        interactor.OnWalkOn(user);

        Assert.Equal(own, user.LookFor(client.GetHabbo()));
        Assert.Null(user.FootballGateOutfit);
        Assert.Equal(own, SelfLook(sent));
    }

    [Fact]
    public void AGateWithoutALookForTheUsersGenderChangesNothing()
    {
        var (room, client, sent) = Context("F", "hd-600-1.ch-635-70");
        var gate = AddGate(room, $"{MaleKit},");
        var user = AddRoomUser(room, client);

        new InteractorFootballGate(gate).OnWalkOn(user);

        Assert.Null(user.FootballGateOutfit);
        Assert.Empty(sent);
    }

    [Fact]
    public void AnOwnLookChangeDropsTheGateOutfit()
    {
        var (room, client, _) = Context("M", "hd-180-1.ch-210-66");
        var gate = AddGate(room, $"{MaleKit},{FemaleKit}");
        var user = AddRoomUser(room, client);

        new InteractorFootballGate(gate).OnWalkOn(user);
        client.GetHabbo().Look = "hd-180-1.ch-215-66";

        Assert.Equal("hd-180-1.ch-215-66", user.LookFor(client.GetHabbo()));
    }

    [Theory]
    [InlineData("", "", "")]
    [InlineData("ch-1-2,lg-3-4", "ch-1-2", "lg-3-4")]
    [InlineData(",lg-3-4", "", "lg-3-4")]
    [InlineData("M:ch-1-2", "", "")]
    [InlineData("0", "", "")]
    public void StuffDataSplitsOnCommaMaleFirst(string data, string male, string female) =>
        Assert.Equal((male, female), FootballGateLooks.Parse(data));

    private static string SelfLook(List<(uint Header, byte[] Payload)> sent)
    {
        var payload = sent.Single(packet => packet.Header == ServerPacketHeader.UserChangeComposer
            && BinaryPrimitives.ReadInt32BigEndian(packet.Payload) == -1).Payload;
        var length = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4, 2));

        return Encoding.UTF8.GetString(payload, 6, length);
    }

    private static (Room Room, GameClient Client, List<(uint Header, byte[] Payload)> Sent) Context(string gender, string look)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        room.Id = 9;
        room.OwnerName = "owner";
        room.Type = "private";
        room.UsersWithRights = [];
        typeof(Room).GetField("_roomItemHandling", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, new RoomItemHandling(room, TestRoomItemStore.Instance, TestRoomItemMetadataStore.Instance, TestGameClientManager.Empty, TestLanguageManager.RoomItems, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards));
        typeof(Room).GetField("_roomUserManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, new RoomUserManager(room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(), TestChatEmotions.Unused, TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel));
        var (client, sent) = HabbiconTestSupport.Client(new Habbo { Id = 1, Username = "owner", CurrentRoom = room, Gender = gender, Look = look, Motto = "" });

        return (room, client, sent);
    }

    private static Item AddGate(Room room, string data)
    {
        var item = new Item { Id = 7, RoomId = room.Id, OwnerId = 1, Definition = new() { InteractionType = InteractionType.FootballGate }, ExtraData = new LegacyDataFormat { Data = data } };
        var floor = (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(room.GetRoomItemHandler())!;
        typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, room);
        floor[item.Id] = item;

        return item;
    }

    private static RoomUser AddRoomUser(Room room, GameClient client)
    {
        var user = new RoomUser(1, room.Id, 1, room, client, TestChatEmotions.Unused, TestRewardProgress.Unused);
        var users = (ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager).GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(room.GetRoomUserManager())!;
        users.TryAdd(1, user);

        return user;
    }

    private sealed class RecordingGates : IFootballGateService
    {
        public FootballGateLookRequest? Request;
        public void SaveLook(GameClient session, FootballGateLookRequest request) => Request = request;
    }

    private sealed class RecordingStore : IFootballGateStore
    {
        public int Writes { get; private set; }
        public string? Data;
        public void SetLooks(uint itemId, uint roomId, string data) => (Data, Writes) = (data, Writes + 1);
    }
}
