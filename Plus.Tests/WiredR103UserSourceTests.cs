using Xunit;
using static Plus.Tests.WiredR103EditorSave;

namespace Plus.Tests;

// A user source the box does not offer is refused with its own key.
public sealed class WiredR103UserSourceTests
{
    public static TheoryData<string, int[]> Boxes => new()
    {
        { "wf_cnd_actor_dir", [0] },
        { "wf_cnd_actor_in_team", [0] },
        { "wf_cnd_not_in_team", [0] },
        { "wf_cnd_not_trggrer_on", [] },
        { "wf_act_unfreeze", [] },
        { "wf_act_move_furni_as_group", [1, 0, 0] },
        { "wf_act_neg_send_signal", [0, 0] }
    };

    [Theory]
    [MemberData(nameof(Boxes))]
    public void AUserSourceTheBoxDoesNotOfferIsRefusedWithTheUserSourceKey(string name, int[] ints)
    {
        var refused = Save(name, ints, users: [100]);
        Assert.False(refused.Accepted);
        Assert.Equal("wiredfurni.error.invalid_user_source", refused.Key);
    }

    [Theory]
    [MemberData(nameof(Boxes))]
    public void TheUserSourcesTheBoxOffersAreStored(string name, int[] ints)
    {
        foreach (var source in new[] { 0, 200, 201, 11 }) {
            Assert.Equal([source], Saved(name, ints, users: [source]).Users);
        }
    }
}
