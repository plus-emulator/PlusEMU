using System.Data;
using Dapper;
using MySqlConnector;
using Plus.Database;
using Plus.HabboHotel.Users.Effects;
using Xunit;

namespace Plus.Tests;

public sealed class AvatarEffectStoreMariaDbTests
{
    [FoundationSchemaDatabaseFact]
    public void MaterializesNativeSchemaAndRefusesPublicationAfterRowRemoval()
    {
        var serverConnection = Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!;
        var schema = "task_refactor_tests_effects_" + Guid.NewGuid().ToString("N");
        using var server = new MySqlConnection(serverConnection);
        server.Execute($"CREATE DATABASE `{schema}`");

        try {
            SqlMapper.AddTypeHandler(new UtcDateTimeOffsetHandler());
            var database = new RawDatabase(new MySqlConnectionStringBuilder(serverConnection) { Database = schema }.ConnectionString);
            using var connection = database.Connection();
            connection.Execute("CREATE TABLE user_effects(id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,user_id INT UNSIGNED NULL,effect_id INT DEFAULT 1,total_duration INT DEFAULT 3600,is_activated BOOL DEFAULT FALSE,activated_stamp DATETIME NULL,quantity INT DEFAULT 0)");
            var store = new AvatarEffectStore(database);
            connection.Open();
            AvatarEffect created;

            using (var transaction = connection.BeginTransaction()) {
                created = store.Create(connection, transaction, 7, 42, 60);
                transaction.Commit();
            }

            var effect = Assert.Single(store.Load(7));
            Assert.Equal(created.Id, effect.Id);
            Assert.Equal(42, effect.SpriteId);
            Assert.Equal(60, effect.Duration);
            Assert.False(effect.Activated);
            Assert.Null(effect.ActivatedAt);
            var instant = new DateTimeOffset(2042, 1, 1, 0, 0, 0, TimeSpan.Zero);
            effect.Activate(instant);
            var loaded = Assert.Single(store.Load(7));
            Assert.True(loaded.Activated);
            Assert.Equal(instant, loaded.ActivatedAt);
            store.SaveQuantity(effect.Id, 2, true, instant);
            Assert.Equal(2, Assert.Single(store.Load(7)).Quantity);

            using (var transaction = connection.BeginTransaction()) {
                store.AddQuantity(connection, transaction, effect.Id);
                transaction.Commit();
            }

            Assert.Equal(3, Assert.Single(store.Load(7)).Quantity);

            connection.Execute("DELETE FROM user_effects WHERE id=@id", new { id = effect.Id });
            Assert.Throws<DBConcurrencyException>(() => effect.Activate(instant.AddHours(1)));
            Assert.Equal(instant, effect.ActivatedAt);

            using (var transaction = connection.BeginTransaction()) {
                Assert.Throws<DBConcurrencyException>(() => store.AddQuantity(connection, transaction, effect.Id));
            }

            Assert.Throws<DBConcurrencyException>(() => store.SaveQuantity(effect.Id, 0, false, null));
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
