using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// A furni that a firing moved onto a user and then changes in height or turns on that tile is not stopped by the user.
// A furni that a user already stood on before the firing is still refused.
public partial class PlacedFurniRoomTests
{
    private const uint InPlaceMove = 981, InPlaceOrder = 982, InPlaceNoAnimation = 983, InPlaceTurn = 984;

    private async Task<(RoomUser Actor, Item Tile)> InPlaceRoom(bool noAnimation, bool altitude, bool turn, bool moveToUser = true)
    {
        var (actor, tile, service) = BuiltInRuntimeRoom();

        if (altitude) {
            SaveRuntimeBox(service, Change, 0, "-123", 30, true);
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

    [Fact]
    public async Task AClickOnABotRaisesAFurniThatItMovedOntoAUserEarlierInTheFiring()
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: true, altitude: true, turn: false);
        Stand(actor, 0, 3);

        var sent = await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.Contains(sent, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    [Fact]
    public async Task AClickOnABotDoesNotRaiseAFurniThatAUserAlreadyStoodOn()
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: true, altitude: true, turn: false, moveToUser: false);
        Stand(actor, 2, 2);

        var sent = await ClickABot();

        Assert.Equal((2, 2, 0), (tile.GetX, tile.GetY, tile.GetZ));
        Assert.DoesNotContain(sent, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    [Fact]
    public async Task AClickOnABotDoesNotTurnAFurniThatAUserAlreadyStoodOn()
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: true, altitude: false, turn: true, moveToUser: false);
        Stand(actor, 2, 2);

        var sent = await ClickABot();

        Assert.Equal((2, 2, 0), (tile.GetX, tile.GetY, tile.Rotation));
        Assert.DoesNotContain(sent, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }

    [Fact]
    public async Task AnAnimatedMoveOntoAUserStillLetsTheFurniBeRaisedLaterInTheFiring()
    {
        var (actor, tile) = await InPlaceRoom(noAnimation: false, altitude: true, turn: false);
        Stand(actor, 0, 3);

        await ClickABot();

        Assert.Equal((0, 3, 0.3), (tile.GetX, tile.GetY, tile.GetZ));
    }
}
