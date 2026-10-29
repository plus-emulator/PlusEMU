using System.Buffers.Binary;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// A furni that changes in height or turns on its tile is not stopped by the users standing on it, whether the same firing moved it there or not.
public partial class PlacedFurniRoomTests
{
    private const uint InPlaceMove = 981, InPlaceOrder = 982, InPlaceNoAnimation = 983, InPlaceTurn = 984;

    private async Task<(RoomUser Actor, Item Tile)> InPlaceRoom(bool noAnimation, bool altitude, bool turn, bool moveToUser = true, int altitudeValue = 30)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom();

        if (altitude) {
            SaveRuntimeBox(service, Change, 0, "-123", altitudeValue, true);
        }

        if (moveToUser) {
            PlaceSavedBox(InPlaceMove, "wf_act_furni_to_user", 1, 1, 0.5);
            _client.Packets.Clear();
            await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket((int)InPlaceMove, 0, "", 1, (int)tile.Id, 0, 1, 100, 1, 0, 0, 0));
            Assert.Equal("saved", SaveAnswer());
        }

        PlaceUnsaved(InPlaceOrder, "wf_xtra_exec_in_order", 3);

        if (turn) {
            PlaceSavedBox(InPlaceTurn, "wf_act_move_rotate", 1, 1, 2);
            _client.Packets.Clear();
            await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket((int)InPlaceTurn, 2, 0, 1, "", 1, (int)tile.Id, 0, 1, 100, 0, 0, 0));
            Assert.Equal("saved", SaveAnswer());
        }

        if (noAnimation) {
            PlaceUnsaved(InPlaceNoAnimation, "wf_xtra_mov_no_animation", 4);
        }

        return (actor, tile);
    }

    [Fact]
    public async Task AnAltitudeChangeOfAFurniOnTheTileOfAUserRaisesIt()
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: true, altitude: true, turn: false);
        Stand(actor, 0, 3);

        FireRuntimeStack(actor);

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Equal([(0, 3, 0, "0.3")], SentObjectStates());
    }

    [Fact]
    public async Task AnAnimatedAltitudeChangeOfAFurniOnTheTileOfAUserRaisesIt()
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: false, altitude: true, turn: false);
        Stand(actor, 0, 3);

        FireRuntimeStack(actor);

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
    }

    [Fact]
    public async Task ATurnOfAFurniOnTheTileOfAUserTurnsIt()
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: true, altitude: false, turn: true);
        Stand(actor, 0, 3);

        FireRuntimeStack(actor);

        Assert.Equal((0, 3, 2), (tile.GetX, tile.GetY, tile.Rotation));
        Assert.Equal([(0, 3, 2)], SentObjectStates().Select(state => (state.X, state.Y, state.Rotation)));
    }

    // With No Move Animation a Move and Rotate that turns a furni on its tile sends one WiredMovements entry from and to the new rotation, with or without a user on the tile.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASingleTurnOnTheSpotWithNoMoveAnimationSendsOneWiredMovements(bool userOnTile)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: true, altitude: false, turn: true, moveToUser: false);

        if (userOnTile) {
            Stand(actor, 2, 2);
        }
        else {
            Stand(actor, 0, 3);
        }

        FireRuntimeStack(actor);

        Assert.Equal((2, 2, 2), (tile.GetX, tile.GetY, tile.Rotation));
        var entry = Assert.Single(SentFloorMovements());
        Assert.Equal((2, 2, 2, 2, 2, 0), (entry.FromX, entry.FromY, entry.ToX, entry.ToY, entry.Rotation, entry.AnimationMs));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    [Fact]
    public async Task AClickOnABotRaisesAFurniThatItMovedOntoAUserEarlierInTheFiring()
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: true, altitude: true, turn: false);
        Stand(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Contains(sent, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AClickOnABotRaisesAFurniThatAUserAlreadyStoodOn(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        Stand(actor, 2, 2);

        var sent = await ClickABot();

        Assert.Equal((2, 2, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Contains(sent, packet => packet.Header == (noAnimation ? ServerPacketHeader.ObjectUpdateComposer : ServerPacketHeader.WiredMovementsComposer));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AClickOnABotTurnsAFurniThatAUserAlreadyStoodOn(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: false, turn: true, moveToUser: false);
        Stand(actor, 2, 2);

        var sent = await ClickABot();

        Assert.Equal((2, 2, 2), (tile.GetX, tile.GetY, tile.Rotation));
        Assert.Contains(sent, packet => packet.Header is ServerPacketHeader.ObjectUpdateComposer or ServerPacketHeader.WiredMovementsComposer);
    }

    [Fact]
    public async Task AnAnimatedMoveOntoAUserStillLetsTheFurniBeRaisedLaterInTheFiring()
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: false, altitude: true, turn: false);
        Stand(actor, 0, 3);

        await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
    }

    // The user stands on top of the furni at (2, 2); a lowered furni starts 0.3 higher.
    private (RoomUser Actor, Item Tile) UserOnTop(RoomUser actor, Item tile, double tileZ)
    {
        if (tileZ != 0) {
            Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: tileZ, animate: false, announce: false));
        }

        Stand(actor, 2, 2);
        actor.Z = tile.TotalHeight;

        return (actor, tile);
    }

    private sealed record WiredEntry(int Type, int Id, double FromZ, double ToZ, int AnimationMs);

    // Every WiredMovements packet, in order, with the type, id, heights and animation time of each entry.
    private static List<List<WiredEntry>> WiredMovementPackets(IEnumerable<(uint Header, byte[] Body)> sent)
    {
        var packets = new List<List<WiredEntry>>();

        foreach (var packet in sent.Where(packet => packet.Header == ServerPacketHeader.WiredMovementsComposer)) {
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

            var entries = new List<WiredEntry>();

            for (var count = Int(); count > 0; count--) {
                var type = Int();

                if (type == 3) {
                    _ = (Int(), Int(), Int());

                    continue;
                }

                _ = (Int(), Int(), Int(), Int());
                var (fromZ, toZ) = (String(), String());
                var id = Int();

                if (type == 1) {
                    var animation = Int();
                    _ = Int();
                    entries.Add(new(type, id, double.Parse(fromZ, System.Globalization.CultureInfo.InvariantCulture), double.Parse(toZ, System.Globalization.CultureInfo.InvariantCulture), animation));

                    if (bytes[position++] != 0) {
                        _ = Int();
                    }

                    if (bytes[position++] != 0) {
                        _ = Int();
                    }
                }
                else {
                    _ = Int();
                    var animation = Int();
                    _ = (Int(), Int());
                    entries.Add(new(type, id, double.Parse(fromZ, System.Globalization.CultureInfo.InvariantCulture), double.Parse(toZ, System.Globalization.CultureInfo.InvariantCulture), animation));

                    if (bytes[position++] != 0) {
                        _ = Int();
                    }
                }
            }

            packets.Add(entries);
        }

        return packets;
    }

    // Wired lifts or lowers a user who stands on top of a furni that it raises or lowers in place, in the same update as the furni.
    // With the animation: one WiredMovements with the furni entry and an Avatar entry. With No Move Animation: the ObjectUpdate of the furni,
    // then a WiredMovements with only the Avatar entry and no animation.
    [Theory]
    [InlineData(0.0, 30, 0.0, 0.3, 0.5, 0.8)]
    [InlineData(0.3, 0, 0.3, 0.0, 0.8, 0.5)]
    public async Task AClickOnABotMovesAUserWithTheAnimatedFurniThatWiredRaisesOrLowers(double startZ, int value,
        double furniFrom, double furniTo, double userFrom, double userTo)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: false, altitude: true, turn: false, moveToUser: false, altitudeValue: value);
        UserOnTop(actor, tile, startZ);

        var sent = await ClickABot();

        var packet = Assert.Single(WiredMovementPackets(sent));
        WiredEntry[] expected = [new(1, (int)tile.Id, furniFrom, furniTo, 500), new(0, actor.VirtualId, userFrom, userTo, 500)];
        Assert.Equal(expected, packet);
        Assert.DoesNotContain(sent, other => other.Header == ServerPacketHeader.ObjectUpdateComposer);
        Assert.Equal(userTo, actor.Z, 3);
    }

    [Theory]
    [InlineData(0.0, 30, 0.5, 0.8)]
    [InlineData(0.3, 0, 0.8, 0.5)]
    public async Task AClickOnABotMovesAUserWithTheFurniThatWiredRaisesOrLowersWithNoMoveAnimation(double startZ, int value, double userFrom, double userTo)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: true, altitude: true, turn: false, moveToUser: false, altitudeValue: value);
        UserOnTop(actor, tile, startZ);

        var sent = await ClickABot();

        var packet = Assert.Single(WiredMovementPackets(sent));
        WiredEntry[] expected = [new(0, actor.VirtualId, userFrom, userTo, 0)];
        Assert.Equal(expected, packet);
        var headers = sent.Select(other => other.Header).ToList();
        Assert.Single(headers, ServerPacketHeader.ObjectUpdateComposer);
        Assert.True(headers.IndexOf(ServerPacketHeader.ObjectUpdateComposer) < headers.IndexOf(ServerPacketHeader.WiredMovementsComposer));
        Assert.Equal(userTo, actor.Z, 3);
    }

    // A furni that Furni To User moved onto a user in the same firing and then raised sends no entry for the user, who ends on top of it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFurniMovedOntoAUserAndRaisedInTheSameFiringLeavesTheUserOnTopWithNoEntry(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false);
        Stand(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.All(WiredMovementPackets(sent), packet => Assert.DoesNotContain(packet, entry => entry.Type == 0));
        Assert.Equal(tile.TotalHeight, actor.Z, 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUserOnAnotherTileIsNotMovedWhenWiredRaisesAFurni(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        Stand(actor, 5, 5);

        var sent = await ClickABot();

        Assert.Equal((2, 2, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Equal(0, actor.Z);
        Assert.All(WiredMovementPackets(sent), packet => Assert.DoesNotContain(packet, entry => entry.Type == 0));
    }
}
