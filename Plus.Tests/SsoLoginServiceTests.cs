using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Reflection;
using Plus.Communication.Attributes;
using Plus.Communication.Packets.Incoming.Handshake;
using Plus.Communication.Packets.Outgoing;
using Plus.Core;
using Plus.Core.FigureData;
using Plus.Core.Language;
using Plus.Core.Settings;
using Plus.HabboHotel.Campaigns;
using Plus.HabboHotel.Achievements;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.Cache;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rewards;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Styles;
using Plus.HabboHotel.Subscriptions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.Effects;
using Plus.HabboHotel.Users.Process;
using Xunit;

namespace Plus.Tests;

public class SsoLoginServiceTests
{
    [Fact]
    public async Task HandlerDecodesOnlyTheTicketAndAwaitsTheService()
    {
        var login = new PendingLogin();
        var (client, sent) = HabbiconTestSupport.Client(new Habbo());
        var pending = new SSOTicketEvent(login).Parse(client, HabbiconTestSupport.Incoming("ticket"));
        Assert.False(pending.IsCompleted);
        Assert.Equal("ticket", login.Ticket);
        Assert.Same(client, login.Client);
        Assert.Empty(sent);
        Assert.True(Attribute.IsDefined(typeof(SSOTicketEvent), typeof(NoAuthenticationRequiredAttribute)));
        login.Completed.SetResult();
        await pending;
    }

