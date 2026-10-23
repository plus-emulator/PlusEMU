using System.Collections.Concurrent;
using System.Reflection;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Speech;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// When a bot speaks, a standing avatar turns its head one step toward the bot on the next status update. The head goes back to the body direction 5000 ms after that update.
public sealed class BotSpeechHeadTurnTests
{
    private const int VisitorId = 9;
    private const int BotId = 31;

    [Theory]
    [InlineData(4, 5, 2, 3)]
    [InlineData(0, 5, 2, 1)]
    [InlineData(1, 5, 2, 2)]
    [InlineData(2, 4, 3, 3)]
    [InlineData(7, 2, 1, 0)]
    [InlineData(0, 1, 1, 7)]
    public void ABotLineTurnsAStandingAvatarsHeadOneStepTowardTheBot(int body, int botX, int botY, int head)
    {
        var world = new World(body, botX, botY);

        world.Bot.Chat("Yo!");

        Assert.Equal((head, body), world.NextStatusOf(world.Avatar));
    }

    [Fact]
    public void TheHeadGoesBackToTheBodyTenRoomCyclesAfterTheTurnWasShown()
    {
        var world = new World(4, 5, 2);
        world.Bot.Chat("Yo!");
        Assert.Equal((3, 4), world.NextStatusOf(world.Avatar));

        for (var cycle = 1; cycle < 10; cycle++) {
            Assert.Null(world.NextStatusOf(world.Avatar));
        }

        Assert.Equal((4, 4), world.NextStatusOf(world.Avatar));
        Assert.Null(world.NextStatusOf(world.Avatar));
    }

    [Fact]
    public void ANewBotLineStartsTheFiveSecondsAgain()
    {
        var world = new World(4, 5, 2);
        world.Bot.Chat("Yo!");
        world.NextStatusOf(world.Avatar);

        for (var cycle = 1; cycle < 6; cycle++) {
            world.NextStatusOf(world.Avatar);
        }

        world.Bot.Chat("Yo again!");

        for (var cycle = 0; cycle < 10; cycle++) {
            Assert.Null(world.NextStatusOf(world.Avatar));
        }

        Assert.Equal((4, 4), world.NextStatusOf(world.Avatar));
    }

    [Fact]
    public void ABotLineSaidDuringTheRoomTickShowsOnTheFollowingStatusUpdate()
    {
        var world = new World(4, 5, 2);
        world.Ai.OnTick = () => world.Bot.Chat("Yo!");

        Assert.Null(world.NextStatusOf(world.Avatar));
        world.Ai.OnTick = null;
        Assert.Equal((3, 4), world.NextStatusOf(world.Avatar));
    }

    [Fact]
    public void ABotStraightAheadPutsAnAlreadyTurnedHeadBackOnTheBody()
    {
        var world = new World(1, 5, 2);
        world.Bot.Chat("Yo!");
        Assert.Equal((2, 1), world.NextStatusOf(world.Avatar));

        world.Bot.X = 3;
        world.Bot.Y = 1;
        world.Bot.Chat("bra sel");

        Assert.Equal((1, 1), world.NextStatusOf(world.Avatar));
    }

    [Fact]
    public void ABotStraightAheadSendsNoStatusAtAll()
    {
        var world = new World(2, 5, 2);

        world.Bot.Chat("Yo!");

        for (var cycle = 0; cycle < 12; cycle++) {
            Assert.Null(world.NextStatusOf(world.Avatar));
        }
    }

    [Theory]
    [InlineData(6, 5, 2)]
    [InlineData(0, 4, 3)]
    [InlineData(5, 4, 1)]
    public void ABotBehindTheAvatarDoesNotTurnItsHead(int body, int botX, int botY)
    {
        var world = new World(body, botX, botY);

        world.Bot.Chat("Yo!");

        Assert.Null(world.NextStatusOf(world.Avatar));
        Assert.Equal(body, world.Avatar.RotHead);
    }

    [Theory]
    [InlineData("sit")]
    [InlineData("lay")]
    [InlineData("walk")]
    public void OnlyAStandingAvatarTurnsItsHead(string posture)
    {
        var world = new World(4, 5, 2);

        if (posture == "walk") {
            world.Avatar.IsWalking = true;
        }
        else {
            world.Avatar.SetStatus(posture, "0.5");
        }

        world.Bot.Chat("Yo!");

        Assert.Equal(4, world.Avatar.RotHead);
    }

