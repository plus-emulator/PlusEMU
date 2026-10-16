using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Room 14's Fast Queue as stored in wired_item_configurations (native v2 rows): a 200 ms repeater moves six colour tiles
    // east with carry users, and a signal restores them from their snapshot in the same pulse. Observed on hhus (R95-FQ): each
    // pulse is one frame where every tile goes home to home and the rider is carried one tile.
    private static (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[] Room14FastQueueRows() =>
    [
        (91, 4, 11, 0.0, 0, "wf_trg_period_short", "native:" + """{"Version":2,"Category":0,"NativeCode":19,"OwnedIntParams":[4],"Text":"","PrimaryItems":[],"SecondaryItems":[],"FurniSourceTypes":[],"UserSourceTypes":[],"VariableIds":[],"Delay":null,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""),
        (106, 5, 11, 0.0, 0, "wf_trg_recv_signal", "native:" + """{"Version":2,"Category":0,"NativeCode":17,"OwnedIntParams":[],"Text":"","PrimaryItems":[{"ItemId":113,"Wall":false}],"SecondaryItems":[],"FurniSourceTypes":[100],"UserSourceTypes":[],"VariableIds":[],"Delay":null,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""),
        (107, 4, 11, 0.65, 2, "wf_act_move_to_dir", "native:" + """{"Version":2,"Category":1,"NativeCode":13,"OwnedIntParams":[2,0,1],"Text":"","PrimaryItems":[{"ItemId":115,"Wall":false},{"ItemId":114,"Wall":false},{"ItemId":116,"Wall":false},{"ItemId":117,"Wall":false},{"ItemId":118,"Wall":false},{"ItemId":119,"Wall":false}],"SecondaryItems":[],"FurniSourceTypes":[100],"UserSourceTypes":[],"VariableIds":[],"Delay":0,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""),
        (108, 4, 11, 1.3, 0, "wf_act_send_signal", "native:" + """{"Version":2,"Category":1,"NativeCode":30,"OwnedIntParams":[0,0],"Text":"","PrimaryItems":[{"ItemId":113,"Wall":false}],"SecondaryItems":[],"FurniSourceTypes":[100,200],"UserSourceTypes":[200],"VariableIds":[],"Delay":0,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""),
        (109, 5, 11, 0.65, 2, "wf_act_match_to_sshot", "native:" + """{"Version":2,"Category":1,"NativeCode":3,"OwnedIntParams":[0,0,1,1],"Text":"","PrimaryItems":[{"ItemId":115,"Wall":false},{"ItemId":114,"Wall":false},{"ItemId":116,"Wall":false},{"ItemId":117,"Wall":false},{"ItemId":118,"Wall":false},{"ItemId":119,"Wall":false}],"SecondaryItems":[],"FurniSourceTypes":[100],"UserSourceTypes":[],"VariableIds":[],"Delay":0,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[{"ItemId":115,"DefinitionId":29284,"X":4,"Y":13,"Z":0,"Rotation":0,"State":""},{"ItemId":114,"DefinitionId":29284,"X":5,"Y":13,"Z":0,"Rotation":0,"State":""},{"ItemId":116,"DefinitionId":29284,"X":6,"Y":13,"Z":0,"Rotation":0,"State":""},{"ItemId":117,"DefinitionId":29284,"X":7,"Y":13,"Z":0,"Rotation":0,"State":""},{"ItemId":118,"DefinitionId":29284,"X":8,"Y":13,"Z":0,"Rotation":0,"State":""},{"ItemId":119,"DefinitionId":29284,"X":9,"Y":13,"Z":0,"Rotation":0,"State":""}]}}"""),
        (110, 4, 11, 1.95, 0, "wf_xtra_mov_physics", "native:" + """{"Version":2,"Category":4,"NativeCode":7,"OwnedIntParams":[1,0,1,0],"Text":"","PrimaryItems":[],"SecondaryItems":[],"FurniSourceTypes":[0,0],"UserSourceTypes":[900],"VariableIds":[],"Delay":null,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""),
        (111, 4, 11, 2.32, 0, "wf_xtra_mov_carry_users", null),
        (112, 4, 11, 2.69, 0, "wf_xtra_anim_time", "native:" + """{"Version":2,"Category":4,"NativeCode":9,"OwnedIntParams":[200],"Text":"","PrimaryItems":[],"SecondaryItems":[],"FurniSourceTypes":[],"UserSourceTypes":[],"VariableIds":[],"Delay":null,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""),
        (113, 5, 11, 1.67, 2, "wf_antenna2", null),
        (114, 5, 13, 0.0, 0, "wf_colortile", null),
        (115, 4, 13, 0.0, 0, "wf_colortile", null),
        (116, 6, 13, 0.0, 0, "wf_colortile", null),
        (117, 7, 13, 0.0, 0, "wf_colortile", null),
        (118, 8, 13, 0.0, 0, "wf_colortile", null),
        (119, 9, 13, 0.0, 0, "wf_colortile", null),
        (120, 10, 13, 0.0, 0, "wf_colortile", null),
        (121, 5, 11, 1.3, 0, "wf_xtra_mov_physics", "native:" + """{"Version":2,"Category":4,"NativeCode":7,"OwnedIntParams":[0,1,1,0],"Text":"","PrimaryItems":[],"SecondaryItems":[],"FurniSourceTypes":[0,0],"UserSourceTypes":[900],"VariableIds":[],"Delay":null,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""),
    ];

    // Observed on hhus (R95-FQ): one frame per pulse wherever the rider stands. A rider on a middle tile has a moved tile land on
    // it before its own tile carries it; that pulse must still be one frame with the rider entry last.
    [Theory]
    [InlineData(4, 12, 4)]
    [InlineData(5, 12, 5)]
    [InlineData(7, 12, 7)]
    [InlineData(9, 12, 9)]
    [InlineData(4, 13, 7)]
    public void Room14FastQueueCarriesTheRiderWhileTheTilesStayHome(int fromX, int fromY, int x)
    {
        RunRoom14Queue(fromX, fromY, x, Room14FastQueueRows());
    }

    // Room 14 also holds an idle, unconfigured Change Variable Value box. It must not split a carried pulse into one packet per
    // tile: hhus R95-FQ shows the forward step, the carry and the snapshot reset as one frame.
    // Room 14 also holds an idle, unconfigured Change Variable Value box. It must not split a carried pulse into one packet per
    // tile: hhus R95-FQ shows the forward step, the carry and the snapshot reset as one frame.
    [Theory]
    [InlineData(4, 12, 4)]
    [InlineData(7, 12, 7)]
    [InlineData(4, 13, 7)]
    public void Room14WholeRoomCarriesTheRiderWhileTheTilesStayHome(int fromX, int fromY, int x)
    {
        RunRoom14Queue(fromX, fromY, x, [.. Room14FastQueueRows(), .. Room14OtherRows()]);
    }

    private static void RunRoom14Queue(int fromX, int fromY, int x, (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[] layout)
    {
        // Live wf_colortile stacks 0.2 high.
        var f = new Fixture(layout, live: true, colourTileHeight: 0.2);
        var rider = f.Walker(1, fromX, fromY, x, 13);
        Assert.Equal((x, 13), (rider.X, rider.Y));
        var packets = CapturePublication(rider);
        var homes = Enumerable.Range(114, 7).ToDictionary(id => (uint)id, id => (f.Items[(uint)id].GetX, f.Items[(uint)id].GetY));

        for (var pulse = 1; x + pulse <= 10; pulse++) {
            packets.Clear();
            f.Advance(pulse == 1 ? 250 : 200);

            var group = Assert.Single(packets.Where(IsMovementBody).Select(bytes => ReadPublication([bytes]).ToArray()));
            var furniture = group.Where(move => move.Type == 1).ToArray();
            Assert.Equal(6, furniture.Length);
            Assert.All(furniture, move => Assert.Equal((move.FromX, 13), (move.ToX, move.ToY)));
            var carry = Assert.Single(group, move => move.Type == 0);
            Assert.Equal((x + pulse - 1, x + pulse), (carry.FromX, carry.ToX));
            Assert.Equal(0, group[^1].Type);
            Assert.Equal((x + pulse, 13), (rider.X, rider.Y));
            Assert.All(homes, home => Assert.Equal(home.Value, (f.Items[home.Key].GetX, f.Items[home.Key].GetY)));
        }
    }
}
