using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// Give Variable saves `@can_move_freely` (a variable without a value) on a picked furni, Has Variable and Not Has Variable read it,
// and Remove Variable takes it away. Every box is saved from its raw editor packet and a real speech trigger drains each stack.
public partial class PlacedFurniRoomTests
{
    private const uint FreeNext = 970;
    private const string CanMoveFreely = "-2000";

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task HasAndNotHasVariableReadACanMoveFreelyGivenToAPickedFurni(bool moved, bool removed)
    {
        var actor = EditedVariableRoom();
        var wired = _room.GetWired();
        var service = new WiredConfigurationService(new EmptyConfigurationStore(), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        var tile = _room.GetRoomItemHandler().GetItem(HeldFurniB)!;
        await FreeBox(service, 1, 1, "mf1", "wf_act_give_var", GiveFree());
        await FreeBox(service, 2, 1, "mfq", "wf_cnd_has_var", HasFree());
        await FreeBox(service, 3, 1, "mfn", "wf_cnd_neg_has_var", HasFree());
        await FreeBox(service, 1, 0, "mf2", "wf_act_remove_var", RemoveFree());
        await FreeBox(service, 2, 0, "mvw", "wf_act_move_rotate", MoveFree());

        Assert.Equal("has=- not=not", Answers(wired, actor));

        if (moved) {
            Fire(wired, actor, "mvw");
            Assert.NotEqual((2, 2), (tile.GetX, tile.GetY));
        }

        Fire(wired, actor, "mf1");
        Assert.Equal("has=has not=-", Answers(wired, actor));

        if (removed) {
            Fire(wired, actor, "mf2");
            Assert.Equal("has=- not=not", Answers(wired, actor));
        }
    }

    [Theory]
    [InlineData("-130", false)]
    [InlineData("-130", true)]
    [InlineData("-131", false)]
    [InlineData("-131", true)]
    [InlineData("-132", false)]
    [InlineData("-132", true)]
    [InlineData("-133", false)]
    [InlineData("-133", true)]
    public async Task HasAndNotHasVariableReadAValuelessFlagOfThePickedFurni(string variable, bool flagged)
    {
        var actor = EditedVariableRoom();
        var wired = _room.GetWired();
        var service = new WiredConfigurationService(new EmptyConfigurationStore(), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        var definition = _room.GetRoomItemHandler().GetItem(HeldFurniB)!.Definition;

        definition.Stackable = flagged && variable == "-130";
        definition.Walkable = flagged && variable == "-131";
        definition.IsSeat = flagged && variable == "-132";
        definition.InteractionType = flagged && variable == "-133" ? InteractionType.Bed : InteractionType.None;

        object[] has = [1, 0, "", 1, (int)HeldFurniB, 0, 1, 100, 1, 0, 1, variable, 0];
        await FreeBox(service, 2, 1, "mfq", "wf_cnd_has_var", has);
        await FreeBox(service, 3, 1, "mfn", "wf_cnd_neg_has_var", has);

        Assert.Equal(flagged ? "has=has not=-" : "has=- not=not", Answers(wired, actor));
    }

    private static object[] GiveFree() => [7, 0, 0, 0, 1, 0, -10, 0, "", 1, (int)HeldFurniB, 0, 2, 100, 200, 2, 0, 200, 2, CanMoveFreely, "0", 0];
    private static object[] RemoveFree() => [1, 0, "", 1, (int)HeldFurniB, 0, 1, 100, 1, 0, 1, CanMoveFreely, 0];
    private static object[] HasFree() => [1, 0, "", 1, (int)HeldFurniB, 0, 1, 100, 1, 0, 1, CanMoveFreely, 0];
    private static object[] MoveFree() => [2, 4, 0, "", 1, (int)HeldFurniB, 0, 1, 100, 0, 0, 0];

    // The trigger is the first box of a tile, the box under test follows it and a condition stack ends with a Show Message.
    private async Task FreeBox(WiredConfigurationService service, int x, int y, string word, string name, object[] body)
    {
        var next = FreeNext + (uint)(x * 10 + y) * 4;
        AddSpeechBox(next, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = word }, 0, x, y);
        PlaceSavedBox(next + 1, name, x, y, 1);
        var condition = name.StartsWith("wf_cnd", StringComparison.Ordinal);
        _client.Packets.Clear();
        await (condition ? new SaveWiredConditionConfigEvent(service).Parse(_client, ClientPacket([(int)next + 1, .. body]))
            : new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket([(int)next + 1, .. body])));
        Assert.Equal("saved", SaveAnswer());

        if (condition) {
            AddSpeechBox(next + 2, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = name == "wf_cnd_has_var" ? "has" : "not" }, 2, x, y);
        }
    }

    private string Answers(WiredComponent wired, RoomUser actor) =>
        $"has={string.Join(",", Fire(wired, actor, "mfq").DefaultIfEmpty("-"))} not={string.Join(",", Fire(wired, actor, "mfn").DefaultIfEmpty("-"))}";

    private string[] Fire(WiredComponent wired, RoomUser actor, string word)
    {
        wired.FlushFlashes();
        _client.Packets.Clear();
        actor.OnChat(0, word, false);
        wired.OnFastCycle();
        wired.OnCycle();

        return [.. SpeechMessages(ServerPacketHeader.ChatComposer).Where(message => message is "has" or "not")];
    }
}
