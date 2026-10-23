using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Interactor;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Using a wf_colortile sends one ObjectDataUpdate with the new state.
    [Fact]
    public void AUserSwitchedStateGoesOutAsADataUpdate()
    {
        PrepareSpeech(new SpeechClock());
        var tile = Furni(303, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        tile.RoomId = RoomId;
        tile.Definition.Modes = 7;
        tile.ExtraData = new LegacyDataFormat { Data = "1" };
        tile.SetState(2, 2, 0);
        _room.GetRoomItemHandler().LoadFurniture([tile]);
        _client.Packets.Clear();

        tile.LegacyDataString = "2";
        tile.UpdateUsedState();

        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    // Double-clicking a wf_colortile in a room sends one ObjectDataUpdate with the new state and no ObjectUpdate.
    [Fact]
    public void UsingASwitchInARoomSendsOnlyADataUpdate()
    {
        PrepareSpeech(new SpeechClock());
        var tile = Furni(304, InteractionType.None, WiredBoxType.None);
        tile.RoomId = RoomId;
        tile.Definition.Modes = 7;
        tile.ExtraData = new LegacyDataFormat { Data = "1" };
        tile.SetState(2, 2, 0);
        _room.GetRoomItemHandler().LoadFurniture([tile]);
        _client.Packets.Clear();

        new FurnitureUseService(new FurnitureUseStore(_database), Proxy<IQuestManager>((_, _) => null)).Use(_room, _client, new(tile.Id, 0));

        Assert.Equal("2", tile.LegacyDataString);
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    // A user who switches a Floor Switch from the next tile makes the room receive one ObjectDataUpdate with the new state and no ObjectUpdate.
    [Fact]
    public void UsingAFloorSwitchFromTheNextTileSendsOnlyADataUpdate()
    {
        PrepareSpeech(new SpeechClock());
        var tile = Furni(305, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        tile.RoomId = RoomId;
        tile.Definition.Modes = 3;
        tile.ExtraData = new LegacyDataFormat { Data = "0" };
        tile.SetState(2, 2, 0);
        _room.GetRoomItemHandler().LoadFurniture([tile]);
        var user = _room.GetRoomUserManager().GetRoomUserByHabbo(_client.GetHabbo().Id)!;
        user.X = 2;
        user.Y = 3;
        _client.Packets.Clear();

        new InteractorSwitch(Proxy<IQuestManager>((_, _) => null), new TestRewardProgress()).OnTrigger(_client, tile, 0, true);

        Assert.Equal("1", tile.LegacyDataString);
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectDataUpdateComposer);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }
}
