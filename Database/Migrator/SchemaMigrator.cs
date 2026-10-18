using System.Data;
using System.Diagnostics;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace Plus.Database.Migrator;

public enum MigrationMode
{
    /// <summary>Normal start: apply pending migrations when AutoMigrate is on, refuse to start on an old schema otherwise.</summary>
    Startup,
    /// <summary>--migrate: apply pending migrations and exit.</summary>
    Apply,
    /// <summary>--migrate-status: print what would happen and change nothing.</summary>
    Status,
}

public interface ISchemaMigrator
{
    /// <summary>Brings the database up to date. False means the emulator must not start.</summary>
    Task<bool> Run(MigrationMode mode, MigrationRunOptions? options = null);
}

/// <param name="AllowGaps">Also apply legacy migrations that look unapplied while later ones look applied.</param>
/// <param name="MarkApplied">Record these migrations as applied without running them.</param>
public sealed record MigrationRunOptions(bool AllowGaps = false, IReadOnlyCollection<string>? MarkApplied = null);

/// <summary>
/// Applies Database/Migrations (compiled into the emulator) and records each one in schema_migrations.
/// A database from before the ledger is matched against every legacy migration's probe first, so only what it lacks runs.
/// An empty database gets FreshInstall.sql.
/// </summary>
public sealed class SchemaMigrator : ISchemaMigrator
{
    public const string LedgerTable = "schema_migrations";
    private const string LockName = "plusemu.schema_migrations";
    private const string FurnidataMigration = "60_FurnitureFurnidata.sql";

    private readonly DatabaseConfiguration _configuration;
    private readonly ILogger<SchemaMigrator> _logger;
    private readonly IReadOnlyList<MigrationScript> _scripts;
    private readonly Func<string?> _freshInstall;

    public SchemaMigrator(IOptions<DatabaseConfiguration> configuration, ILogger<SchemaMigrator> logger)
        : this(configuration.Value, logger, MigrationScripts.Load(), () => MigrationScripts.FreshInstall(typeof(SchemaMigrator).Assembly)) { }

    internal SchemaMigrator(DatabaseConfiguration configuration, ILogger<SchemaMigrator> logger, IReadOnlyList<MigrationScript> scripts, Func<string?> freshInstall)
    {
        _configuration = configuration;
        _logger = logger;
        _scripts = scripts;
        _freshInstall = freshInstall;
    }

    private MigrationConfiguration Settings => _configuration.Migrations;

    public async Task<bool> Run(MigrationMode mode, MigrationRunOptions? options = null)
    {
        options ??= new MigrationRunOptions();

        try {
            await using var connection = Open();

            if (mode == MigrationMode.Status) {
                return Status(connection);
            }

            if (connection.ExecuteScalar<int>("SELECT GET_LOCK(@name, 600)", new { name = LockName }) != 1) {
                _logger.LogError("Another process holds the migration lock; is a second emulator migrating this database?");

                return false;
            }

            try {
                return await Migrate(connection, mode, options);
            }
            finally {
                connection.Execute("DO RELEASE_LOCK(@name)", new { name = LockName });
            }
        }
        catch (MySqlException e) {
            _logger.LogError(e, "Database migration failed: {Message}", e.Message);

            return false;
        }
    }

    private async Task<bool> Migrate(MySqlConnection connection, MigrationMode mode, MigrationRunOptions options)
    {
        var plan = Plan(connection, options);

        if (plan == null) {
            return false;
        }

        if (plan.Empty && !plan.Ledger.Any()) {
            if (mode == MigrationMode.Startup && !Settings.AutoMigrate) {
                _logger.LogError("The database is empty. Import Database/FreshInstall.sql or turn Database.Migrations.AutoMigrate on.");

                return false;
            }

            if (!ImportFreshInstall()) {
                return false;
            }

            using var fresh = Open();
            plan = Plan(fresh, options, freshInstall: true);

            if (plan == null) {
                return false;
            }
        }

        if (plan.Pending.Count > 0 && mode == MigrationMode.Startup && !Settings.AutoMigrate) {
            _logger.LogError("{Count} database migrations are pending ({First} first). Start once with --migrate, or turn Database.Migrations.AutoMigrate on.",
                plan.Pending.Count, plan.Pending[0].Id);

            return false;
        }

        if (plan.Pending.Any(s => s.Id == FurnidataMigration) && LoadFurnidata() == null) {
            return false;
        }

        EnsureLedger(connection);
        Record(connection, plan.Detected, plan.DetectedSource);
        Record(connection, plan.Marked, "marked");

        if (plan.Pending.Count == 0) {
            _logger.LogInformation("Database schema is up to date ({Count} migrations).", _scripts.Count);

            return true;
        }

        _logger.LogWarning("Applying {Count} database migrations: {Names}", plan.Pending.Count, string.Join(", ", plan.Pending.Select(s => s.Id)));

        if (Settings.Backup && !plan.FreshInstall && !await Backup()) {
            _logger.LogError("No migrations were applied because the backup failed. Fix the backup or set Database.Migrations.Backup to false.");

            return false;
        }

        foreach (var script in plan.Pending) {
            if (!Apply(script)) {
                return false;
            }

            Record(connection, new[] { script }, "applied");
        }

        _logger.LogInformation("Database migrations complete.");

        return true;
    }

