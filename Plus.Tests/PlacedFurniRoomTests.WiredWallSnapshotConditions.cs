using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// Observed on hhus (AIR WIN63-202610131318-860371703): a wall torch picked in match_snapshot / not_match_snap with state matching
/// passes / fails at its saved state, and fails / passes after a Wired toggle changed it.
/// </summary>
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("wf_cnd_match_snapshot", true)]
    [InlineData("wf_cnd_not_match_snap", false)]
    public void SnapshotConditionsCaptureAndCompareWallState(string name, bool passesAtSavedState)
    {
        WallSnapshotInstallStore();
        var wall = WallSnapshotItem(301, ":w=5,0 l=8,40 r");
        wall.ExtraData = new LegacyDataFormat { Data = "0" };
        _room.GetRoomItemHandler().LoadFurniture([wall]);
        var condition = WallSnapshotCondition(name);

        Assert.True(WiredNativeTestSupport.TrySavePrepared(condition, new() { IntParams = [1, 0, 0, 0, 100, 0], SelectedItems = [wall.Id] },
            TestWiredConfigurationStore.Instance, out var error), error);
        var snapshot = Assert.Single(condition.Configuration.Snapshots);
        Assert.Equal((wall.Id, "0"), (snapshot.ItemId, snapshot.State));
        Assert.Equal(new WiredWallSnapshot(5, 0, 8, 40, false, null) { CapturedAltitudeHundredths = snapshot.Wall!.CapturedAltitudeHundredths }, snapshot.Wall);

        Assert.Equal(passesAtSavedState, condition.Execute(WallSnapshotContext()));
        wall.LegacyDataString = "1";
        Assert.Equal(!passesAtSavedState, condition.Execute(WallSnapshotContext()));
    }

    [Fact]
    public void WallSnapshotConditionComparesPositionAndIgnoresDirection()
    {
        WallSnapshotInstallStore();
        var wall = WallSnapshotItem(301, ":w=5,0 l=8,40 r");
        _room.GetRoomItemHandler().LoadFurniture([wall]);
        var condition = WallSnapshotCondition("wf_cnd_match_snapshot");

        Assert.True(WiredNativeTestSupport.TrySavePrepared(condition, new() { IntParams = [0, 1, 1, 0, 100, 0], SelectedItems = [wall.Id] },
            TestWiredConfigurationStore.Instance, out var error), error);
        Assert.True(condition.Execute(WallSnapshotContext()));
        wall.WallCoordinates = ":w=5,0 l=8,60 r";
        Assert.True(condition.Execute(WallSnapshotContext()));
        wall.WallCoordinates = ":w=5,0 l=9,40 r";
        Assert.False(condition.Execute(WallSnapshotContext()));
        wall.WallCoordinates = ":w=5,0 l=8,40 l";
        Assert.False(condition.Execute(WallSnapshotContext()));
    }

    private WiredModernCondition WallSnapshotCondition(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        var item = Furni(401, InteractionType.WiredCondition, name == "wf_cnd_match_snapshot"
            ? WiredBoxType.ConditionMatchStateAndPosition : WiredBoxType.ConditionDontMatchStateAndPosition);
        item.RoomId = RoomId;
        typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, _room);

        return Assert.IsType<WiredModernCondition>(_room.GetWired().CreateConfiguredBox(item, descriptor));
    }
}
