using Dapper;
using MySqlConnector;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Process;
using Xunit;

namespace Plus.Tests;

[Collection("SharedDatabase")]
public sealed class ScheduledCurrencyDatabaseTests
{
    [RoomComponentDatabaseFact]
    public void PaymentPreservesUnflushedRewardsAndSurvivesReloadWithoutChangingOtherCurrencies()
    {
        using var fixture = new Fixture();
        var habbo = new Habbo { Id = 7, Credits = 140, Duckets = 25 };
        habbo.CheckCreditsTimer(Settings(), fixture.Store);
        Assert.Equal((145, 35), (habbo.Credits, habbo.Duckets));
        using var reconnected = new MySqlConnection(fixture.ConnectionString);
        reconnected.Open();
        var loaded = new Habbo { Id = 7, Credits = reconnected.QuerySingle<int>("SELECT credits FROM users WHERE id=7") };
        loaded.Currencies.Load(UserCurrencyStore.Load(reconnected, 7));
        Assert.Equal((145, 35, 9), (loaded.Credits, loaded.Duckets, loaded.Diamonds));
    }

    [RoomComponentDatabaseFact]
    public void FailedDucketWriteRollsBackCreditsAndCanRetryWithoutPublishingAPartialReward()
    {
        using var fixture = new Fixture();
        fixture.Connection.Execute("CREATE TRIGGER refuse_duckets BEFORE INSERT ON user_currencies FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='forced currency failure'");
        var habbo = new Habbo { Id = 7, Credits = 100, Duckets = 20 };
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        habbo.Client = client;
        habbo.CheckCreditsTimer(Settings(), fixture.Store);
        Assert.Equal((100, 20), (habbo.Credits, habbo.Duckets));
        Assert.Equal((100, 20), fixture.Balances());
        Assert.Empty(sent);
        fixture.Connection.Execute("DROP TRIGGER refuse_duckets");
        habbo.CheckCreditsTimer(Settings(), fixture.Store);
        Assert.Equal((105, 30), fixture.Balances());
        Assert.Equal((105, 30), (habbo.Credits, habbo.Duckets));
        Assert.Equal(2, sent.Count);
    }

    [RoomComponentDatabaseFact]
    public void OverflowDoesNotPartiallyPersistEitherCurrency()
    {
        using var fixture = new Fixture();
        var habbo = new Habbo { Id = 7, Credits = 100, Duckets = int.MaxValue };
        habbo.CheckCreditsTimer(Settings(), fixture.Store);
        Assert.Equal((100, 20), fixture.Balances());
        Assert.Equal((100, int.MaxValue), (habbo.Credits, habbo.Duckets));
    }

    [RoomComponentDatabaseFact]
    public void MissingUsersAndInvalidBalancesNeverCreateOrChangeCurrencyRows()
    {
        using var fixture = new Fixture();
        Assert.False(fixture.Store.SaveCurrency(404, 105, 30));
        Assert.False(fixture.Store.SaveCurrency(0, 105, 30));
        Assert.False(fixture.Store.SaveCurrency(7, -1, 30));
        Assert.False(fixture.Store.SaveCurrency(7, 105, -1));
        Assert.Equal((100, 20), fixture.Balances());
        Assert.Equal(2, fixture.Connection.QuerySingle<int>("SELECT COUNT(*) FROM user_currencies"));
    }

    [RoomComponentDatabaseFact]
    public void SavingTheSameRewardedBalancesAgainDoesNotApplyAnotherDelta()
    {
        using var fixture = new Fixture();
        Assert.True(fixture.Store.SaveCurrency(7, 105, 30));
        Assert.True(fixture.Store.SaveCurrency(7, 105, 30));
        Assert.Equal((105, 30), fixture.Balances());
    }

    private static TestRoomSettings Settings() => new(new()
    {
        ["user.currency_scheduler.credit_reward"] = "5",
        ["user.currency_scheduler.ducket_reward"] = "10",
        ["user.currency_scheduler.tick"] = "3"
    });

    private sealed class Fixture : IDisposable
    {
        private readonly MySqlConnection _admin;
        private readonly string _schema = "task_scheduled_currency_" + Guid.NewGuid().ToString("N");
        public string ConnectionString { get; }
        public MySqlConnection Connection { get; }
        public UserProcessStore Store { get; }

        public Fixture()
        {
            var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE"))
            {
                Database = "information_schema"
            };
            _admin = new MySqlConnection(options.ConnectionString);
            _admin.Open();
            _admin.Execute($"CREATE DATABASE `{_schema}`");
            options.Database = _schema;
            ConnectionString = options.ConnectionString;
            Connection = new MySqlConnection(ConnectionString);
            Connection.Open();
            Connection.Execute("""
                CREATE TABLE users (id INT PRIMARY KEY, credits INT NOT NULL) ENGINE=InnoDB;
                CREATE TABLE user_currencies (user_id INT NOT NULL, type INT NOT NULL, amount INT NOT NULL,
                    PRIMARY KEY(user_id,type),FOREIGN KEY(user_id) REFERENCES users(id)) ENGINE=InnoDB;
                INSERT INTO users VALUES(7,100);
                INSERT INTO user_currencies VALUES(7,0,20),(7,5,9);
                """);
            Store = new UserProcessStore(new HabbiconDatabaseTests.TestDatabase(ConnectionString));
        }

        public (int Credits, int Duckets) Balances() =>
            (Connection.QuerySingle<int>("SELECT credits FROM users WHERE id=7"),
                UserCurrencyStore.Get(Connection, 7, ActivityPointType.Duckets));

        public void Dispose()
        {
            Connection.Dispose();
            _admin.Execute($"DROP DATABASE `{_schema}`");
            _admin.Dispose();
        }
    }
}