    private bool Status(MySqlConnection connection)
    {
        var plan = Plan(connection, new MigrationRunOptions(AllowGaps: true));

        if (plan == null) {
            return false;
        }

        if (plan.Empty && !plan.Ledger.Any()) {
            Console.WriteLine("The database is empty: FreshInstall.sql would be imported.");

            return true;
        }

        foreach (var script in _scripts) {
            var state = plan.Ledger.Contains(script.Id) ? "applied" :
                plan.Detected.Contains(script) ? "already in the database (will be recorded)" :
                plan.Pending.Contains(script) ? "PENDING" : "?";
            Console.WriteLine($"{script.Id,-55} {state}");
        }

        foreach (var gap in plan.Gaps) {
            Console.WriteLine($"Gap: {gap.Id} looks unapplied although later migrations are applied.");
        }

        Console.WriteLine($"{plan.Pending.Count} pending.");

        return true;
    }

    private sealed record MigrationPlan(
        bool Empty,
        bool FreshInstall,
        HashSet<string> Ledger,
        List<MigrationScript> Detected,
        string DetectedSource,
        List<MigrationScript> Marked,
        List<MigrationScript> Gaps,
        List<MigrationScript> Pending);

    private MigrationPlan? Plan(MySqlConnection connection, MigrationRunOptions options, bool freshInstall = false)
    {
        var schema = new SchemaSnapshot(connection);
        var ledger = new Dictionary<string, string>(StringComparer.Ordinal);

        if (schema.HasTable(LedgerTable)) {
            foreach (var row in connection.Query<(string Migration, string Checksum)>($"SELECT migration, checksum FROM `{LedgerTable}`")) {
                ledger[row.Migration] = row.Checksum;
            }
        }

        foreach (var script in _scripts) {
            if (ledger.TryGetValue(script.Id, out var checksum) && checksum != script.Checksum) {
                _logger.LogDebug("{Migration} changed after it was applied; it is not run again.", script.Id);
            }
        }

        var empty = schema.TableCount == 0;
        var detected = new List<MigrationScript>();
        var gaps = new List<MigrationScript>();

        // A database from before the ledger: find out which legacy migrations it already has.
        if (ledger.Count == 0 && !empty) {
            var applied = _scripts.Where(s => s.Probe != null).Select(s => (Script: s, Applied: s.Probe!.Evaluate(schema))).ToList();
            detected.AddRange(applied.Where(a => a.Applied).Select(a => a.Script));
            var lastApplied = applied.FindLastIndex(a => a.Applied);
            var markedNames = options.MarkApplied ?? Array.Empty<string>();
            // An unapplied migration before an applied one usually means a probe misjudged this database. That only matters
            // for a migration that is unsafe to run twice.
            gaps.AddRange(applied.Take(lastApplied + 1).Where(a => !a.Applied && !a.Script.Rerunnable && !markedNames.Contains(a.Script.Id)).Select(a => a.Script));

            if (gaps.Count > 0 && !options.AllowGaps) {
                _logger.LogError("These migrations look unapplied although later ones are applied: {Gaps}. " +
                                 "Check them against your database, then start with --migrate-mark-applied <name> for the ones you have, " +
                                 "or --migrate-allow-gaps to apply them.", string.Join(", ", gaps.Select(g => g.Id)));

                return null;
            }
        }

        var marked = new List<MigrationScript>();

        foreach (var name in options.MarkApplied ?? Array.Empty<string>()) {
            var script = _scripts.FirstOrDefault(s => s.Id == name);

            if (script == null) {
                _logger.LogError("--migrate-mark-applied: there is no migration named {Name}.", name);

                return null;
            }

            if (!ledger.ContainsKey(script.Id) && !detected.Contains(script)) {
                marked.Add(script);
            }
        }

        var pending = _scripts.Where(s => !ledger.ContainsKey(s.Id) && !detected.Contains(s) && !marked.Contains(s)).ToList();

        return new MigrationPlan(empty, freshInstall, ledger.Keys.ToHashSet(StringComparer.Ordinal), detected,
            freshInstall ? "fresh_install" : "detected", marked, gaps, pending);
    }

    private bool ImportFreshInstall()
    {
        var sql = _freshInstall();

        if (sql == null) {
            _logger.LogError("The database is empty and this build has no FreshInstall.sql.");

            return false;
        }

        _logger.LogWarning("The database is empty: importing FreshInstall.sql.");

        return Apply(new MigrationScript("FreshInstall.sql", sql, MigrationScripts.Checksum(sql), null));
    }

