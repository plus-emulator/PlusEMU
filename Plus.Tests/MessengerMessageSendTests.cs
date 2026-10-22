using System.Buffers.Binary;
using System.Reflection;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.FriendList;
using Plus.Communication.Revisions;
using Plus.HabboHotel.Friends;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.Messenger;
using Xunit;

namespace Plus.Tests;

/// <summary>A private message through the real communication, messenger and synchronizer code; only the message log is faked.</summary>
public sealed class MessengerMessageSendTests
{
    [Fact]
    public async Task OnlineFriendReceivesTheMessageAfterItIsLogged()
    {
        var f = new Fixture();
        f.Clients.RegisterClient(f.Target.Client, f.Target.Habbo.Id, f.Target.Habbo.Username);
        await f.LogIn();

        await f.Communication.SendMessage(f.Sender.Client, f.Target.Habbo.Id, "hello");

        Assert.Equal(new[] { "LogPrivateMessage 1 2 hello" }, f.Logs);
        Assert.Equal(new uint[] { ServerPacketHeader.NewConsoleMessageComposer }, f.Target.Headers);
        Assert.Empty(f.Sender.Headers);
        Assert.Equal(new[] { $"Progress {RewardTrackActions.SendMessengerMessage}" }, f.Rewards);
    }

    [Fact]
    public async Task OfflineFriendGetsTheMessageQueued()
    {
        var f = new Fixture();
        await f.LogIn();

        await f.Communication.SendMessage(f.Sender.Client, f.Target.Habbo.Id, "later");

        Assert.Equal(new[] { "LogPrivateMessage 1 2 later", "LogPrivateOfflineMessage 1 2 later" }, f.Logs);
        Assert.Empty(f.Target.Headers);
        Assert.Equal(new[] { $"Progress {RewardTrackActions.SendMessengerMessage}" }, f.Rewards);
    }

    [Fact]
    public async Task SendWaitsForTheLogBeforeDelivering()
    {
        var f = new Fixture();
        f.Clients.RegisterClient(f.Target.Client, f.Target.Habbo.Id, f.Target.Habbo.Username);
        var pending = new TaskCompletionSource();
        f.NextLog = pending.Task;
        await f.LogIn();

        var send = f.Communication.SendMessage(f.Sender.Client, f.Target.Habbo.Id, "hello");
        Assert.False(send.IsCompleted);
        Assert.Empty(f.Target.Headers);

        pending.SetResult();
        await send;
        Assert.Equal(new uint[] { ServerPacketHeader.NewConsoleMessageComposer }, f.Target.Headers);
    }

    [Fact]
    public async Task LogFailureFailsTheAwaitedSendWithoutDeliveryOrReward()
    {
        var f = new Fixture();
        f.Clients.RegisterClient(f.Target.Client, f.Target.Habbo.Id, f.Target.Habbo.Username);
        f.NextLog = Task.FromException(new InvalidOperationException("Injected log failure"));
        await f.LogIn();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => f.Communication.SendMessage(f.Sender.Client, f.Target.Habbo.Id, "hello"));

