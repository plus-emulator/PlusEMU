using System.Reflection;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Chat;
using Plus.Communication.Packets.Outgoing;
using Plus.Core.Settings;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms.Chat;
using Plus.HabboHotel.Rooms.Chat.Commands;
using Plus.HabboHotel.Rooms.Chat.Logs;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Styles;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Observed on AIR WIN63-202610201032-107964540 (3 runs, 8 gates each): a user who walks onto a Banzai or Freeze gate talks with
    // the team bubble (130 Red, 131 Blue, 133 Green, 132 Yellow) from then on, also after walking off the gate.
    [Theory]
    [InlineData(InteractionType.Banzaigatered, 130)]
    [InlineData(InteractionType.Banzaigateblue, 131)]
    [InlineData(InteractionType.Banzaigategreen, 133)]
    [InlineData(InteractionType.Banzaigateyellow, 132)]
    [InlineData(InteractionType.FreezeRedGate, 130)]
    [InlineData(InteractionType.FreezeBlueGate, 131)]
    [InlineData(InteractionType.FreezeGreenGate, 133)]
    [InlineData(InteractionType.FreezeYellowGate, 132)]
    public void AUserWhoWalksOntoAGameGateTalksWithTheTeamBubble(InteractionType kind, int bubble)
    {
        var gate = InteractionItem(10, 1, 1, kind);
        gate.Definition.Walkable = true;
        var actor = JoinableActor();

        Assert.Equal(0, Talk(actor, "before"));

        StepTo(actor, 1);
        Assert.Equal(bubble, Talk(actor, "on the gate"));

        StepTo(actor, 2);
        Assert.Equal(bubble, Talk(actor, "off the gate"));
    }

    // The same user leaves the team by walking onto a gate again, and then talks with the own bubble.
    [Fact]
    public void AUserWhoLeavesTheTeamAtAGateTalksWithTheOwnBubble()
    {
        var red = InteractionItem(10, 1, 1, InteractionType.Banzaigatered);
        red.Definition.Walkable = true;
        var again = InteractionItem(11, 2, 1, InteractionType.Banzaigatered);
        again.Definition.Walkable = true;
        var actor = JoinableActor();

        StepTo(actor, 1);
        Assert.Equal(130, Talk(actor, "in"));

        StepTo(actor, 2);
        Assert.Equal(Team.None, actor.Team);
        Assert.Equal(0, Talk(actor, "out"));
    }

    // The bubble the room shows for one line the user speaks through the real chat packet handler.
    private int Talk(RoomUser actor, string text)
    {
        typeof(RoomUser).GetField("_chatEmotions", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(actor, new TestChatEmotions());
        var chat = new ChatEvent(new RoomChatService(Quiet<IChatStyleManager>(), Quiet<IChatlogManager>(), new FakeWordFilter(),
            Quiet<ICommandManager>(), Quiet<IModerationManager>(), Quiet<ISettingsManager>(), Quiet<IQuestManager>(),
            Quiet<IRewardTrackManager>(), Quiet<IGameClientManager>(), _chatClock));
        _room.MutedUsers = [];
        _room.WordFilterList = [];
        _client.GetHabbo().IgnoresComponent ??= new([]);
        _client.Packets.Clear();
        chat.Parse(_client, HabbiconTestSupport.Incoming(text, 0)).GetAwaiter().GetResult();
        var packet = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ChatComposer);
        var body = new FlashIncomingPacket { Buffer = packet.Body };
        body.ReadInt();
        Assert.Equal(text, body.ReadString());
        body.ReadInt();

        return body.ReadInt();
    }

    // Each line is a few seconds after the last one, so the flood guard stays out of the way.
    private readonly SteppingChatClock _chatClock = new();

    private sealed class SteppingChatClock : TimeProvider
    {
        private DateTimeOffset _now = new(2040, 1, 2, 3, 4, 5, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now += TimeSpan.FromSeconds(5);
    }

    private static T Quiet<T>() where T : class => DispatchProxy.Create<T, QuietService<T>>();

    private class QuietService<T> : DispatchProxy where T : class
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod?.ReturnType == typeof(Task) ? Task.CompletedTask :
            targetMethod?.ReturnType.IsValueType == true && targetMethod.ReturnType != typeof(void) ? Activator.CreateInstance(targetMethod.ReturnType) : null;
    }
}
