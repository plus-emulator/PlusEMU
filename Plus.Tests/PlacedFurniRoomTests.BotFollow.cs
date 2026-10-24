using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // After Bot Follow stop a walking bot walks on to the tile it had picked, then stands.
    [Fact]
    public void BotFollowStopLetsAWalkingBotFinishItsWalk()
    {
        var user = Viewer(3, 3);
        var bot = WalkActor(WalkActorKind.Bot, 0, 0, v2: true);
        var targets = WiredBotTargets.For(_room);
        targets.Random = new Random(1);
        targets.Follow(bot, user);
        targets.Poll(_room, 0);
        var goal = bot.Movement.Commands.Read() is { } command ? (command.X, command.Y) : (-1, -1);
        Assert.NotEqual((-1, -1), goal);
        ExecutorTick();
        Assert.True(bot.HasStatus("mv"));

        using (Plus.HabboHotel.Rooms.PathFinding.RoomOwnerScope.Enter(_room)) {
            targets.Stop(bot);
        }

        var log = $"goal {goal}; after announce ({bot.X},{bot.Y}) setstep={bot.SetStep}; ";

        for (var tick = 0; tick < 8; tick++) {
            ExecutorTick();
            var sent = _client.Packets.Any(packet => packet.Header == ServerPacketHeader.UserUpdateComposer);
            log += $"t{tick}:({bot.X},{bot.Y}) mv={bot.HasStatus("mv")} uu={sent}; ";
        }

        Assert.True(goal == (bot.X, bot.Y), log);
    }
}
