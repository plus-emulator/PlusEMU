using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A user's double-click on the super blob changes its state on the next room tick, in an ObjectsDataUpdate, and sends no data update at once.
    [Fact]
    public async Task DoubleClickingTheSuperBlobChangesItOnTheNextRoomTickInABatch()
    {
        AdmitOwner();
        var blob = Furni(262, InteractionType.None, WiredBoxType.None);
        blob.Definition.Modes = 2;
        blob.Definition.ItemName = "wf_blob2";
        blob.ExtraData = FurniExtraData.Load(blob.Definition, "0", keepLegacy: true);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, blob, 2, 2, 0, true, false, false));
        var use = new UseFurnitureEvent(new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)));

        foreach (var state in new[] { "1", "0", "1" }) {
            _client.Packets.Clear();

            await use.Parse(_room, _client, ClientPacket((int)blob.Id, 0));

            Assert.Equal(StateHeightPackets().Where(packet => packet.Header != ServerPacketHeader.ObjectsDataUpdateComposer), []);
            Assert.NotEqual(state, blob.LegacyDataString);

            _room.GetWired().OnCycle();

            Assert.Equal(state, blob.LegacyDataString);
            Assert.Contains(StateHeightPackets(), packet => packet.Header == ServerPacketHeader.ObjectsDataUpdateComposer);
            Assert.DoesNotContain(StateHeightPackets(), packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer);
        }
    }
}
