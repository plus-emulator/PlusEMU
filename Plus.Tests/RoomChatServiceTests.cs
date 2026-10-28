using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Plus.Communication.Packets.Incoming.Habbicons;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.Core.Settings;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Habbicons;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Rooms.Chat.Logs;
using Plus.HabboHotel.Rooms.Chat.Styles;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Effects;
using Plus.HabboHotel.Users.Ignores;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

public sealed class RoomChatServiceTests
{
    private static readonly DateTimeOffset Now = new(2040, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task HandlersDecodeFullPrimitiveRequestsAndDelegateOnly()
    {
        var service = new RecordingRoomChatService();
        GameClient session = null!;
        await new ChatEvent(service).Parse(session, HabbiconTestSupport.Incoming("chat", 2));
        await new ShoutEvent(service).Parse(session, HabbiconTestSupport.Incoming("shout", 3));
        await new WhisperEvent(service).Parse(session, HabbiconTestSupport.Incoming("Bob private words", 4));

        Assert.Equal(("chat", 2), service.ChatRequest);
        Assert.Equal(("shout", 3), service.ShoutRequest);
        Assert.Equal(("Bob private words", 4), service.WhisperRequest);
    }

    [Fact]
    public async Task FloodDenialStopsLoggingCommandsAndPublication()
    {
        var world = new World(new ZonedClock(Now, TimeZoneInfo.Utc));
        world.Sender.GetHabbo().FloodUntil = Now.AddSeconds(1);

        await world.Service.Chat(world.Sender, "blocked", 1);

        Assert.Empty(world.Logs.Entries);
        Assert.Empty(world.Commands.Messages);
        Assert.Empty(world.SenderPackets);
        Assert.Empty(world.Quests.Progresses);
        Assert.Empty(world.Rewards.Progresses);
    }

    [Fact]
    public async Task CommandIsLoggedButDoesNotPublishOrProgress()
    {
        var world = new World();
        world.Commands.Handled = true;

        await world.Service.Chat(world.Sender, ":wave", 1);

        var entry = Assert.Single(world.Logs.Entries);
        Assert.Equal(":wave", entry.Message);
        Assert.Equal(Now, entry.CreatedAt);
        Assert.Equal(new[] { ":wave" }, world.Commands.Messages);
        Assert.Empty(world.SenderPackets);
        Assert.Empty(world.Quests.Progresses);
        Assert.Empty(world.Rewards.Progresses);
    }

    [Fact]
    public async Task BannedNormalAndShoutPublishTheirExactPacketKindsWithoutQuestProgress()
    {
        var world = new World();
        world.Filter.Banned = true;

        await world.Service.Chat(world.Sender, "blocked normal", 2);
        await world.Service.Shout(world.Sender, "blocked shout", 3);

        Assert.Equal(2, world.Logs.Entries.Count);
        Assert.Equal(Now, world.Logs.Entries[0].CreatedAt);
        Assert.Equal(Now, world.Logs.Entries[1].CreatedAt);
        Assert.Equal(2, world.SenderPackets.Count);
        Assert.Contains("blocked normal", Text(world.SenderPackets[0].Payload));
        Assert.Contains("blocked shout", Text(world.SenderPackets[1].Payload));
        Assert.Empty(world.Quests.Progresses);
        Assert.Empty(world.Rewards.Progresses);
    }

    [Fact]
    public async Task WhisperSplitsCombinedFieldFiltersAndPublishesToBothUsers()
    {
        var world = new World();
        world.Filter.Replacement = "filtered words";

        await world.Service.Whisper(world.Sender, "Bob secret words", 2);

        var entry = Assert.Single(world.Logs.Entries);
        Assert.Equal("<Whisper to Bob>: filtered words", entry.Message);
        Assert.Equal(Now, entry.CreatedAt);
        Assert.Single(world.SenderPackets);
        Assert.Single(world.RecipientPackets);
        Assert.Contains("filtered words", Text(world.SenderPackets[0].Payload));
        Assert.Contains("filtered words", Text(world.RecipientPackets[0].Payload));
        Assert.Single(world.Quests.Progresses);
        Assert.Single(world.Rewards.Progresses);
    }

    [Theory]
    [InlineData(Team.Red, 130)]
    [InlineData(Team.Blue, 131)]
    [InlineData(Team.Yellow, 132)]
    [InlineData(Team.Green, 133)]
    public async Task UsersInAWiredTeamTalkShoutAndWhisperWithTheTeamBubble(Team team, int expected)
    {
        // Own Talk echo carries 130 in Red, 133 in Green and 0 before joining.
        // The banned-phrase path echoes the sender's own chat packet without a full room, with the same resolved bubble.
        // Each line comes a chat step later, inside the chat budget.
        var world = new World(new SteppingClock(Now, TimeSpan.FromMilliseconds(600)));
        world.Filter.Banned = true;
        await world.Service.Chat(world.Sender, "before", 2);
        Assert.Equal(2, Bubble(world.SenderPackets[^1]));

        Assert.True(WiredGameState.For(world.Room).Join(world.Room, world.SenderUser, 0, team, 0, [world.SenderUser, world.RecipientUser],
            applyEffect: (_, _, _) => { }));
        await world.Service.Chat(world.Sender, "talk", 2);
        Assert.Equal((ServerPacketHeader.ChatComposer, expected), (world.SenderPackets[^1].Header, Bubble(world.SenderPackets[^1])));
        await world.Service.Shout(world.Sender, "shout", 2);
        Assert.Equal((ServerPacketHeader.ShoutComposer, expected), (world.SenderPackets[^1].Header, Bubble(world.SenderPackets[^1])));
        world.Filter.Banned = false;
        await world.Service.Whisper(world.Sender, "Bob whisper", 2);
        Assert.Equal((ServerPacketHeader.WhisperComposer, expected), (world.SenderPackets[^1].Header, Bubble(world.SenderPackets[^1])));
        Assert.Equal(expected, Bubble(world.RecipientPackets[^1]));

        Assert.True(WiredGameState.For(world.Room).Leave(world.Room, world.SenderUser, (_, _) => { }));
        world.Filter.Banned = true;
        await world.Service.Chat(world.Sender, "after", 2);
        Assert.Equal(2, Bubble(world.SenderPackets[^1]));
    }

    // A user put into a Battle Banzai or Freeze team by Join Team talks with the same team bubble as in a Wired team
    // (130 Red, 131 Blue, 132 Yellow, 133 Green), and with the own bubble after Leave Team (0).
    [Theory]
    [InlineData(1, Team.Red, 130)]
    [InlineData(1, Team.Blue, 131)]
    [InlineData(1, Team.Green, 133)]
    [InlineData(1, Team.Yellow, 132)]
    [InlineData(2, Team.Red, 130)]
    [InlineData(2, Team.Blue, 131)]
    [InlineData(2, Team.Green, 133)]
    [InlineData(2, Team.Yellow, 132)]
    public async Task UsersJoinedToABanzaiOrFreezeTeamTalkWithTheTeamBubble(int type, Team team, int expected)
    {
        var world = new World(new SteppingClock(Now, TimeSpan.FromMilliseconds(600)));
        world.Filter.Banned = true;

        Assert.True(WiredGameState.For(world.Room).Join(world.Room, world.SenderUser, type, team, 0, [world.SenderUser, world.RecipientUser],
            applyEffect: (_, _, _) => { }));
        await world.Service.Chat(world.Sender, "talk", 2);
        Assert.Equal((ServerPacketHeader.ChatComposer, expected), (world.SenderPackets[^1].Header, Bubble(world.SenderPackets[^1])));

        Assert.True(WiredGameState.For(world.Room).Leave(world.Room, world.SenderUser, (_, _) => { }));
        await world.Service.Chat(world.Sender, "after", 2);
        Assert.Equal(2, Bubble(world.SenderPackets[^1]));
    }

    // Plus guard: a plain Wired team that is cleared and set again does not bring the Join Team bubble back
    // (the Banzai and Freeze team lists keep the user, so the guard covers the Wired team only).
    // The test sets the team directly and does not run the gate code.
    [Fact]
    public async Task ATeamSetAgainOfTheSameColourAfterAJoinTeamKeepsTheOwnBubble()
    {
        const int type = 0;
        var world = new World(new SteppingClock(Now, TimeSpan.FromMilliseconds(600)));
        world.Filter.Banned = true;
        Assert.True(WiredGameState.For(world.Room).Join(world.Room, world.SenderUser, type, Team.Red, 0, [world.SenderUser, world.RecipientUser],
            applyEffect: (_, _, _) => { }));

        world.SenderUser.Team = Team.None;
        world.SenderUser.Team = Team.Red;
        await world.Service.Chat(world.Sender, "talk", 2);

        Assert.Equal(2, Bubble(world.SenderPackets[^1]));
    }

    // Plus guard: a team set directly, with no Join Team and no game gate, keeps the own bubble.
    [Fact]
    public async Task ATeamSetWithoutAGateOrJoinTeamKeepsTheOwnBubble()
    {
        var world = new World();
        world.Filter.Banned = true;
        world.SenderUser.Team = Team.Red;

        await world.Service.Chat(world.Sender, "talk", 2);

        Assert.Equal(2, Bubble(world.SenderPackets[^1]));
    }

    // Talk is kept up to 200 characters.
    [Theory]
    [InlineData(150, 150)]
    [InlineData(200, 200)]
    [InlineData(300, 200)]
    public async Task TalkKeepsUpTo200Characters(int said, int kept)
    {
        var world = new World();
        world.Filter.Banned = true;

        await world.Service.Chat(world.Sender, new string('x', said), 0);

        var payload = world.SenderPackets[^1].Payload;
        Assert.Equal(kept, System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4, 2)));
    }

    // Room chat packets: i32 virtual id, string message, i32 gesture, i32 bubble style.
    private static int Bubble((uint Header, byte[] Payload) packet)
    {
        var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(packet.Payload.AsSpan(4, 2));

        return System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(packet.Payload.AsSpan(6 + length + 4, 4));
    }

    [Fact]
    public async Task FilteredNormalAndShoutReachWiredBeforeVisibleChatPublication()
    {
        var world = new World();
        world.Filter.Replacement = "filtered words";
        world.AddHiddenSpeechTrigger("filtered words");

        await world.Service.Chat(world.Sender, "normal source", 2);
        await world.Service.Shout(world.Sender, "shout source", 3);

        Assert.Equal(2, world.Logs.Entries.Count);
        Assert.Equal(2, world.Quests.Progresses.Count);
        Assert.Equal(2, world.SenderPackets.Count);
        Assert.All(world.SenderPackets, packet =>
        {
            Assert.Equal(ServerPacketHeader.WhisperComposer, packet.Header);
            Assert.Contains("filtered words", Text(packet.Payload));
        });
        Assert.Empty(world.RecipientPackets);
        Assert.False(world.Sender.GetHabbo().HasSpoken);
    }

    // Exact matching ignores case but preserves message whitespace.
    [Theory]
    [InlineData("hello", true)]
    [InlineData("HELLO", true)]
    [InlineData(" hello", false)]
    [InlineData("hello ", false)]
    [InlineData(" hello ", false)]
    [InlineData("hello world", false)]
    public async Task ExactWiredSpeechPreservesTheReceivedMessageWhitespace(string message, bool hidden)
    {
        var world = new World();
        world.AddHiddenSpeechTrigger("hello");
        Set(world.SenderUser, "_chatEmotions", new TestChatEmotions());

        await world.Service.Chat(world.Sender, message, 0);

        if (hidden) {
            Assert.Empty(world.RecipientPackets);
            Assert.Equal(ServerPacketHeader.WhisperComposer, Assert.Single(world.SenderPackets).Header);
        }
        else {
            Assert.Equal(ServerPacketHeader.ChatComposer, Assert.Single(world.RecipientPackets).Header);
            var payload = Assert.Single(world.RecipientPackets).Payload;
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(4, 2));
            Assert.Equal(message.TrimEnd(' '), Encoding.UTF8.GetString(payload, 6, length));
        }
    }

    // A whisper fires a Says Something trigger like a spoken line does, whoever it is addressed to. A hidden message is not delivered to its
    // recipient and the speaker gets one whisper echo. The "disable whispering to Wired" preference changes nothing.
    [Theory]
    [InlineData("Bob", false)]
    [InlineData("Bob", true)]
    [InlineData("Alice", false)]
    [InlineData("Wired", false)]
    [InlineData("Wired", true)]
    public async Task AWhisperFiresASaysSomethingTriggerAndHidesTheMessage(string target, bool whisperDisabled)
    {
        var world = new World();
        world.AddHiddenSpeechTrigger("hello");
        Set(world.SenderUser, "_chatEmotions", new TestChatEmotions());
        world.Sender.GetHabbo().WiredPreferences = new(false, false, false, whisperDisabled, false, "");

        await new WhisperEvent(world.Service).Parse(world.Sender, HabbiconTestSupport.Incoming(target + " hello", 0));

        Assert.Empty(world.RecipientPackets);
        var echo = Assert.Single(world.SenderPackets);
        Assert.Equal(ServerPacketHeader.WhisperComposer, echo.Header);
        Assert.Contains("hello", Text(echo.Payload));
    }

    // A whisper that a trigger hides is still written to the chat log, like a spoken line.
    [Theory]
    [InlineData("Bob")]
    [InlineData("Nobody")]
    public async Task AWhisperThatATriggerHidesIsLogged(string target)
    {
        var world = new World();
        world.AddHiddenSpeechTrigger("hello");
        Set(world.SenderUser, "_chatEmotions", new TestChatEmotions());

        await new WhisperEvent(world.Service).Parse(world.Sender, HabbiconTestSupport.Incoming(target + " hello", 0));

        Assert.Empty(world.RecipientPackets);
        var entry = Assert.Single(world.Logs.Entries);
        Assert.Equal($"<Whisper to {target}>: hello", entry.Message);
        Assert.Equal(Now, entry.CreatedAt);
    }

    // A banned phrase in a whisper that fires a trigger is logged and counted, and the speaker gets the echo once.
    [Fact]
    public async Task ABannedPhraseInAWhisperThatATriggerHidesIsLoggedAndCounted()
    {
        var world = new World();
        world.AddHiddenSpeechTrigger("hello");
        world.Filter.Banned = true;
        Set(world.SenderUser, "_chatEmotions", new TestChatEmotions());

        await new WhisperEvent(world.Service).Parse(world.Sender, HabbiconTestSupport.Incoming("Bob hello", 0));

        Assert.Equal(1, world.Sender.GetHabbo().BannedPhraseCount);
        Assert.Equal("<Whisper to Bob>: hello", Assert.Single(world.Logs.Entries).Message);
        Assert.Empty(world.RecipientPackets);
        Assert.Equal(ServerPacketHeader.WhisperComposer, Assert.Single(world.SenderPackets).Header);
        Assert.Empty(world.Quests.Progresses);
    }

    // A banned phrase in an ordinary whisper is logged, counted and not delivered.
    [Fact]
    public async Task ABannedPhraseInAnOrdinaryWhisperIsLoggedCountedAndNotDelivered()
    {
        var world = new World();
        world.Filter.Banned = true;

        await world.Service.Whisper(world.Sender, "Bob blocked words", 2);

        Assert.Equal(1, world.Sender.GetHabbo().BannedPhraseCount);
        Assert.Equal("<Whisper to Bob>: blocked words", Assert.Single(world.Logs.Entries).Message);
        Assert.Empty(world.RecipientPackets);
        Assert.Single(world.SenderPackets);
    }

    // A whisper to a name that is no user in the room is echoed to the speaker only.
    [Theory]
    [InlineData("Nobody")]
    [InlineData("Wired")]
    public async Task AWhisperToNoUserIsEchoedToTheSpeaker(string target)
    {
        var world = new World();
        Set(world.SenderUser, "_chatEmotions", new TestChatEmotions());

        await new WhisperEvent(world.Service).Parse(world.Sender, HabbiconTestSupport.Incoming(target + " hello", 0));

        Assert.Empty(world.RecipientPackets);
        var echo = Assert.Single(world.SenderPackets);
        Assert.Equal(ServerPacketHeader.WhisperComposer, echo.Header);
        Assert.Contains("hello", Text(echo.Payload));
    }

    // A whisper that no trigger hides still reaches its recipient.
    [Fact]
    public async Task AWhisperThatNoTriggerHidesIsStillDelivered()
    {
        var world = new World();
        world.AddHiddenSpeechTrigger("hello");
        Set(world.SenderUser, "_chatEmotions", new TestChatEmotions());

        await new WhisperEvent(world.Service).Parse(world.Sender, HabbiconTestSupport.Incoming("Bob goodbye", 0));

        Assert.Single(world.SenderPackets);
        Assert.Contains("goodbye", Text(Assert.Single(world.RecipientPackets).Payload));
    }

    // "k105a 5 " does not fire the exact keyword "k105a #(a)", and its echo reads "k105a 5".
    [Theory]
    [InlineData("k105a 5", true)]
    [InlineData("k105a 5 ", false)]
    [InlineData("k105a 5  ", false)]
    public async Task ExactWiredSpeechSeesTrailingSpacesThatTheWordFilterDropsFromTheEcho(string said, bool hidden)
    {
        var world = new World(filter: new WordFilterManager(DefaultProxy<Plus.Database.IDatabase>.Create()));
        world.AddHiddenSpeechTrigger("k105a 5");
        Set(world.SenderUser, "_chatEmotions", new TestChatEmotions());

        await new ChatEvent(world.Service).Parse(world.Sender, HabbiconTestSupport.Incoming(said, 0));

        if (hidden) {
            Assert.Empty(world.RecipientPackets);
            Assert.Equal(ServerPacketHeader.WhisperComposer, Assert.Single(world.SenderPackets).Header);
        }
        else {
            var packet = Assert.Single(world.RecipientPackets);
            Assert.Equal(ServerPacketHeader.ChatComposer, packet.Header);
            var length = System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(packet.Payload.AsSpan(4, 2));
            Assert.Equal("k105a 5", Encoding.UTF8.GetString(packet.Payload, 6, length));
        }
    }

    [Fact]
    public async Task UtcFloodDeadlinePreservesBeforeExactAndAfterBoundariesInNonUtcZone()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("chat-test-plus-two", TimeSpan.FromHours(2), "test", "test");
        var deadlineUtc = Now.AddMilliseconds(500);
        var before = new World(new ZonedClock(deadlineUtc.AddMilliseconds(-1), zone));
        before.Sender.GetHabbo().FloodUntil = deadlineUtc;
        before.Commands.Handled = true;
        await before.Service.Chat(before.Sender, ":before", 1);
        Assert.Empty(before.Logs.Entries);

        var exact = new World(new ZonedClock(deadlineUtc, zone));
        exact.Sender.GetHabbo().FloodUntil = deadlineUtc;
        exact.Commands.Handled = true;
        await exact.Service.Chat(exact.Sender, ":exact", 1);
        Assert.Single(exact.Logs.Entries);

        var after = new World(new ZonedClock(deadlineUtc.AddMilliseconds(1), zone));
        after.Sender.GetHabbo().FloodUntil = deadlineUtc;
        after.Commands.Handled = true;
        await after.Service.Chat(after.Sender, ":after", 1);
        Assert.Single(after.Logs.Entries);
    }

    // Two lines in hand, one more on every 600 ms step, no mute.
    [Fact]
    public void ChatSpendsATwoLineBudgetThatRefillsEverySixHundredMilliseconds()
    {
        var world = new World();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(1_800_000_000_000);
        bool Drop(int ms) => world.SenderUser.IncrementAndCheckFlood(start.AddMilliseconds(ms), out _);

        Assert.False(Drop(0));
        Assert.False(Drop(100));
        Assert.True(Drop(200));
        Assert.False(Drop(600));
        Assert.True(Drop(700));
        Assert.False(Drop(1800));
        Assert.False(Drop(1850));
        Assert.True(Drop(1900));
        Assert.Null(world.Sender.GetHabbo().FloodUntil);
    }

    [Fact]
    public async Task HabbiconOverTheChatBudgetIsDroppedWithoutAMute()
    {
        var now = Now.AddMilliseconds(375);
        var clock = new ZonedClock(now, TimeZoneInfo.CreateCustomTimeZone("habbicon-plus-nine", TimeSpan.FromHours(9), "test", "test"));
        var world = new World(clock);
        world.SenderUser.IncrementAndCheckFlood(now, out _);
        world.SenderUser.IncrementAndCheckFlood(now, out _);
        world.Sender.GetHabbo().LastHabbiconTriggeredAt = now.AddSeconds(-2);
        var store = new HabbiconTestSupport.Service();

        await new TriggerHabbiconEvent(new RoomHabbiconService(store, world.Rewards, clock))
            .Parse(world.Sender, HabbiconTestSupport.Incoming(61));

        Assert.Empty(store.Used);
        Assert.Null(world.Sender.GetHabbo().FloodUntil);
    }

    [Theory]
    [InlineData(999, false)]
    [InlineData(1000, true)]
    [InlineData(1001, true)]
    public async Task RoomHabbiconCooldownUsesOneUtcInstantAtTheExactDeadline(int elapsed, bool allowed)
    {
        var clock = new ZonedClock(Now.ToOffset(TimeSpan.FromHours(9)), TimeZoneInfo.Utc);
        var world = new World(clock);
        var habbo = world.Sender.GetHabbo();
        habbo.LastHabbiconTriggeredAt = Now.AddMilliseconds(-elapsed);
        var store = new HabbiconTestSupport.Service();
        clock.Calls = 0;
        var packet = HabbiconTestSupport.Incoming(61);
        await new TriggerHabbiconEvent(new RoomHabbiconService(store, world.Rewards, clock)).Parse(world.Sender, packet);
        Assert.False(packet.HasDataRemaining());
        Assert.Equal(1, clock.Calls);

        if (allowed) {
            Assert.Equal(new[] { 61 }, store.Used);
            Assert.Equal(new[] { RewardTrackActions.UseHabbicon }, world.Rewards.Progresses);
            Assert.Equal(Now, habbo.LastHabbiconTriggeredAt);
            Assert.Equal(new[] { ServerPacketHeader.RoomUseHabbiconComposer, ServerPacketHeader.UserHabbiconsComposer }, world.SenderPackets.Select(p => p.Header));
        }
        else {
            Assert.Empty(store.Used);
            Assert.Empty(world.Rewards.Progresses);
            Assert.Empty(world.SenderPackets);
        }
    }

    [Fact]
    public async Task MissingTriggerTimeAllowsFirstUseAndUnownedUseDoesNotPublishOrStamp()
    {
        var clock = new ZonedClock(Now, TimeZoneInfo.Utc);
        var world = new World(clock);
        var habbo = world.Sender.GetHabbo();
        var store = new HabbiconTestSupport.Service();
        var handler = new TriggerHabbiconEvent(new RoomHabbiconService(store, world.Rewards, clock));
        await handler.Parse(world.Sender, HabbiconTestSupport.Incoming(999));
        Assert.Null(habbo.LastHabbiconTriggeredAt);
        Assert.Empty(world.SenderPackets);
        Assert.Empty(world.Rewards.Progresses);
        store.Used.Clear();
        await handler.Parse(world.Sender, HabbiconTestSupport.Incoming(61));
        Assert.Equal(new[] { 61 }, store.Used);
        Assert.Equal(Now, habbo.LastHabbiconTriggeredAt);
        Assert.Equal(TimeSpan.Zero, habbo.LastHabbiconTriggeredAt!.Value.Offset);
    }

    [Fact]
    public async Task FutureTriggerTimeAndMaxValueRemainOnCooldownWithoutOverflow()
    {
        var clock = new ZonedClock(Now, TimeZoneInfo.Utc);
        var world = new World(clock);
        world.Sender.GetHabbo().LastHabbiconTriggeredAt = DateTimeOffset.MaxValue;
        var store = new HabbiconTestSupport.Service();
        await new TriggerHabbiconEvent(new RoomHabbiconService(store, world.Rewards, clock))
            .Parse(world.Sender, HabbiconTestSupport.Incoming(61));
        Assert.Empty(store.Used);
        Assert.Empty(world.SenderPackets);
        Assert.Empty(world.Rewards.Progresses);
    }

    [Fact]
    public async Task TriggerHandlerOnlyDecodesAndDelegates()
    {
        var recorder = new RecordingRoomHabbicons();
        var (client, sent) = HabbiconTestSupport.Client(new Habbo { Id = 7 });
        var packet = HabbiconTestSupport.Incoming(61);
        await new TriggerHabbiconEvent(recorder).Parse(client, packet);
        Assert.Equal(61, recorder.Id);
        Assert.False(packet.HasDataRemaining());
        Assert.Empty(sent);
    }

    private sealed class RecordingRoomHabbicons : IRoomHabbiconService
    {
        public int Id;
        public void Trigger(GameClient session, int id) => Id = id;
    }

    [Fact]
    public async Task BannedThresholdUsesSameSampledUtcInstantForExpiry()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("chat-test-plus-five-thirty", TimeSpan.FromHours(5.5), "test", "test");
        var now = Now.AddMilliseconds(789);
        var (moderation, recorder) = RecordingModeration.Create();
        var clock = new ZonedClock(now, zone);
        var world = new World(clock, moderation);
        world.Filter.Banned = true;
        world.Settings.Chances = 1;
        clock.Calls = 0;

        await world.Service.Chat(world.Sender, "threshold", 1);

        Assert.Equal(now.AddSeconds(78892200), recorder.Expiry);
        Assert.Equal(now, Assert.Single(world.Logs.Entries).CreatedAt);
        Assert.Equal(1, clock.Calls);
    }

    [Fact]
    public async Task MalformedBotDisconnectedOrDepartedWhisperRecipientDoesNotPublishBeyondTheSpeakerEcho()
    {
        var world = new World();

        await world.Service.Whisper(world.Sender, "Bob", 1);
        Assert.Empty(world.Logs.Entries);
        Assert.Empty(world.SenderPackets);
        Assert.Empty(world.RecipientPackets);

        world.RecipientUser.BotData = (Plus.HabboHotel.Rooms.AI.RoomBot)RuntimeHelpers.GetUninitializedObject(typeof(Plus.HabboHotel.Rooms.AI.RoomBot));
        await world.Service.Whisper(world.Sender, "Bob message", 1);
        Assert.Single(world.Logs.Entries);
        Assert.Equal(ServerPacketHeader.WhisperComposer, Assert.Single(world.SenderPackets).Header);
        world.SenderPackets.Clear();
        Assert.Empty(world.RecipientPackets);

        world.RecipientUser.BotData = null!;
        world.Clients.ByUserId.Remove(8);
        await world.Service.Whisper(world.Sender, "Bob message", 1);
        Assert.Equal(2, world.Logs.Entries.Count);
        Assert.Empty(world.SenderPackets);
        Assert.Empty(world.RecipientPackets);

        world.Clients.ByUserId[8] = world.Recipient;
        world.Recipient.GetHabbo().CurrentRoom = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        Set(world.SenderUser, "_chatTokens", RoomUser.ChatBurst);
        await world.Service.Whisper(world.Sender, "Bob message", 1);
        Assert.Equal(3, world.Logs.Entries.Count);
        Assert.Empty(world.SenderPackets);
        Assert.Empty(world.RecipientPackets);
    }

    private static string Text(byte[] packet) => Encoding.UTF8.GetString(packet);

    private sealed class World
    {
        public GameClient Sender { get; }
        public GameClient Recipient { get; }
        public RoomUser RecipientUser { get; }
        public RoomUser SenderUser { get; }
        public Room Room => _room;
        public List<(uint Header, byte[] Payload)> SenderPackets { get; }
        public List<(uint Header, byte[] Payload)> RecipientPackets { get; }
        public RecordingLogs Logs { get; } = new();
        public RecordingCommands Commands { get; } = new();
        public RecordingFilter Filter { get; } = new();
        public RecordingQuests Quests { get; } = new();
        public RecordingRewards Rewards { get; } = new();
        public Settings Settings { get; } = new();
        public ClientDirectory Clients { get; }
        public IRoomChatService Service { get; }
        private readonly Room _room;
        private readonly WiredComponent _wired;
        private readonly ConcurrentDictionary<uint, Item> _floorItems;
        private uint _nextItemId = 1;

        public World(TimeProvider? clock = null, IModerationManager? moderation = null, IWordFilterManager? filter = null)
        {
            clock ??= new FixedClock(Now);
            _room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
            _room.Id = 42;
            Set(_room, "_interactionClock", clock);
            _room.MutedUsers = [];
            _room.WordFilterList = [];
            var users = new RoomUserManager(_room, TestRoomUserStore.Instance, clock, new TestRewardProgress(), TestChatEmotions.Unused, TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel, TestItemRuntime.Settings);
            var items = new RoomItemHandling(_room, TestRoomItemStore.Instance, TestRoomItemMetadataStore.Instance, TestGameClientManager.Empty, TestLanguageManager.RoomItems, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
            _floorItems = (ConcurrentDictionary<uint, Item>)Get(items, "_floorItems");
            _wired = new WiredComponent(_room, TestLogging.Logger, clock, TestRoomSettings.Empty, TestWiredRoomSettingsFactory.Instance, TestWiredConfigurationStore.Instance, TestWiredDatabase.Instance, TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty, TestGroupManager.Empty, TestWiredDefinitions.Unused, TestWiredCommands.Unused, TestWiredAccess.Unused, TestItemRuntime.Travel);
            Set(_room, "_roomUserManager", users);
            Set(_room, "_roomItemHandling", items);
            Set(_room, "_wiredComponent", _wired);

            (Sender, SenderPackets) = Client(new Habbo
            {
                Id = 7,
                Username = "Alice",
                CurrentRoom = _room,
                Effects = new EffectsComponent(clock),
                IgnoresComponent = new([]),
                ReceiveWhispers = true
            });
            (Recipient, RecipientPackets) = Client(new Habbo
            {
                Id = 8,
                Username = "Bob",
                CurrentRoom = _room,
                Effects = new EffectsComponent(clock),
                IgnoresComponent = new([]),
                ReceiveWhispers = true
            });
            SenderUser = new RoomUser(7, 1, 11, _room, Sender, TestChatEmotions.Unused, TestRewardProgress.Unused);
            Add(users, SenderUser);
            RecipientUser = new RoomUser(8, 2, 12, _room, Recipient, TestChatEmotions.Unused, TestRewardProgress.Unused);
            Add(users, RecipientUser);
            var clientManager = ClientDirectory.Create(out var clients);
            Clients = clients;
            Clients.ByUserId[7] = Sender;
            Clients.ByUserId[8] = Recipient;

            Service = new RoomChatService(new Styles(), Logs, filter ?? Filter, Commands,
                moderation ?? DefaultProxy<IModerationManager>.Create(), Settings, Quests, Rewards, clientManager, clock);
        }

        public void AddHiddenSpeechTrigger(string message)
        {
            _room.HideWired = true;

            var item = new Item
            {
                Id = _nextItemId++,
                RoomId = _room.Id,
                ExtraData = new LegacyDataFormat { Data = "1" },
                Definition = new() { ItemName = "wf_trg_says_something", Type = ItemType.Floor }
            };
            item.Attach(_room, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
            _floorItems[item.Id] = item;
            var trigger = _wired.CreateConfiguredBox(item)!;
            Assert.Same(item, trigger.Item);
            Assert.Same(_room, trigger.Instance);
            Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, trigger.Descriptor, new()
            {
                Category = WiredBoxCategory.Trigger,
                NativeCode = 0,
                OwnedIntParams = [0, 1, 1],
                Text = message,
                FurniSourceTypes = [],
                UserSourceTypes = [],
                VariableIds = []
            }, out var proposed));
            Assert.True(trigger.TryValidateConfiguration(proposed, out var configuration, out var error), error);
            trigger.ApplyConfiguration(configuration);
            Assert.True(_wired.AddBox(trigger));
        }

        private static (GameClient Client, List<(uint Header, byte[] Payload)> Packets) Client(Habbo habbo)
        {
            var (client, sent) = HabbiconTestSupport.Client(habbo);
            habbo.Client = client;

            return (client, sent);
        }

        private static void Add(RoomUserManager manager, RoomUser user)
        {
            user.UserId = user.HabboId;
            var users = (ConcurrentDictionary<int, RoomUser>)Get(manager, "_users");
            users[user.VirtualId] = user;
        }
    }

    private sealed class RecordingRoomChatService : IRoomChatService
    {
        public (string Message, int Colour) ChatRequest { get; private set; }
        public (string Message, int Colour) ShoutRequest { get; private set; }
        public (string Parameters, int Colour) WhisperRequest { get; private set; }
        public Task Chat(GameClient session, string message, int colour)
        {
            ChatRequest = (message, colour);

            return Task.CompletedTask;
        }
        public Task Shout(GameClient session, string message, int colour)
        {
            ShoutRequest = (message, colour);

            return Task.CompletedTask;
        }
        public Task Whisper(GameClient session, string parameters, int colour)
        {
            WhisperRequest = (parameters, colour);

            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLogs : IChatlogManager
    {
        public List<ChatlogEntry> Entries { get; } = [];
        public void StoreChatlog(ChatlogEntry entry) => Entries.Add(entry);
        public void FlushAndSave() { }
    }

    private sealed class RecordingCommands : ICommandManager
    {
        public bool Handled { get; set; }
        public List<string> Messages { get; } = [];
        public Task<bool> Parse(GameClient session, string message)
        {
            Messages.Add(message);

            return Task.FromResult(Handled);
        }
        public void Register(string commandText, ICommandBase command) => throw new NotSupportedException();
        public void LogCommand(int userId, string data, string? machineId) => throw new NotSupportedException();
        public bool TryGetCommand(string command, out ICommandBase? chatCommand)
        {
            chatCommand = null;

            return false;
        }
    }

    private sealed class RecordingFilter : IWordFilterManager
    {
        public bool Banned { get; set; }
        public string Replacement { get; set; } = "";
        public void Init() { }
        public string CheckMessage(string message) => Replacement.Length == 0 ? message : Replacement;
        public bool CheckBannedWords(string message) => Banned;
        public bool IsFiltered(string message) => Banned;
    }

    private sealed class Styles : IChatStyleManager
    {
        public void Init() { }
        public IReadOnlyList<int> GetAllowedStyleIds(Plus.HabboHotel.Permissions.UserAccess access) => [0, 1, 2, 3];
        public bool TryGetStyle(int id, out ChatStyle? style)
        {
            style = new(id, "test", "");

            return true;
        }
    }

    private sealed class Settings : ISettingsManager
    {
        public int Chances { get; set; } = 99;
        public string TryGetValue(string value) => Chances.ToString();
        public string? GetOptionalValue(string key) => null;
        public Task Reload() => Task.CompletedTask;
    }

    private sealed class RecordingQuests : IQuestManager
    {
        public List<QuestType> Progresses { get; } = [];
        public void ProgressUserQuest(GameClient session, QuestType type, int data = 0) => Progresses.Add(type);
        public void Init() => throw new NotSupportedException();
        public Quest GetQuest(int id) => throw new NotSupportedException();
        public int GetAmountOfQuestsInCategory(string category) => throw new NotSupportedException();
        public Quest GetNextQuestInSeries(string category, int number) => throw new NotSupportedException();
        public void GetList(GameClient session, Plus.Communication.Packets.Incoming.ClientPacket? message) => throw new NotSupportedException();
        public void QuestReminder(GameClient session, int questId) => throw new NotSupportedException();
    }

    private sealed class RecordingRewards : IRewardTrackManager
    {
        public List<string> Progresses { get; } = [];
        public void Progress(GameClient session, string actionType, int amount = 1) => Progresses.Add(actionType);
        public void SendTracks(GameClient session) => throw new NotSupportedException();
        public Task Claim(GameClient session, string trackId, string prizeId) => throw new NotSupportedException();
        public void PurchasePremium(GameClient session, string trackId) => throw new NotSupportedException();
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class SteppingClock(DateTimeOffset start, TimeSpan step) : TimeProvider
    {
        private DateTimeOffset _now = start;
        public override DateTimeOffset GetUtcNow() => _now += step;
    }

    private sealed class ZonedClock(DateTimeOffset now, TimeZoneInfo zone) : TimeProvider
    {
        public int Calls { get; set; }
        public override DateTimeOffset GetUtcNow()
        {
            Calls++;

            return now;
        }
        public override TimeZoneInfo LocalTimeZone => zone;
    }

    public class ClientDirectory : DispatchProxy
    {
        public Dictionary<int, GameClient> ByUserId { get; } = [];
        public static IGameClientManager Create(out ClientDirectory directory)
        {
            var manager = DispatchProxy.Create<IGameClientManager, ClientDirectory>();
            directory = (ClientDirectory)(object)manager;

            return manager;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IGameClientManager.GetClientByUserId)) {
                return ByUserId.GetValueOrDefault((int)args![0]!);
            }

            return targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
    }

    public class RecordingModeration : DispatchProxy
    {
        public DateTimeOffset? Expiry { get; private set; }
        public static (IModerationManager Manager, RecordingModeration Recorder) Create()
        {
            var manager = DispatchProxy.Create<IModerationManager, RecordingModeration>();

            return (manager, (RecordingModeration)(object)manager);
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IModerationManager.BanUser)) {
                Expiry = (DateTimeOffset?)args![4];

                return Task.CompletedTask;
            }

            return targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null;
        }
    }

    private class DefaultProxy<T> : DispatchProxy where T : class
    {
        public static T Create() => DispatchProxy.Create<T, DefaultProxy<T>>();
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask :
            targetMethod?.ReturnType.IsValueType == true ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }

    private static object Get(object value, string field) =>
        value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
    private static void Set(object value, string field, object data) =>
        value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(value, data);
}
