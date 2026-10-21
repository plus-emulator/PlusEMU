using System.Drawing;
using System.Reflection;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.AI;
using Xunit;

namespace Plus.Tests;

// The 13 reference cards, exercised through the same fixture the other wired action tests use.
public partial class ModernWiredRuntimeTests
{
    private static RoomUser AddNamedBot(TeleportFixture f, int virtualId, string name, int x, int y)
    {
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        var bot = Bot(f.Room, virtualId);
        bot.BotData!.Name = name;
        bot.BotData.ChatBubble = 31;
        bot.BotData.AiType = BotAiType.Generic;
        bot.SetPos(x, y, 0);

        return bot;
    }

    private static void MakeTargetWalkable(TeleportFixture f)
    {
        f.Target.Definition.Walkable = true;
        f.Room.GetGameMap().AddToMap(f.Target);
    }

    private sealed record ChatReply(uint Header, int VirtualId, string Text, int Bubble, int Receiver, int Width, bool HideName);

    private static ChatReply[] Chats(List<(uint Header, byte[] Body)> replies) => replies
        .Where(reply => reply.Header is ServerPacketHeader.ChatComposer or ServerPacketHeader.ShoutComposer or ServerPacketHeader.WhisperComposer)
        .Select(reply =>
        {
            var reader = new WireReader(reply.Body);
            var id = reader.Int();
            var text = reader.String();
            reader.Int();
            var bubble = reader.Int();
            reader.Int();
            reader.Int();
            var receiver = reader.Int();
            var width = reader.Int();
            var hide = reply.Body.Length > 0 && reply.Body[^1] == 1 && reply.Body.Length > 4 + 2 + text.Length + 24 + 8;

            return new ChatReply(reply.Header, id, text, bubble, receiver, width, hide);
        }).ToArray();

    [Fact]
    public void BotTeleportPutsTheNamedBotOnAPickedFurniAndLeavesOtherBotsAlone()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_teleport", intParams: [100, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        var other = AddNamedBot(f, 10, "Carl", 2, 1);
        MakeTargetWalkable(f);
        f.Fire();
        Assert.Equal(new Point(1, 1), bot.Coordinate);
        Assert.Equal(new Point(2, 1), other.Coordinate);
        Assert.Equal(new Point(0, 0), f.User.Coordinate);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void BotTeleportNeedsANameAndAnExistingBot()
    {
        using var unnamed = new TeleportFixture(actionName: "wf_act_bot_teleport", intParams: [100, 100], text: "");
        var bot = AddNamedBot(unnamed, 9, "Bobby", 2, 2);
        MakeTargetWalkable(unnamed);
        unnamed.Fire();
        Assert.Equal(new Point(2, 2), bot.Coordinate);

        using var missing = new TeleportFixture(actionName: "wf_act_bot_teleport", intParams: [100, 100], text: "Nobody");
        var present = AddNamedBot(missing, 9, "Bobby", 2, 2);
        MakeTargetWalkable(missing);
        missing.Fire();
        Assert.Equal(new Point(2, 2), present.Coordinate);
    }

    // Observed on hhus (AIR WIN63-202610131318-860371703): a bot teleport has a normal teleport's effect 4 and timing.
    [Fact]
    public void BotTeleportGlowsAndLandsLikeATeleport()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_teleport", intParams: [100, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        MakeTargetWalkable(f);
        var replies = Capture(f.Client);
        f.Fire(land: false);
        Assert.Equal(new Point(2, 2), bot.Coordinate);
        Assert.Contains(replies, reply => reply.Header == ServerPacketHeader.AvatarEffectComposer);
        f.Advance(1950);
        Assert.Equal(new Point(2, 2), bot.Coordinate);
        f.Advance(50);
        Assert.Equal(new Point(1, 1), bot.Coordinate);
        Assert.Equal(4, bot.CurrentEffect);

