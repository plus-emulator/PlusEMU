using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: each Variable Value Match save below was accepted, reopened with its ints unchanged and
// an empty variable slot as "0", and then passed or blocked as listed. Comparisons follow the editor's order: 0 below, 1 equal, 2 above,
// 3 at most, 4 not equal, 5 at least; the probe showed 1, 3 and 5 on a state of 0. @index is the first user variable (-200) and @furni_count the first global one (-300).
public sealed partial class WiredGlideConveyorTests
{
    private const string UserVariable = "-200", GlobalVariable = "-300";

    public static TheoryData<string, int[], string, bool> ValueMatchShapes => new()
    {
        { "furni = 0", [0, 1, 0, 0, 0, -10, 0], "-110,n", true },
        { "furni = 5", [0, 1, 0, 0, 5, -10, 0], "-110,n", false },
        { "furni at most 0", [0, 3, 0, 0, 0, -10, 0], "-110,n", true },
        { "furni at most 5", [0, 3, 0, 0, 5, -10, 0], "-110,n", true },
        { "furni at least 0", [0, 5, 0, 0, 0, -10, 0], "-110,n", true },
        { "furni at least 5", [0, 5, 0, 0, 5, -10, 0], "-110,n", false },
        { "at most, no variables", [0, 3, 0, 0, 0, -10, 0], "n,n", false },
        { "at least, no variables", [0, 5, 0, 0, 0, -10, 0], "n,n", false },
        { "reference without same holder", [0, 1, 1, 0, 0, 0, 0], "-110,-110", false },
        { "reference, empty left", [0, 1, 1, 0, 0, 0, 0], "n,-110", false },
        { "reference not equal, empty left", [0, 4, 1, 0, 0, 0, 0], "n,-110", false },
        { "reference, both empty", [0, 1, 1, 0, 0, 0, 0], "n,n", false },
        { "same holder", [0, 1, 1, 0, 0, 0, 1], "-110,-110", true },
        { "same holder, empty reference", [0, 1, 1, 0, 0, 0, 1], "-110,n", false },
        { "same holder, empty left", [0, 1, 1, 0, 0, 0, 1], "n,-110", false },
        { "same holder with a value, both empty", [0, 1, 0, 0, 0, 0, 1], "n,n", false },
        { "user not equal", [1, 4, 0, 0, 77777, -10, 0], UserVariable + ",n", true },
        { "user = 0, no variables", [1, 1, 0, 0, 0, -10, 0], "n,n", false },
        { "user not equal, no variables", [1, 4, 0, 0, 77777, -10, 0], "n,n", false },
        { "user not equal, no variable list", [1, 4, 0, 0, 77777, -10, 0], "", false },
        { "user reference, empty reference", [1, 4, 1, 0, 0, 1, 0], UserVariable + ",n", false },
        { "user reference, empty left", [1, 4, 1, 0, 0, 1, 0], "n," + UserVariable, false },
        { "user same holder", [1, 1, 1, 0, 0, 1, 1], UserVariable + "," + UserVariable, true },
        { "global not equal", [-10, 4, 0, 0, 77777, -10, 0], GlobalVariable + ",n", true },
        { "global not equal, no variables", [-10, 4, 0, 0, 77777, -10, 0], "n,n", false },
        { "global not equal, no variable list", [-10, 4, 0, 0, 77777, -10, 0], "", false },
        { "global reference, empty reference", [-10, 4, 1, 0, 0, -10, 0], GlobalVariable + ",n", false },
        { "global reference, empty left", [-10, 4, 1, 0, 0, -10, 0], "n," + GlobalVariable, false },
        { "global same holder", [-10, 1, 1, 0, 0, -10, 1], GlobalVariable + "," + GlobalVariable, true },
    };

    [Theory]
    [MemberData(nameof(ValueMatchShapes))]
    public void AValueMatchSavedLikeOnHabboReopensAndPassesOrBlocksAsOnHabbo(string shape, int[] ints, string variables, bool passes)
    {
        var (f, owner, frames) = MatchStackWithToggle();

        SaveMatch(f, owner, frames, ints, variables.Length == 0 ? [] : variables.Split(','));

        Assert.True(SaveAnswer(frames) == "ok", $"{shape}: {SaveAnswer(frames)}");
        var reopened = Reopen(f, owner, frames, MatchCondition, ServerPacketHeader.WiredConditionConfigComposer);
        Assert.Equal(ints, reopened.Ints);
        Assert.Equal(variables.Length == 0 ? ["0", "0"] : variables.Split(',').Select(token => token == "n" ? "0" : token), reopened.Variables);
        Assert.Equal(passes ? "1" : "0", Fire(f, owner));
    }
}
