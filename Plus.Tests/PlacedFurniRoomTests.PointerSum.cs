using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// One pointer over many user variables: a Variable Changed trigger picks the variable boxes, a Give Variable puts the changed box id
/// in the context variable, and a Change Variable adds the pointer to a total. Every box is saved from its raw packet.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private const uint SumTrigger = 450;

    // The Change Variable that adds the pointer needs a delay of 1 or more. Each changed variable adds its value to the total.
    [Theory]
    [InlineData(1, "s t=103")]
    [InlineData(2, "s t=103")]
    [InlineData(0, "s t=100")]
    public async Task ATotalAddsEveryChangedVariableThroughOnePointerAfterADelay(int delay, string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(wired.Variables,
            new WiredVariableModule(RoomId, new DelayedPointerDirectory(_room), new MemoryWiredVariableStore(), TimeProvider.System,
                new RoomWiredBuiltinVariables(_room, wired.ReadBuiltin)));
        await DpGive(DpUser, "user:" + DpUt, 100);
        await DpGive(DpUser, "user:" + DpUa, 1);
        await DpGive(DpUser, "user:" + DpUb, 2);
        await DpShow("s t=$(t)", 9);
        await DpOutput("t", DpUser, "user:" + DpUt);

        _dpTile = (2, 1);
        _dpZ = 0;
        PlaceSavedBox(SumTrigger, "wf_trg_var_changed", _dpTile.X, _dpTile.Y, _dpZ++);
        await new SaveWiredTriggerConfigEvent(_dpService!).Parse(_client,
            ClientPacket((int)SumTrigger, 6, 1, 1, 0, 7, -1, 1, "", 2, (int)DpUa, (int)DpUb, 0, 0, 1, "0", 0));
        await DpEffect("wf_act_give_var", [DpContext, 0, 0, 1, 1, DpContext, 0], ["ctx:" + DpCtx, "-459"], furni: [0, 0], users: [0, 0]);
        await DpChangeFrom(DpPu, delay);

        Assert.Equal([expected], DpFire(wired, actor, clock, 9));
    }
}