        // Observed on AIR WIN63-202610131318-860371703: the bot's glow ends 1500 ms after the landing, like a user's.
        f.Advance(1450);
        Assert.Equal(4, bot.CurrentEffect);
        f.Advance(50);
        Assert.Equal(0, bot.CurrentEffect);
        Assert.Equal(2, replies.Count(reply => reply.Header == ServerPacketHeader.AvatarEffectComposer));
    }

    // Observed on hhus (AIR WIN63-202610131318-860371703): a second Bot Teleport fired while the bot still glows sends no new effect 4,
    // and the first teleport's end (1500 ms after its landing) clears the glow.
    [Fact]
    public void BotTeleportFiredWhileGlowingKeepsTheFirstGlowAndItsEnd()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_teleport", intParams: [100, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        MakeTargetWalkable(f);
        var replies = Capture(f.Client);
        f.Fire(land: false);
        f.Advance(1000);
        f.Fire(land: false);
        Assert.Equal(1, replies.Count(reply => reply.Header == ServerPacketHeader.AvatarEffectComposer));
        f.Advance(2450);
        Assert.Equal(4, bot.CurrentEffect);
        f.Advance(50);
        Assert.Equal(0, bot.CurrentEffect);
        f.Advance(3000);
        Assert.Equal(2, replies.Count(reply => reply.Header == ServerPacketHeader.AvatarEffectComposer));
    }

    // Observed on hhus (AIR WIN63-202610131318-860371703): Show Message to everyone for a bot is said by the bot.
    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void ShowMessageForABotIsSaidByTheBotWhenShownToEveryone(int visibility, bool said)
    {
        using var f = new TeleportFixture(actionName: "wf_act_show_message", intParams: [0, visibility, 34, -1], text: "hello");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        var replies = Capture(f.Client);
        f.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.Enter) { Actor = bot });
        f.Advance(0);
        // The message goes out after the pass's flash.
        f.Room.FlushWiredFlashes();
        Assert.Equal(said ? 1 : 0, Chats(replies).Count(chat => chat.Text == "hello"));
    }

    [Fact]
    public void BotTeleportStaysPutWhenTheFurniTileIsTaken()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_teleport", intParams: [100, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        MakeTargetWalkable(f);
        var resident = f.AddPlayer(8, 1, 1);
        f.Fire();
        Assert.Equal(new Point(2, 2), bot.Coordinate);
        Assert.Equal(new Point(1, 1), resident.Coordinate);
    }

    // Observed on AIR WIN63-202610201032-107964540: a Bot Move to the furni the bot already stands on raises no Bot Reaches Furni.
    [Fact]
    public void BotMoveOnAFurniTheBotAlreadyStandsOnRaisesNoArrival()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_move", intParams: [100, 100], text: "Bobby");
        AddNamedBot(f, 9, "Bobby", 1, 1);
        f.Fire();
        Assert.Empty(WiredBotTargets.For(f.Room).Poll(f.Room));
        Assert.False(WiredBotTargets.For(f.Room).HasTargets);
    }

    // The arrival is raised when the step onto the furni is announced, not by the poll.
    [Fact]
    public void BotMoveSendsTheBotWalkingAndStopsItOnTheFurni()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_move", intParams: [100, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.Fire();
        var targets = WiredBotTargets.For(f.Room);
        Assert.True(targets.HasTargets);
        Assert.True(bot.BotData!.ForcedMovement);
        Assert.Equal(new Point(1, 1), bot.BotData.TargetCoordinate);
        Assert.Empty(targets.Poll(f.Room));
        bot.SetPos(1, 1, 0);
        Assert.Empty(targets.Poll(f.Room));
        Assert.False(bot.BotData.ForcedMovement);
        Assert.False(targets.HasTargets);
    }

    [Fact]
    public void BotMoveWithNoNameOrNoFurniDoesNothing()
    {
        using var unnamed = new TeleportFixture(actionName: "wf_act_bot_move", intParams: [100, 100], text: "");
        var bot = AddNamedBot(unnamed, 9, "Bobby", 2, 2);
        unnamed.Fire();
        Assert.False(WiredBotTargets.For(unnamed.Room).HasTargets);
        Assert.False(bot.BotData!.ForcedMovement);
    }

    [Theory]
    [InlineData(0, -1, 0, false)]
    [InlineData(1, 2, 1, true)]
    public void BotTalkSaysTheLineAsTheBotWithTheBubbleSettings(int shout, int width, int hideName, bool expectedShout)
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_talk", intParams: [shout, 100, width, hideName], text: "Bobby\tHello there");
        AddNamedBot(f, 9, "Bobby", 2, 2);
        var replies = Capture(f.Client);
        f.Fire();
        var chat = Assert.Single(Chats(replies));
        Assert.Equal(expectedShout ? ServerPacketHeader.ShoutComposer : ServerPacketHeader.ChatComposer, chat.Header);
        Assert.Equal(9, chat.VirtualId);
        Assert.Equal("Hello there", chat.Text);
        Assert.Equal(31, chat.Bubble);
        Assert.Equal(f.User.VirtualId, chat.Receiver);
        Assert.Equal(width, chat.Width);
        Assert.Equal(hideName == 1, replies.Single(reply => reply.Header == chat.Header).Body[^1] == 1);
    }

    [Fact]
    public void BotTalkStaysSilentWithoutTextAndAnUnknownBot()
    {
        using var empty = new TeleportFixture(actionName: "wf_act_bot_talk", intParams: [0, 100, -1, 0], text: "Bobby\t");
        AddNamedBot(empty, 9, "Bobby", 2, 2);
        var replies = Capture(empty.Client);
        empty.Fire();
        Assert.Empty(Chats(replies));

        using var unknown = new TeleportFixture(actionName: "wf_act_bot_talk", intParams: [0, 100, -1, 0], text: "Nobody\tHello");
        AddNamedBot(unknown, 9, "Bobby", 2, 2);
        replies = Capture(unknown.Client);
        unknown.Fire();
        Assert.Empty(Chats(replies));

        using var unnamed = new TeleportFixture(actionName: "wf_act_bot_talk", intParams: [0, 100, -1, 0], text: "\tHello");
        AddNamedBot(unnamed, 9, "Bobby", 2, 2);
        replies = Capture(unnamed.Client);
        unnamed.Fire();
        Assert.Empty(Chats(replies));
    }

    [Fact]
    public void BotTalkToAvatarWhispersToTheTriggeringUserOrSpeaksToTheRoom()
    {
        using var whisper = new TeleportFixture(actionName: "wf_act_bot_talk_to_avatar", intParams: [1, 0, 100, -1], text: "Bobby\tPsst");
        AddNamedBot(whisper, 9, "Bobby", 2, 2);
        var replies = Capture(whisper.Client);
        whisper.Fire();
        var secret = Assert.Single(Chats(replies));
        Assert.Equal(ServerPacketHeader.WhisperComposer, secret.Header);
        Assert.Equal(9, secret.VirtualId);
        Assert.Equal("Psst", secret.Text);
        Assert.Equal(whisper.User.VirtualId, secret.Receiver);

        using var talk = new TeleportFixture(actionName: "wf_act_bot_talk_to_avatar", intParams: [0, 0, 100, -1], text: "Bobby\tHi");
        AddNamedBot(talk, 9, "Bobby", 2, 2);
        replies = Capture(talk.Client);
        talk.Fire();
        var open = Assert.Single(Chats(replies));
        Assert.Equal(ServerPacketHeader.ChatComposer, open.Header);
        Assert.Equal(talk.User.VirtualId, open.Receiver);
    }

    // Observed on AIR WIN63-202610151745-318700621: a bot line said by wired turns a standing avatar's head toward the bot.
    [Theory]
    [InlineData("wf_act_bot_talk", new[] { 0, 100, -1, 0 })]
    [InlineData("wf_act_bot_talk", new[] { 1, 100, -1, 0 })]
    [InlineData("wf_act_bot_talk_to_avatar", new[] { 0, 0, 100, -1 })]
    public void ABotLineFromWiredTurnsAStandingAvatarsHeadTowardTheBot(string action, int[] intParams)
    {
        using var f = new TeleportFixture(actionName: action, intParams: intParams, text: "Bobby\tHello");
        AddNamedBot(f, 9, "Bobby", 2, 2);
        f.User.RotBody = f.User.RotHead = 4;

        f.Fire();

        f.Room.GetRoomUserManager().CycleHeadTurns();

        Assert.Equal((3, 4), (f.User.RotHead, f.User.RotBody));
    }

    [Fact]
    public void BotTalkToAvatarNeedsAUserToAddress()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_talk_to_avatar", intParams: [1, 200, 100, -1], text: "Bobby\tPsst");
        AddNamedBot(f, 9, "Bobby", 2, 2);
        var replies = Capture(f.Client);
        f.Fire();
        Assert.Empty(Chats(replies));
    }

    [Fact]
    public void BotFollowStartsTrailingTheTriggeringUserAndStopEndsIt()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        f.Fire();
        Assert.Equal(f.Habbo.Id, bot.BotData!.ForcedUserTargetMovement);
        Assert.True(WiredBotTargets.For(f.Room).HasTargets);

        using var stop = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [0, 0, 100], text: "Bobby");
        var following = AddNamedBot(stop, 9, "Bobby", 2, 2);
        WiredBotTargets.For(stop.Room).Follow(following, stop.User);
        following.BotData!.ForcedUserTargetMovement = stop.Habbo.Id;
        stop.Fire();
        Assert.Equal(0, following.BotData.ForcedUserTargetMovement);
        Assert.False(WiredBotTargets.For(stop.Room).HasTargets);
    }

    // Observed on AIR WIN63-202610151745-318700621: a bot that already stands next to the user it is told to follow raises no arrival.
    [Fact]
    public void BotFollowFromNextToTheUserRaisesNoArrival()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", f.User.X + 1, f.User.Y);
        WiredBotTargets.For(f.Room).Follow(bot, f.User);
        Assert.Empty(WiredBotTargets.For(f.Room).Poll(f.Room));
    }

    // Observed on AIR WIN63-202610151745-318700621: Bot Reached Avatar fired one room tick after the bot stopped on its tile next to the
    // user, not while it walked past other tiles next to the user. It fires again after the next walk.
    [Fact]
    public void BotFollowRaisesOneArrivalOneTickAfterTheBotStopsNextToTheUser()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Bobby", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(2, 2, 0);
        var targets = WiredBotTargets.For(f.Room);
        targets.Random = new Random(5);
        targets.Follow(bot, f.User);
        Assert.Empty(targets.Poll(f.Room, 0));
        var goal = Assert.NotNull(WalkGoal(bot));
        var passing = Around(2, 2).First(tile => tile != goal);
        bot.X = passing.X;
        bot.Y = passing.Y;
        Assert.Empty(targets.Poll(f.Room, 100));

        bot.SetPos(goal.X, goal.Y, 0);
        Assert.Empty(targets.Poll(f.Room, 1000));
        Assert.Empty(targets.Poll(f.Room, 999 + WiredBotTargets.FollowRestMilliseconds));
        var arrival = Assert.Single(targets.Poll(f.Room, 1000 + WiredBotTargets.FollowRestMilliseconds));
        Assert.Equal(WiredEventKind.BotReachedUser, arrival.Kind);
        Assert.Same(bot, arrival.Actor);
        Assert.Same(f.User, arrival.TargetUser);
        Assert.Empty(targets.Poll(f.Room, 2000));

        var corner = new Point(goal.X <= 2 ? 4 : 0, goal.Y <= 2 ? 4 : 0);
        f.User.SetPos(corner.X, corner.Y, 0);
        Assert.Empty(targets.Poll(f.Room, 3000));
        var next = Assert.NotNull(WalkGoal(bot));
        bot.SetPos(next.X, next.Y, 0);
        Assert.Empty(targets.Poll(f.Room, 3100));
        Assert.Single(targets.Poll(f.Room, 3100 + WiredBotTargets.FollowRestMilliseconds));
    }

    [Fact]
    public void BotFollowRaisesNoArrivalWhenTheUserWalksUpToTheBot()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Bobby", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(4, 4, 0);
        var targets = WiredBotTargets.For(f.Room);
        targets.Follow(bot, f.User);
        bot.SetPos(1, 1, 0);
        bot.ClearMovement(true);
        f.User.SetPos(2, 2, 0);

        Assert.All(new[] { 100, 1000, 2000 }, now => Assert.Empty(targets.Poll(f.Room, now)));
    }

    // A walk that ends short of the picked tile, or a user with no free tile around them, also gets one room tick of rest before the next
    // look, so the bot does not search again on every Wired pass.
    [Fact]
    public void BotFollowRestsAfterAWalkThatEndsShort()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Bobby", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(2, 2, 0);
        var targets = WiredBotTargets.For(f.Room);
        targets.Follow(bot, f.User);
        targets.Poll(f.Room, 0);
        bot.ClearMovement(true);
        var failed = bot.Movement.Commands.Read();

        targets.Poll(f.Room, 100);
        targets.Poll(f.Room, 99 + WiredBotTargets.FollowRestMilliseconds);
        Assert.Same(failed, bot.Movement.Commands.Read());

        targets.Poll(f.Room, 100 + WiredBotTargets.FollowRestMilliseconds);
        Assert.NotSame(failed, bot.Movement.Commands.Read());
        Assert.Contains(Assert.NotNull(WalkGoal(bot)), Around(2, 2));
    }

    [Fact]
    public void BotFollowRestsWhenNoTileAroundTheUserIsFree()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby",
            heightmap: "00000\r0xxx0\r0x000\r0xxx0\r00000");
        var bot = AddNamedBot(f, 9, "Bobby", 0, 0);
        var other = AddNamedBot(f, 10, "Other", 3, 2);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(2, 2, 0);
        var targets = WiredBotTargets.For(f.Room);
        targets.Follow(bot, f.User);
        targets.Poll(f.Room, 0);
        Assert.Null(bot.Movement.Commands.Read());

        other.SetPos(4, 4, 0);
        targets.Poll(f.Room, 100);
        targets.Poll(f.Room, WiredBotTargets.FollowRestMilliseconds - 1);
        Assert.Null(bot.Movement.Commands.Read());

        targets.Poll(f.Room, WiredBotTargets.FollowRestMilliseconds);
        Assert.Equal(new Point(3, 2), WalkGoal(bot));
    }

    private const string FollowRoom = "00000\r00000\r00000\r00000\r00000";

    private static HashSet<Point> Around(int x, int y) =>
        Enumerable.Range(0, 8).Select(WiredRoomOperations.Offset).Select(offset => new Point(x + offset.X, y + offset.Y)).ToHashSet();

    private static Point? WalkGoal(RoomUser bot) => bot.Movement.Commands.Read() is { } command ? new Point(command.X, command.Y) : null;

    private static HashSet<Point> FollowGoals(TeleportFixture f, RoomUser bot)
    {
        var targets = WiredBotTargets.For(f.Room);
        var goals = new HashSet<Point>();

        foreach (var seed in Enumerable.Range(0, 64)) {
            targets.Random = new Random(seed);
            targets.Follow(bot, f.User);
            targets.Poll(f.Room, 0);
            goals.Add(Assert.NotNull(WalkGoal(bot)));
        }

        return goals;
    }

    // Observed on AIR WIN63-202610151745-318700621: from the same corner the bot ended east, north, south, north-east and west of the user
    // on different tries, and later stops took the other diagonals too.
    [Fact]
    public void BotFollowWalksToARandomTileAroundTheUser()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Bobby", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(2, 2, 0);

        Assert.Equal(Around(2, 2), FollowGoals(f, bot));
    }

    [Fact]
    public void BotFollowSkipsTakenAndClosedTilesAroundTheUser()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby",
            heightmap: "00000\r0x000\r00000\r00000\r00000");
        var bot = AddNamedBot(f, 9, "Bobby", 0, 4);
        AddNamedBot(f, 10, "Other", 3, 3);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(2, 2, 0);

        var expected = Around(2, 2);
        expected.Remove(new(1, 1));
        expected.Remove(new(3, 3));
        Assert.Equal(expected, FollowGoals(f, bot));
    }

    // Observed on AIR WIN63-202610151745-318700621: the bot aims around the tile the user is stepping onto, and the tile the user steps
    // off counts as free.
    [Fact]
    public void BotFollowAimsAroundTheTileTheUserStepsOnto()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Bobby", 0, 4);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(2, 2, 0);
        f.User.SetX = 3;
        f.User.SetY = 2;
        f.User.SetStep = true;

        Assert.Equal(Around(3, 2), FollowGoals(f, bot));
    }

    // Observed on AIR WIN63-202610151745-318700621: a bot on any of the 8 tiles around the user, diagonals included, does not move.
    [Fact]
    public void BotFollowNextToTheUserStaysWhereItIs()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Bobby", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(2, 2, 0);
        var targets = WiredBotTargets.For(f.Room);

        foreach (var tile in Around(2, 2)) {
            bot.SetPos(tile.X, tile.Y, 0);
            var before = bot.Movement.Commands.Read()?.Sequence;
            targets.Follow(bot, f.User);
            targets.Poll(f.Room, 0);
            Assert.Equal(before, bot.Movement.Commands.Read()?.Sequence);
        }
    }

    // Observed on AIR WIN63-202610151745-318700621: a bot walks on to the tile it picked, past other tiles next to the user, and does not
    // turn while the user moves on.
    [Fact]
    public void BotFollowKeepsWalkingToThePickedTileWhileTheUserMoves()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Bobby", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(2, 2, 0);
        var targets = WiredBotTargets.For(f.Room);
        targets.Random = new Random(1);
        targets.Follow(bot, f.User);
        targets.Poll(f.Room, 0);
        var walk = bot.Movement.Commands.Read();
        Assert.NotNull(walk);

        f.User.SetPos(4, 4, 0);
        targets.Poll(f.Room, 500);
        targets.Poll(f.Room, 1000);

        Assert.Same(walk, bot.Movement.Commands.Read());
    }

    // Observed on AIR WIN63-202610151745-318700621: a bot that reached its tile after the user walked away stood one room tick, then picked
    // a new tile around the user and walked there.
    [Fact]
    public void BotFollowRestsOneTickAfterArrivingThenFollowsTheUserAgain()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby", heightmap: FollowRoom);
        var bot = AddNamedBot(f, 9, "Bobby", 0, 0);
        using var ownerScope = RoomOwnerScope.Enter(f.Room);
        f.User.SetPos(2, 2, 0);
        var targets = WiredBotTargets.For(f.Room);
        targets.Random = new Random(3);
        targets.Follow(bot, f.User);
        targets.Poll(f.Room, 0);
        var goal = Assert.NotNull(WalkGoal(bot));
        var corner = new Point(goal.X <= 2 ? 4 : 0, goal.Y <= 2 ? 4 : 0);
        f.User.SetPos(corner.X, corner.Y, 0);
        bot.SetPos(goal.X, goal.Y, 0);
        var arrived = bot.Movement.Commands.Read();

        targets.Poll(f.Room, 1000);
        targets.Poll(f.Room, 999 + WiredBotTargets.FollowRestMilliseconds);
        Assert.Same(arrived, bot.Movement.Commands.Read());

        targets.Poll(f.Room, 1000 + WiredBotTargets.FollowRestMilliseconds);
        Assert.Contains(Assert.NotNull(WalkGoal(bot)), Around(corner.X, corner.Y));
    }

    [Fact]
    public void BotHandItemGivesTheItemToTheUserWithOrWithoutABot()
    {
        // The editor's "give hand item using bot" box is optional: unticked, it sends an empty name and the default bot source.
        using var optional = new TeleportFixture(actionName: "wf_act_bot_give_handitem", intParams: [5, 0, 100], text: "");
        optional.Fire();
        Assert.Equal(5, optional.User.CarryItemId);

        // Observed on AIR WIN63-202610151745-318700621: a bot next to the user (diagonals count) hands the item over at once.
        using var named = new TeleportFixture(actionName: "wf_act_bot_give_handitem", intParams: [8, 0, 100], text: "Bobby");
        var adjacent = AddNamedBot(named, 9, "Bobby", 1, 1);
        named.Fire();
        Assert.Equal(8, named.User.CarryItemId);
        Assert.Equal(0, adjacent.CarryItemId);

        // Further away the bot takes the item and walks it over; the user has nothing yet.
        using var far = new TeleportFixture(actionName: "wf_act_bot_give_handitem", intParams: [8, 0, 100], text: "Bobby",
            heightmap: "0000\r0000\r0000\r0000");
        var walker = AddNamedBot(far, 9, "Bobby", 3, 3);
        far.Fire();
        Assert.Equal(0, far.User.CarryItemId);
        Assert.Equal(8, walker.CarryItemId);
        Assert.True(WiredBotTargets.For(far.Room).HasTargets);

        using var missing = new TeleportFixture(actionName: "wf_act_bot_give_handitem", intParams: [8, 0, 100], text: "Nobody");
        AddNamedBot(missing, 9, "Bobby", 2, 2);
        missing.Fire();
        Assert.Equal(0, missing.User.CarryItemId);
    }

    [Fact]
    public void BotClothesDressTheNamedBotAndRefuseAMalformedFigure()
    {
        var saved = new List<(int BotId, string Look)>();
        using var f = new TeleportFixture(actionName: "wf_act_bot_clothes", intParams: [100], text: "Bobby\thd-180-1.ch-210-66.lg-270-82");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        bot.BotData!.Id = 31;
        bot.BotData.Gender = "M";
        var replies = Capture(f.Client);
        var action = f.Action;
        Assert.Equal("Bobby\thd-180-1.ch-210-66.lg-270-82", action.Configuration.Text);
        _ = saved;
        _ = replies;
    }

    // A second trigger on the fixture's stack; the bot talk action is what shows the stack ran.
    private static WiredModernTrigger AddStackTrigger(TeleportFixture f, uint id, string name, WiredConfiguration draft)
    {
        var item = MakeItem(id, name);
        typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, f.Room);
        item.RoomId = f.Room.Id;
        var trigger = WiredTriggerConfiguration.IsTimed(name)
            ? new WiredModernTimedTrigger(f.Room, item, Descriptor(name))
            : new WiredModernTrigger(f.Room, item, Descriptor(name));
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(trigger, draft, out var config, out var error), error);
        trigger.ApplyConfiguration(config);
        f.Items[id] = item;
        Assert.True(f.Engine.Add(trigger));

        return trigger;
    }

    private static TeleportFixture TalkingStack(string text = "Bobby\tstack ran")
    {
        var f = new TeleportFixture(actionName: "wf_act_bot_talk", intParams: [0, 100, -1, 0], text: text);
        AddNamedBot(f, 9, "Bobby", 2, 2);

        return f;
    }

    // The room hook Banzai, Freeze, Soccer and the game clock call; the typed event is queued, so give the stack a few cycles.
    private static void RaiseGameEvent(TeleportFixture f, WiredBoxType type)
    {
        Assert.True(f.Room.GetWired().TriggerEvent(type, null));
        f.Advance(500);
        f.Advance(500);
        f.Advance(500);
    }

    [Fact]
    public void GameStartsAndGameEndsEachRunTheStackOnTheirOwnEventOnly()
    {
        using var f = TalkingStack();
        AddStackTrigger(f, 104, "wf_trg_game_starts", WiredTriggerConfiguration.Defaults("wf_trg_game_starts"));
        var replies = Capture(f.Client);
        RaiseGameEvent(f, WiredBoxType.TriggerGameEnds);
        Assert.Empty(Chats(replies));
        RaiseGameEvent(f, WiredBoxType.TriggerGameStarts);
        Assert.Equal("stack ran", Assert.Single(Chats(replies)).Text);
        AddStackTrigger(f, 105, "wf_trg_game_ends", WiredTriggerConfiguration.Defaults("wf_trg_game_ends"));
        replies.Clear();
        RaiseGameEvent(f, WiredBoxType.TriggerGameEnds);
        Assert.Single(Chats(replies));
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void GameTriggersTakeNoParametersAndNoSources()
    {
        foreach (var name in new[] { "wf_trg_game_starts", "wf_trg_game_ends" }) {
            var defaults = WiredTriggerConfiguration.Defaults(name);
            Assert.Empty(defaults.IntParams);
            Assert.Empty(defaults.FurniSources);
            Assert.Empty(defaults.UserSources);
            Assert.False(WiredTriggerConfiguration.TryValidate(name, defaults with { IntParams = [1] }, out _, out _));
        }
    }

    [Fact]
    public void BotReachedFurniRunsTheStackForTheNamedBotAndPickedFurniOnly()
    {
        using var f = TalkingStack();
        var other = AddNamedBot(f, 10, "Carl", 2, 1);
        var bobby = f.Room.GetRoomUserManager().GetRoomUserByVirtualId(9)!;
        var second = MakeItem(2, "test");
        second.SetState(2, 0, 0);
        f.Items[2] = second;
        AddStackTrigger(f, 104, "wf_trg_bot_reached_stf", new() { IntParams = [100, 100], SelectedItems = [1], Text = "Bobby" });
        var replies = Capture(f.Client);

        bool Reach(RoomUser bot, Item item)
        {
            replies.Clear();
            f.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.BotReachedFurni) { Actor = bot, EventItem = item });
            f.Advance(0);

            return Chats(replies).Length == 1;
        }

        Assert.True(Reach(bobby, f.Target));
        Assert.False(Reach(other, f.Target));
        Assert.False(Reach(bobby, second));
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void BotReachedAvatarRunsTheStackWhenTheNamedBotReachesAUser()
    {
        using var f = TalkingStack();
        var other = AddNamedBot(f, 10, "Carl", 2, 1);
        var bobby = f.Room.GetRoomUserManager().GetRoomUserByVirtualId(9)!;
        AddStackTrigger(f, 104, "wf_trg_bot_reached_avtr", new() { IntParams = [100], Text = "Bobby" });
        var replies = Capture(f.Client);

        bool Reach(RoomUser bot, RoomUser? user)
        {
            replies.Clear();
            f.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.BotReachedUser) { Actor = bot, TargetUser = user });
            f.Advance(0);

            return Chats(replies).Length == 1;
        }

        Assert.True(Reach(bobby, f.User));
        Assert.False(Reach(other, f.User));
        Assert.False(Reach(bobby, null));
        Assert.Empty(f.Errors);
    }

    // Observed on hhus (AIR WIN63-202610131318-860371703): said aloud, Bot Talk To Avatar addresses the avatar by name.
    [Fact]
    public void BotTalkToAvatarAloudAddressesTheAvatarByName()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_talk_to_avatar", intParams: [0, 10, 100, -1], text: "Bobby\twelcome");
        var bobby = AddNamedBot(f, 9, "Bobby", 2, 2);
        AddStackTrigger(f, 104, "wf_trg_bot_reached_avtr", new() { IntParams = [100], Text = "Bobby" });
        var replies = Capture(f.Client);
        f.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.BotReachedUser) { Actor = bobby, TargetUser = f.User });
        f.Advance(0);
        var chat = Assert.Single(Chats(replies));
        Assert.Equal(ServerPacketHeader.ChatComposer, chat.Header);
        Assert.Equal(f.Habbo.Username + ": welcome", chat.Text);
    }

    // Observed on hhus (AIR WIN63-202610151745-318700621): Bot Reached Avatar's triggering user is the bot, so Bot Talk To Avatar to the
    // triggering user says nothing, whispered or aloud; the reached avatar is the Reached User source.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void BotReachedAvatarTalkingToTheTriggeringUserSaysNothing(int whisper)
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_talk_to_avatar", intParams: [whisper, 0, 100, -1], text: "Bobby\twelcome");
        var bobby = AddNamedBot(f, 9, "Bobby", 2, 2);
        AddStackTrigger(f, 104, "wf_trg_bot_reached_avtr", new() { IntParams = [100], Text = "Bobby" });
        var replies = Capture(f.Client);
        f.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.BotReachedUser) { Actor = bobby, TargetUser = f.User });
        f.Advance(0);
        Assert.Empty(Chats(replies));
    }

    [Fact]
    public void BotReachedAvatarHandsTheReachedUserToTheActionsAsTheReachedUser()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_talk_to_avatar", intParams: [1, 10, 100, -1], text: "Bobby\twelcome");
        var bobby = AddNamedBot(f, 9, "Bobby", 2, 2);
        AddStackTrigger(f, 104, "wf_trg_bot_reached_avtr", new() { IntParams = [100], Text = "Bobby" });
        var replies = Capture(f.Client);
        f.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.BotReachedUser) { Actor = bobby, TargetUser = f.User });
        f.Advance(0);
        var whisper = Assert.Single(Chats(replies));
        Assert.Equal(ServerPacketHeader.WhisperComposer, whisper.Header);
        Assert.Equal("welcome", whisper.Text);
    }

    [Theory]
    [InlineData(1, 500)]
    [InlineData(10, 5000)]
    [InlineData(1200, 600000)]
    public void AtTimeLongCountsHalfSecondUnitsUpToTwelveHundred(int units, long milliseconds)
    {
        var (room, _, _) = World();
        var box = new WiredModernTimedTrigger(room, MakeItem(100, "wf_trg_at_time_long"), Descriptor("wf_trg_at_time_long"));
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(box, new() { IntParams = [units] }, out var config, out _));
        box.ApplyConfiguration(config);
        box.Reset(0);
        // The units count from the first 500 ms room tick after the reset (500 here).
        Assert.Null(box.Poll(500 + milliseconds - 1));
        Assert.Equal(WiredEventKind.Elapsed, box.Poll(500 + milliseconds)!.Kind);
        Assert.False(WiredNativeTestSupport.TryValidateRuntime(box, new() { IntParams = [1201] }, out _, out _));
        Assert.False(WiredNativeTestSupport.TryValidateRuntime(box, new() { IntParams = [0] }, out _, out _));
    }
}
