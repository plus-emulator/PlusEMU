using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// With No Move Animation, every change of one furni's tile or height in one firing reaches the clients as one ObjectUpdate with the final state.
public partial class PlacedFurniRoomTests
{
    private const uint OneUpdateMove = 959, OneUpdateOrder = 972, OneUpdateNoAnimation = 973, OneUpdatePhysics = 974;

    private async Task<(RoomUser Actor, Item Tile)> OneUpdateRoom(bool noAnimation)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom();
        SaveRuntimeBox(service, Change, 0, "-123", 30, true);
        PlaceSavedBox(OneUpdateMove, "wf_act_furni_to_user", 1, 1, 0.5);
        _client.Packets.Clear();
        await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket((int)OneUpdateMove, 0, "", 1, (int)tile.Id, 0, 1, 100, 1, 0, 0, 0));
        Assert.Equal("saved", SaveAnswer());
        PlaceUnsaved(OneUpdateOrder, "wf_xtra_exec_in_order", 3);

        var physics = WiredBox(OneUpdatePhysics, "wf_xtra_mov_physics", 1, 1);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(physics, new() { IntParams = [1, 1, 1, 0, 900, 900, 900] }, out var config, out var error), error);
        physics.ApplyConfiguration(config);
        physics.Item.GetZ = 5;
        Assert.True(_room.GetWired().AddBox(physics));

        if (noAnimation) {
            PlaceUnsaved(OneUpdateNoAnimation, "wf_xtra_mov_no_animation", 4);
        }

        return (actor, tile);
    }

    [Fact]
    public async Task FurniToUserAndAnAltitudeChangeWithNoMoveAnimationSendOneObjectUpdateWithTheFinalState()
    {
        var (actor, tile) = await OneUpdateRoom(noAnimation: true);
        Stand(actor, 0, 3);

        FireRuntimeStack(actor);

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Equal([(0, 3, 0, "0.3")], SentObjectStates());
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredMovementsComposer);
    }

    // The ObjectUpdate comes before the ObjectsDataUpdate that flashes the stack's boxes.
    [Fact]
    public async Task TheObjectUpdateOfAMoveWithNoMoveAnimationComesBeforeTheFlashes()
    {
        var (actor, _) = await OneUpdateRoom(noAnimation: true);
        Stand(actor, 0, 3);

        FireRuntimeStack(actor);

        var headers = _client.Packets.Select(packet => packet.Header).ToList();
        Assert.True(headers.IndexOf(ServerPacketHeader.ObjectUpdateComposer) < headers.IndexOf(ServerPacketHeader.ObjectsDataUpdateComposer),
            string.Join(",", headers));
    }

    // The tile, rotation and height each ObjectUpdate sent to the client carries.
    private (int X, int Y, int Rotation, string Z)[] SentObjectStates() => [.. _client.Packets.Where(packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer).Select(packet =>
    {
        var body = packet.Body;
        var read = (int at) => System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(body.AsSpan(at));
        var length = (body[20] << 8) | body[21];

        return (read(8), read(12), read(16), System.Text.Encoding.UTF8.GetString(body, 22, length));
    })];
}
