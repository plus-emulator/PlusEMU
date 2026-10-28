using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The amount of Filter Users By Variable and of the users Selector Filter can come from a variable. In a stack started by a Send Signal a
/// pointer amount reads like the pointed variable. An amount variable that reads nothing (a pointer at nothing, a context variable that was
/// never given) keeps no user. Every box is saved from its raw packet and a real speech trigger fires the stack on the room cycle.
/// </summary>
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("wf_xtra_filter_users_by_var", "pointer", true, "1")]
    [InlineData("wf_xtra_filter_users_by_var", "pointer", false, "0")]
    [InlineData("wf_xtra_filter_users_by_var", "empty context", true, "0")]
    [InlineData("wf_xtra_filter_users", "pointer", true, "1")]
    [InlineData("wf_xtra_filter_users", "pointer", false, "0")]
    [InlineData("wf_xtra_filter_users", "empty context", true, "0")]
    public async Task AFilterAmountThatReadsNothingKeepsNoUser(string filter, string amount, bool accessorSet, string expected)
    {
        var (wired, actor, clock) = await SignalledPointerRoom(accessorSet);
        var selector = _dpNext++;
        PlaceSavedBox(selector, "wf_slc_users_bytype", 3, 1, _dpZ++);
        _client.Packets.Clear();
        await new SaveWiredSelectorConfigEvent(_dpService!).Parse(_client, ClientPacket((int)selector, 1, 1, "", 0, false, false, 0, 0, 0, 0));
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);

        var (target, variable) = amount == "pointer" ? (DpUser, "user:" + DpPu) : (DpContext, "ctx:" + DpCt);

        if (filter == "wf_xtra_filter_users_by_var") {
            await DpSave(WiredBoxCategory.Addon, filter, [2, 0, 1, target], "", [], null, [100], [0], ["user:" + DpUa, variable], []);
        }
        else {
            await DpSave(WiredBoxCategory.Addon, filter, [2, 1, target], "", [], null, [100], [0], [variable], []);
        }

        await DpShow("$(n)", 4);
        await DpOutput("n", DpContext, "-401");

        Assert.Equal([expected], DpFire(wired, actor, clock, 5));
    }
}
