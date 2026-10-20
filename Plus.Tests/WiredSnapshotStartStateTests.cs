using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// A colour tile that was saved in a Wired snapshot while it still held "" now loads as "0" (FurniStartStateTests). Its saved ""
// is the same state.
public sealed class WiredSnapshotStartStateTests
{
    private static readonly WiredFurniSnapshot SavedEmpty = new(1, 9, 0, 0, 0, 0, "");

    [Fact]
    public void ASnapshotSavedWithAnEmptyStateMatchesTheReloadedTile()
    {
        var tile = Tile("");

        Assert.Equal("0", tile.LegacyDataString);
        Assert.True(WiredRoomOperations.MatchesSnapshot(tile, SavedEmpty, true, false, false, false));
        tile.LegacyDataString = "1";
        Assert.False(WiredRoomOperations.MatchesSnapshot(tile, SavedEmpty, true, false, false, false));
    }

    [Fact]
    public void TheSavedStateTriggerMatchesTheReloadedTile()
    {
        var tile = Tile("");
        var config = new WiredConfiguration { IntParams = [1], SelectedItems = [tile.Id], Snapshots = [SavedEmpty] };

        Assert.True(WiredTriggerPredicates.MatchesItem(config, tile, [tile], supportsSavedState: true));
    }

    [Fact]
    public void MatchToSnapshotRestoresAnEmptySavedStateAsZero()
    {
        var tile = Tile("1");
        var written = new List<string>();

        new WiredMovementActions().Execute("wf_act_match_to_sshot",
            new() { IntParams = [1, 0, 0, 0, 100], SelectedItems = [tile.Id], Snapshots = [SavedEmpty] }, [tile], [], [],
            (_, _, _, _, _) => false, (_, _, _, _, _) => false, (_, state) => written.Add(state));

        Assert.Equal(["0"], written);
    }

    [Theory]
    [InlineData("1", "0")]
    [InlineData("0", null)]
    public void MatchToSnapshotTogglesAnEmptySavedStateAsZero(string current, string? expected)
    {
        var tile = Tile(current);
        string? next = "unset";

        new WiredMovementActions().Execute("wf_act_match_to_sshot",
            new() { IntParams = [1, 0, 0, 0, 100], SelectedItems = [tile.Id], Snapshots = [SavedEmpty] }, [tile], [], [],
            (_, _, _, _, _) => false, (_, _, _, _, _) => false, (_, _) => { },
            (_, toggle) => { next = toggle(current); return next != null; });

        Assert.Equal(expected, next);
    }

    private static Item Tile(string stored)
    {
        var definition = new ItemDefinition
        {
            Id = 9, Type = ItemType.Floor, ItemName = "wf_colortile", InteractionName = "default", InteractionType = InteractionType.None,
            Modes = 7, Width = 1, Length = 1, VendingIds = [], AdjustableHeights = []
        };

        return new() { Id = 1, Definition = definition, ExtraData = FurniExtraData.Load(definition, stored, keepLegacy: true) };
    }
}