    [Theory]
    [InlineData(AuthenticationError.EmptySSO)]
    [InlineData(AuthenticationError.InvalidSSO)]
    [InlineData(AuthenticationError.NoAccountFound)]
    [InlineData(AuthenticationError.LoginProhibited)]
    public async Task RefusedTicketIsAnsweredWithTheReasonAndTheConnectionCloses(AuthenticationError error)
    {
        var (client, sent) = HabbiconTestSupport.Client(new Habbo());
        var authenticate = Proxy<IAuthenticator>((_, _) => Task.FromResult<AuthenticationError?>(error));
        var service = new SsoLoginService(authenticate, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

        await service.Login(client, "spent");

        Assert.Equal(new[] { ServerPacketHeader.GenericErrorComposer, ServerPacketHeader.DisconnectReasonComposer }, sent.Select(packet => packet.Header));
        Assert.Equal(-3, BinaryPrimitives.ReadInt32BigEndian(sent[0].Payload));
        Assert.Equal(22, BinaryPrimitives.ReadInt32BigEndian(sent[1].Payload));
        Assert.True(client.Closed.IsCancellationRequested);
    }

    [Fact]
    public async Task LoginEndedByAClosedConnectionSendsNothing()
    {
        var (client, sent) = HabbiconTestSupport.Client(new Habbo());
        var authenticate = Proxy<IAuthenticator>((_, _) => Task.FromResult<AuthenticationError?>(AuthenticationError.SessionClosed));
        var service = new SsoLoginService(authenticate, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!, null!);

        await service.Login(client, "ticket");

        Assert.Empty(sent);
        Assert.True(client.Closed.IsCancellationRequested);
    }

    [Fact]
    public async Task SuccessfulLoginPreservesInitializationOrderAndAwaitsRewards()
    {
        var rewardCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var world = new LoginWorld(rewardCompletion.Task);
        var (calls, habbo, client, sent, service) = (world.Calls, world.Habbo, world.Client, world.Sent, world.Service);

        var pending = service.Login(client, "valid");

        try {
            if (pending.IsFaulted) {
                await pending;
            }

            Assert.False(pending.IsCompleted);
            Assert.Equal(new[] { "authenticate", "definitions", "cache", "rewards" }, calls);
            Assert.Equal(new[]
            {
                ServerPacketHeader.AuthenticationOkComposer, ServerPacketHeader.AvatarEffectsComposer,
                ServerPacketHeader.NavigatorSettingsComposer, ServerPacketHeader.FavouritesComposer,
                ServerPacketHeader.FigureSetIdsComposer, ServerPacketHeader.UserRightsComposer,
                ServerPacketHeader.AllowedChatStylesComposer, ServerPacketHeader.CreatableRoomModelsComposer,
                ServerPacketHeader.AvailabilityStatusComposer, ServerPacketHeader.AchievementScoreComposer,
                ServerPacketHeader.CfhTopicsInitComposer, ServerPacketHeader.SoundSettingsComposer,
                ServerPacketHeader.ScrSendUserInfoComposer, ServerPacketHeader.MessengerInitComposer
            }, sent.Select(packet => packet.Header));
            Assert.NotNull(ProcessOf(habbo));

            rewardCompletion.SetResult();
            await pending;
            Assert.Equal(new[] { "tracks", "calendar" }, calls.TakeLast(2));
        }
        finally {
            rewardCompletion.TrySetResult();

            try {
                await pending;
            }
            finally {
                ProcessOf(habbo)?.Dispose();
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoginTellsTheClientWhetherAShutdownIsPending(bool pending)
    {
        using var world = new LoginWorld(Task.CompletedTask, pending ? TestPlannedShutdown.Pending : TestPlannedShutdown.None);

        try {
            await world.Service.Login(world.Client, "valid");
        }
        finally {
            ProcessOf(world.Habbo)?.Dispose();
        }

        var status = Assert.Single(world.Sent, packet => packet.Header == ServerPacketHeader.AvailabilityStatusComposer);
        Assert.Equal(new byte[] { 1, pending ? (byte)1 : (byte)0, 1 }, status.Payload);
    }

    [Fact]
    public async Task LoginSendsTheAccountSettingsFrameWithTheFiveDisplayPreferencesAfterTheWiredStyle()
    {
        using var world = new LoginWorld(Task.CompletedTask);

        try {
            await world.Service.Login(world.Client, "valid");
        }
        finally {
            ProcessOf(world.Habbo)?.Dispose();
        }

        var settings = Assert.Single(world.Sent, packet => packet.Header == ServerPacketHeader.SoundSettingsComposer);
        const string emptyStyle = "0000";
        const string chatSize = "00000000";
        const string chatMode = "00000000";
        const string bubbleWidth = "00000001";
        const string scrollSpeed = "00000001";
        const string onlineIndicator = "00000000";
        Assert.EndsWith(emptyStyle + chatSize + chatMode + bubbleWidth + scrollSpeed + onlineIndicator, Convert.ToHexString(settings.Payload));
        Assert.Equal(3 * 4 + 3 + 4 + 4 + 3 + 4 + 2 + 2 + 5 * 4, settings.Payload.Length);
    }

    private static ProcessComponent? ProcessOf(Habbo habbo) =>
        (ProcessComponent?)typeof(Habbo).GetProperty("Process", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(habbo);

    private static T Proxy<T>(Func<string, object?[]?, object?> invoke) where T : class => CatalogSnapshotTestSupport.Proxy<T>(invoke);

    private sealed class LoginWorld : IDisposable
    {
        private readonly ClubLifecycle _lifecycle;

        public List<string> Calls { get; } = [];
        public Habbo Habbo { get; }
        public Plus.Communication.Flash.FlashGameClient Client { get; }
        public List<(uint Header, byte[] Payload)> Sent { get; }
        public SsoLoginService Service { get; }

        public LoginWorld(Task rewards, IPlannedShutdown? plannedShutdown = null)
        {
            var calls = Calls;
            var habbo = Habbo = new Habbo
            {
                Id = 7,
                Look = "look",
                Clothing = new(),
                Access = UserAccess.Empty,
                HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0)
            };
            (Client, Sent) = HabbiconTestSupport.Client(habbo);
            habbo.Client = Client;
            var settings = Proxy<ISettingsManager>((_, _) => "0");
            var rooms = Proxy<IRoomManager>((method, _) => method == "GetRooms" ? Array.Empty<Room>() : Array.Empty<RoomModel>());
            var styles = Proxy<IChatStyleManager>((_, _) => Array.Empty<int>());
            var permissions = Proxy<IAccessControl>((_, _) => null);
            _lifecycle = new ClubLifecycle(permissions, null!, null!,
                Proxy<IFigureDataManager>((_, args) => args![0]), styles, rooms, settings, new GroupManagementTests.RecordingDatabase(),
                new FixedTimeProvider(FixedTimeProvider.Epoch), TestLogging.For<ClubLifecycle>());
            Service = new SsoLoginService(
                Proxy<IAuthenticator>((_, _) => { calls.Add("authenticate"); return Task.FromResult<AuthenticationError?>(null); }),
                Proxy<IBadgeManager>((method, _) => throw new NotSupportedException(method)),
                Proxy<IModerationManager>((_, _) => new Dictionary<string, List<ModerationPresetActions>>()),
                Proxy<IAchievementShowcaseService>((_, _) => { calls.Add("definitions"); return null; }),
                Proxy<ICacheManager>((method, _) => { calls.Add("cache"); return method == "ContainsUser" ? true : throw new NotSupportedException(method); }),
                Proxy<ILanguageManager>((method, _) => throw new NotSupportedException(method)), settings,
                Proxy<IRewardManager>((_, _) => { calls.Add("rewards"); return rewards; }), _lifecycle,
                new ClientAccessLists(permissions, styles, rooms), new UserProcessFactory(TestLogging.For<ProcessComponent>(), new FixedTimeProvider(FixedTimeProvider.Epoch),
                    Proxy<IUserProcessStore>((_, _) => null), Proxy<IAchievementManager>((_, _) => null), settings),
                Proxy<IModeratorTicketService>((method, _) => throw new NotSupportedException(method)),
                Proxy<IAvatarEffectService>((_, _) => ImmutableArray<AvatarEffectEntry>.Empty),
                Proxy<IRewardTrackManager>((_, _) => { calls.Add("tracks"); return null; }),
                Proxy<ICampaignCalendarService>((_, _) => { calls.Add("calendar"); return null; }),
                plannedShutdown: plannedShutdown);
        }

        public void Dispose() => _lifecycle.Dispose();
    }

    private sealed class PendingLogin : ISsoLoginService
    {
        public TaskCompletionSource Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string? Ticket { get; private set; }
        public GameClient? Client { get; private set; }
        public Task Login(GameClient session, string sso)
        {
            Client = session;
            Ticket = sso;

            return Completed.Task;
        }
    }
}
