using System.Reflection;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// Two Change Variable Value boxes on one user variable in one stack without Execute in Order, each saved from its raw packet and fired by
/// a real speech trigger on the room cycle. The variable starts at 5 before every firing.
/// </summary>
public partial class PlacedFurniRoomTests
{
    // Observed on AIR WIN63-202610201032-107964540: the boxes run by operation, whatever the box order (12 of 12 firings for each pair and
    // box order): Assign, Power, Multiply, Divide, Modulo, Add and Subtract, then Minimum and Maximum. Operations are the editor's codes
    // 0 assign, 1 add, 2 subtract, 3 multiply, 4 divide, 5 power, 6 modulo, 40 minimum, 41 maximum. Pairs that give the same value in both
    // orders, or a random one in Habbo (two Assign boxes, Minimum with Maximum), are not listed.
    public static TheoryData<int, int, int, int, int, bool> ChangeOperationPairs()
    {
        (int, int, int, int, int)[] pairs =
        [
            (0, 10, 1, 1, 11), (0, 10, 3, 3, 30), (0, 10, 2, 1, 9), (0, 10, 5, 2, 100), (0, 10, 4, 2, 5), (0, 10, 6, 3, 1), (1, 1, 3, 3, 16),
            (2, 1, 3, 3, 14), (3, 3, 4, 2, 7), (1, 1, 5, 2, 26), (1, 1, 6, 3, 3), (3, 3, 5, 2, 75), (3, 3, 6, 7, 1), (4, 2, 5, 2, 12),
            (4, 2, 6, 3, 2), (5, 2, 6, 3, 1), (4, 2, 1, 3, 5), (4, 2, 2, 1, 1), (2, 1, 5, 2, 24), (2, 3, 6, 3, -1), (1, 5, 41, 8, 8),
            (3, 3, 41, 8, 8), (0, 20, 41, 8, 8), (2, 5, 40, 3, 3), (0, 1, 40, 3, 3), (4, 2, 40, 3, 3)
        ];
        var data = new TheoryData<int, int, int, int, int, bool>();

        foreach (var (first, firstOperand, second, secondOperand, expected) in pairs) {
            data.Add(first, firstOperand, second, secondOperand, expected, false);
            data.Add(first, firstOperand, second, secondOperand, expected, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ChangeOperationPairs))]
    public async Task TwoChangeVariableBoxesRunByOperationWhateverTheBoxOrder(int firstOperation, int firstOperand, int secondOperation, int secondOperand,
        int expected, bool swapped)
    {
        var (wired, actor, clock) = await DelayedPointerRoom(executeInOrder: false);
        var engine = typeof(WiredComponent).GetField("_engine", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;
        engine.GetType().GetField("_effectOrderRandom", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(engine, new Random(0));
        _dpTile = (2, 1);
        AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "init" }, _dpZ++, 2, 1);
        await DpGive(DpUser, "user:" + DpUa, 5);
        _dpTile = (1, 1);
        (int Operation, int Operand)[] boxes = swapped
            ? [(secondOperation, secondOperand), (firstOperation, firstOperand)]
            : [(firstOperation, firstOperand), (secondOperation, secondOperand)];

        foreach (var (operation, operand) in boxes) {
            await DpEffect("wf_act_change_var_val", [DpUser, operation, 0, 0, operand, -10, 0], ["user:" + DpUa, "n"], furni: [0, 0], users: [0, 0]);
        }

        await DpShow("q=$(q)", 4);
        await DpOutput("q", DpUser, "user:" + DpUa);

        for (var firing = 0; firing < 12; firing++) {
            EffectOrderFire(wired, actor, clock, "init", 2);
            Assert.Equal([$"q={expected}"], EffectOrderFire(wired, actor, clock, "go", 10));
        }
    }
}
