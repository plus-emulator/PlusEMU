using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// A schema 1 row saved by the old direction editor of Move Furni As Group, as it is stored for item 70 on plusemu.dev.
public partial class PlacedFurniRoomTests
{
    private const uint LegacyGroupId = 70;
    private const string LegacyGroupRow = """{"Version":1,"IntParams":[3,100],"Text":"","SelectedItems":[50,49,38,130],"Delay":0,"SelectionCode":0,"ScoreQuotaPerGame":null,"TemporaryPlacement":null,"SecondarySelectedItems":[],"FurniSources":{"movers":100},"UserSources":{},"VariableIds":[],"Snapshots":[]}""";

    [Fact]
    public void LegacyDirectionGroupRowLoadsAsTheTargetEditor()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_furni_as_group", out var descriptor));

        var config = new WiredConfigurationStore(new ModernWiredRuntimeTests.StoredRuntimeRowsDatabase(
            [new(LegacyGroupId, descriptor.CanonicalName, 1, LegacyGroupRow)])).Load(LegacyGroupId, descriptor);

        // Direction 3 is one tile at (+1, +1): the picks are both movers and target, so the first pick steps from itself.
        Assert.NotNull(config?.Origin?.Native);
        Assert.Equal([0, 1, 1, 100, 100, 0], config.IntParams.ToArray());
        Assert.Equal([50u, 49, 38, 130], config.SelectedItems.ToArray());
        Assert.Empty(config.SecondarySelectedItems);
    }

    [Fact]
    public void RoomLoadKeepsTheLegacyGroupBoxAndDropsPicksThatLeftTheRoom()
    {
        var (wired, _) = PrepareSpeech(new SpeechClock());
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new WiredConfigurationStore(new ModernWiredRuntimeTests.StoredRuntimeRowsDatabase(
                [new(LegacyGroupId, "wf_act_move_furni_as_group", 1, LegacyGroupRow)])));

        foreach (var (id, x) in new (uint, int)[] { (50, 1), (49, 2), (130, 3) }) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, Furni(id, InteractionType.None, WiredBoxType.None), x, 3, 0, true, false, false));
        }

        var item = Furni(LegacyGroupId, InteractionType.None, WiredBoxType.None);
        item.Definition.InteractionName = item.Definition.ItemName = "wf_act_move_furni_as_group";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, 1, 1, 0, true, false, false));

        var loaded = Assert.IsType<WiredModernAction>(wired.LoadWiredBox(item));

        Assert.Equal([50u, 49, 130], loaded.Configuration.SelectedItems.ToArray());
        Assert.Empty(loaded.Configuration.SecondarySelectedItems);
        Assert.True(WiredNativeEditorProjection.IsBound(LegacyGroupId, loaded.Descriptor, loaded.Configuration));
    }
}
