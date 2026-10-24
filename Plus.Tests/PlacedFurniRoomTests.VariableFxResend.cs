using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Items.Wired.Variables.Fx;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void AGiveWithOverrideOfTheSameValueSendsTheVariableFxStatusAgain()
    {
        var (wired, actor) = PrepareFxGive(1, "fxo");

        actor.OnChat(0, "fxo", false);
        Assert.Equal(1, FxStatusCount());
        _client.Packets.Clear();

        actor.OnChat(0, "fxo", false);
        wired.OnFastCycle();

        Assert.Equal(1, FxStatusCount());
    }

    [Fact]
    public void AGiveWithoutOverrideOnAnExistingVariableSendsNoVariableFxStatus()
    {
        var (wired, actor) = PrepareFxGive(0, "fxk");

        actor.OnChat(0, "fxk", false);
        Assert.Equal(1, FxStatusCount());
        _client.Packets.Clear();

        actor.OnChat(0, "fxk", false);
        wired.OnFastCycle();

        Assert.Equal(0, FxStatusCount());
    }

    [Fact]
    public void AGiveWithOverrideOfANewValueSendsExactlyOneVariableFxStatus()
    {
        var (wired, actor) = PrepareFxGive(1, "fxc");
        actor.OnChat(0, "fxc", false);
        wired.OnFastCycle();
        _client.Packets.Clear();
        AddVariableBox(303, "wf_act_give_var", [1, 0, 9, 1, 0, -10, 0], ["user:993", "n"], 1, 3, 3);
        AddSpeechBox(304, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "fxn" }, 0, 3, 3);

        actor.OnChat(0, "fxn", false);
        wired.OnFastCycle();

        Assert.Equal(1, FxStatusCount());
    }

    private (Plus.HabboHotel.Rooms.Instance.WiredComponent Wired, Plus.HabboHotel.Rooms.RoomUser Actor) PrepareFxGive(int overrideExisting, string word)
    {
        var (wired, actor) = PrepareSameTickVariable();
        typeof(WiredRoomVariables).GetField("<Fx>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, new WiredVariableFxTracker(wired.Variables.Module));
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = word }, 0);
        AddVariableBox(302, "wf_act_give_var", [1, 0, 5, overrideExisting, 0, -10, 0], ["user:993", "n"], 1, 1, 1);
        AddDefinitionBox(SameTickVariableId, "wf_var_user", 0, 2, 2);
        AddDefinitionBox(320, "wf_xtra_var_fx_number", 1, 2, 2);
        wired.SnapshotEnqueued(_client, _room.GetRoomItemHandler().GetFloor, [actor]);
        wired.OnFastCycle();
        _client.Packets.Clear();

        return (wired, actor);
    }

    private int FxStatusCount() => _client.Packets.Count(packet => packet.Header == ServerPacketHeader.WiredVariableFxStatusComposer);
}
