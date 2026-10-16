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
using Plus.HabboHotel.Rooms.AI;
using Xunit;

namespace Plus.Tests;

// The 13 reference cards, exercised through the same fixture the other wired action tests use.
public partial class ModernWiredRuntimeTests
{
    private static RoomUser AddNamedBot(TeleportFixture f, int virtualId, string name, int x, int y)
    {
        var bot = Bot(f.Room, virtualId);
        bot.BotData!.Name = name;
        bot.BotData.ChatBubble = 31;
        bot.BotData.AiType = BotAiType.Generic;
        bot.SetPos(x, y, 0);
        RoomUsers(f.Room)[virtualId] = bot;
        f.Room.GetGameMap().AddUserToMap(bot, new(x, y));

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

    [Fact]
    public void BotMoveOnAFurniTheBotAlreadyStandsOnArrivesOnTheNextPoll()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_move", intParams: [100, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 1, 1);
        f.Fire();
        var arrival = Assert.Single(WiredBotTargets.For(f.Room).Poll(f.Room));
        Assert.Equal(WiredEventKind.BotReachedFurni, arrival.Kind);
        Assert.Same(bot, arrival.Actor);
    }

    [Fact]
    public void BotMoveSendsTheBotWalkingAndRaisesOneArrivalOnTheFurni()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_move", intParams: [100, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        f.Fire();
        var targets = WiredBotTargets.For(f.Room);
        Assert.True(targets.HasTargets);
        Assert.True(bot.BotData!.ForcedMovement);
        Assert.Equal(new Point(1, 1), bot.BotData.TargetCoordinate);
        Assert.Empty(targets.Poll(f.Room));
        bot.SetPos(1, 1, 0);
        var arrival = Assert.Single(targets.Poll(f.Room));
        Assert.Equal(WiredEventKind.BotReachedFurni, arrival.Kind);
        Assert.Same(bot, arrival.Actor);
        Assert.Same(f.Target, arrival.EventItem);
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
        Assert.Equal(-1, chat.Receiver);
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
        Assert.Equal(0, secret.Receiver);

        using var talk = new TeleportFixture(actionName: "wf_act_bot_talk_to_avatar", intParams: [0, 0, 100, -1], text: "Bobby\tHi");
        AddNamedBot(talk, 9, "Bobby", 2, 2);
        replies = Capture(talk.Client);
        talk.Fire();
        var open = Assert.Single(Chats(replies));
        Assert.Equal(ServerPacketHeader.ChatComposer, open.Header);
        Assert.Equal(-1, open.Receiver);
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

    [Fact]
    public void BotFollowRaisesOneArrivalPerApproachOnceNextToTheUser()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_follow_avatar", intParams: [1, 0, 100], text: "Bobby");
        var bot = AddNamedBot(f, 9, "Bobby", 2, 2);
        f.Fire();
        var targets = WiredBotTargets.For(f.Room);
        Assert.Empty(targets.Poll(f.Room));
        bot.SetPos(1, 0, 0);
        var arrival = Assert.Single(targets.Poll(f.Room));
        Assert.Equal(WiredEventKind.BotReachedUser, arrival.Kind);
        Assert.Same(bot, arrival.Actor);
        Assert.Same(f.User, arrival.TargetUser);
        Assert.Empty(targets.Poll(f.Room));
        bot.SetPos(2, 2, 0);
        Assert.Empty(targets.Poll(f.Room));
        bot.SetPos(0, 1, 0);
        Assert.Single(targets.Poll(f.Room));
    }

    [Fact]
    public void BotHandItemGivesTheItemToTheUserWithOrWithoutABot()
    {
        // The editor's "give hand item using bot" box is optional: unticked, it sends an empty name and the default bot source.
        using var optional = new TeleportFixture(actionName: "wf_act_bot_give_handitem", intParams: [5, 0, 100], text: "");
        optional.Fire();
        Assert.Equal(5, optional.User.CarryItemId);

        using var named = new TeleportFixture(actionName: "wf_act_bot_give_handitem", intParams: [8, 0, 100], text: "Bobby");
        AddNamedBot(named, 9, "Bobby", 2, 2);
        named.Fire();
        Assert.Equal(8, named.User.CarryItemId);

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
        second.SetState(2, 0, 0, Gamemap.GetAffectedTiles(1, 1, 2, 0, 0));
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
    public void BotReachedFurniWithABlankNameAndNoPicksAcceptsAnyBotOnAnyFurni()
    {
        using var f = TalkingStack();
        var other = AddNamedBot(f, 10, "Carl", 2, 1);
        AddStackTrigger(f, 104, "wf_trg_bot_reached_stf", new() { IntParams = [100, 100], Text = "" });
        var replies = Capture(f.Client);
        f.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.BotReachedFurni) { Actor = other, EventItem = f.Target });
        f.Advance(0);
        Assert.Single(Chats(replies));
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

    [Fact]
    public void BotReachedAvatarHandsTheReachedUserToTheActionsAsTheTriggeringUser()
    {
        using var f = new TeleportFixture(actionName: "wf_act_bot_talk_to_avatar", intParams: [1, 0, 100, -1], text: "Bobby\twelcome");
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
        Assert.Null(box.Poll(milliseconds - 1));
        Assert.Equal(WiredEventKind.Elapsed, box.Poll(milliseconds)!.Kind);
        Assert.False(WiredNativeTestSupport.TryValidateRuntime(box, new() { IntParams = [1201] }, out _, out _));
        Assert.False(WiredNativeTestSupport.TryValidateRuntime(box, new() { IntParams = [0] }, out _, out _));
    }
}
