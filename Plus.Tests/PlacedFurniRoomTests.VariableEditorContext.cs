using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// The editor of a user, furni or room variable box ends with one Wired context entry that holds the box's variable. A user or furni variable lists each holder in the room with its value, a room variable its value. A context variable box has no entry. Holders come in no fixed order.
public partial class PlacedFurniRoomTests
{
    private const uint EditedUser = 861, EditedNoValue = 862, EditedFurni = 863, EditedRoom = 864, EditedContext = 865;
    private const uint HeldFurniA = 866, HeldFurniB = 867, HeldFurniC = 868;
    private const int FurniInfo = 1, UserInfo = 2, RoomInfo = 3;

    private sealed record EditorVariable(string Id, string Name, int Target, bool HasValue);

    private sealed record EditorContextEntry(int Type, EditorVariable? Variable, (int ObjectId, int Value)[] Holders, int? Value);

    [Fact]
    public async Task VariableBoxEditorsCarryTheirVariableBeforeAnyValue()
    {
        EditedVariableRoom();

        var user = Assert.Single(await OpenVariableEditor(EditedUser));
        Assert.Equal(UserInfo, user.Type);
        Assert.Equal(new EditorVariable($"user:{EditedUser}", "ec_u", 1, true), user.Variable);
        Assert.Empty(user.Holders);

        var noValue = Assert.Single(await OpenVariableEditor(EditedNoValue));
        Assert.Equal(UserInfo, noValue.Type);
        Assert.Equal(new EditorVariable($"user:{EditedNoValue}", "ec_n", 1, false), noValue.Variable);
        Assert.Empty(noValue.Holders);

        var furni = Assert.Single(await OpenVariableEditor(EditedFurni));
        Assert.Equal(FurniInfo, furni.Type);
        Assert.Equal(new EditorVariable($"furni:{EditedFurni}", "ec_f", 0, true), furni.Variable);
        Assert.Empty(furni.Holders);

        var room = Assert.Single(await OpenVariableEditor(EditedRoom));
        Assert.Equal(RoomInfo, room.Type);
        Assert.Equal(new EditorVariable($"room:{EditedRoom}", "ec_g", -10, true), room.Variable);
        Assert.Equal(0, room.Value);

        Assert.Empty(await OpenVariableEditor(EditedContext));
    }

    [Fact]
    public async Task VariableBoxEditorsListTheHoldersAndValuesTheWiredCycleGave()
    {
        var actor = EditedVariableRoom();
        var wireB = BuildersClubItemIds.Assign(HeldFurniB);

        try {
            GiveEditedVariables(actor, wireB);

            var user = Assert.Single(await OpenVariableEditor(EditedUser));
            Assert.Equal(UserInfo, user.Type);
            Assert.NotEqual(actor.HabboId, actor.VirtualId);
            Assert.Equal([(actor.VirtualId, 7)], user.Holders);

            var noValue = Assert.Single(await OpenVariableEditor(EditedNoValue));
            Assert.Equal(UserInfo, noValue.Type);
            Assert.Equal([(actor.VirtualId, -1)], noValue.Holders);

            var furni = Assert.Single(await OpenVariableEditor(EditedFurni));
            Assert.Equal(FurniInfo, furni.Type);
            Assert.Equal([((int)HeldFurniA, 10), ((int)wireB, 20), ((int)HeldFurniC, 30)], furni.Holders.OrderBy(holder => holder.Value));

            var room = Assert.Single(await OpenVariableEditor(EditedRoom));
            Assert.Equal(RoomInfo, room.Type);
            Assert.Equal(9, room.Value);

            Assert.Empty(await OpenVariableEditor(EditedContext));
        }
        finally {
            BuildersClubItemIds.Release(HeldFurniB);
        }
    }

