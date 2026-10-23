using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // The reference editor context holds only the shared variables, with no variable catalog hash. The editor picks from that list.
    [Fact]
    public void AReferenceEditorCarriesTheSharedListButNoVariableCatalogHash()
    {
        var item = Furni(601, InteractionType.WiredVariable, WiredBoxType.None);
        item.RoomId = RoomId;
        item.Definition.InteractionName = "wf_var_reference";
        item.Definition.ItemName = "wf_var_reference";
        _room.GetRoomItemHandler().LoadFurniture([item]);
        Assert.True(_room.GetWired().TryGet(item.Id, out var loaded));

        var snapshot = WiredEditorSnapshot.Capture(Assert.IsAssignableFrom<IWiredConfiguredItem>(loaded));

        Assert.Null(snapshot.CatalogHash);
        Assert.NotNull(snapshot.SharedVariables);
    }

    // wf_var_reference reads and writes variables of the owner's other rooms, so only the room's owner may save one.
    // WiredVariableReference.ApplyWiredUpdateAsync refuses everyone else; a perfectly valid frame from a rights holder changes nothing.
    [Fact]
    public void ReferenceVariableSaveFromSomeoneWhoDoesNotOwnTheRoomIsRefusedAndStoresNothing()
    {
        var store = new CanonicalStore(null);
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(_room.GetWired(), store);
        var item = Furni(601, InteractionType.WiredVariable, WiredBoxType.None);
        item.RoomId = RoomId;
        item.Definition.InteractionName = "wf_var_reference";
        item.Definition.ItemName = "wf_var_reference";
        _room.GetRoomItemHandler().LoadFurniture([item]);
        Assert.True(_room.GetWired().TryGet(item.Id, out var loaded));
        var box = Assert.IsAssignableFrom<IWiredConfiguredItem>(loaded);
        _client.Packets.Clear();
        var before = box.Configuration;
        _room.OwnerId = _client.GetHabbo().Id + 1000;
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with
        {
            OwnedIntParams = [1],
            VariableIds = ["shared:7:room:4"],
            Text = "far_coins"
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(601, box.Descriptor, native, out var runtime));

        CanonicalService(store).Save(_client, new(601, WiredBoxCategory.Variable, runtime, native));

        Assert.Empty(store.Saves);
        Assert.Same(before, box.Configuration);
        var refusal = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredValidationErrorComposer);
        Assert.Equal(WiredConfigurationService.WiredVariableOwnerOnly, System.Text.Encoding.UTF8.GetString(refusal.Body, 2, (refusal.Body[0] << 8) | refusal.Body[1]));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
    }
}