        Assert.Equal("Injected log failure", exception.Message);
        Assert.Empty(f.Target.Headers);
        Assert.Empty(f.Rewards);
    }

    [Fact]
    public async Task MutedOrBusyFriendsRejectTheMessageWithTheirError()
    {
        var f = new Fixture();
        f.Clients.RegisterClient(f.Target.Client, f.Target.Habbo.Id, f.Target.Habbo.Username);
        await f.LogIn();

        f.Target.Habbo.TimeMuted = 60;
        await f.Communication.SendMessage(f.Sender.Client, f.Target.Habbo.Id, "muted");
        f.Target.Habbo.TimeMuted = 0;
        f.Target.Habbo.AllowConsoleMessages = false;
        await f.Communication.SendMessage(f.Sender.Client, f.Target.Habbo.Id, "busy");

        Assert.Equal(new[] { "LogPrivateMessage 1 2 muted", "LogPrivateMessage 1 2 busy" }, f.Logs);
        Assert.Empty(f.Target.Headers);
        Assert.Equal(new uint[] { ServerPacketHeader.InstantMessageErrorComposer, ServerPacketHeader.InstantMessageErrorComposer }, f.Sender.Headers);
        Assert.Equal(new[] { MessengerMessageErrorsUtility.GetMessageErrorPacketNum(MessengerMessageErrors.FriendMuted), MessengerMessageErrorsUtility.GetMessageErrorPacketNum(MessengerMessageErrors.FriendBusy) }, f.Sender.FirstInts);
    }

    private sealed class Fixture
    {
        public readonly GameClientManager Clients = new(null!, null!);
        public readonly Participant Sender = new(1, "Alice", 2, "Bob");
        public readonly Participant Target = new(2, "Bob", 1, "Alice");
        public readonly List<string> Logs = [];
        public readonly List<string> Rewards = [];
        public Task NextLog { get; set; } = Task.CompletedTask;
        public readonly MessengerCommunicationService Communication;
        private readonly IMessengerDataLoader _loader;

        public Fixture()
        {
            _loader = Fake<IMessengerDataLoader>((method, args) =>
            {
                Logs.Add($"{method.Name} {args![0]} {args[1]} {args[2]}");

                return method.Name switch
                {
                    nameof(IMessengerDataLoader.LogPrivateMessage) => NextLog,
                    nameof(IMessengerDataLoader.LogPrivateOfflineMessage) => Task.CompletedTask,
                    _ => throw new NotSupportedException(method.Name),
                };
            });
            var filter = Fake<IWordFilterManager>((method, args) => method.Name == nameof(IWordFilterManager.CheckMessage) ? args![0] : throw new NotSupportedException(method.Name));
            var rewards = Fake<IRewardTrackManager>((method, args) =>
            {
                Rewards.Add($"{method.Name} {args![1]}");

                return null;
            });
            Communication = new MessengerCommunicationService(
                filter,
                _loader,
                Fake<IQuestManager>((method, _) => throw new NotSupportedException(method.Name)),
                rewards,
                new MessengerCommunicationOutput(),
                new MessengerFriendMutationService(_loader, new AccountSessionGate(), Clients),
                Clients);
        }

        public async Task LogIn()
        {
            var synchronizer = new MessengerEventSynchronizer(_loader, Clients);
            await synchronizer.UserLoggedIn(Sender.Habbo);
            await synchronizer.UserLoggedIn(Target.Habbo);
            Sender.Headers.Clear();
            Target.Headers.Clear();
            Sender.FirstInts.Clear();
        }
    }

    private static T Fake<T>(Func<MethodInfo, object?[]?, object?> handler) where T : class
    {
        var proxy = DispatchProxy.Create<T, MessengerCommunicationServiceTests.ServiceProxy>();
        ((MessengerCommunicationServiceTests.ServiceProxy)(object)proxy).Handler = handler;

        return proxy;
    }

    private sealed class Participant
    {
        public Participant(int id, string username, int friendId, string friendName)
        {
            Messenger = new HabboMessenger(new() { [friendId] = new MessengerBuddy { Id = friendId, Username = friendName } }, [], [], TimeProvider.System);
            Client = new FlashGameClient(TestGameServer.Instance, new FlashPacketFactory(), TestLogging.GameClient)
            {
                Revision = new Revision
                {
                    InternalIdToOutgoingIdMapping = typeof(ServerPacketHeader).GetFields(BindingFlags.Public | BindingFlags.Static)
                    .Where(field => field.FieldType == typeof(uint)).Select(field => (uint)field.GetValue(null)!).Where(headerId => headerId > 0).Distinct().ToDictionary(headerId => headerId, headerId => headerId)
                },
                SendCallback = args =>
                {
                    var bytes = args.MemoryBuffer.Span.Slice(args.Offset, args.Count).ToArray();
                    Headers.Add(BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(4, 2)));

                    if (bytes.Length >= 10) {
                        FirstInts.Add(BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(6, 4)));
                    }

                    return true;
                }
            };
            Habbo = new Habbo { Id = id, Username = username, Messenger = Messenger, Client = Client };
            Client.SetHabbo(Habbo);
        }

        public FlashGameClient Client { get; }
        public Habbo Habbo { get; }
        public HabboMessenger Messenger { get; }
        public List<uint> Headers { get; } = [];
        public List<int> FirstInts { get; } = [];
    }
}
