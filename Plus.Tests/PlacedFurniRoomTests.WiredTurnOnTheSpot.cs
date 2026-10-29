using System.Buffers.Binary;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// With No Move Animation, a furni that turns on its tile twice, or turns and changes height, in one firing is sent as one WiredMovements
// entry from and to the final state. Without the add-on the turn is one ObjectUpdate.
public partial class PlacedFurniRoomTests
{
    private const uint SpotTurnA = 991, SpotTurnB = 992, SpotNoAnimation = 993, SpotOrder = 994;

    private async Task<(RoomUser Actor, Item Tile)> SpotRoom(bool noAnimation, bool altitude)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom(x: 2, y: 2, rotation: 0);

        if (altitude) {
            SaveRuntimeBox(service, Change, 0, "-123", 30, true);
        }

        foreach (var (id, z) in new[] { (SpotTurnA, 2.0), (SpotTurnB, 2.5) }.Take(altitude ? 1 : 2)) {
            PlaceSavedBox(id, "wf_act_move_rotate", 1, 1, z);
            _client.Packets.Clear();
            await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket((int)id, 2, 0, 1, "", 1, (int)tile.Id, 0, 1, 100, 0, 0, 0));
            Assert.Equal("saved", SaveAnswer());
        }

        PlaceUnsaved(SpotOrder, "wf_xtra_exec_in_order", 3);

        if (noAnimation) {
            PlaceUnsaved(SpotNoAnimation, "wf_xtra_mov_no_animation", 4);
        }

        return (actor, tile);
    }

    [Fact]
    public async Task TwoTurnsOnTheSpotWithNoMoveAnimationSendOneWiredMovementsWithTheFinalRotation()
    {
        var (actor, tile) = await SpotRoom(noAnimation: true, altitude: false);

        FireRuntimeStack(actor);

        Assert.Equal((2, 2, 4), (tile.GetX, tile.GetY, tile.Rotation));
        var entry = Assert.Single(SentFloorMovements());
        Assert.Equal((2, 2, 2, 2, 4, 0), (entry.FromX, entry.FromY, entry.ToX, entry.ToY, entry.Rotation, entry.AnimationMs));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    [Fact]
    public async Task AnAltitudeChangeAndATurnWithNoMoveAnimationSendOneWiredMovementsWithTheFinalState()
    {
        var (actor, tile) = await SpotRoom(noAnimation: true, altitude: true);

        FireRuntimeStack(actor);

        Assert.Equal((2, 2, 2, 0.3), (tile.GetX, tile.GetY, tile.Rotation, tile.GetZ));
        var entry = Assert.Single(SentFloorMovements());
        Assert.Equal((2, 2, 2, 2, 2, 0, "0.3"), (entry.FromX, entry.FromY, entry.ToX, entry.ToY, entry.Rotation, entry.AnimationMs, entry.ToZ));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    [Fact]
    public async Task TwoTurnsOnTheSpotWithoutTheAddonSendOneObjectUpdate()
    {
        var (actor, tile) = await SpotRoom(noAnimation: false, altitude: false);

        FireRuntimeStack(actor);

        Assert.Equal((2, 2, 4), (tile.GetX, tile.GetY, tile.Rotation));
        Assert.Equal([(2, 2, 4)], SentObjectStates().Select(state => (state.X, state.Y, state.Rotation)));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredMovementsComposer);
    }

    private sealed record FloorMovement(int FromX, int FromY, int ToX, int ToY, string ToZ, int Rotation, int AnimationMs);

    // The floor furni entries of every WiredMovements sent: both tiles, the target height, the rotation and the animation time.
    private List<FloorMovement> SentFloorMovements()
    {
        var entries = new List<FloorMovement>();

        foreach (var packet in _client.Packets.Where(packet => packet.Header == ServerPacketHeader.WiredMovementsComposer)) {
            var bytes = packet.Body;
            var position = 0;
            int Int()
            {
                var value = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position));
                position += 4;

                return value;
            }
            string String()
            {
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
                var value = System.Text.Encoding.UTF8.GetString(bytes, position + 2, length);
                position += 2 + length;

                return value;
            }

            for (var count = Int(); count > 0; count--) {
                Assert.Equal(1, Int());
                var (fromX, fromY, toX, toY) = (Int(), Int(), Int(), Int());
                _ = String();
                var toZ = String();
                _ = Int();
                var animation = Int();
                var rotation = Int();
                entries.Add(new(fromX, fromY, toX, toY, toZ, rotation, animation));

                if (bytes[position++] != 0) {
                    _ = Int();
                }

                if (bytes[position++] != 0) {
                    _ = Int();
                }
            }
        }

        return entries;
    }
}
