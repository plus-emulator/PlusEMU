using Plus.HabboHotel.Rooms.Games.Freeze;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint GamePlayerValueHeader = 786;

    [Fact]
    public void StartingAFreezeGameShowsThePlayerThreeLives()
    {
        FreezeTile(10, 1, 1);
        var red = FreezePlayer(Viewer(1, 1), Team.Red);
        FreezePlayer(SecondFreezeUser(3, 3), Team.Blue);
        _client.Packets.Clear();

        _room.GetFreeze().StartGame();

        Assert.Equal([(red.VirtualId, 3)], PlayerValues());
    }

    [Fact]
    public void AFreezeHitShowsThePlayerTheLivesLeft()
    {
        var tile = FreezeTile(10, 1, 1);
        var red = FreezePlayer(Viewer(1, 1), Team.Red);
        FreezePlayer(SecondFreezeUser(3, 3), Team.Blue);
        var freeze = _room.GetFreeze();
        freeze.StartGame();
        _client.Packets.Clear();

        freeze.OnFreezeTiles(tile, FreezePowerUp.None);

        Assert.Equal([(red.VirtualId, 2)], PlayerValues());
    }

    private List<(int User, int Value)> PlayerValues() => [.. _client.Packets.Where(packet => packet.Header == GamePlayerValueHeader)
        .Select(packet => (System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(packet.Body), System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(packet.Body.AsSpan(4))))];
}
