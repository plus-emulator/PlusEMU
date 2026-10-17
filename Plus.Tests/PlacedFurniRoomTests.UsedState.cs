using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Observed on hhus (AIR WIN63-202610131318-860371703): using a wf_colortile sends one ObjectDataUpdate with the new state.
    [Fact]
    public void AUserSwitchedStateGoesOutAsADataUpdate()
    {
        PrepareSpeech(new SpeechClock());
        var tile = Furni(303, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        tile.RoomId = RoomId;
        tile.Definition.Modes = 7;
        tile.ExtraData = new LegacyDataFormat { Data = "1" };
        tile.SetState(2, 2, 0, []);
        _room.GetRoomItemHandler().LoadFurniture([tile]);
        _client.Packets.Clear();

        tile.LegacyDataString = "2";
        tile.UpdateUsedState();

        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }
}
