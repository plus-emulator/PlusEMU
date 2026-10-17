using System.Reflection;
using System.Text.Json;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Xunit;

namespace Plus.Tests;

// Schema 1 rows as they are stored on plusemu.dev, saved as runtime configurations before native editor records.
public partial class PlacedFurniRoomTests
{
    private const uint LegacyBoxId = 70;

    private static string LegacyRow(string ints, string text, string selected, int delay, string furni, string users) =>
        $$"""{"Version":1,"IntParams":[{{ints}}],"Text":"{{text}}","SelectedItems":[{{selected}}],"Delay":{{delay}},"SelectionCode":0,"ScoreQuotaPerGame":null,"TemporaryPlacement":null,"SecondarySelectedItems":[],"FurniSources":{{{furni}}},"UserSources":{{{users}}},"VariableIds":[],"Snapshots":[]}""";

    // Rows already in today's runtime layout load with every field unchanged.
    [Theory]
    [InlineData("wf_act_show_message", "0,0,202,-1", "plus emulator", "", 0, "", "\"users\":0")]
    [InlineData("wf_act_teleport_to", "0,0,0", "", "22", 3, "\"targets\":0", "\"users\":0")]
    [InlineData("wf_act_teleport_to", "1,100,0", "", "179,254,256,255", 0, "\"targets\":100", "\"users\":0")]
    [InlineData("wf_xtra_random", "1,0", "", "", 0, "", "")]
    [InlineData("wf_cnd_trggrer_on_frn", "0,0,0", "", "22", 0, "\"items\":0", "\"users\":0")]
    [InlineData("wf_trg_enter_room", "", "", "", 0, "", "")]
    [InlineData("wf_trg_says_something", "0,1,0", "test", "", 0, "", "")]
    public void SchemaOneRowInTodaysLayoutLoadsUnchanged(string name, string ints, string text, string selected, int delay, string furni, string users)
    {
        var json = LegacyRow(ints, text, selected, delay, furni, users);
        var legacy = JsonSerializer.Deserialize<WiredConfiguration>(json)!;

        var loaded = LoadLegacyBox(name, json, legacy.SelectedItems);

        Assert.Equal(legacy.IntParams.ToArray(), loaded.Configuration.IntParams.ToArray());
        Assert.Equal(legacy.Text, loaded.Configuration.Text);
        Assert.Equal(legacy.SelectedItems.ToArray(), loaded.Configuration.SelectedItems.ToArray());
        Assert.Equal(legacy.Delay, loaded.Configuration.Delay);
        Assert.Equal(legacy.FurniSources.OrderBy(pair => pair.Key), loaded.Configuration.FurniSources.OrderBy(pair => pair.Key));
        Assert.Equal(legacy.UserSources.OrderBy(pair => pair.Key), loaded.Configuration.UserSources.OrderBy(pair => pair.Key));
    }

    // The walk trigger once took source 0; its editor offers picked (100) and selector (200) furni, so it opens on its picks as Habbo shows it.
    [Fact]
    public void SchemaOneWalkRowWithAnUnofferedSourceUsesItsPicks()
    {
        var loaded = LoadLegacyBox("wf_trg_walks_on_furni", LegacyRow("0", "", "22", 0, "\"items\":0", ""), [22]);

        Assert.Equal([100], loaded.Configuration.IntParams.ToArray());
        Assert.Equal(100, loaded.Configuration.FurniSources["items"]);
        Assert.Equal([22u], loaded.Configuration.SelectedItems.ToArray());
    }

    // The old log editor also stored a user source; the log has none today, so only the level is kept.
    [Fact]
    public void SchemaOneLogRowKeepsItsLevelAndText()
    {
        var loaded = LoadLegacyBox("wf_act_log", LegacyRow("1,0", "Plus emulator: test", "", 0, "", "\"users\":0"), []);

        Assert.Equal([1], loaded.Configuration.IntParams.ToArray());
        Assert.Equal("Plus emulator: test", loaded.Configuration.Text);
        Assert.Empty(loaded.Configuration.UserSources);
    }