    // A user, a user without a value, a furni, a room and a context variable, saved through the real save packet, three furni to hold
    // the furni variable and the owner in the room.
    private RoomUser EditedVariableRoom()
    {
        var service = UseVariableRows();
        var users = new RoomUserManager(_room, TestRoomUserStore.Instance, TimeProvider.System, TestRewardProgress.Unused,
            new TestChatEmotions(), TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel, TestItemRuntime.Settings);
        Set("_roomUserManager", users);
        _room.WordFilterList = [];
        _client.GetHabbo().IgnoresComponent = new([]);
        Assert.True(users.AddAvatarToRoom(_client));
        var actor = Assert.IsType<RoomUser>(users.GetRoomUserByHabbo(7));

        foreach (var (id, box, name, ints) in new[] { (EditedUser, "wf_var_user", "ec_u", new[] { 0, 1 }), (EditedNoValue, "wf_var_user", "ec_n", new[] { 0, 0 }),
            (EditedFurni, "wf_var_furni", "ec_f", new[] { 1, 1 }), (EditedRoom, "wf_var_room", "ec_g", new[] { 1 }), (EditedContext, "wf_var_context", "ec_c", new[] { 1 }) }) {
            PlaceVariableBox(id, box);
            Assert.Equal("saved", SaveVariable(service, id, ints, name));
        }

        foreach (var (id, x) in new[] { (HeldFurniA, 0), (HeldFurniB, 2), (HeldFurniC, 3) }) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, Furni(id, InteractionType.None, WiredBoxType.None), x, 2, 0, true, false, false));
        }

        return actor;
    }

    // One "go" runs a stack that gives both user variables to the speaker, 10, 20 and 30 to the three furni and sets the room variable to 9.
    private void GiveEditedVariables(RoomUser actor, uint wireB)
    {
        var service = new WiredConfigurationService(new EmptyConfigurationStore(), null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());
        AddSpeechBox(871, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0, 1, 1);

        foreach (var (id, height) in new[] { (872u, 1.0), (873u, 2.0), (874u, 3.0), (875u, 4.0), (876u, 5.0), (877u, 6.0) }) {
            PlaceSavedBox(id, id == 877 ? "wf_act_change_var_val" : "wf_act_give_var", 1, 1, height);
        }

        Save(new SaveWiredEffectConfigEvent(service), 872, 7, 1, 0, 7, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"user:{EditedUser}", "n", 0);
        Save(new SaveWiredEffectConfigEvent(service), 873, 7, 1, 0, 0, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"user:{EditedNoValue}", "n", 0);

        foreach (var (id, wire, value) in new[] { (874, (int)HeldFurniA, 10), (875, (int)wireB, 20), (876, (int)HeldFurniC, 30) }) {
            Save(new SaveWiredEffectConfigEvent(service), id, 7, 0, 0, value, 1, 0, -10, 0, "", 1, wire, 0, 1, 100, 1, 0, 2, $"furni:{EditedFurni}", "n", 0);
        }

        Save(new SaveWiredEffectConfigEvent(service), 877, 7, -10, 0, 0, 0, 9, -10, 0, "", 0, 0, 2, 0, 200, 2, 0, 200, 2, $"room:{EditedRoom}", "n", 0);

        actor.OnChat(0, "go", false);
        _room.GetWired().OnFastCycle();
        _room.GetWired().OnCycle();
    }

    private void Save(SaveWiredEffectConfigEvent save, params object[] body)
    {
        _client.Packets.Clear();
        save.Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();
        Assert.Equal($"{body[0]} saved", $"{body[0]} {SaveAnswer()}");
    }

    // The raw WIRED_OPEN packet, then the context list of the variable editor frame.
    private async Task<EditorContextEntry[]> OpenVariableEditor(uint id)
    {
        _client.Packets.Clear();
        await new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)id));
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredVariableConfigComposer).Body };

        frame.ReadInt();
        SkipInts(frame);
        SkipInts(frame);
        frame.ReadInt();
        Assert.Equal(id, frame.ReadUInt());
        frame.ReadString();
        SkipInts(frame);

        for (var count = frame.ReadInt(); count > 0; count--) {
            frame.ReadString();
        }

        SkipInts(frame);
        SkipInts(frame);
        frame.ReadInt();
        frame.ReadBool();

        for (var groups = 0; groups < 2; groups++) {
            for (var count = frame.ReadInt(); count > 0; count--) {
                SkipInts(frame);
            }
        }

        SkipInts(frame);
        SkipInts(frame);
        frame.ReadBool();
        var entries = new EditorContextEntry[frame.ReadInt()];

        for (var index = 0; index < entries.Length; index++) {
            var type = frame.ReadInt();
            entries[index] = type switch
            {
                FurniInfo or UserInfo => new(type, ReadEditorVariable(frame), ReadHolders(frame), null),
                RoomInfo => new(type, ReadEditorVariable(frame), [], frame.ReadInt()),
                0 => new(type, null, [], frame.ReadInt()),
                _ => throw new InvalidDataException($"Unexpected context type {type}.")
            };
        }

        SkipInts(frame);
        Assert.False(frame.HasDataRemaining());

        return entries;
    }

    private static (int ObjectId, int Value)[] ReadHolders(FlashIncomingPacket frame)
    {
        var holders = new (int, int)[frame.ReadInt()];

        for (var index = 0; index < holders.Length; index++) {
            holders[index] = (frame.ReadInt(), frame.ReadInt());
        }

        return holders;
    }

    // Variable: id, type, name, availability, target, nine flags, then the optional text connector.
    private static EditorVariable ReadEditorVariable(FlashIncomingPacket frame)
    {
        var id = frame.ReadString();
        frame.ReadInt();
        var name = frame.ReadString();
        frame.ReadInt();
        var target = frame.ReadInt();
        frame.ReadBool();
        frame.ReadBool();
        var hasValue = frame.ReadBool();

        for (var flag = 0; flag < 6; flag++) {
            frame.ReadBool();
        }

        if (frame.ReadBool()) {
            for (var count = frame.ReadInt(); count > 0; count--) {
                frame.ReadInt();
                frame.ReadString();
            }

            if (frame.ReadBool()) {
                frame.ReadString();
            }
        }

        return new(id, name, target, hasValue);
    }

    private static void SkipInts(FlashIncomingPacket frame)
    {
        for (var count = frame.ReadInt(); count > 0; count--) {
            frame.ReadInt();
        }
    }
}
