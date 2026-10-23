using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: a Give Variable that names a built-in variable the box cannot create leaves the holder as it
// was, and a Change Variable Value on the same variable writes it, in the tick the box flashes. A colour tile has 7 states (0 to 6) and a
// Wired floor switch 2.
public partial class PlacedFurniRoomTests
{
    private const uint RuntimeTile = 961, RuntimeSpeech = 962, RuntimeBox = 963;

    private (RoomUser Actor, Item Tile, WiredConfigurationService Service) BuiltInRuntimeRoom(int modes = 7, string state = "0", int x = 2, int y = 2, int rotation = 0)
    {
        var service = UseVariableRows();
        var users = new RoomUserManager(_room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(),
            new TestChatEmotions(), TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel, TestItemRuntime.Settings);
        Set("_roomUserManager", users);
        _room.WordFilterList = [];
        _client.GetHabbo().IgnoresComponent = new([]);
        Assert.True(users.AddAvatarToRoom(_client));
        var actor = Assert.IsType<RoomUser>(users.GetRoomUserByHabbo(7));
        var tile = Furni(RuntimeTile, InteractionType.None, WiredBoxType.None);
        tile.Definition.Modes = modes;
        tile.ExtraData = new LegacyDataFormat { Data = state };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, tile, x, y, rotation, true, false, false));
        AddSpeechBox(RuntimeSpeech, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0, 1, 1);

        return (actor, tile, service);
    }

    private void SaveRuntimeBox(WiredConfigurationService service, string card, int target, string variableId, int value, bool picksTile, int operation = 0)
    {
        PlaceSavedBox(RuntimeBox, card, 1, 1, 1);
        object[] picks = picksTile ? [1, (int)RuntimeTile] : [0];
        var high = value < 0 ? -1 : 0;
        object[] body = card == Give
            ? [(int)RuntimeBox, 7, target, high, value, 1, 0, -10, 0, "", .. picks, 0, 2, 100, 200, 2, 0, 200, 2, variableId, "0", 0]
            : [(int)RuntimeBox, 7, target, operation, 0, high, value, -10, 0, "", .. picks, 0, 2, 100, 200, 2, 0, 200, 2, variableId, "0", 0];
        _client.Packets.Clear();
        new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket(body)).GetAwaiter().GetResult();
        Assert.Equal("saved", SaveAnswer());
    }

    private void FireRuntimeStack(RoomUser actor)
    {
        _client.Packets.Clear();
        actor.OnChat(0, "go", false);
        _room.GetWired().OnFastCycle();
        _room.GetWired().OnCycle();
    }

    [Theory]
    [InlineData(Give, "-110", 3, "0")]
    [InlineData(Change, "-110", 3, "3")]
    public void GiveLeavesAFurnisStateAloneAndChangeWritesIt(string card, string variableId, int value, string expected)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom();
        SaveRuntimeBox(service, card, 0, variableId, value, true);

        FireRuntimeStack(actor);

        Assert.Equal(expected, tile.LegacyDataString);
    }

    // Observed on AIR WIN63-202610201032-107964540 (three runs): assigning a value from the furni's state count up sets state 0. A colour tile (states
    // 0 to 6) from state 1 or 0 with 7, 8, 24, 1000 or 2147483647; a floor switch (states 0 and 1) with 2 or 5. A negative result sets state 0 too
    // (EveryOperationWritesItsResultAsTheState, 2 - 3). The probe also sent -5 and -1 with a zero high word, which Plus reads as 4294967291 and
    // 4294967295; a value beyond 32 bits is not handled.
    [Theory]
    [InlineData(7, "1", 7, "0")]
    [InlineData(7, "1", 8, "0")]
    [InlineData(7, "1", 24, "0")]
    [InlineData(7, "1", 1000, "0")]
    [InlineData(7, "1", int.MaxValue, "0")]
    [InlineData(7, "0", 7, "0")]
    [InlineData(7, "1", 6, "6")]
    [InlineData(7, "1", 0, "0")]
    [InlineData(2, "1", 2, "0")]
    [InlineData(2, "1", 5, "0")]
    [InlineData(2, "0", 2, "0")]
    [InlineData(2, "0", 1, "1")]
    public void AStateOutsideTheFurnisStatesSetsStateZero(int modes, string start, int value, string expected)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom(modes, start);
        SaveRuntimeBox(service, Change, 0, "-110", value, true);

        FireRuntimeStack(actor);

        Assert.Equal(expected, tile.LegacyDataString);
    }

    // Observed on AIR WIN63-202610201032-107964540 (colour tile in state 2, constant operand 3, three runs): each operation writes its result
    // as the state, and a result outside the 7 states sets state 0 (2 - 3 is -1 and 2 ** 3 is 8).
    [Theory]
    [InlineData(0, "3")]
    [InlineData(1, "5")]
    [InlineData(2, "0")]
    [InlineData(3, "6")]
    [InlineData(4, "0")]
    [InlineData(5, "0")]
    [InlineData(6, "2")]
    [InlineData(40, "3")]
    [InlineData(41, "2")]
    [InlineData(100, "2")]
    [InlineData(101, "3")]
    [InlineData(102, "1")]
    public void EveryOperationWritesItsResultAsTheState(int operation, string expected)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom(7, "2");
        SaveRuntimeBox(service, Change, 0, "-110", 3, true, operation);

        FireRuntimeStack(actor);

        Assert.Equal(expected, tile.LegacyDataString);
    }

    // Observed on AIR WIN63-202610201032-107964540 (colour tile, three runs): Change Variable Value assigning @position.y, @position or @altitude moves
    // or lifts the tile (y 1 to 9, (4,1) to (12,9), z 0 to 3 on a larger floor), and Give Variable with the same value leaves it where it is.
    [Theory]
    [InlineData(Change, "-121", 3, 2, 3, 0.0)]
    [InlineData(Give, "-121", 3, 2, 2, 0.0)]
    [InlineData(Change, "-125", (3 << 8) | 2, 3, 2, 0.0)]
    [InlineData(Give, "-125", (3 << 8) | 2, 2, 2, 0.0)]
    [InlineData(Change, "-123", 300, 2, 2, 3.0)]
    [InlineData(Give, "-123", 300, 2, 2, 0.0)]
    public void ChangeMovesAFurniAndGiveLeavesItWhereItIs(string card, string variableId, int value, int x, int y, double z)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom();
        SaveRuntimeBox(service, card, 0, variableId, value, true);

        FireRuntimeStack(actor);
        _room.GetWired().OnCycle();

        Assert.Equal((x, y, z), (tile.GetX, tile.GetY, tile.GetZ));
    }

    // Observed on AIR WIN63-202610201032-107964540: a write of @height, @id, @owner_id or @dimensions.x, which a furni cannot take, changes nothing.
    [Theory]
    [InlineData("-102", 55)]
    [InlineData("-100", 5)]
    [InlineData("-180", 5)]
    [InlineData("-140", 2)]
    public void ChangeLeavesTheReadOnlyVariablesOfAFurniAlone(string variableId, int value)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom();
        SaveRuntimeBox(service, Change, 0, variableId, value, true);

        FireRuntimeStack(actor);
        _room.GetWired().OnCycle();

        Assert.Equal(("0", 2, 2, 0.0), (tile.LegacyDataString, tile.GetX, tile.GetY, tile.GetZ));
    }

    // Observed on AIR WIN63-202610201032-107964540 (the speaker, three runs): Change Variable Value assigning @position.x, @position.y, @position or
    // @direction moves or turns the speaker, and Give Variable with the same value does not.
    [Theory]
    [InlineData(Change, "-230", 3, 3, 0, 0)]
    [InlineData(Give, "-230", 3, 0, 0, 0)]
    [InlineData(Change, "-231", 3, 0, 3, 0)]
    [InlineData(Change, "-234", (3 << 8) | 2, 3, 2, 0)]
    [InlineData(Change, "-232", 4, 0, 0, 4)]
    [InlineData(Give, "-232", 4, 0, 0, 0)]
    public void ChangeMovesAndTurnsTheSpeakerAndGiveDoesNot(string card, string variableId, int value, int x, int y, int direction)
    {
        var (actor, _, service) = BuiltInRuntimeRoom();
        SaveRuntimeBox(service, card, 1, variableId, value, false);

        FireRuntimeStack(actor);
        _room.GetWired().OnCycle();
        _room.GetWired().OnCycle();

        Assert.Equal((x, y, direction), (actor.X, actor.Y, actor.RotBody));
    }

    // Observed on AIR WIN63-202610201032-107964540 (the speaker, three runs): @habbicon, @effect, @altitude, @level and @team.score (in no team)
    // take neither Give Variable nor Change Variable Value.
    [Theory]
    [InlineData(Give, "-245", 5)]
    [InlineData(Change, "-245", 5)]
    [InlineData(Change, "-241", 5)]
    [InlineData(Change, "-233", 100)]
    [InlineData(Change, "-210", 9)]
    [InlineData(Give, "-237", 9)]
    [InlineData(Change, "-237", 9)]
    public void TheSpeakersOtherBuiltInVariablesTakeNothing(string card, string variableId, int value)
    {
        var (actor, _, service) = BuiltInRuntimeRoom();
        SaveRuntimeBox(service, card, 1, variableId, value, false);

        FireRuntimeStack(actor);
        _room.GetWired().OnCycle();

        Assert.Equal((0, 0, 0, 0, 0), (actor.X, actor.Y, actor.RotBody, actor.CarryItemId, actor.CurrentEffect));
    }

    // Observed on AIR WIN63-202610201032-107964540 (@handitem of the speaker, no item in hand): Give with 3 hands item 3, and so does Change
    // Variable Value assigning 3, which on a custom variable the speaker does not hold changes nothing. Both show CarryObject in the tick the box
    // flashes.
    [Theory]
    [InlineData(Give)]
    [InlineData(Change)]
    public void AHandItemIsGivenToASpeakerWithNoneByGiveAndByChange(string card)
    {
        var (actor, _, service) = BuiltInRuntimeRoom();
        SaveRuntimeBox(service, card, 1, "-240", 3, false);

        FireRuntimeStack(actor);

        Assert.Equal(3, actor.CarryItemId);
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.CarryObjectComposer);
    }

    // Observed on AIR WIN63-202610201032-107964540 (@handitem of the speaker, constant operand 5, three runs, every operation of the editor):
    // assigning 5 hands item 5 whether the speaker holds one (2) or not, and every other operation changes nothing, held or not.
    [Theory]
    [InlineData(0, 0, 5)]
    [InlineData(0, 2, 5)]
    [MemberData(nameof(OtherOperations))]
    public void OnlyAssignChangesAHandItem(int operation, int held, int expected)
    {
        var (actor, _, service) = BuiltInRuntimeRoom();
        actor.CarryItem(held);
        SaveRuntimeBox(service, Change, 1, "-240", 5, false, operation);

        FireRuntimeStack(actor);

        Assert.Equal(expected, actor.CarryItemId);
    }

    public static TheoryData<int, int, int> OtherOperations()
    {
        var data = new TheoryData<int, int, int>();

        int[] operations = [1, 2, 3, 4, 5, 6, 40, 41, 50, 60, 100, 101, 102, 103, 104, 105, .. Enumerable.Range(110, 13)];

        foreach (var operation in operations) {
            data.Add(operation, 0, 0);
            data.Add(operation, 2, 2);
        }

        return data;
    }
}
