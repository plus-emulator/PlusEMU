using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// Place Furni "spawn with variable" gives the copy its variable whether or not a variable box ran before it in the stack. The boxes are
/// saved from their raw packets and a real speech trigger fires the stack on the room cycle.
/// </summary>
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PlaceFurniGivesTheCopyItsSpawnVariable(bool variableBoxBefore)
    {
        var definition = Furni(430, InteractionType.None, WiredBoxType.None).Definition;
        definition.Id = 430;
        var template = new Item { Id = 430, IsBuildersClub = true, OwnerId = 7, UserId = 7, RoomId = RoomId, Definition = definition };
        var definitions = new TestWiredDefinitions(() => new() { [template.Definition.Id] = template.Definition });
        var (wired, actor, clock) = await DelayedPointerRoom(definitions: definitions);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, template, 3, 3, 0, true, false, false));

        if (variableBoxBefore) {
            await DpGive(DpContext, "ctx:" + DpCt, 3);
        }

        await DpEffect("wf_act_place_furni", [0, 0, 0, 0, 0, 0, 1, 0, 37, DpContext], ["furni:" + DpFa, "n"], furni: [110, 0, 200], users: [0, 200],
            picks: [template.Id]);
        DpFire(wired, actor, clock, 1);

        var copy = Assert.Single(_room.GetRoomItemHandler().GetFloor, item => item.IsTemporary);
        var holder = WiredVariableRuntimeFrames.FurniHolder(copy);
        Assert.Equal(37, wired.Variables.Module.Read(new(WiredVariableTarget.Furni, "custom:" + DpFa), holder, new(RoomId, [holder]))?.Value);
    }
}
