using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A Wired effect that sends a bot walking raises the walk between two room ticks, and the bot's first step is announced on the next room tick.
    private const string BotWalkGive = "wf_act_bot_give_handitem";
    private const string BotWalkMove = "wf_act_bot_move";

    [Theory]
    [InlineData(BotWalkGive, 0)]
    [InlineData(BotWalkGive, 100)]
    [InlineData(BotWalkGive, 250)]
    [InlineData(BotWalkGive, 450)]
    [InlineData(BotWalkMove, 0)]
    [InlineData(BotWalkMove, 100)]
    [InlineData(BotWalkMove, 250)]
    [InlineData(BotWalkMove, 450)]
    public void ABotWalkRaisedBetweenTwoRoomTicksStartsOnTheNextRoomTick(string action, int sayAfterTick)
    {
        var steps = BotStepTimes(action, sayAfterTick, out var started);

        Assert.NotEmpty(steps);
        Assert.Equal(started - started % 500 + 500, steps[0]);
    }

    [Theory]
    [InlineData(BotWalkGive)]
    [InlineData(BotWalkMove)]
    public void ABotKeepsSteppingOncePerRoomTickAfterItsFirstStep(string action)
    {
        var steps = BotStepTimes(action, 250, out _);

        Assert.True(steps.Count >= 3, string.Join(",", steps));
        Assert.All(steps.Zip(steps.Skip(1)), pair => Assert.Equal(500, pair.Second - pair.First));
    }

    private List<long> BotStepTimes(string action, int sayAfterTick, out long started)
    {
        var config = action == BotWalkGive
            ? new WiredConfiguration
            {
                IntParams = [2, 0, 100],
                Text = "Robbie",
                UserSources = ImmutableDictionary<string, int>.Empty.Add("users", 0).Add("bots", 100)
            }
            : new WiredConfiguration
            {
                Text = "Robbie",
                SelectedItems = [40],
                FurniSources = ImmutableDictionary<string, int>.Empty.Add("items", 100),
                UserSources = ImmutableDictionary<string, int>.Empty.Add("bots", 100)
            };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (30, 4, 9, 0.0, 0, "wf_trg_says_something", SaysConfig("bw1")),
            (31, 4, 9, 0.65, 0, action, JsonSerializer.Serialize(config)),
            (40, 8, 13, 0.0, 0, "wf_colortile", null)
        };
        var f = new Fixture(layout, live: true);
        f.Room.MutedUsers = [];
        var owner = f.Walker(7, 6, 11, 5, 12);
        var bot = PlaceBot(f, 50, "Robbie", 11, 5);
        f.Advance(1000);
        var packets = CapturePublication(owner);
        var chat = new ChatEvent(ChatService());
        f.Advance(sayAfterTick - sayAfterTick % 50);
        started = 1000 + sayAfterTick - sayAfterTick % 50;
        packets.Clear();

        Say(f, chat, owner, "bw1");
        var steps = new List<long>();

        for (var at = started + 50; at <= started + 4000; at += 50) {
            f.Advance(50);

            if (packets.Any(bytes => BotStatus(bytes, bot) is { } status && status.Contains("mv "))) {
                steps.Add(at);
            }

            packets.Clear();
        }

        return steps;
    }
}
