using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540 (three runs, identical; a couch and a chair that can rotate, started facing 2): Change Variable Value
// writes @rotation as a position in the furni's list of directions (0, 2, 4, 6) and @occupation as x, y and such a position; a position that is not in
// the list (4 and more, or -1) changes nothing, and with @occupation it changes nothing at all, also when x or y differ. Give Variable (override on)
// and Remove Variable change neither variable.
public partial class PlacedFurniRoomTests
{
    private const string RotationId = "-122", OccupationId = "-126";

    // The tile starts on (0, 1) facing 2 (position 1 in the list); a row is the value written and the facing after it.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(3, 6)]
    [InlineData(4, 2)]
    [InlineData(-1, 2)]
    public void ChangeWritesRotationAsAPositionInTheListOfDirections(int value, int facing)
    {
        var (actor, tile, service) = RotatableRuntimeRoom();
        SaveRuntimeBox(service, Change, 0, RotationId, value, true);

        FireRuntimeStack(actor);

        Assert.Equal((0, 1, facing), (tile.GetX, tile.GetY, tile.Rotation));
    }

    [Theory]
    [InlineData(0, 0, 2, 0, 1, 4)]
    [InlineData(0, 0, 3, 0, 1, 6)]
    [InlineData(0, 0, 7, 0, 1, 2)]
    [InlineData(2, 0, 1, 2, 1, 2)]
    [InlineData(0, 2, 1, 0, 3, 2)]
    [InlineData(2, 0, 3, 2, 1, 6)]
    [InlineData(2, 0, 7, 0, 1, 2)]
    [InlineData(2, 0, 4, 0, 1, 2)]
    [InlineData(0, 0, 4, 0, 1, 2)]
    [InlineData(0, 0, 255, 0, 1, 2)]
    public void ChangeWritesOccupationAsPositionAndAPositionInTheListOfDirections(int dx, int dy, int direction, int x, int y, int facing)
    {
        var (actor, tile, service) = RotatableRuntimeRoom();
        SaveRuntimeBox(service, Change, 0, OccupationId, ((tile.GetX + dx) << 16) | ((tile.GetY + dy) << 8) | direction, true);

        FireRuntimeStack(actor);

        Assert.Equal((x, y, facing), (tile.GetX, tile.GetY, tile.Rotation));
    }

    [Theory]
    [InlineData(RotationId, 0)]
    [InlineData(RotationId, 3)]
    [InlineData(OccupationId, 66050)]
    public void GiveAndRemoveChangeNeitherRotationNorOccupation(string variable, int value)
    {
        var (actor, tile, service) = RotatableRuntimeRoom();
        SaveRuntimeBox(service, Give, 0, variable, value, true);
        FireRuntimeStack(actor);
        Assert.Equal((0, 1, 2), (tile.GetX, tile.GetY, tile.Rotation));

        PlaceSavedBox(RuntimeBox + 10, Remove, 1, 1, 2);
        _client.Packets.Clear();
        new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket((int)RuntimeBox + 10, 1, 0, "", 1, (int)RuntimeTile, 0, 1, 100, 1, 0, 1, variable, 0))
            .GetAwaiter().GetResult();
        Assert.Equal("saved", SaveAnswer());
        FireRuntimeStack(actor);
        Assert.Equal((0, 1, 2), (tile.GetX, tile.GetY, tile.Rotation));
    }

    private (Plus.HabboHotel.Rooms.RoomUser Actor, Plus.HabboHotel.Items.Item Tile, Plus.HabboHotel.Items.Wired.Configuration.WiredConfigurationService Service) RotatableRuntimeRoom()
    {
        var room = BuiltInRuntimeRoom(7, "0", 0, 1, 2);
        Assert.Equal((0, 1, 2), (room.Tile.GetX, room.Tile.GetY, room.Tile.Rotation));

        return room;
    }
}
