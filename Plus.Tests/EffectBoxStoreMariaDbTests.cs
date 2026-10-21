using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Database;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public sealed class EffectBoxStoreMariaDbTests
{
    private static readonly DateTimeOffset Now = new(2042, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [FoundationSchemaDatabaseFact]
    public void OpeningDeletesTheBoxAndStoresTheEffectForGood()
    {
        WithSchema((connection, connectionString) =>
        {
            var effect = Store(connectionString).ConsumeEffectBox(50, 7, 42, 16, Now);

            Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id=50"));
            var row = connection.QuerySingle<(int Id, int UserId, int EffectId, int Duration, bool Activated, DateTime Stamp, int Quantity)>(
                "SELECT id,user_id,effect_id,total_duration,is_activated,activated_stamp,quantity FROM user_effects");
            Assert.Equal((effect.Id, 7, 16, 0, true, Now.UtcDateTime, 1), row);
            Assert.Equal((16, 0d, true, 1), (effect.SpriteId, effect.Duration, effect.Activated, effect.Quantity));
        });
    }

    [FoundationSchemaDatabaseFact]
    public void AnEffectTheUserAlreadyHasBecomesPermanentInsteadOfASecondRow()
    {
        WithSchema((connection, connectionString) =>
        {
            var id = connection.ExecuteScalar<int>("INSERT INTO user_effects(user_id,effect_id,total_duration,is_activated,activated_stamp,quantity) " +
                "VALUES(7,16,3600,0,NULL,2); SELECT LAST_INSERT_ID()");

            var effect = Store(connectionString).ConsumeEffectBox(50, 7, 42, 16, Now);

            Assert.Equal(id, effect.Id);
            var row = connection.QuerySingle<(int Duration, bool Activated, int Quantity)>("SELECT total_duration,is_activated,quantity FROM user_effects");
            Assert.Equal((0, true, 2), row);
        });
    }

    [FoundationSchemaDatabaseFact]
    public void ABoxThatIsNoLongerThereGrantsNothing()
    {
        WithSchema((connection, connectionString) =>
        {
            var store = Store(connectionString);

            Assert.Throws<InvalidOperationException>(() => store.ConsumeEffectBox(50, 8, 42, 16, Now));
            Assert.Throws<InvalidOperationException>(() => store.ConsumeEffectBox(51, 7, 42, 16, Now));

            Assert.Equal(1, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM items WHERE id=50"));
            Assert.Equal(0, connection.ExecuteScalar<int>("SELECT COUNT(*) FROM user_effects"));
        });
    }

    private static ItemRedemptionStore Store(string connectionString) => new(new RawDatabase(connectionString));

    private static void WithSchema(Action<IDbConnection, string> test)
    {
        var serverConnection = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        var schema = "effect_box_tests_" + Guid.NewGuid().ToString("N");
        using var server = new MySqlConnection(serverConnection);
        server.Execute($"CREATE DATABASE `{schema}`");

        try {
            var connectionString = new MySqlConnectionStringBuilder(serverConnection) { Database = schema }.ConnectionString;
            using var connection = new MySqlConnection(connectionString);
            connection.Execute("CREATE TABLE items(id INT UNSIGNED NOT NULL PRIMARY KEY,user_id INT NOT NULL,room_id INT UNSIGNED NOT NULL DEFAULT 0,base_item INT NOT NULL DEFAULT 0)");
            connection.Execute("CREATE TABLE user_effects(id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,user_id INT NULL,effect_id INT DEFAULT 1,total_duration INT DEFAULT 3600," +
                "is_activated TINYINT(1) DEFAULT 0,quantity INT DEFAULT 0,activated_stamp DATETIME NULL)");
            connection.Execute("INSERT INTO items(id,user_id,room_id) VALUES(50,7,42)");
            test(connection, connectionString);
        }
        finally {
            server.Execute($"DROP DATABASE `{schema}`");
        }
    }

    private sealed class RawDatabase(string connectionString) : IDatabase
    {
        public IDbConnection Connection() => new MySqlConnection(connectionString);
        public bool IsConnected() => true;
    }
}
