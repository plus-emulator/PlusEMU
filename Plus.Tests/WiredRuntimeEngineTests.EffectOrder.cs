using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

// Without Execute in Order toggle_state and rel_mov run before match_to_sshot, and change_var_val and toggle_state run before
// show_message, whichever box is higher; with it the lower box runs first.
public sealed partial class WiredRuntimeEngineTests
{
    [Theory]
    [InlineData("wf_act_toggle_state", "wf_act_match_to_sshot", false)]
    [InlineData("wf_act_toggle_state", "wf_act_match_to_sshot", true)]
    [InlineData("wf_act_rel_mov", "wf_act_match_to_sshot", false)]
    [InlineData("wf_act_rel_mov", "wf_act_match_to_sshot", true)]
    [InlineData("wf_act_change_var_val", "wf_act_show_message", false)]
    [InlineData("wf_act_change_var_val", "wf_act_show_message", true)]
    [InlineData("wf_act_toggle_state", "wf_act_show_message", false)]
    [InlineData("wf_act_toggle_state", "wf_act_show_message", true)]
    public void DefaultOrderRunsTheObservedEarlierTypeFirstAtEitherHeight(string earlier, string later, bool laterIsLower)
    {
        for (var seed = 0; seed < 16; seed++) {
            var order = RunNamedPair(seed, laterIsLower ? later : earlier, laterIsLower ? earlier : later, inOrder: false);

            Assert.Equal([earlier, later], order);
        }
    }

    [Theory]
    [InlineData("wf_act_toggle_state", "wf_act_match_to_sshot")]
    [InlineData("wf_act_match_to_sshot", "wf_act_toggle_state")]
    [InlineData("wf_act_rel_mov", "wf_act_match_to_sshot")]
    [InlineData("wf_act_match_to_sshot", "wf_act_rel_mov")]
    [InlineData("wf_act_change_var_val", "wf_act_show_message")]
    [InlineData("wf_act_show_message", "wf_act_change_var_val")]
    [InlineData("wf_act_toggle_state", "wf_act_show_message")]
    [InlineData("wf_act_show_message", "wf_act_toggle_state")]
    public void ExecuteInOrderRunsStrictlyByHeightLowestFirst(string lower, string upper)
    {
        for (var seed = 0; seed < 16; seed++) {
            Assert.Equal([lower, upper], RunNamedPair(seed, lower, upper, inOrder: true));
        }
    }

    // The other type pairs were not separable, so they keep the per-firing permutation.
    [Theory]
    [InlineData("wf_act_toggle_state", "wf_act_rel_mov")]
    [InlineData("wf_act_change_var_val", "wf_act_rel_mov")]
    [InlineData("wf_act_match_to_sshot", "wf_act_show_message")]
    public void DefaultOrderOfUnobservedPairsStillVaries(string lower, string upper)
    {
        var firsts = new HashSet<string>();

        for (var seed = 0; seed < 16; seed++) {
            firsts.Add(RunNamedPair(seed, lower, upper, inOrder: false)[0]);
        }

        Assert.Equal(2, firsts.Count);
    }

    [Fact]
    public void DefaultPrecedenceKeepsTheOtherEffectsInTheirOrder()
    {
        for (var seed = 0; seed < 16; seed++) {
            var plain = new List<string>();
            var ranked = new List<string>();
            var f = new Fixture(effectOrderRandom: new Random(seed));
            f.Trigger();
            Named(f, "wf_act_match_to_sshot", plain);
            Named(f, "test_a", plain);
            Named(f, "wf_act_toggle_state", plain);
            Named(f, "test_b", plain);
            Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));

            var g = new Fixture(effectOrderRandom: new Random(seed));
            g.Trigger();
            Named(g, "test_x", ranked);
            Named(g, "test_a", ranked);
            Named(g, "test_y", ranked);
            Named(g, "test_b", ranked);
            Assert.True(g.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));

            Assert.True(plain.IndexOf("wf_act_toggle_state") < plain.IndexOf("wf_act_match_to_sshot"), string.Join(",", plain));
            // Only the match moves; the others keep the permutation's relative order.
            Assert.Equal(ranked.Where(x => x != "test_x").Select(x => x == "test_y" ? "wf_act_toggle_state" : x),
                plain.Where(x => x != "wf_act_match_to_sshot"));
        }
    }

    [Fact]
    public void DefaultPrecedenceDoesNotHoldBackADelayedEarlierType()
    {
        var order = new List<string>();
        var f = new Fixture(effectOrderRandom: new Random(0));
        f.Trigger();
        Named(f, "wf_act_match_to_sshot", order);
        Named(f, "wf_act_toggle_state", order, delay: 1);

        Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));
        Assert.Equal(["wf_act_match_to_sshot"], order);

        f.Advance(500);

        Assert.Equal(["wf_act_match_to_sshot", "wf_act_toggle_state"], order);
    }

    private static List<string> RunNamedPair(int seed, string lower, string upper, bool inOrder)
    {
        var order = new List<string>();
        var f = new Fixture(effectOrderRandom: new Random(seed));
        f.Trigger();
        Named(f, lower, order);
        Named(f, upper, order);

        if (inOrder) {
            f.Add(new Addon { ApplyBody = ctx => { ctx.Policy.Addons.ExecuteInOrder = true; return true; } });
        }

        Assert.True(f.DispatchAndResume(new WiredRuntimeEvent(WiredEventKind.Enter)));

        return order;
    }

    private static Box Named(Fixture f, string name, List<string> order, int delay = 0) => f.Add(new Box(WiredBoxCategory.Action)
    {
        Descriptor = new(name, WiredBoxCategory.Action, 0, "test") { Support = WiredBoxSupport.Implemented },
        Body = _ => { order.Add(name); return true; },
        Configuration = new() { Delay = delay }
    });
}
