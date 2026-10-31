using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint StateToggleBox = 990;

    private async Task<(RoomUser Actor, Item Block)> UserOnAStateHeightBlock(bool sitting)
    {
        var (actor, block) = await InPlaceRoom(noAnimation: false, altitude: false, turn: false, moveToUser: false);
        block.Definition.Modes = 5;
        block.Definition.AdjustableHeights = [1.0, 0.75, 0.5, 0.25, 0.0];
        block.Definition.Height = 1.0;
        block.ExtraData = new LegacyDataFormat { Data = "0" };
        await UserOnTop(actor, block, 0, height: 1.0);

        if (sitting) {
            await new SitEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(1));
            ExecutorTick();
            Assert.True(actor.IsSitting);
        }

        ToggleStack(StateToggleBox, 1, 1, block);

        return (actor, block);
    }

    // Toggle State changes the height of a block a user stands or sits on: he gets no entry, follows the new height in his next update and keeps his posture.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AToggleStateThatChangesTheHeightOfABlockMovesTheUserOnItWithoutAnEntry(bool sitting)
    {
        var (actor, block) = await UserOnAStateHeightBlock(sitting);
        Assert.Equal(1.0, actor.Z, 3);

        var sent = await ClickABot();

        Assert.Equal("1", block.LegacyDataString);
        Assert.Empty(AvatarEntries(sent));
        Assert.Equal(0.75, actor.Z, 3);
        Assert.Equal(sitting, actor.IsSitting);
        Assert.Contains(sent, packet => packet.Header == ServerPacketHeader.UserUpdateComposer);
    }

    private async Task<(RoomUser Actor, Item Bed)> UserLyingOnABed(bool noAnimation, bool altitude, int width)
    {
        var (actor, bed) = await InPlaceRoom(noAnimation, altitude, turn: false, moveToUser: false, west: true);
        bed.Definition.InteractionType = InteractionType.Bed;
        bed.Definition.Walkable = true;
        bed.Definition.Width = width;
        Assert.True(WiredRoomOperations.MoveItem(_room, bed, 2, 1, height: 0, animate: false, announce: false));
        Assert.True(WiredRoomOperations.MoveItem(_room, bed, 2, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 2);
        ExecutorTick();
        ExecutorTick();
        Assert.True(actor.Statusses.ContainsKey("lay"));

        return (actor, bed);
    }

    // A bed that leaves the user's tile, with or without a rise in the same wave, sends no entry; the user gets up where he is.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ALyingUserWhoseBedMovesAwayGetsNoEntryAndStandsUp(bool noAnimation, bool altitude)
    {
        var (actor, bed) = await UserLyingOnABed(noAnimation, altitude, width: 1);

        var sent = await ClickABot();
        ExecutorTick();
        ExecutorTick();

        Assert.Equal((1, 2), (bed.GetX, bed.GetY));
        Assert.Empty(AvatarEntries(sent));
        Assert.False(actor.Statusses.ContainsKey("lay"));
        Assert.Equal((2, 2, 0), (actor.X, actor.Y, actor.Z));
    }

    // A bed that slides along and still covers the user's tile sends no entry and keeps him lying. A rise in the same wave lifts him with the bed.
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ALyingUserWhoseBedSlidesAlongUnderHimGetsNoEntryAndKeepsLying(bool noAnimation, bool altitude)
    {
        var (actor, bed) = await UserLyingOnABed(noAnimation, altitude, width: 2);

        var sent = await ClickABot();
        ExecutorTick();
        ExecutorTick();

        Assert.Equal((1, 2), (bed.GetX, bed.GetY));
        Assert.Empty(AvatarEntries(sent));
        Assert.True(actor.Statusses.ContainsKey("lay"));
        Assert.Equal(altitude ? 0.3 : 0, actor.Z, 3);
    }

    // A bed moved onto a standing user lays him down with no entry.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ABedMovedOntoAStandingUserLaysHimDownWithoutAnEntry(bool noAnimation)
    {
        var (actor, bed) = await InPlaceRoom(noAnimation, altitude: false, turn: false, moveToUser: true);
        bed.Definition.InteractionType = InteractionType.Bed;
        bed.Definition.Walkable = true;
        await WalkTo(actor, 0, 3);

        var sent = await ClickABot();
        ExecutorTick();
        ExecutorTick();

        Assert.Equal((0, 3), (bed.GetX, bed.GetY));
        Assert.Empty(AvatarEntries(sent));
        Assert.True(actor.Statusses.ContainsKey("lay"));
    }

    // A furni that cannot be walked on is moved onto the path of a walking user. The step he has announced onto its tile still happens, and he walks on over it at floor height with no entry.
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task ANonWalkableFurniMovedOntoAWalkingUsersPathDoesNotStopHim(bool noAnimation, bool stepAnnouncedOntoIt)
    {
        var (actor, tile) = await InPlaceRoom(noAnimation, altitude: false, turn: false, moveToUser: false, west: true);
        Assert.True(WiredRoomOperations.MoveItem(_room, tile, 3, 2, height: 0, animate: false, announce: false));
        await WalkTo(actor, 2, 0);
        ExecutorTick();
        await new MoveAvatarEvent(new RoomAvatarActionService(TimeProvider.System, null!, null!)).Parse(_client, ClientPacket(2, 3));

        for (var tick = 0; tick < 60 && !(stepAnnouncedOntoIt ? actor.SetStep && (actor.SetX, actor.SetY) == (2, 2) : (actor.X, actor.Y) == (2, 2) && actor.SetStep); tick++) {
            ExecutorTick();
        }

        FireByChat(actor);

        var heights = new List<double>();

        for (var tick = 0; tick < 80 && (actor.X, actor.Y) != (2, 3); tick++) {
            ExecutorTick();
            heights.Add(actor.Z);
        }

        Assert.Equal((2, 2, 0), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Equal((2, 3), (actor.X, actor.Y));
        Assert.All(heights, height => Assert.Equal(0, height, 3));
        Assert.Empty(AvatarEntries(_client.Packets.ToArray()));
    }
}
