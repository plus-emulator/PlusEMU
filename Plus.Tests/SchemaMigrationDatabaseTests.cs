using Dapper;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Plus.Database;
using Plus.Database.Migrator;
using Xunit;
using static Plus.Database.Migrator.SchemaProbe;

namespace Plus.Tests;

public sealed class SchemaMigrationDatabaseFactAttribute : FactAttribute
{
    public const string Variable = "SCHEMA_MIGRATION_DATABASE";

    public SchemaMigrationDatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(Variable))) {
            Skip = $"Set {Variable} to a MariaDB server connection string (no Database) that may create scratch schemas.";
        }
    }
}

public sealed class SchemaMigrationDatabaseTests : IDisposable
{
    private readonly MySqlConnectionStringBuilder _server;
    private readonly string _schema = "schema_migration_tests_" + Guid.NewGuid().ToString("N")[..12];
    private readonly string _backups = Path.Combine(Path.GetTempPath(), "plus-migration-tests-" + Guid.NewGuid().ToString("N"));

    public SchemaMigrationDatabaseTests()
    {
        _server = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable(SchemaMigrationDatabaseFactAttribute.Variable) ?? "Server=localhost");

        if (Environment.GetEnvironmentVariable(SchemaMigrationDatabaseFactAttribute.Variable) != null) {
            Server().Execute($"CREATE DATABASE `{_schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
        }
    }

    public void Dispose()
    {
        if (Environment.GetEnvironmentVariable(SchemaMigrationDatabaseFactAttribute.Variable) != null) {
            using var server = Server();
            server.Execute($"DROP DATABASE IF EXISTS `{_schema}`");
            server.Execute($"DROP DATABASE IF EXISTS `{_schema}_restore`");
        }

        if (Directory.Exists(_backups)) {
            Directory.Delete(_backups, true);
        }
    }

