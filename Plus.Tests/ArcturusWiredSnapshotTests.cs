using System.Text.Json;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public class ArcturusWiredSnapshotTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ImportedSkipStateKeepsStateButRestoresPosition(bool skipState, bool atomicState)
    {
        var item = new Item { Id = 7, Definition = new ItemDefinition { Id = 1, Type = ItemType.Floor, ProductType = "s" } };
        var native = new WiredNativeEditorConfiguration
        {
            Category = WiredBoxCategory.Action, NativeCode = 3, OwnedIntParams = [1, 1, 1, 0],
            FurniSourceTypes = [100], PrimaryItems = [new(7, false)], Delay = 0,
            SavedState = new() { Snapshots = [new(7, 1, 4, 5, 1.5, 2, " ") { SkipStateRestore = skipState }] }
        };
        var restored = JsonSerializer.Deserialize<WiredNativeEditorConfiguration>(JsonSerializer.Serialize(native))!;
        Assert.Equal(skipState, restored.SavedState.Snapshots[0].SkipStateRestore);
        Assert.True(WiredBoxRegistry.TryGet("wf_act_match_to_sshot", out var descriptor));
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, restored, out var configuration));
        var writes = 0;
        (int, int, int)? moved = null;
        var action = new WiredMovementActions();
        Assert.True(action.Execute("wf_act_match_to_sshot", configuration, [item], [], [],
            (_, x, y, rotation, _) => { moved = (x, y, rotation); return true; },
            (_, _, _, _, _) => false,
            (_, _) => writes++, atomicState ? (_, change) => { Assert.Equal(" ", change("2")); writes++; return true; } : null));
        Assert.Equal(skipState ? 0 : 1, writes);
        Assert.Equal((4, 5, 2), moved);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SkipStateAlsoAppliesToSingleSnapshotFallback(bool skipState)
    {
        var item = new Item { Id = 8, Definition = new ItemDefinition { Id = 1, Type = ItemType.Floor, ProductType = "s" } };
        var native = new WiredNativeEditorConfiguration
        {
            Category = WiredBoxCategory.Action, NativeCode = 3, OwnedIntParams = [1, 0, 0, 0],
            FurniSourceTypes = [100], PrimaryItems = [new(8, false)], Delay = 0,
            SavedState = new() { Snapshots = [new(7, 1, 4, 5, 1.5, 2, " ") { SkipStateRestore = skipState }] }
        };
        Assert.True(WiredBoxRegistry.TryGet("wf_act_match_to_sshot", out var descriptor));
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out var configuration));
        var writes = 0;
        var changed = new WiredMovementActions().Execute("wf_act_match_to_sshot", configuration, [item], [], [],
            (_, _, _, _, _) => throw new InvalidOperationException("No position snapshot exists for this item"),
            (_, _, _, _, _) => false, (_, _) => writes++);
        Assert.Equal(!skipState, changed);
        Assert.Equal(skipState ? 0 : 1, writes);
    }
}
