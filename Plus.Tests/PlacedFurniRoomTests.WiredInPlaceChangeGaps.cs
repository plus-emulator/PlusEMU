using System.Buffers.Binary;
using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private static WiredEntry[] AvatarEntries(IEnumerable<(uint Header, byte[] Body)> sent) =>
        [.. WiredMovementPackets(sent).SelectMany(packet => packet).Where(entry => entry.Type == 0)];

    // A raise that runs one pulse or more after the move is a new wave: the user the furni was moved onto is lifted with it.
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task ADelayedRaiseAfterTheFurniWasMovedOntoAUserLiftsTheUser(bool noAnimation, int pulses)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, altitudeDelay: pulses);
        tile.Definition.Walkable = true;
        await WalkTo(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        var avatar = Assert.Single(AvatarEntries(sent));
        Assert.Equal((actor.VirtualId, 0.5, 0.8), (avatar.Id, avatar.FromZ, avatar.ToZ));
        AssertBoundTo(actor, tile, 0.8);
    }

    // A furni that cannot be walked on and lands on a user leaves him at its base height. A raise in the same wave moves him with no entry, a later one with an entry.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANonWalkableFurniMovedOntoAUserAndRaisedLeavesTheUserAtItsBase(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false);
        await WalkTo(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Empty(AvatarEntries(sent));
        Assert.Equal(0.3, actor.Z, 3);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ANonWalkableFurniRaisedLaterTakesTheUserInsideItWithIt(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, altitudeDelay: 1);
        await WalkTo(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        var avatar = Assert.Single(AvatarEntries(sent));
        Assert.Equal((actor.VirtualId, 0, 0.3), (avatar.Id, avatar.FromZ, avatar.ToZ));
        Assert.Equal(0.3, actor.Z, 3);
    }

    // Wired raises the furni under the one a user stands on: he gets no entry, and his next update has the new height.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ARaiseOfTheFurniUnderTheFurniAUserStandsOnSendsNoEntry(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        await UserOnTop(actor, tile, 0, stackMore: () => ExecutorFloor(StackedRug, 2, 2, z: tile.TotalHeight, height: 0.2));
        Assert.Equal(0.7, actor.Z, 3);

        var sent = await ClickABot();

        Assert.Equal(0.3, tile.GetZ, 3);
        Assert.Empty(AvatarEntries(sent));
        Assert.Equal(0.8, actor.Z, 3);
    }

    // A sitting user is lifted at his sitting height.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASittingUserOnTheFurniGetsAnEntryAtTheSittingHeight(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        await UserOnTop(actor, tile, 0);
        await new SitEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(1));
        ExecutorTick();
        Assert.True(actor.IsSitting);

        var sent = await ClickABot();

        var avatar = Assert.Single(AvatarEntries(sent));
        Assert.Equal((actor.VirtualId, 1.05, 1.35), (avatar.Id, avatar.FromZ, avatar.ToZ));
    }

    // A 2x1 furni turns on its first tile. The user on its second tile is left on the floor, the user on its first tile stays on it, and neither gets an entry.
    [Theory]
    [InlineData(false, 2, 0.0)]
    [InlineData(true, 2, 0.0)]
    [InlineData(false, 1, 0.5)]
    [InlineData(true, 1, 0.5)]
    public async Task ATurnThatChangesTheHeightUnderAUserSendsNoEntry(bool noAnimation, int userX, double expectedZ)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: false, turn: true, moveToUser: false);
        tile.Definition.Walkable = true;
        tile.Definition.Width = 2;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 1, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, userX, 2);
        Assert.Equal(0.5, actor.Z, 3);

        var sent = await ClickABot();

        Assert.Equal((1, 2, 2), (tile.GetX, tile.GetY, tile.Rotation));
        Assert.Empty(AvatarEntries(sent));
        Assert.Equal(expectedZ, actor.Z, 3);
    }

    private sealed record AvatarEntry(int FromX, int FromY, int ToX, int ToY, double FromZ, double ToZ, int AnimationType, int DurationMs);

    private static List<AvatarEntry> AvatarEntryDetails(IEnumerable<(uint Header, byte[] Body)> sent)
    {
        var entries = new List<AvatarEntry>();

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

            for (var count = Int(); count > 0; count--) {
                var type = Int();

                if (type == 3) {
                    _ = (Int(), Int(), Int());

                    continue;
                }

                var (fromX, fromY, toX, toY) = (Int(), Int(), Int(), Int());
                var (fromZ, toZ) = (double.Parse(String(), System.Globalization.CultureInfo.InvariantCulture), double.Parse(String(), System.Globalization.CultureInfo.InvariantCulture));
                _ = Int();

                if (type == 1) {
                    _ = (Int(), Int());

                    if (bytes[position++] != 0) {
                        _ = Int();
                    }

                    if (bytes[position++] != 0) {
                        _ = Int();
                    }

                    continue;
                }

                var animationType = Int();
                var duration = Int();
                _ = (Int(), Int());
                entries.Add(new(fromX, fromY, toX, toY, fromZ, toZ, animationType, duration));

                if (bytes[position++] != 0) {
                    _ = Int();
                }
            }
        }

        return entries;
    }

    // The user's announced step goes onto the furni when Wired raises it: he gets one entry from his tile to the step target at the new height, which is not a slide.
    [Theory]
    [InlineData(false, 500)]
    [InlineData(true, 0)]
    public async Task AUserSteppingOntoAFurniThatWiredRaisesGetsAnEntryToTheStepTarget(bool noAnimation, int durationMs)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 0);
        ExecutorTick();
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(2, 3));

        for (var tick = 0; tick < 40 && !(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2)); tick++) {
            ExecutorTick();
        }

        Assert.True(actor.SetStep && (actor.SetX, actor.SetY) == (2, 2));
        Assert.Equal((2, 1), (actor.X, actor.Y));

        FireRuntimeStack(actor);

        Assert.Equal(0.3, tile.GetZ, 3);
        var entry = Assert.Single(AvatarEntryDetails(_client.Packets.ToArray()));
        Assert.Equal((2, 1, 2, 2, 0, 0.8, 0, durationMs), (entry.FromX, entry.FromY, entry.ToX, entry.ToY, entry.FromZ, entry.ToZ, entry.AnimationType, entry.DurationMs));
    }

    // The user's announced step goes off the furni: Wired raises it and sends him nothing.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AUserSteppingOffAFurniThatWiredRaisesGetsNoEntry(bool noAnimation)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 2);
        ExecutorTick();
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(2, 3));

        for (var tick = 0; tick < 40 && !(actor.SetStep && (actor.SetX, actor.SetY) == (2, 3)); tick++) {
            ExecutorTick();
        }

        Assert.True(actor.SetStep && (actor.SetX, actor.SetY) == (2, 3));

        FireRuntimeStack(actor);

        Assert.Equal(0.3, tile.GetZ, 3);
        Assert.Empty(AvatarEntryDetails(_client.Packets.ToArray()));
    }

    // A seat or a bed: the user sits or lies at the height of the furni's posture on top of the furni's base, and the base rises with the furni.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AUserOnASeatOrABedGetsAnEntryAtThePostureHeight(bool noAnimation, bool bed)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: true, turn: false, moveToUser: false);
        tile.Definition.Walkable = true;
        tile.Definition.IsSeat = !bed;
        tile.Definition.InteractionType = bed ? InteractionType.Bed : InteractionType.None;
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 2);
        ExecutorTick();
        ExecutorTick();
        Assert.True(actor.Statusses.ContainsKey(bed ? "lay" : "sit"));

        var sent = await ClickABot();

        var avatar = Assert.Single(AvatarEntries(sent));
        Assert.Equal((actor.VirtualId, 0.5, 0.8), (avatar.Id, avatar.FromZ, avatar.ToZ));
        Assert.Equal(0.3, actor.Z, 3);
    }
}