    [Fact]
    public void TheSpeakingBotKeepsItsOwnHead()
    {
        var world = new World(4, 5, 2);

        world.Bot.Chat("Yo!");

        Assert.Equal(world.Bot.RotBody, world.Bot.RotHead);
    }

    private sealed class World
    {
        private readonly List<(uint Header, byte[] Payload)> _viewerPackets;

        public World(int avatarBody, int botX, int botY)
        {
            var model = new RoomModel("head-turn-test", 0, 0, 0, 0, "000000\r000000\r000000\r000000\r000000\r000000", 0, 0, false);
            Room = new Room(new RoomData { Id = 42, OwnerId = 1, OwnerName = "owner", Type = "private", Model = model }, [],
                TestLogging.Navigation, TestLogging.Logger, TestRoomAchievements.Unused, TestRoomOwners.Unused);
            Set(Room, "_gamemap", new Gamemap(Room, model, TestLogging.Navigation, TestRoomSettings.Empty, TestGroupManager.Empty, TestNavigationDatabase.Instance, TestNavigationRewards.Instance));
            Set(Room, "_roomUserManager", new RoomUserManager(Room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(), TestChatEmotions.Unused,
                new TestBotAiFactory((_, _) => Ai), TestGameClientManager.Empty, TestItemRuntime.Travel, TestItemRuntime.Settings));
            Set(Room, "_userSnapshots", CatalogSnapshotTestSupport.Proxy<IRoomUserSnapshotService>((_, _) => null));
            using var owner = RoomOwnerScope.Enter(Room);
            var manager = Room.GetRoomUserManager();
            var speech = new List<RandomSpeech>();
            Bot = manager.DeployBot(new(BotId, 42, "generic", "stand", "Robbie", "", "hr-100", botX, botY, 0, 6, 0, 0, 0, 0, ref speech, "M", 0,
                7, false, 7, false, 0), null!);

            var (viewer, packets) = HabbiconTestSupport.Client(new Habbo { Id = VisitorId, Username = "visitor", CurrentRoom = Room });
            _viewerPackets = packets;
            Avatar = new RoomUser(VisitorId, 42, 50, Room, viewer, TestChatEmotions.Unused, TestRewardProgress.Unused)
            {
                X = 2,
                Y = 2,
                RotBody = avatarBody,
                RotHead = avatarBody,
                InternalRoomId = 50
            };
            var users = (ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager)
                .GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
            Assert.True(users.TryAdd(Avatar.VirtualId, Avatar));
            Room.GetGameMap().Navigation.Admit(Avatar);
            NextStatusOf(Avatar);
        }

        public Room Room { get; }
        public TickingBotAi Ai { get; } = new();
        public RoomUser Bot { get; }
        public RoomUser Avatar { get; }

        public (int Head, int Body)? NextStatusOf(RoomUser unit)
        {
            _viewerPackets.Clear();

            using (RoomOwnerScope.Enter(Room)) {
                Room.GetRoomUserManager().OnCycle();
                Room.GetRoomUserManager().SerializeStatusUpdates();
            }

            foreach (var (_, payload) in _viewerPackets.Where(sent => sent.Header == ServerPacketHeader.UserUpdateComposer)) {
                var packet = new FlashIncomingPacket { Buffer = payload };
                var count = packet.ReadInt();

                for (var i = 0; i < count; i++) {
                    var id = packet.ReadInt();
                    packet.ReadInt();
                    packet.ReadInt();
                    packet.ReadString();
                    var (head, body) = (packet.ReadInt(), packet.ReadInt());
                    packet.ReadInt();
                    packet.ReadString();

                    if (id == unit.VirtualId) {
                        return (head, body);
                    }
                }
            }

            return null;
        }

        private static void Set(Room room, string field, object value) =>
            typeof(Room).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, value);
    }

    private sealed class TickingBotAi : BotAi
    {
        public Action? OnTick { get; set; }
        public override void OnSelfEnterRoom() { }
        public override void OnSelfLeaveRoom(bool kicked) { }
        public override void OnUserEnterRoom(RoomUser user) { }
        public override void OnUserLeaveRoom(GameClient client) { }
        public override void OnUserSay(RoomUser user, string message) { }
        public override void OnUserShout(RoomUser user, string message) { }
        public override void OnTimerTick() => OnTick?.Invoke();
    }
}
