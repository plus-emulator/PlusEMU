using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A Users With Variable selector in a stack started by a Send Signal reads a user pointer like the variable it points at. The sending stack
/// sets the accessor first. Every box is saved from its raw packet and a real speech trigger fires the stack on the room cycle.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private const uint PointerSelectorAntenna = 440;

    [Theory]
    [InlineData("direct", true, "1")]
    [InlineData("pointer", true, "1")]
    [InlineData("pointer", false, "0")]
    public async Task AUsersWithVariableSelectorReadsAUserPointerLikeThePointedVariable(string shape, bool accessorSet, string expected)
    {
        var (wired, actor, clock) = await SignalledPointerRoom(accessorSet);
        var selector = _dpNext++;
        PlaceSavedBox(selector, "wf_slc_users_with_var", 3, 1, _dpZ++);
        _client.Packets.Clear();
        await new SaveWiredSelectorConfigEvent(_dpService!).Parse(_client, ClientPacket((int)selector, 6, 2, 0, 0, 0, -10, 0, "", 0, false, false,
            1, 100, 1, 0, 2, "user:" + (shape == "pointer" ? DpPu : DpUa), "n", 0));
        Assert.Contains(_client.Packets, packet => packet.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.HideWiredConfigComposer);
        await DpShow("$(n)", 4);
        await DpOutput("n", DpContext, "-401");

        Assert.Equal([expected], DpFire(wired, actor, clock, 5));
    }

    /// <summary>
    /// The room whose speech stack gives pu_a = 7, optionally points pctx at the pu_a box and sends a signal; the receiving stack is placed
    /// next, on the tile (3, 1).
    /// </summary>
    private async Task<(Plus.HabboHotel.Rooms.Instance.WiredComponent Wired, Plus.HabboHotel.Rooms.RoomUser Actor, SpeechClock Clock)> SignalledPointerRoom(
        bool accessorSet)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(wired.Variables,
            new WiredVariableModule(RoomId, new DelayedPointerDirectory(_room), new MemoryWiredVariableStore(), TimeProvider.System,
                new RoomWiredBuiltinVariables(_room, wired.ReadBuiltin)));
        var antenna = Furni(PointerSelectorAntenna, InteractionType.None, WiredBoxType.None);
        antenna.Definition.InteractionName = "antenna";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, antenna, 3, 3, 0, true, false, false));

        await DpGive(DpUser, "user:" + DpUa, 7);

        if (accessorSet) {
            await DpGive(DpContext, "ctx:" + DpCtx, DpUa);
        }

        await DpEffect("wf_act_send_signal", [0, 0], [], furni: [100, 100], users: [0], picks: [PointerSelectorAntenna]);

        _dpTile = (3, 1);
        _dpZ = 0;
        AddSpeechBox(_dpNext++, "wf_trg_recv_signal", new() { IntParams = [0, 100], SelectedItems = [PointerSelectorAntenna] }, _dpZ++, 3, 1);

        return (wired, actor, clock);
    }
}
