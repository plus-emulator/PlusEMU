using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Database;
using Plus.HabboHotel.Users.Authentication;
using Xunit;

namespace Plus.Tests;

public sealed class LauncherTokenDatabaseFactAttribute : FactAttribute
{
    public LauncherTokenDatabaseFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LAUNCHER_TOKEN_DATABASE"))) {
            Skip = "Set LAUNCHER_TOKEN_DATABASE to an isolated MariaDB server with CREATE DATABASE permission.";
        }
    }
}

public sealed class LauncherTokenDatabaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    [LauncherTokenDatabaseFact]
    public async Task TokensResolveUntilExpiryRevocationOrAPasswordChange()
    {
        using var database = new ScratchDatabase();
        var time = new FakeTimeProvider(Now);
        var store = new LauncherTokenStore(database, time);

        var token = Assert.IsType<string>(await store.Issue(7, TimeSpan.FromDays(30)));
        Assert.Null(await store.Issue(404, TimeSpan.FromDays(30)));
        Assert.Equal(LauncherTokenStore.Hash(token), database.Connection.QuerySingle<string>("SELECT token_hash FROM user_launcher_tokens"));
        Assert.Equal(7, await store.Resolve(token));
        Assert.Null(await store.Resolve(token + "x"));

        database.Connection.Execute("UPDATE users SET credential_generation = credential_generation + 1 WHERE id = 7");
        Assert.Null(await store.Resolve(token));

        var second = Assert.IsType<string>(await store.Issue(7, TimeSpan.FromDays(30)));
        Assert.Equal(7, await store.Resolve(second));
        await store.Revoke(second);
        Assert.Null(await store.Resolve(second));

        var third = Assert.IsType<string>(await store.Issue(7, TimeSpan.FromDays(1)));
        time.Advance(TimeSpan.FromDays(1));
        Assert.Null(await store.Resolve(third));
    }

    [LauncherTokenDatabaseFact]
    public async Task IssuedTicketsLogInOnceBeforeTheyExpire()
    {
        using var database = new ScratchDatabase();
        var time = new FakeTimeProvider(Now);
        var tickets = new SsoTicketStore(database, time);

        var ticket = Assert.IsType<string>(await tickets.Issue(7, TimeSpan.FromSeconds(60)));
        Assert.Null(await tickets.Issue(404, TimeSpan.FromSeconds(60)));
        Assert.Equal(7, await tickets.Consume(ticket));
        Assert.Null(await tickets.Consume(ticket));

        var late = Assert.IsType<string>(await tickets.Issue(7, TimeSpan.FromSeconds(60)));
        time.Advance(TimeSpan.FromSeconds(61));
        Assert.Null(await tickets.Consume(late));
    }

    private sealed class FakeTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }

    private sealed class ScratchDatabase : IDatabase, IDisposable
    {
        private readonly MySqlConnectionStringBuilder _options;
        private readonly string _schema = "launcher_tokens_" + Guid.NewGuid().ToString("N");

        public ScratchDatabase()
        {
            SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
            _options = new(Environment.GetEnvironmentVariable("LAUNCHER_TOKEN_DATABASE")) { Pooling = false };
            Connection = new(_options.ConnectionString);
            Connection.Open();
            Connection.Execute($"CREATE DATABASE `{_schema}`");
            Connection.Execute($"USE `{_schema}`");
            _options.Database = _schema;

            try {
                var fresh = File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql"));
                var start = fresh.IndexOf("CREATE TABLE `users` (", StringComparison.Ordinal);
                Connection.Execute(fresh[start..(fresh.IndexOf(';', start) + 1)]);
                var migration = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/88_LauncherTokens.sql"));
                Connection.Execute(migration);
                Connection.Execute(migration);
                Connection.Execute("INSERT INTO users (id, username, auth_ticket) VALUES (7, 'launcher', '')");
            }
            catch {
                Dispose();
                throw;
            }
        }

        public MySqlConnection Connection { get; }

        IDbConnection IDatabase.Connection() => new MySqlConnection(_options.ConnectionString);

        public bool IsConnected() => true;

        public void Dispose()
        {
            Connection.Execute("USE information_schema");
            Connection.Execute($"DROP DATABASE IF EXISTS `{_schema}`");
            Connection.Dispose();
        }
    }
}