    private bool Apply(MigrationScript script)
    {
        var statements = SqlScript.Split(script.Sql);
        var watch = Stopwatch.StartNew();
        _logger.LogInformation("Applying {Migration} ({Count} statements)...", script.Id, statements.Count);

        // Its own session: user variables, temporary tables and SET SESSION from one file never leak into the next.
        using var session = Open();

        if (script.Id == FurnidataMigration && LoadFurnidata() is { } furnidata) {
            session.Execute("SET @furnidata = @json", new { json = furnidata });
        }

        for (var i = 0; i < statements.Count; i++) {
            try {
                using var command = session.CreateCommand();
                command.CommandText = statements[i];
                command.CommandTimeout = 0;
                command.ExecuteNonQuery();
            }
            catch (MySqlException e) {
                var statement = statements[i].Length > 400 ? statements[i][..400] + "..." : statements[i];
                _logger.LogError("Migration {Migration} failed at statement {Index} of {Count}: {Message}\n{Statement}\n" +
                                 "The emulator did not start. Statements before this one were applied; restore the backup or fix the cause " +
                                 "and start again (the migration runs again from the top).",
                    script.Id, i + 1, statements.Count, e.Message, statement);

                return false;
            }
        }

        _logger.LogInformation("Applied {Migration} in {Seconds:0.0} s.", script.Id, watch.Elapsed.TotalSeconds);

        return true;
    }

    private string? LoadFurnidata()
    {
        var path = Settings.FurnidataFile;

        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) {
            return File.ReadAllText(path);
        }

        _logger.LogError("Migration {Migration} moves your furnidata into the furniture table and needs your hotel's current FurnitureData.json. " +
                         "Set Database.Migrations.FurnidataFile in Config/config.json to its path{Missing} and start again.",
            FurnidataMigration, string.IsNullOrWhiteSpace(path) ? string.Empty : $" ({path} does not exist)");

        return null;
    }

    private async Task<bool> Backup()
    {
        Directory.CreateDirectory(Settings.BackupDirectory);
        var path = Path.GetFullPath(Path.Join(Settings.BackupDirectory, $"{_configuration.Name}-{DateTime.UtcNow:yyyyMMdd-HHmmss}-before-migrations.sql"));
        _logger.LogWarning("Backing up database {Database} to {Path} before migrating...", _configuration.Name, path);
        var watch = Stopwatch.StartNew();

        var ok = DumpTool() is { } tool ? await DatabaseDump.WithTool(tool, _configuration, path, _logger) : await DatabaseDump.Builtin(ConnectionString(backup: true), path);

        if (ok) {
            _logger.LogInformation("Backup written in {Seconds:0.0} s.", watch.Elapsed.TotalSeconds);
        }

        return ok;
    }

    private string? DumpTool()
    {
        if (!string.IsNullOrWhiteSpace(Settings.DumpCommand)) {
            return Settings.DumpCommand;
        }

        var names = OperatingSystem.IsWindows() ? new[] { "mariadb-dump.exe", "mysqldump.exe" } : new[] { "mariadb-dump", "mysqldump" };
        var path = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? Array.Empty<string>();

        return names.SelectMany(n => path.Select(p => Path.Join(p, n))).FirstOrDefault(File.Exists);
    }

    private void EnsureLedger(MySqlConnection connection) =>
        connection.Execute($"""
            CREATE TABLE IF NOT EXISTS `{LedgerTable}` (
                `migration` VARCHAR(191) NOT NULL,
                `checksum` CHAR(64) NOT NULL,
                `source` ENUM('applied', 'detected', 'fresh_install', 'marked') NOT NULL,
                `applied_at` DATETIME(6) NOT NULL,
                PRIMARY KEY (`migration`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
            """);

    private static void Record(MySqlConnection connection, IEnumerable<MigrationScript> scripts, string source)
    {
        foreach (var script in scripts) {
            connection.Execute($"INSERT IGNORE INTO `{LedgerTable}` (migration, checksum, source, applied_at) VALUES (@Id, @Checksum, @source, UTC_TIMESTAMP(6))",
                new { script.Id, script.Checksum, source });
        }
    }

    private MySqlConnection Open()
    {
        var connection = new MySqlConnection(ConnectionString(backup: false));
        connection.Open();

        return connection;
    }

    private string ConnectionString(bool backup) => new MySqlConnectionStringBuilder
    {
        Server = _configuration.Hostname,
        Port = _configuration.Port,
        UserID = _configuration.Username,
        Password = _configuration.Password,
        Database = _configuration.Name,
        ConnectionTimeout = 10,
        DefaultCommandTimeout = 0,
        Pooling = false,
        // Migrations use @variables; the backup must read values exactly as stored.
        AllowUserVariables = true,
        AllowZeroDateTime = true,
        ConvertZeroDateTime = !backup,
        TreatTinyAsBoolean = !backup,
        GuidFormat = MySqlGuidFormat.None,
        SslMode = MySqlSslMode.None,
    }.ToString();
}
