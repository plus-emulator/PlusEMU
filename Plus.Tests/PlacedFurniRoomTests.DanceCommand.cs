using Plus.HabboHotel.Rooms.Chat.Commands.User.Fun;
using Xunit;

namespace Plus.Tests;

// The dance command keeps the server dance in step with the dance the room sees.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void TheDanceCommandStartsTheDanceOnTheServer()
    {
        var actor = OwnerInRoom();

        new DanceCommand().Execute(_client, _room, ["1"]);

        Assert.Equal(1, actor.DanceId);
        Assert.True(actor.IsDancing);
    }

    [Fact]
    public void TheDanceCommandWithZeroStopsTheDanceOnTheServer()
    {
        var actor = OwnerInRoom();
        new DanceCommand().Execute(_client, _room, ["1"]);

        new DanceCommand().Execute(_client, _room, ["0"]);

        Assert.Equal(0, actor.DanceId);
        Assert.False(actor.IsDancing);
    }
}
