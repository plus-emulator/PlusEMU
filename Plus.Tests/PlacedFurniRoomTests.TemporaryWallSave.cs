using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(110, 0, 0, ":w=5,0 l=8,119 r a=0")]
    [InlineData(100, 1, 0, ":w=5,0 l=8,40 r a=247")]
    [InlineData(101, 1, 100, ":w=5,0 l=8,8 r a=347")]
    public async Task TemporaryWallNativeSaveCapturesTemplateAndFires(int source, int altitude, int offset, string expected)
    {
        var (box, store) = CanonicalBox("wf_act_place_furni", new() { TemporaryPlacement = new() });
        var template = TemporaryWallTemplate();
        box.Item.UserId = 7;
        box.Item.OwnerId = 7;
        _room.GetRoomItemHandler().LoadFurniture([box.Item, template]);
        var primary = source == 101 ? Array.Empty<object>() : new object[] { -(int)template.Id };
        var secondary = source == 101 ? new object[] { -(int)template.Id } : Array.Empty<object>();
        object[] fields = [601, 10, 0, 0, altitude, 0, 0, offset, 0, 0, 0, -20, "", primary.Length, .. primary,
            0, 3, source, 0, 200, 2, 0, 200, 2, WiredVariableAbsent.WireId, WiredVariableAbsent.WireId, secondary.Length, .. secondary];
        await CanonicalHandler().Parse(_client, ClientPacket(fields));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredValidationErrorComposer);
        var saved = Assert.Single(store.Saves);
        var snapshot = Assert.Single(saved.Snapshots);
        Assert.Equal(template.Id, snapshot.ItemId);
        Assert.Equal(247, snapshot.Wall!.NativeAltitude);
        Assert.Equal("1", snapshot.State);
        Assert.True(WiredNativeEditorProjection.TryProject(box.Item, box.Descriptor, saved, out var reopened));
        var selected = Assert.Single(source == 101 ? reopened.SecondaryItems : reopened.PrimaryItems);
        Assert.True(selected.Wall);
        Assert.Equal(template.Id, selected.ItemId);
        Assert.Equal(snapshot, Assert.Single(reopened.SavedState.Snapshots));
        Viewer();
        _client.Packets.Clear();
        Assert.True(WiredTemporaryFurnitureActions.Execute("wf_act_place_furni", box.Item, WallSnapshotContext(), saved,
            new TestWiredDefinitions(() => new() { [template.Definition.Id] = template.Definition })));
        var copy = Assert.Single(_room.GetRoomItemHandler().GetWall, item => item.IsTemporary);
        Assert.Equal(expected, copy.WallCoordinates);
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ItemsComposer);
    }
}
