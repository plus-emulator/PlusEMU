using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Plus.Database;
using Plus.HabboHotel.Moderation;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Authentication;
using Plus.HabboHotel.Users.UserData;
using Xunit;
using static Plus.Tests.CatalogSnapshotTestSupport;

namespace Plus.Tests;

public sealed class ModerationBanCacheTests
{
    private static readonly DateTimeOffset Now = new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public async Task MachineCheckOnABannedUsernameLeavesTheUserBanInForce()
    {
        var manager = Manager();
        await manager.BanUser("mod", ModerationBanType.Username, "Bob", "reason", Now.AddDays(1));

        // The database holds no machine row named "Bob", which must not lift the user ban.
        Assert.False(manager.HasBan(ModerationBanType.Machine, "Bob"));

        Assert.True(manager.IsBanned(ModerationBanType.Username, "Bob", out var ban));
        Assert.Equal(ModerationBanType.Username, ban.Type);
    }

    [Fact]
    public async Task UserAndMachineBansWithTheSameValueAreKeptApart()
    {
        var manager = Manager();
        await manager.BanUser("mod", ModerationBanType.Machine, "Bob", "machine", Now.AddDays(1));
        await manager.BanUser("mod", ModerationBanType.Username, "Bob", "user", Now.AddDays(2));

        Assert.True(manager.IsBanned(ModerationBanType.Machine, "Bob", out var machine));
        Assert.Equal(ModerationBanType.Machine, machine.Type);
        Assert.Equal(Now.AddDays(1), machine.ExpiresAt);
        Assert.True(manager.IsBanned(ModerationBanType.Username, "Bob", out var user));
        Assert.Equal(ModerationBanType.Username, user.Type);
        Assert.Equal(Now.AddDays(2), user.ExpiresAt);
    }

    [Fact]
    public async Task AMachineBanDoesNotBanTheUsernameWithTheSameValue()
    {
        var manager = Manager();
        await manager.BanUser("mod", ModerationBanType.Machine, "Bob", "reason", Now.AddDays(1));

        Assert.False(manager.IsBanned(ModerationBanType.Username, "Bob", out _));
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    public async Task LoginIsRefusedForABannedUsernameOrABannedRecordedMachine(bool userBanned, bool machineBanned, bool allowed)
    {
        var (session, _) = HabbiconTestSupport.Client(new Habbo { Id = 1 });
        session.RecordMachineId("machine");
        var moderation = Proxy<IModerationManager>((method, args) => method switch
        {
            nameof(IModerationManager.HasBan) => (ModerationBanType)args[0]! switch
            {
                ModerationBanType.Username => userBanned && (string)args[1]! == "bob",
                ModerationBanType.Machine => machineBanned && (string)args[1]! == "machine",
                _ => false
            },
            _ => false
        });
        var users = Proxy<IUserDataFactory>((method, _) => method == nameof(IUserDataFactory.GetUsernameForHabboById) ? Task.FromResult("bob") : null);

        Assert.Equal(allowed, await new BanLoginCheckTask(moderation, users).CanLogin(session, 1));
    }

    private static ModerationManager Manager() => new(
        Proxy<IDatabase>((method, _) => method == nameof(IDatabase.Connection) ? new PlacedFurniRoomTests.NoOpConnection() : null),
        NullLogger<ModerationManager>.Instance, Proxy<ISsoTicketStore>((_, _) => Task.CompletedTask), new SharedTestClients(),
        new AccountSessionGate(), new FakeTimeProvider(Now));
}
