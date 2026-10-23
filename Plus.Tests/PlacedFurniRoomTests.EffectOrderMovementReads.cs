using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// User To Furni, Furni To User or Teleport To and a Give or Change Variable that reads the position, in one stack without Execute in
/// Order. Each box is saved from its raw packet and fired by a real speech trigger on the room cycle.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private const uint MrTarget = 431;

    // Observed on AIR WIN63-202610201032-107964540: a Give or Change Variable that reads the x of the user (User To Furni) or of the furni
    // (Furni To User) reads the new x, whatever the box order (12 of 12 firings for each case, Show Message included). With Teleport To a Give or Change reads the x the user had
    // (12 of 12): the teleport lands later.
    [Theory]
    [InlineData("GUa", "move,give", "wf_act_user_to_furni", "-230", true)]
    [InlineData("GUb", "give,move", "wf_act_user_to_furni", "-230", true)]
    [InlineData("CUa", "move,change", "wf_act_user_to_furni", "-230", true)]
    [InlineData("CUb", "change,move", "wf_act_user_to_furni", "-230", true)]
    [InlineData("GFa", "move,give", "wf_act_furni_to_user", "-120", true)]
    [InlineData("GFb", "give,move", "wf_act_furni_to_user", "-120", true)]
    [InlineData("CFa", "move,change", "wf_act_furni_to_user", "-120", true)]
    [InlineData("CFb", "change,move", "wf_act_furni_to_user", "-120", true)]
    [InlineData("SUa", "move,show", "wf_act_user_to_furni", "-230", true)]
    [InlineData("SUb", "show,move", "wf_act_user_to_furni", "-230", true)]
    [InlineData("SFa", "move,show", "wf_act_furni_to_user", "-120", true)]
    [InlineData("SFb", "show,move", "wf_act_furni_to_user", "-120", true)]
    [InlineData("GTa", "move,give", "wf_act_teleport_to", "-230", false)]
    [InlineData("GTb", "give,move", "wf_act_teleport_to", "-230", false)]
    [InlineData("CTa", "move,change", "wf_act_teleport_to", "-230", false)]
    [InlineData("CTb", "change,move", "wf_act_teleport_to", "-230", false)]
    public async Task AGiveOrChangeThatReadsAPositionRunsAfterTheMoveWhateverTheBoxOrder(string tag, string boxes, string mover, string read, bool seesNew)
    {
        var (wired, actor, clock) = await DelayedPointerRoom(executeInOrder: false);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(wired.Variables,
            new WiredVariableModule(RoomId, new DelayedPointerDirectory(_room), new MemoryWiredVariableStore(), TimeProvider.System,
                new RoomWiredBuiltinVariables(_room)));
        var engine = typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;
        engine.GetType().GetField("_effectOrderRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, new Random(0));
        _client.GetHabbo().Effects = new Plus.HabboHotel.Users.Effects.EffectsComponent(TimeProvider.System);
        ((Plus.HabboHotel.Users.Effects.EffectsComponent)_client.GetHabbo().Effects).Init(_client.GetHabbo());
        var furniRead = read == "-120";
        var target = Furni(MrTarget, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, target, 3, 3, 0, true, false, false));
        var change = tag.StartsWith('C');
        var pulses = boxes.Contains("show") ? 1 : 5;

        if (change) {
            _dpTile = (2, 1);
            AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "init" }, _dpZ++, 2, 1);
            await DpGive(DpUser, "user:" + DpUb, 0);
            _dpTile = (1, 1);
            EffectOrderFire(wired, actor, clock, "init", 2);
        }

        foreach (var box in boxes.Split(',')) {
            if (box == "move") {
                switch (mover) {
                    case "wf_act_user_to_furni":
                        await DpEffect(mover, [0], [], furni: [100], users: [0], picks: [MrTarget]);
                        break;
                    case "wf_act_furni_to_user":
                        await DpEffect(mover, [], [], furni: [100], users: [0], picks: [MrTarget]);
                        break;
                    default:
                        await DpEffect(mover, [0], [], furni: [100], users: [0], picks: [MrTarget]);
                        break;
                }
            }
            else if (box == "show") {
                await DpShow($"{tag} q=$(q)", 0);
            }
            else if (box == "give") {
                await DpEffect("wf_act_give_var", [DpUser, 0, 0, 1, 1, furniRead ? DpFurni : DpUser, 0], ["user:" + DpUb, read],
                    furni: furniRead ? [0, 101] : [0, 0], users: [0, 0], secondary: furniRead ? [MrTarget] : null);
            }
            else {
                await DpEffect("wf_act_change_var_val", [DpUser, 0, 1, 0, 0, furniRead ? DpFurni : DpUser, 0], ["user:" + DpUb, read],
                    furni: furniRead ? [0, 101] : [0, 0], users: [0, 0], secondary: furniRead ? [MrTarget] : null);
            }
        }

        if (boxes.Contains("show")) {
            await DpOutput("q", furniRead ? DpFurni : DpUser, read, furniRead ? [MrTarget] : null);
        }
        else {
            await DpShow($"{tag} q=$(q)", 5);
            await DpOutput("q", DpUser, "user:" + DpUb);
        }


        for (var firing = 0; firing < 12; firing++) {
            var start = firing % 3;

            using (RoomOwnerScope.Enter(_room)) {
                Assert.True(WiredRoomOperations.RelocateAvatar(_room, actor, start, 2, false, ignoreOccupants: true));
            }

            var shown = Assert.Single(DpFire(wired, actor, clock, pulses));
            var q = long.Parse(shown.Split('=')[1]);
            var expected = furniRead ? actor.X : seesNew ? 3 : start;
            Assert.True(expected == q, $"{tag} firing {firing}: q {q}, expected {expected}");
        }
    }
}
