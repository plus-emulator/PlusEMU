using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.UserData;
using Xunit;

namespace Plus.Tests;

public sealed class AuthenticatorLogoutTests
{
    private const int UserId = 7;

    [Fact]
    public async Task AFailingLogoutTaskDoesNotStopTheOthersOrTheDisconnect()
    {
        var clients = new SharedTestClients();
        var saves = 0;
        var persistence = CatalogSnapshotTestSupport.Proxy<IUserPersistenceService>((name, _) =>
        {
            if (name == nameof(IUserPersistenceService.Save)) {
                saves++;
            }

            return null;
        });
        Habbo? loaded = null;
        var after = new RecordingTask();
        var (session, _) = HabbiconTestSupport.Client(null!);

        Assert.Null(await Login([new FailingTask(), after], clients, persistence, habbo => loaded = habbo).AuthenticateUsingSSO(session, "ticket-for-user-seven"));
        loaded!.OnDisconnect();

        Assert.Equal(1, after.LoggedOut);
        Assert.Equal(1, saves);
        Assert.Null(clients.GetClientByUserId(UserId));
    }

    private static Authenticator Login(IAuthenticationTask[] tasks, SharedTestClients clients, IUserPersistenceService persistence, Action<Habbo> loaded)
    {
        var tickets = CatalogSnapshotTestSupport.Proxy<ISsoTicketStore>((name, _) => name == nameof(ISsoTicketStore.Consume)
            ? Task.FromResult<int?>(UserId)
            : throw new NotSupportedException(name));
        var users = CatalogSnapshotTestSupport.Proxy<IUserDataFactory>((name, _) =>
        {
            if (name != nameof(IUserDataFactory.Create)) {
                throw new NotSupportedException(name);
            }

            var habbo = new Habbo
            {
                Id = UserId,
                Username = "seven",
                Access = UserAccess.Empty,
                SessionStartedAt = DateTimeOffset.UtcNow,
                HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0),
                Persistence = persistence
            };
            habbo.SetHotelServices(clients, TestRoomManager.Unused);
            loaded(habbo);

            return Task.FromResult<Habbo?>(habbo);
        });

        return new Authenticator(tasks, clients, users, tickets, new AccountSessionGate(), persistence);
    }

    private sealed class FailingTask : IAuthenticationTask
    {
        public void UserLoggedOut(Habbo habbo) => throw new InvalidOperationException("logout task failed");
    }

    private sealed class RecordingTask : IAuthenticationTask
    {
        public int LoggedOut { get; private set; }

        public void UserLoggedOut(Habbo habbo) => LoggedOut++;
    }
}
