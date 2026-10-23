using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The client does not filter furni picks itself: it only checks the wall flag and the furni limit. Wrong picks are refused by
/// the server on save with the wiredfurni.error.* keys.
/// </summary>
public sealed partial class WiredNativeLifecycleTests
{
    private static string? SavePicks(World f, string boxName, uint pickId, string pickInteraction, string pickItemName = "test")
    {
        var pick = f.Item(pickId);
        pick.Definition.InteractionName = pickInteraction;
        pick.Definition.ItemName = pickItemName;
        var boxItem = f.Item(1);
        boxItem.Definition.InteractionName = boxName;
        var box = f.Wired.CreateConfiguredBox(boxItem)!;
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with { PrimaryItems = [new(pick.Id, false)] };
        Assert.True(WiredNativeEditorProjection.TryCompile(boxItem.Id, box.Descriptor, native, out var runtime));

        return WiredConfigurationSave.TrySave(box, runtime, TestWiredConfigurationStore.Instance, out var error) ? null : error;
    }

    // Client text: wiredfurni.error.require_antenna_furni = "Can only select antenna furni."
    [Theory]
    [InlineData("wf_act_send_signal")]
    [InlineData("wf_act_neg_send_signal")]
    public void SendSignalRefusesAPickThatIsNotAnAntennaWithTheOfficialKey(string boxName)
    {
        Assert.Equal("wiredfurni.error.require_antenna_furni", SavePicks(new World(), boxName, 2, ""));
        Assert.Null(SavePicks(new World(), boxName, 2, "antenna", "wf_antenna2"));
    }

    // Client text: wiredfurni.error.require_counter_furni = "Can only select counter furni."
    [Theory]
    [InlineData("wf_act_control_clock")]
    [InlineData("wf_act_adjust_clock")]
    [InlineData("wf_trg_clock_counter")]
    [InlineData("wf_cnd_counter_time_matches")]
    public void CounterBoxesRefuseAPickThatIsNotACounter(string boxName)
    {
        Assert.Equal("wiredfurni.error.require_counter_furni", SavePicks(new World(), boxName, 2, "default"));
        Assert.Null(SavePicks(new World(), boxName, 2, "wf_upcounter1", "wf_upcounter1"));
        Assert.Null(SavePicks(new World(), boxName, 2, "wf_game_upcounter2", "wf_game_upcounter2"));
        Assert.Null(SavePicks(new World(), boxName, 2, "counter", "fball_counter"));
    }

    // Client text: wiredfurni.error.condition_evaluation_furni = "Can only select WIRED Conditions and Add-Ons!"
    [Fact]
    public void OrEvaluationRefusesAPickThatIsNotAConditionOrAddon()
    {
        Assert.Equal("wiredfurni.error.condition_evaluation_furni", SavePicks(new World(), "wf_xtra_or_eval", 2, "default"));
        Assert.Equal("wiredfurni.error.condition_evaluation_furni", SavePicks(new World(), "wf_xtra_or_eval", 2, "wf_act_show_message"));
        Assert.Null(SavePicks(new World(), "wf_xtra_or_eval", 2, "wf_cnd_has_furni_on"));
        Assert.Null(SavePicks(new World(), "wf_xtra_or_eval", 2, "wf_xtra_random"));
    }
}
