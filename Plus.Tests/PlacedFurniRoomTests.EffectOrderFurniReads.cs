using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A Toggle State or Move and Rotate Furni and a Give or Change Variable that reads the furni's state or rotation, in one stack without
/// Execute in Order. Each box is saved from its raw packet and fired by a real speech trigger on the room cycle.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private const uint EoHolder = 421;

    // The Toggle State or Move and Rotate runs before the Give or Change that reads the furni, whatever the box order. The variable holds
    // the new state or rotation.
    [Theory]
    [InlineData("GTa", "toggle,give", "wf_act_toggle_state", "-110")]
    [InlineData("GTb", "give,toggle", "wf_act_toggle_state", "-110")]
    [InlineData("CTa", "toggle,change", "wf_act_toggle_state", "-110")]
    [InlineData("CTb", "change,toggle", "wf_act_toggle_state", "-110")]
    [InlineData("GMa", "move,give", "wf_act_move_rotate", "-122")]
    [InlineData("GMb", "give,move", "wf_act_move_rotate", "-122")]
    [InlineData("CMa", "move,change", "wf_act_move_rotate", "-122")]
    [InlineData("CMb", "change,move", "wf_act_move_rotate", "-122")]
    public async Task AGiveOrChangeThatReadsAFurniRunsAfterTheToggleOrMoveWhateverTheBoxOrder(string tag, string boxes, string changer, string read)
    {
        var (wired, actor, clock) = await DelayedPointerRoom(executeInOrder: false);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(wired.Variables,
            new WiredVariableModule(RoomId, new DelayedPointerDirectory(_room), new MemoryWiredVariableStore(), TimeProvider.System,
                new RoomWiredBuiltinVariables(_room)));
        var engine = typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;
        engine.GetType().GetField("_effectOrderRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, new Random(0));
        var subject = Furni(EoHolder, InteractionType.None, WiredBoxType.None);
        subject.Definition.Modes = 5;
        subject.Definition.AdjustableHeights = new List<double>();
        subject.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, subject, 2, 2, 0, true, false, false));
        var change = tag.StartsWith('C');

        if (change) {
            _dpTile = (2, 1);
            AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "init" }, _dpZ++, 2, 1);
            await DpGive(DpUser, "user:" + DpUb, 0);
            _dpTile = (1, 1);
            EffectOrderFire(wired, actor, clock, "init", 2);
        }

        foreach (var box in boxes.Split(',')) {
            switch (box) {
                case "toggle":
                    await DpEffect("wf_act_toggle_state", [0], [], furni: [100], users: [], picks: [EoHolder]);
                    break;
                case "move":
                    await DpEffect("wf_act_move_rotate", [0, 1], [], furni: [100], users: [], picks: [EoHolder]);
                    break;
                case "give":
                    await DpEffect("wf_act_give_var", [DpUser, 0, 0, 1, 1, DpFurni, 0], ["user:" + DpUb, read], furni: [0, 101], users: [0, 0],
                        secondary: [EoHolder]);
                    break;
                default:
                    await DpEffect("wf_act_change_var_val", [DpUser, 0, 1, 0, 0, DpFurni, 0], ["user:" + DpUb, read], furni: [0, 101], users: [0, 0],
                        secondary: [EoHolder]);
                    break;
            }
        }

        await DpShow($"{tag} q=$(q) st=$(st)", 5);
        await DpOutput("q", DpUser, "user:" + DpUb);
        await DpOutput("st", DpFurni, read, picks: [EoHolder]);
        var states = new HashSet<long>();

        for (var firing = 0; firing < 12; firing++) {
            var shown = Assert.Single(DpFire(wired, actor, clock, 5));
            Assert.DoesNotContain("null", shown);
            var parts = shown.Split([' ', '='], StringSplitOptions.RemoveEmptyEntries);
            var (q, st) = (long.Parse(parts[2]), long.Parse(parts[4]));
            states.Add(st);
            Assert.Equal(st, q);
        }

        Assert.True(states.Count > 1, $"the {changer} changed the furni");
    }
}