    [SchemaMigrationDatabaseFact]
    public async Task LegacyDatabaseRecordsWhatItHasAndAppliesOnlyTheRest()
    {
        Db().Execute("CREATE TABLE legacy_one (id INT PRIMARY KEY); CREATE TABLE users (id INT PRIMARY KEY, credits INT); INSERT INTO users VALUES (1, 50)");
        var scripts = new[]
        {
            Script("Updates/1_LegacyOne.sql", "CREATE TABLE legacy_one (id INT PRIMARY KEY);", Table("legacy_one")),
            // Running this again would double every balance: it must be detected, not re-run.
            Script("2_DoubleCredits.sql", "ALTER TABLE users ADD COLUMN doubled BOOL NOT NULL DEFAULT TRUE; UPDATE users SET credits = credits * 2;", Column("users", "doubled")),
            Script("3_LegacyThree.sql", "CREATE TABLE legacy_three (id INT PRIMARY KEY);", Table("legacy_three")),
            Script("4_New.sql", "CREATE TABLE added (id INT PRIMARY KEY); INSERT INTO added VALUES (7);", null),
        };
        Db().Execute("ALTER TABLE users ADD COLUMN doubled BOOL NOT NULL DEFAULT TRUE");

        Assert.True(await Migrator(scripts).Run(MigrationMode.Apply));

        Assert.Equal(50, Db().ExecuteScalar<int>("SELECT credits FROM users WHERE id = 1"));
        Assert.Equal(7, Db().ExecuteScalar<int>("SELECT id FROM added"));
        Assert.True(Db().ExecuteScalar<bool>("SELECT COUNT(*) = 1 FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'legacy_three'"));
        Assert.Equal(
            new[] { "2_DoubleCredits.sql detected", "3_LegacyThree.sql applied", "4_New.sql applied", "Updates/1_LegacyOne.sql detected" },
            Db().Query<string>("SELECT CONCAT(migration, ' ', source) FROM schema_migrations ORDER BY migration"));
        Assert.Single(Directory.GetFiles(_backups, "*.sql"));

        // A second start finds nothing to do.
        Assert.True(await Migrator(scripts).Run(MigrationMode.Startup));
        Assert.Equal(4, Db().ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations"));
    }

    [SchemaMigrationDatabaseFact]
    public async Task GapsStopTheStartUnlessAllowed()
    {
        Db().Execute("CREATE TABLE second (id INT PRIMARY KEY)");
        var scripts = new[]
        {
            Script("1_First.sql", "CREATE TABLE first (id INT PRIMARY KEY);", Table("first")),
            Script("2_Second.sql", "CREATE TABLE second (id INT PRIMARY KEY);", Table("second")),
        };

        Assert.False(await Migrator(scripts).Run(MigrationMode.Startup));
        Assert.Equal(0, Db().ExecuteScalar<int>("SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME IN ('first', 'schema_migrations')"));

        Assert.True(await Migrator(scripts).Run(MigrationMode.Startup, new MigrationRunOptions(MarkApplied: new[] { "1_First.sql" })));
        Assert.Equal("1_First.sql marked,2_Second.sql detected", Db().ExecuteScalar<string>("SELECT GROUP_CONCAT(migration, ' ', source ORDER BY migration) FROM schema_migrations"));
    }

    [SchemaMigrationDatabaseFact]
    public async Task FailedMigrationStopsAndRunsAgainFromTheTopNextTime()
    {
        Db().Execute("CREATE TABLE existing (id INT PRIMARY KEY)");
        var broken = Script("1_Broken.sql", "CREATE TABLE IF NOT EXISTS half (id INT PRIMARY KEY);\nINSERT INTO missing_table VALUES (1);", null);

        Assert.False(await Migrator(new[] { broken }).Run(MigrationMode.Startup));
        Assert.Equal(0, Db().ExecuteScalar<int>("SELECT COUNT(*) FROM schema_migrations"));

        var fixedScript = Script("1_Broken.sql", "CREATE TABLE IF NOT EXISTS half (id INT PRIMARY KEY);\nINSERT INTO half VALUES (1);", null);
        Assert.True(await Migrator(new[] { fixedScript }).Run(MigrationMode.Startup));
        Assert.Equal(1, Db().ExecuteScalar<int>("SELECT COUNT(*) FROM half"));
    }

    [SchemaMigrationDatabaseFact]
    public async Task EmptyDatabaseGetsTheFreshInstall()
    {
        var scripts = new[] { Script("1_Legacy.sql", "CREATE TABLE legacy (id INT PRIMARY KEY);", Table("legacy")), Script("2_New.sql", "INSERT INTO legacy VALUES (2);", null) };
        var migrator = new SchemaMigrator(Configuration(), NullLogger<SchemaMigrator>.Instance, scripts, () => "CREATE TABLE legacy (id INT PRIMARY KEY);\nINSERT INTO legacy VALUES (1);");

        Assert.True(await migrator.Run(MigrationMode.Startup));

        Assert.Equal(new[] { 1, 2 }, Db().Query<int>("SELECT id FROM legacy ORDER BY id"));
        Assert.Equal("1_Legacy.sql fresh_install,2_New.sql applied", Db().ExecuteScalar<string>("SELECT GROUP_CONCAT(migration, ' ', source ORDER BY migration) FROM schema_migrations"));
        Assert.False(Directory.Exists(_backups));
    }

    [SchemaMigrationDatabaseFact]
    public async Task BuiltinBackupRestoresEveryRowExactly()
    {
        Db().Execute("""
            CREATE TABLE sample (
                id INT PRIMARY KEY, name VARCHAR(64) NULL, flag TINYINT(1) NOT NULL, bits BIT(8) NOT NULL, blob_value BLOB NULL,
                created DATETIME(6) NULL, day DATE NULL, span TIME NULL, amount DECIMAL(12, 4) NULL, ratio DOUBLE NULL,
                kind ENUM('a', 'b') NOT NULL, uuid CHAR(36) NULL, doc JSON NULL
            );
            INSERT INTO sample VALUES
                (1, 'it''s a \\ test\n; -- not a comment', 1, b'10100101', 0x00FF10, '2026-10-18 12:34:56.123456', '2026-10-18', '-838:59:59', 12345678.1234, 0.1, 'b', 'c0e1a2b3-0000-4000-8000-000000000001', '{"a": [1, "x"]}'),
                (2, NULL, 0, b'0', '', NULL, NULL, '25:00:00.5', NULL, -1.5e300, 'a', NULL, NULL),
                (3, '日本語 🎉', 0, b'1', NULL, '1970-01-01 00:00:01', '0000-00-00', '00:00:00', 0, NULL, 'a', '', 'null');
            """);
        var path = Path.Combine(Path.GetTempPath(), $"{_schema}.sql");

        try {
            var builder = new MySqlConnectionStringBuilder(_server.ConnectionString) { Database = _schema, AllowZeroDateTime = true, TreatTinyAsBoolean = false, GuidFormat = MySqlGuidFormat.None, AllowUserVariables = true };
            Assert.True(await DatabaseDump.Builtin(builder.ConnectionString, path));

            using var server = Server();
            server.Execute($"CREATE DATABASE `{_schema}_restore` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            using var restore = new MySqlConnection(new MySqlConnectionStringBuilder(_server.ConnectionString) { Database = _schema + "_restore", AllowUserVariables = true }.ConnectionString);
            restore.Open();

            foreach (var statement in SqlScript.Split(await File.ReadAllTextAsync(path))) {
                restore.Execute(statement);
            }

            Assert.Equal(Checksum(server, _schema), Checksum(server, _schema + "_restore"));
        }
        finally {
            File.Delete(path);
        }
    }

    private static long Checksum(MySqlConnection server, string schema) => server.QuerySingle<(string Table, long Checksum)>($"CHECKSUM TABLE `{schema}`.`sample`").Checksum;

    private static MigrationScript Script(string id, string sql, SchemaProbe? probe) => new(id, sql, MigrationScripts.Checksum(sql), probe);

    private SchemaMigrator Migrator(IReadOnlyList<MigrationScript> scripts) => new(Configuration(), NullLogger<SchemaMigrator>.Instance, scripts, () => null);

    private DatabaseConfiguration Configuration() => new()
    {
        Hostname = _server.Server,
        Port = _server.Port,
        Username = _server.UserID,
        Password = _server.Password,
        Name = _schema,
        // No dump tool: the built-in dump writes the backup.
        Migrations = new MigrationConfiguration { BackupDirectory = _backups, DumpCommand = string.Empty },
    };

    private MySqlConnection Server()
    {
        var connection = new MySqlConnection(new MySqlConnectionStringBuilder(_server.ConnectionString) { Database = string.Empty, AllowUserVariables = true }.ConnectionString);
        connection.Open();

        return connection;
    }

    private MySqlConnection Db()
    {
        var connection = new MySqlConnection(new MySqlConnectionStringBuilder(_server.ConnectionString) { Database = _schema, AllowUserVariables = true }.ConnectionString);
        connection.Open();

        return connection;
    }
}
