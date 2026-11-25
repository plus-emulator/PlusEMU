using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.Administrator;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator.Fun;
using Plus.HabboHotel.Rooms.Chat.Commands.User;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData]
    [InlineData("-1")]
    [InlineData("cola")]
    public void CarryRefusesAMissingOrNegativeItemWithAWhisper(params string[] parameters)
    {
        var actor = OwnerInRoom();

        new CarryCommand().Execute(_client, _room, parameters);

        Assert.Equal(0, actor.CarryItemId);
        Assert.Equal([ServerPacketHeader.WhisperComposer], _client.Sent);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("5", 5)]
    public void CarryStillSetsOrClearsTheHandItem(string itemId, int expected)
    {
        var actor = OwnerInRoom();

        new CarryCommand().Execute(_client, _room, [itemId]);

        Assert.Equal(expected, actor.CarryItemId);
        Assert.Contains(ServerPacketHeader.CarryObjectComposer, _client.Sent);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("-2147483648")]
    public void MassEnableRefusesANegativeEffectWithAWhisper(string effectId)
    {
        OwnerInRoom();

        new MassEnableCommand().Execute(_client, _room, [effectId]);

        Assert.Equal(0, _client.GetHabbo().Effects.CurrentEffect);
        Assert.Equal([ServerPacketHeader.WhisperComposer], _client.Sent);
    }

    [Fact]
    public void MassEnableStillAppliesAnEffect()
    {
        OwnerInRoom();

        new MassEnableCommand().Execute(_client, _room, ["4"]);

        Assert.Equal(4, _client.GetHabbo().Effects.CurrentEffect);
        Assert.Contains(ServerPacketHeader.AvatarEffectComposer, _client.Sent);
    }

    [Theory]
    [InlineData("-5")]
    [InlineData("-1")]
    [InlineData("4")]
    [InlineData("2147483647")]
    public void SetSpeedRefusesASpeedOutsideTheRollerControllerRange(string speed)
    {
        OwnerInRoom();

        new SetSpeedCommand().Execute(_client, _room, [speed]);

        Assert.Equal(4, _room.GetRoomItemHandler().RollerStepTicks);
        Assert.Equal([ServerPacketHeader.WhisperComposer], _client.Sent);
    }

    [Theory]
    [InlineData("0", 1)]
    [InlineData("2", 3)]
    [InlineData("3", 4)]
    public void SetSpeedStillSetsASpeedInsideTheRollerControllerRange(string speed, int stepTicks)
    {
        OwnerInRoom();
        _room.GetRoomItemHandler().SetSpeed(1);

        new SetSpeedCommand().Execute(_client, _room, [speed]);

        Assert.Equal(stepTicks, _room.GetRoomItemHandler().RollerStepTicks);
    }

    [Theory]
    [InlineData("disco")]
    [InlineData("99999999999")]
    [InlineData("-1")]
    [InlineData("5")]
    public void MassDanceRefusesAnInvalidDanceWithAWhisper(string danceId)
    {
        var actor = OwnerInRoom();

        new MassDanceCommand().Execute(_client, _room, [danceId]);

        Assert.Equal(0, actor.DanceId);
        Assert.Equal([ServerPacketHeader.WhisperComposer], _client.Sent);
    }

    private RoomUser OwnerInRoom()
    {
        var habbo = _client.GetHabbo();
        habbo.Client = _client;
        habbo.Effects = new(_interactionClock);
        habbo.Effects.Init(habbo);
        habbo.IgnoresComponent = new([]);
        Assert.True(_room.GetRoomUserManager().AddAvatarToRoom(_client));
        _client.Sent.Clear();
        _client.Packets.Clear();

        return Assert.IsType<RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id));
    }
}
