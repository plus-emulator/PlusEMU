using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A bot sent by Bot Move sends one idle status when it arrives, and no second one
    // a tick later.
    [Fact]
    public void BotMoveArrivalSendsOneIdleStatus()
    {
        var goal = ExecutorFloor(11, 2, 1);
        Viewer();
        var bot = WalkActor(WalkActorKind.Bot, 0, 1, v2: true);
        var targets = WiredBotTargets.For(_room);
        SendBot(bot, goal, targets);
        var statuses = new List<string?>();

        // Each room tick also runs the Wired pass that polls the bot targets.
        for (var tick = 0; tick < 8; tick++) {
            ExecutorTick();
            statuses.Add(BotStatus(bot));
        }

        Assert.Equal((2, 1), (bot.X, bot.Y));
        Assert.False(targets.HasTargets);
        Assert.Single(statuses, status => status != null && !status.Contains("mv"));
        Assert.Null(statuses[^1]);
    }

    // A bot sent on to new furni while it steps onto its old goal starts the new walk
    // in the tick that ends that step, with no idle status in between.
    [Fact]
    public void BotMoveSentOnDuringItsLastStepWalksOnInTheNextTick()
    {
        var first = ExecutorFloor(11, 2, 0);
        var second = ExecutorFloor(12, 2, 2);
        Viewer();
        var bot = WalkActor(WalkActorKind.Bot, 0, 0, v2: true);
        var targets = WiredBotTargets.For(_room);
        SendBot(bot, first, targets);

        for (var tick = 0; tick < 8 && !(bot.SetStep && WiredRoomOperations.StandingTile(bot) == new System.Drawing.Point(2, 0)); tick++) {
            ExecutorTick();
        }

        Assert.True(bot.SetStep);
        SendBot(bot, second, targets);
        ExecutorTick();
        var status = BotStatus(bot);

        Assert.Equal((2, 0), (bot.X, bot.Y));
        Assert.Contains("mv 2,1,", status);
    }

    // Bot Follow Avatar set to stop sends no status for a bot that already stands still.
    [Fact]
    public void BotFollowStopSendsNoStatusForAStandingBot()
    {
        var user = Viewer(1, 1);
        var bot = WalkActor(WalkActorKind.Bot, 0, 1, v2: true);
        var targets = WiredBotTargets.For(_room);
        targets.Follow(bot, user);
        ExecutorTick();

        targets.Stop(bot);
        ExecutorTick();

        Assert.Null(BotStatus(bot));
        Assert.False(targets.HasTargets);
    }

    private static void SendBot(RoomUser bot, HabboHotel.Items.Item item, WiredBotTargets targets)
    {
        bot.BotData.ForcedUserTargetMovement = 0;
        bot.BotData.ForcedMovement = true;
        bot.BotData.TargetCoordinate = new(item.GetX, item.GetY);
        bot.MoveTo(item.GetX, item.GetY);
        targets.Walk(bot, item);
    }

    // The bot's status string from this tick's UserUpdate, or null when the tick sent none for it.
    private string? BotStatus(RoomUser bot)
    {
        string? found = null;

        foreach (var sent in _client.Packets.Where(packet => packet.Header == ServerPacketHeader.UserUpdateComposer)) {
            var body = new FlashIncomingPacket { Buffer = sent.Body.ToArray() };
            var count = body.ReadInt();

            for (var i = 0; i < count; i++) {
                var id = body.ReadInt();
                body.ReadInt();
                body.ReadInt();
                body.ReadString();
                body.ReadInt();
                body.ReadInt();
                body.ReadInt();
                var status = body.ReadString();

                if (id == bot.VirtualId) {
                    found = status;
                }
            }
        }

        return found;
    }
}
