using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// Two effects of one stack without Execute in Order, each box saved from its raw packet and fired by a real speech trigger on the
/// room cycle. Observed on AIR WIN63-202610201032-107964540: the order of the boxes does not matter for the pairs below, in 12 of 12
/// firings for each box order, and Execute in Order still follows the box order.
/// </summary>
public partial class PlacedFurniRoomTests
{
    // Give Variable and Change Variable Value: the Give runs first (GCa and GCb print q=6, never q=5).
    // Give Variable and Remove Variable: the Give runs first (GRa and GRb print q=null).
    // Give Variable and Show Message: the Give runs first (Gsa and Gsb print q=5). With Execute in Order and the Show first
    // (Gsctl) the message prints q=null.
    // Remove Variable and Show Message: the Remove runs first (Rsa and Rsb print q=null).
    // Change Variable Value and Show Message: the Change runs first (Csa and Csb print q=11).
    [Theory]
    [InlineData("GCa", "give,change", false, 4, "GCa q=6")]
    [InlineData("GCb", "change,give", false, 4, "GCb q=6")]
    [InlineData("GRa", "give,remove", false, 4, "GRa q=null")]
    [InlineData("GRb", "remove,give", false, 4, "GRb q=null")]
    [InlineData("Gsa", "give,show", false, 0, "Gsa q=5")]
    [InlineData("Gsb", "show,give", false, 0, "Gsb q=5")]
    [InlineData("Gsctl", "show,give", true, 0, "Gsctl q=null")]
    [InlineData("Csa", "change,show", false, 0, "Csa q=11")]
    [InlineData("Csb", "show,change", false, 0, "Csb q=11")]
    [InlineData("Rsa", "remove,show", false, 0, "Rsa q=null")]
    [InlineData("Rsb", "show,remove", false, 0, "Rsb q=null")]
    public async Task TwoVariableEffectsRunInHabbosOrderWhateverTheBoxOrder(string tag, string boxes, bool executeInOrder, int showDelay,
        string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom(executeInOrder);
        var engine = typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;
        engine.GetType().GetField("_effectOrderRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, new Random(0));
        var startsAtTen = tag.StartsWith('C') || tag.StartsWith('R');

        if (startsAtTen) {
            _dpTile = (2, 1);
            AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "init" }, _dpZ++, 2, 1);
            await DpGive(DpUser, "user:" + DpUa, 10);
            _dpTile = (1, 1);
        }

        foreach (var box in boxes.Split(',')) {
            switch (box) {
                case "give":
                    await DpGive(DpUser, "user:" + DpUa, 5);
                    break;
                case "change":
                    await DpEffect("wf_act_change_var_val", [DpUser, 1, 0, 0, 1, -10, 0], ["user:" + DpUa, "n"], furni: [0, 0], users: [0, 0]);
                    break;
                case "remove":
                    await DpEffect("wf_act_remove_var", [DpUser], ["user:" + DpUa], furni: [0], users: [0]);
                    break;
                default:
                    await DpShow($"{tag} q=$(q)", showDelay);
                    break;
            }
        }

        if (!boxes.Contains("show")) {
            await DpShow($"{tag} q=$(q)", showDelay);
        }

        await DpOutput("q", DpUser, "user:" + DpUa);

        if (startsAtTen) {
            await DpGive(DpUser, "user:" + DpUa, 10, delay: 8);
            EffectOrderFire(wired, actor, clock, "init", 2);
        }
        else if (!boxes.Contains("remove")) {
            await DpEffect("wf_act_remove_var", [DpUser], ["user:" + DpUa], furni: [0], users: [0], delay: 8);
        }

        var shown = new List<string>();

        for (var firing = 0; firing < 16; firing++) {
            shown.AddRange(EffectOrderFire(wired, actor, clock, "go", 10));
        }

        Assert.Equal(Enumerable.Repeat(expected, 16), shown);
    }

    private string[] EffectOrderFire(WiredComponent wired, RoomUser actor, SpeechClock clock, string say, int pulses)
    {
        wired.FlushFlashes();
        _client.Packets.Clear();
        actor.OnChat(0, say, false);

        for (var elapsed = 50; elapsed <= pulses * 500; elapsed += 50) {
            clock.Now = clock.Now.AddMilliseconds(50);
            wired.OnFastCycle();
        }

        return SpeechMessages(ServerPacketHeader.WhisperComposer);
    }
}
