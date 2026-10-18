using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Plus.Database.Migrator;

/// <param name="Id">The file name for Database/Migrations, "Updates/&lt;file&gt;" for Resources/SQLs/Updates.</param>
/// <param name="Probe">How to tell that a legacy migration already ran on a database that predates the ledger; null for new ones.</param>
/// <param name="Rerunnable">Running the legacy migration again changes nothing that matters, so an unclear probe just runs it.</param>
public sealed record MigrationScript(string Id, string Sql, string Checksum, SchemaProbe? Probe, bool Rerunnable = false);

/// <summary>
/// The migrations compiled into the emulator, in the order they apply: the legacy files in the order they were written,
/// then every newer Database/Migrations file by its number.
/// </summary>
public static class MigrationScripts
{
    private const string MigrationPrefix = "Plus.Migrations.";
    private const string UpdatePrefix = "Plus.Migrations.Updates.";
    public const string FreshInstallResource = "Plus.Database.FreshInstall.sql";

    public static IReadOnlyList<MigrationScript> Load() => Load(typeof(MigrationScripts).Assembly);

    public static IReadOnlyList<MigrationScript> Load(Assembly assembly)
    {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var resource in assembly.GetManifestResourceNames()) {
            if (resource.StartsWith(UpdatePrefix, StringComparison.Ordinal)) {
                sources["Updates/" + resource[UpdatePrefix.Length..]] = ReadResource(assembly, resource);
            }
            else if (resource.StartsWith(MigrationPrefix, StringComparison.Ordinal)) {
                sources[resource[MigrationPrefix.Length..]] = ReadResource(assembly, resource);
            }
        }

        var scripts = new List<MigrationScript>();

        foreach (var (id, probe, rerunnable) in LegacyMigrations.All) {
            if (!sources.Remove(id, out var sql)) {
                throw new InvalidOperationException($"Legacy migration {id} is not embedded in the emulator.");
            }

            scripts.Add(new MigrationScript(id, sql, Checksum(sql), probe, rerunnable));
        }

        foreach (var (id, sql) in sources.OrderBy(s => Number(s.Key)).ThenBy(s => s.Key, StringComparer.Ordinal)) {
            if (id.StartsWith("Updates/", StringComparison.Ordinal)) {
                throw new InvalidOperationException($"{id} has no entry in LegacyMigrations; Resources/SQLs/Updates takes no new files.");
            }

            scripts.Add(new MigrationScript(id, sql, Checksum(sql), null));
        }

        return scripts;
    }

    public static string? FreshInstall(Assembly assembly) =>
        assembly.GetManifestResourceNames().Contains(FreshInstallResource) ? ReadResource(assembly, FreshInstallResource) : null;

    /// <summary>The leading number of a migration file name ("95_Foo.sql" → 95); files without one sort last.</summary>
    public static long Number(string id)
    {
        var name = id[(id.LastIndexOf('/') + 1)..];
        var digits = name.TakeWhile(char.IsAsciiDigit).Count();

        return digits > 0 && long.TryParse(name[..digits], out var number) ? number : long.MaxValue;
    }

    // Line endings do not change a migration: checkouts with autocrlf must give the same checksum.
    public static string Checksum(string sql) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sql.Replace("\r\n", "\n"))));

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException($"Missing resource {name}.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        return reader.ReadToEnd();
    }
}
