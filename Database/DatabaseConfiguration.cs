namespace Plus.Database;

public class DatabaseConfiguration
{
    public string Hostname { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public uint Port { get; set; }
    public uint MinimumPoolSize { get; set; }
    public uint MaximumPoolSize { get; set; }
    public MigrationConfiguration Migrations { get; set; } = new();
}

public class MigrationConfiguration
{
    /// <summary>Apply pending migrations when the emulator starts. Off: the emulator refuses to start until `--migrate` ran.</summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>Dump the whole database before applying migrations.</summary>
    public bool Backup { get; set; } = true;

    public string BackupDirectory { get; set; } = "backups";

    /// <summary>mariadb-dump/mysqldump to use; empty looks for one on PATH and falls back to the emulator's own dump.</summary>
    public string DumpCommand { get; set; } = string.Empty;

    /// <summary>The hotel's FurnitureData.json, needed once by migration 60 when it is pending.</summary>
    public string FurnidataFile { get; set; } = string.Empty;
}
