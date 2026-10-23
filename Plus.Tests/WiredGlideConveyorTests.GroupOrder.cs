using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Picks (6,12) then (7,12) moved east as a group are listed in that order in one WiredMovements, though the leading (7,12)
    // member lands first.
    [Fact]
    public void AGroupMoveListsItsMembersInPickOrder()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_furni_as_group", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [0, 0, 0],
            PrimaryItems = [new(80, false), new(81, false)],
            SecondaryItems = [new(82, false)],
            FurniSourceTypes = [100, 101],
            UserSourceTypes = [0],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (80, 6, 12, 0.0, 0, "bc_tile_1", null),
            (81, 7, 12, 0.0, 0, "bc_tile_1", null),
            (82, 9, 12, 0.0, 0, "bc_tile_1", null),
            (83, 4, 14, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "grp" })),
            (84, 4, 14, 0.65, 0, "wf_act_move_furni_as_group", "native:" + JsonSerializer.Serialize(native))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(1000);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "grp", false));
        f.Advance(200);

        var moves = ReadPublication(packets).Where(move => move.Type == 0 || move.Type == 1).Select(move => (move.FromX, move.ToX)).ToArray();
        Assert.Equal([(6, 9), (7, 10)], moves);
    }
}
