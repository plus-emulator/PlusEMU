using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A Send Signal and one other effect in one stack without Execute in Order, and a Receive Signal stack on the antenna. The effects are saved
/// from their raw packets and the stack is fired by a real speech trigger on the room cycle.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private const uint SignalAntenna = 430, SignalSubject = 431;

    // Observed on AIR WIN63-202610201032-107964540: the receiving stack sees the results of the other effect of the sending stack, whatever the box
    // order (12 of 12 firings for each case): a Give Variable in the receiving stack copies the new variable value, the new state or
    // rotation, and its message comes after the sender's.
    [Theory]
    [InlineData("give", false)]
    [InlineData("give", true)]
    [InlineData("change", false)]
    [InlineData("change", true)]
    [InlineData("toggle", false)]
    [InlineData("toggle", true)]
    [InlineData("move", false)]
    [InlineData("move", true)]
    [InlineData("show", false)]
    [InlineData("show", true)]
    public async Task AReceivingStackSeesTheResultsOfTheOtherEffectsOfTheSendingStack(string effect, bool signalLast)
    {
        var (wired, actor, clock) = await DelayedPointerRoom(executeInOrder: false);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(wired.Variables,
            new WiredVariableModule(RoomId, new DelayedPointerDirectory(_room), new MemoryWiredVariableStore(), TimeProvider.System,
                new RoomWiredBuiltinVariables(_room)));
        var engine = typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;
        engine.GetType().GetField("_effectOrderRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, new Random(0));
        var antenna = Furni(SignalAntenna, InteractionType.None, WiredBoxType.None);
        antenna.Definition.InteractionName = "antenna";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, antenna, 3, 3, 0, true, false, false));
        var subject = Furni(SignalSubject, InteractionType.None, WiredBoxType.None);
        subject.Definition.Modes = 5;
        subject.Definition.AdjustableHeights = new List<double>();
        subject.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, subject, 2, 2, 0, true, false, false));

        if (effect is "give" or "change") {
            _dpTile = (2, 1);
            AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "init" }, _dpZ++, 2, 1);
            await DpGive(DpUser, "user:" + DpUa, 0);
            await DpGive(DpUser, "user:" + DpUb, 0);
        }

        _dpTile = (1, 1);
        var send = () => DpEffect("wf_act_send_signal", [0, 0], [], furni: [100, 100], users: [0], picks: [SignalAntenna]);
        Func<Task> other = effect switch
        {
            "give" => () => DpGive(DpUser, "user:" + DpUa, 7),
            "change" => () => DpEffect("wf_act_change_var_val", [DpUser, 1, 0, 0, 1, -10, 0], ["user:" + DpUa, "n"], furni: [0, 0], users: [0, 0]),
            "toggle" => () => DpEffect("wf_act_toggle_state", [0], [], furni: [100], users: [], picks: [SignalSubject]),
            "move" => () => DpEffect("wf_act_move_rotate", [0, 1], [], furni: [100], users: [], picks: [SignalSubject]),
            _ => () => DpShow("sender", 0)
        };

        if (signalLast) {
            await other();
            await send();
        }
        else {
            await send();
            await other();
        }

        var read = effect == "toggle" ? "-110" : "-122";

        if (effect is "toggle" or "move") {
            await DpShow("after st=$(st)", 6);
            await DpOutput("st", DpFurni, read, picks: [SignalSubject]);
        }

        _dpTile = (3, 1);
        AddSpeechBox(_dpNext++, "wf_trg_recv_signal", new() { IntParams = [0, 100], SelectedItems = [SignalAntenna] }, _dpZ++, 3, 1);

        if (effect is "give" or "change") {
            await DpGiveFrom(DpUa, 0);
            await DpShow("received q=$(q)", 4);
            await DpOutput("q", DpUser, "user:" + DpUb);
        }
        else if (effect is "toggle" or "move") {
            await DpEffect("wf_act_give_var", [DpUser, 0, 0, 1, 1, DpFurni, 0], ["user:" + DpUb, read], furni: [0, 101], users: [0, 0], secondary: [SignalSubject]);
            await DpShow("received st=$(st)", 4);
            await DpOutput("st", DpUser, "user:" + DpUb);
        }
        else {
            await DpShow("received", 0);
        }

        var seen = new HashSet<string>();

        for (var firing = 0; firing < 12; firing++) {
            if (effect is "give" or "change") {
                EffectOrderFire(wired, actor, clock, "init", 2);
            }

            var shown = EffectOrderFire(wired, actor, clock, "go", 10);

            switch (effect) {
                case "give":
                    Assert.Equal(["received q=7"], shown);
                    break;
                case "change":
                    Assert.Equal(["received q=1"], shown);
                    break;
                case "show":
                    Assert.Equal(["sender", "received"], shown);
                    break;
                default:
                    var received = Assert.Single(shown, message => message.StartsWith("received"));
                    var after = Assert.Single(shown, message => message.StartsWith("after"));
                    Assert.Equal(received["received st=".Length..], after["after st=".Length..]);
                    seen.Add(received);
                    break;
            }
        }

        if (effect is "toggle" or "move") {
            Assert.True(seen.Count > 1, "the furni changed between firings");
        }
    }
}
