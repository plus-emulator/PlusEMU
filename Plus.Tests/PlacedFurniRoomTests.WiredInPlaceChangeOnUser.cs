using System.Buffers.Binary;
using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// A furni that changes in height or turns on its tile is not stopped by the users standing on it, whether the same firing moved it there or not.
public partial class PlacedFurniRoomTests
{
    private const uint InPlaceMove = 981, InPlaceOrder = 982, InPlaceNoAnimation = 983, InPlaceTurn = 984, InPlaceWest = 985, InPlacePhysics = 987, StackedRug = 986;

    private WiredConfigurationService? _inPlaceService;

    private async Task<(RoomUser Actor, Item Tile)> InPlaceRoom(bool noAnimation, bool altitude, bool turn, bool moveToUser = true, int altitudeValue = 30, bool west = false, int altitudeDelay = 0)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom();
        _inPlaceService = service;

        if (altitude) {
            SaveRuntimeBox(service, Change, 0, "-123", altitudeValue, true, delay: altitudeDelay);
        }

        if (moveToUser) {
            PlaceSavedBox(InPlaceMove, "wf_act_furni_to_user", 1, 1, 0.5);
            _client.Packets.Clear();
            await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket((int)InPlaceMove, 0, "", 1, (int)tile.Id, 0, 1, 100, 1, 0, 0, 0));
            Assert.Equal("saved", SaveAnswer());
        }

        if (west) {
            var physics = WiredBox(InPlacePhysics, "wf_xtra_mov_physics", 1, 1);
            Assert.True(WiredNativeTestSupport.TryValidateRuntime(physics, new() { IntParams = [0, 1, 1, 0, 900, 900, 900] }, out var physicsConfig, out var physicsError), physicsError);
            physics.ApplyConfiguration(physicsConfig);
            physics.Item.GetZ = 5;
            Assert.True(_room.GetWired().AddBox(physics));
            PlaceSavedBox(InPlaceWest, "wf_act_move_rotate", 1, 1, 0.5);
            _client.Packets.Clear();
            await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket((int)InPlaceWest, 2, 7, 0, "", 1, (int)tile.Id, 0, 1, 100, 0, 0, 0));
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

    // The furni is walkable and placed at (x, y - 1) then (x, y), so the navigation grid sees it. The user then walks onto it with the raw MoveAvatar packet.
    private async Task<(RoomUser Actor, Item Tile)> UserOnTop(RoomUser actor, Item tile, double tileZ, int width = 1, double height = 0.5, Action? stackMore = null)
    {
        tile.Definition.Walkable = true;
        tile.Definition.Width = width;
        tile.Definition.Height = height;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: tileZ, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: tileZ, animate: false, announce: false));
        stackMore?.Invoke();
        await WalkTo(actor, 2, 2);

        if (stackMore == null) {
            AssertBoundTo(actor, tile, tile.TotalHeight);
        }

        return (actor, tile);
    }

    private async Task WalkTo(RoomUser actor, int x, int y)
    {
        ExecutorTick();
        ExecutorTick();
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(x, y));

        for (var tick = 0; tick < 40 && ((actor.X, actor.Y) != (x, y) || actor.IsWalking || actor.SetStep); tick++) {
            ExecutorTick();
        }

        Assert.Equal((x, y), (actor.X, actor.Y));
    }

    private static void AssertBoundTo(RoomUser user, Item item, double z)
    {
        Assert.Equal(z, user.Z, 3);
        Assert.Equal(z, user.Movement.SupportZ, 3);
        Assert.Equal(item.Id, user.Movement.CurrentRef!.Value.SupportItemId);
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
    // then a WiredMovements with only the Avatar entry and no animation. The user walked onto the furni, so the navigation state moves with it.
    [Theory]
    [InlineData(0.0, 30, 0.0, 0.3, 0.5, 0.8)]
    [InlineData(0.3, 0, 0.3, 0.0, 0.8, 0.5)]
    public async Task AClickOnABotMovesAUserWithTheAnimatedFurniThatWiredRaisesOrLowers(double startZ, int value,
        double furniFrom, double furniTo, double userFrom, double userTo)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: false, altitude: true, turn: false, moveToUser: false, altitudeValue: value);
        await UserOnTop(actor, tile, startZ);

        var sent = await ClickABot();

        var packet = Assert.Single(WiredMovementPackets(sent));
        WiredEntry[] expected = [new(1, (int)tile.Id, furniFrom, furniTo, 500), new(0, actor.VirtualId, userFrom, userTo, 500)];
        Assert.Equal(expected, packet);
        Assert.DoesNotContain(sent, other => other.Header == ServerPacketHeader.ObjectUpdateComposer);
        AssertBoundTo(actor, tile, userTo);
    }

    [Theory]
    [InlineData(0.0, 30, 0.5, 0.8)]
    [InlineData(0.3, 0, 0.8, 0.5)]
    public async Task AClickOnABotMovesAUserWithTheFurniThatWiredRaisesOrLowersWithNoMoveAnimation(double startZ, int value, double userFrom, double userTo)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: true, altitude: true, turn: false, moveToUser: false, altitudeValue: value);
        await UserOnTop(actor, tile, startZ);

        var sent = await ClickABot();

        var packet = Assert.Single(WiredMovementPackets(sent));
        WiredEntry[] expected = [new(0, actor.VirtualId, userFrom, userTo, 0)];
        Assert.Equal(expected, packet);
        var headers = sent.Select(other => other.Header).ToList();
        Assert.Single(headers, ServerPacketHeader.ObjectUpdateComposer);
        Assert.True(headers.IndexOf(ServerPacketHeader.ObjectUpdateComposer) < headers.IndexOf(ServerPacketHeader.WiredMovementsComposer));
        AssertBoundTo(actor, tile, userTo);
    }

    // A furni that Furni To User moved onto a user in the same firing and then raised sends no entry for the user, who ends on top of it.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AFurniMovedOntoAUserAndRaisedInTheSameFiringLeavesTheUserOnTopWithNoEntry(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false);
        tile.Definition.Walkable = true;
        await WalkTo(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.All(WiredMovementPackets(sent), packet => Assert.DoesNotContain(packet, entry => entry.Type == 0));
        AssertBoundTo(actor, tile, tile.TotalHeight);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUserOnAnotherTileIsNotMovedWhenWiredRaisesAFurni(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        await WalkTo(actor, 3, 3);

        var sent = await ClickABot();

        Assert.Equal((2, 2, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Equal(0, actor.Z);
        Assert.All(WiredMovementPackets(sent), packet => Assert.DoesNotContain(packet, entry => entry.Type == 0));
    }

    // A furni stacked on the lowered one holds the user up, so the user stays where he is.
    [Theory]
    [InlineData(false, 0.0)]
    [InlineData(true, 0.0)]
    [InlineData(false, 0.2)]
    public async Task AUserOnAFurniStackedOnTheLoweredFurniIsLeftWhereHeStands(bool noAnimation, double rugHeight)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false, altitudeValue: 0);
        Item? rug = null;
        await UserOnTop(actor, tile, 0.3, stackMore: () => rug = ExecutorFloor(StackedRug, 2, 2, z: tile.TotalHeight, height: rugHeight));
        Assert.NotNull(rug);
        Assert.Equal(rug.Id, actor.Movement.CurrentRef!.Value.SupportItemId);
        var standingAt = actor.Z;

        var sent = await ClickABot();

        Assert.Equal((2, 2, 0), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.All(WiredMovementPackets(sent), packet => Assert.DoesNotContain(packet, entry => entry.Type == 0));
        Assert.Equal(standingAt, actor.Z, 3);
        Assert.Equal(rug.Id, actor.Movement.CurrentRef!.Value.SupportItemId);
    }

    // A 2x1 furni slides one tile west over a user who stays on top of it, then rises: the furni entry carries the move and the raise, the user gets no entry, and his next update has the new height.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUserWhoStaysOnTopOfAFurniThatSlidesThenRisesGetsNoEntry(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false, west: true);
        await UserOnTop(actor, tile, 0, width: 2);
        Assert.Equal(2, tile.GetX);

        var sent = await ClickABot();

        Assert.Equal((1, 2, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Empty(AvatarEntries(sent));
        AssertBoundTo(actor, tile, 0.8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryUserOnAMultiTileFurniIsLifted(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        await UserOnTop(actor, tile, 0, width: 2);
        var other = ExecutorAdditionalBot(3, 3, 9);
        other.MoveTo(3, 2);

        for (var tick = 0; tick < 40 && ((other.X, other.Y) != (3, 2) || other.IsWalking || other.SetStep); tick++) {
            ExecutorTick();
        }

        Assert.Equal((3, 2), (other.X, other.Y));
        Assert.Equal(tile.Id, other.Movement.CurrentRef!.Value.SupportItemId);

        var sent = await ClickABot();

        var avatars = WiredMovementPackets(sent).SelectMany(packet => packet).Where(entry => entry.Type == 0).OrderBy(entry => entry.Id).ToArray();
        Assert.Equal([actor.VirtualId, other.VirtualId], avatars.Select(entry => entry.Id).Order());
        Assert.All(avatars, entry => Assert.Equal((0.5, 0.8), (entry.FromZ, entry.ToZ)));
        AssertBoundTo(actor, tile, 0.8);
        AssertBoundTo(other, tile, 0.8);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARiderAndHisHorseOnTheFurniAreBothLifted(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        await UserOnTop(actor, tile, 0);
        var horse = ExternalLifecycleHorse(2, 2);
        ExecutorTick();
        EstablishExternalMountedGroup(actor, horse);
        ExecutorTick();
        Assert.Equal((0.5, 1.5), (horse.Z, actor.Z));

        var sent = await ClickABot();

        var avatars = WiredMovementPackets(sent).SelectMany(packet => packet).Where(entry => entry.Type == 0).ToDictionary(entry => entry.Id);
        Assert.Equal(2, avatars.Count);
        Assert.Equal((0.5, 0.8), (avatars[horse.VirtualId].FromZ, avatars[horse.VirtualId].ToZ));
        Assert.Equal((1.5, 1.8), (avatars[actor.VirtualId].FromZ, avatars[actor.VirtualId].ToZ));
        Assert.Equal((0.8, 1.8), (horse.Z, actor.Z));
    }
}
