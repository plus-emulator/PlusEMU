using Xunit;

namespace Plus.Tests;

// A condition save with a quantifier outside 0..2 is stored as 0, and 2 is kept, instead of being dropped or refused.
// Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredSaveLeniencyQuantifierTests
{
    private const string Boxes = "wf_cnd_actor_in_group,wf_cnd_not_in_group,wf_cnd_slc_quantity,wf_cnd_stuff_is,wf_cnd_not_stuff_is,wf_cnd_team_has_rank,"
        + "wf_cnd_team_has_score,wf_cnd_time_less_than,wf_cnd_time_more_than,wf_cnd_trggrer_on_frn,wf_cnd_has_handitem,wf_cnd_not_has_handitem";

    public static TheoryData<string> Conditions => [.. Boxes.Split(',')];

    [Theory]
    [MemberData(nameof(Conditions))]
    public void AQuantifierOutsideZeroToTwoIsStoredAsZero(string name)
    {
        foreach (var quantifier in new[] { -1, 3, 5, 100, int.MinValue, int.MaxValue }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(quantifier: 1);
            Assert.True(result.Accepted, $"{name} q=1: {result}");

            result = save.Save(quantifier: quantifier);

            Assert.True(result.Accepted, $"{name} q={quantifier}: {result}");
            Assert.Equal(0, result.Quantifier);
        }
    }

    [Theory]
    [MemberData(nameof(Conditions))]
    public void AQuantifierOfZeroOneOrTwoIsStoredAsSent(string name)
    {
        foreach (var quantifier in new[] { 0, 1, 2 }) {
            using var save = new WiredRawSave(name);
            var result = save.Save(quantifier: quantifier);

            Assert.True(result.Accepted, $"{name} q={quantifier}: {result}");
            Assert.Equal(quantifier, result.Quantifier);
        }
    }
}