    // The old neighborhood stored its tiles as offset pairs; today they are the editor's spiral bitmap around the same anchor.
    [Fact]
    public void SchemaOneNeighborhoodRowKeepsItsAnchorAndTiles()
    {
        int[] old = [4, 0, 0, 0, 0, 16, -1, 0, -1, 1, -1, 2, 0, 2, 1, 2, 1, 1, 2, 1, 2, 0, 1, 0, 1, -1, 0, -1, 0, 0, 0, 1, -1, -1, 2, -1, 2, 2];

        var loaded = LoadLegacyBox("wf_slc_users_neighborhood", LegacyRow(string.Join(',', old), "", "41", 0, "", ""), [41]);

        var p = loaded.Configuration.IntParams;
        Assert.Equal(17, p.Length);
        Assert.Equal([0, 0, 0], p[..3].ToArray());
        Assert.Equal(Enumerable.Range(0, 16).Select(i => (old[6 + i * 2], old[7 + i * 2])).Order(), WiredSelectorModule.NeighborhoodOffsets(p).Order());
        Assert.Equal(100, loaded.Configuration.FurniSources["anchor"]);
        Assert.Equal([41u], loaded.Configuration.SelectedItems.ToArray());
    }

    // The old direction editor of Move Furni As Group becomes the target editor with the movers as their own target. Item 38 has left the
    // room and drops out of the selection.
    [Fact]
    public void SchemaOneDirectionGroupRowLoadsAsTheTargetEditor()
    {
        var loaded = LoadLegacyBox("wf_act_move_furni_as_group", LegacyRow("3,100", "", "50,49,38,130", 0, "\"movers\":100", ""), [50, 49, 130]);

        // Direction 3 is one tile at (+1, +1).
        Assert.Equal([0, 1, 1, 100, 100, 0], loaded.Configuration.IntParams.ToArray());
        Assert.Equal([50u, 49, 130], loaded.Configuration.SelectedItems.ToArray());
        Assert.Empty(loaded.Configuration.SecondarySelectedItems);
    }

    [Theory]
    [InlineData("wf_act_log", "1,0,5")]
    [InlineData("wf_slc_users_neighborhood", "4,0,0,0,0,1,11,0")]
    [InlineData("wf_act_teleport_to", "0,0")]
    [InlineData("wf_trg_says_something", "5,1,0")]
    public void SchemaOneRowThatCannotBeReadIsRejectedWithItsReason(string name, string ints)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var store = new WiredConfigurationStore(new ModernWiredRuntimeTests.StoredRuntimeRowsDatabase(
            [new(LegacyBoxId, name, 1, LegacyRow(ints, "", "", 0, "", ""))]));

        var error = Assert.Throws<InvalidDataException>(() => store.Load(LegacyBoxId, descriptor));

        Assert.StartsWith("Cannot read schema 1 Wired configuration for item 70: " + name, error.Message);
    }

    private IWiredConfiguredItem LoadLegacyBox(string name, string json, IReadOnlyList<uint> inRoom)
    {
        var (wired, _) = PrepareSpeech(new SpeechClock());
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new WiredConfigurationStore(new ModernWiredRuntimeTests.StoredRuntimeRowsDatabase([new(LegacyBoxId, name, 1, json)])));

        for (var i = 0; i < inRoom.Count; i++) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, Furni(inRoom[i], InteractionType.None, WiredBoxType.None), i, 3, 0, true, false, false));
        }

        var item = Furni(LegacyBoxId, InteractionType.None, WiredBoxType.None);
        item.Definition.InteractionName = item.Definition.ItemName = name;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, 1, 1, 0, true, false, false));

        var loaded = Assert.IsAssignableFrom<IWiredConfiguredItem>(wired.LoadWiredBox(item));
        Assert.True(WiredNativeEditorProjection.IsBound(LegacyBoxId, loaded.Descriptor, loaded.Configuration));

        return loaded;
    }
}
