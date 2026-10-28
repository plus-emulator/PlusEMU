using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A condition in a stack started by a Send Signal reads a user pointer like the variable it points at, with no delay. A pointer whose
/// accessor was never set is "nothing": Has Variable fails, Not Has passes, and a value match fails for every comparison. Every box is saved
/// from its raw packet and a real speech trigger fires the stack on the room cycle.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private const int CompareEqual = 1, CompareNotEqual = 4;

    [Theory]
    [InlineData("has", true, true)]
    [InlineData("not_has", true, false)]
    [InlineData("eq7", true, true)]
    [InlineData("neq7", true, false)]
    [InlineData("has", false, false)]
    [InlineData("not_has", false, true)]
    [InlineData("eq0", false, false)]
    [InlineData("neq0", false, false)]
    [InlineData("pu_b_eq_pointer", true, true)]
    [InlineData("pu_b_eq_pointer", false, false)]
    [InlineData("pu_b_eq_pu_a", true, true)]
    public async Task AConditionReadsAUserPointerLikeThePointedVariable(string shape, bool accessorSet, bool passes)
    {
        var (wired, actor, clock) = await SignalledPointerRoom(accessorSet, () => DpGive(DpUser, "user:" + DpUb, 7));

        switch (shape) {
            case "has":
            case "not_has":
                await DpCondition(shape == "has" ? "wf_cnd_has_var" : "wf_cnd_neg_has_var", [DpUser], ["user:" + DpPu]);
                break;
            case "pu_b_eq_pointer":
            case "pu_b_eq_pu_a":
                await DpCondition("wf_cnd_var_val_match", [DpUser, CompareEqual, 1, 0, 0, DpUser, 0],
                    ["user:" + DpUb, "user:" + (shape == "pu_b_eq_pointer" ? DpPu : DpUa)], furni: [0, 0], users: [0, 0]);
                break;
            default:
                var comparison = shape.StartsWith("neq", StringComparison.Ordinal) ? CompareNotEqual : CompareEqual;
                var literal = shape.EndsWith('7') ? 7 : 0;
                await DpCondition("wf_cnd_var_val_match", [DpUser, comparison, 0, 0, literal, -10, 0], ["user:" + DpPu, "n"], furni: [0, 0], users: [0, 0]);
                break;
        }

        await DpShow("yes", 0);

        Assert.Equal(passes ? ["yes"] : [], DpFire(wired, actor, clock, 2));
    }

    private Task DpCondition(string name, int[] ints, string[] variables, int[]? furni = null, int[]? users = null) =>
        DpSave(WiredBoxCategory.Condition, name, ints, "", [], 0, furni ?? [0], users ?? [0], variables, []);
}
