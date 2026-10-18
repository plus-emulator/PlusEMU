using System.Diagnostics;
using System.Globalization;
using System.Text;
using Dapper;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace Plus.Database.Migrator;

/// <summary>A full SQL backup of the hotel database, taken before migrations run.</summary>
internal static class DatabaseDump
{
    public static async Task<bool> WithTool(string tool, DatabaseConfiguration configuration, string path, ILogger logger)
    {
        var start = new ProcessStartInfo(tool)
        {
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (var argument in new[] {
                     "--single-transaction", "--routines", "--triggers", "--hex-blob", "--default-character-set=utf8mb4",
                     $"--host={configuration.Hostname}", $"--port={configuration.Port}", $"--user={configuration.Username}",
                     $"--result-file={path}", configuration.Name,
                 }) {
            start.ArgumentList.Add(argument);
        }

        start.Environment["MYSQL_PWD"] = configuration.Password;

        try {
            using var process = Process.Start(start)!;
            var errors = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0) {
                logger.LogError("{Tool} exited with {Code}: {Errors}", tool, process.ExitCode, errors.Trim());

                return false;
            }

            return true;
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or InvalidOperationException) {
            logger.LogError("Could not run {Tool}: {Message}", tool, e.Message);

            return false;
        }
    }

    /// <summary>Tables (structure and rows), views and triggers, for hosts without mariadb-dump.</summary>
    public static async Task<bool> Builtin(string connectionString, string path)
    {
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync();
        await using var output = new StreamWriter(path, false, new UTF8Encoding(false));

        await output.WriteLineAsync($"-- PlusEMU backup of `{connection.Database}` taken {DateTime.UtcNow:O} before applying migrations.");
        await output.WriteLineAsync("SET NAMES utf8mb4;\nSET FOREIGN_KEY_CHECKS = 0;\nSET UNIQUE_CHECKS = 0;\nSET SQL_MODE = 'NO_AUTO_VALUE_ON_ZERO';\nSET time_zone = '+00:00';\n");
        await connection.ExecuteAsync("SET time_zone = '+00:00'");

        var tables = (await connection.QueryAsync<(string Name, string Type)>(
            "SELECT TABLE_NAME, TABLE_TYPE FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() ORDER BY TABLE_TYPE, TABLE_NAME")).ToList();

        foreach (var (name, type) in tables.Where(t => t.Type != "VIEW")) {
            var create = await connection.QuerySingleAsync<(string Table, string Sql)>($"SHOW CREATE TABLE `{name}`");
            await output.WriteLineAsync($"DROP TABLE IF EXISTS `{name}`;\n{create.Sql};");
            await DumpRows(connection, name, output);
            await output.WriteLineAsync();
        }

        foreach (var (name, _) in tables.Where(t => t.Type == "VIEW")) {
            using var reader = await connection.ExecuteReaderAsync($"SHOW CREATE VIEW `{name}`");

            if (await reader.ReadAsync()) {
                await output.WriteLineAsync($"DROP VIEW IF EXISTS `{name}`;\n{reader.GetString(1)};\n");
            }
        }

        foreach (var trigger in (await connection.QueryAsync<string>("SELECT TRIGGER_NAME FROM information_schema.TRIGGERS WHERE TRIGGER_SCHEMA = DATABASE()")).ToList()) {
            using var reader = await connection.ExecuteReaderAsync($"SHOW CREATE TRIGGER `{trigger}`");

            if (await reader.ReadAsync()) {
                await output.WriteLineAsync($"DELIMITER ;;\n{reader.GetString(2)};;\nDELIMITER ;\n");
            }
        }

        await output.WriteLineAsync("SET FOREIGN_KEY_CHECKS = 1;\nSET UNIQUE_CHECKS = 1;");

        return true;
    }

    private static async Task DumpRows(MySqlConnection connection, string table, StreamWriter output)
    {
        await using var command = new MySqlCommand($"SELECT * FROM `{table}`", connection) { CommandTimeout = 0 };
        await using var reader = await command.ExecuteReaderAsync();
        var columns = string.Join(", ", Enumerable.Range(0, reader.FieldCount).Select(i => $"`{reader.GetName(i)}`"));
        var batch = new StringBuilder();
        var rows = 0;

        while (await reader.ReadAsync()) {
            batch.Append(rows == 0 ? $"INSERT INTO `{table}` ({columns}) VALUES\n(" : ",\n(");

            for (var i = 0; i < reader.FieldCount; i++) {
                if (i > 0) {
                    batch.Append(", ");
                }

                AppendLiteral(batch, reader.GetValue(i));
            }

            batch.Append(')');

            if (++rows == 500 || batch.Length > 4_000_000) {
                await output.WriteLineAsync(batch.Append(';'));
                batch.Clear();
                rows = 0;
            }
        }

        if (rows > 0) {
            await output.WriteLineAsync(batch.Append(';'));
        }
    }

    internal static void AppendLiteral(StringBuilder sql, object value)
    {
        switch (value) {
            case DBNull:
                sql.Append("NULL");
                break;
            case byte[] bytes:
                sql.Append(bytes.Length == 0 ? "''" : "0x" + Convert.ToHexString(bytes));
                break;
            case bool b:
                sql.Append(b ? '1' : '0');
                break;
            case DateTime dateTime:
                sql.Append('\'').Append(dateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)).Append('\'');
                break;
            case MySqlDateTime dateTime:
                sql.Append(dateTime.IsValidDateTime
                    ? $"'{dateTime.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture)}'"
                    : $"'{dateTime.Year:0000}-{dateTime.Month:00}-{dateTime.Day:00} {dateTime.Hour:00}:{dateTime.Minute:00}:{dateTime.Second:00}'");
                break;
            case DateOnly date:
                sql.Append('\'').Append(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append('\'');
                break;
            case TimeSpan time: {
                    var duration = time.Duration();
                    sql.Append('\'').Append(time < TimeSpan.Zero ? "-" : string.Empty).Append((long)duration.TotalHours)
                        .Append(duration.ToString(@"\:mm\:ss\.ffffff", CultureInfo.InvariantCulture)).Append('\'');
                    break;
                }
            case float or double or decimal or sbyte or byte or short or ushort or int or uint or long or ulong:
                sql.Append(Convert.ToString(value, CultureInfo.InvariantCulture));
                break;
            default:
                sql.Append('\'');

                foreach (var c in Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty) {
                    sql.Append(c switch
                    {
                        '\'' => "\\'",
                        '\\' => "\\\\",
                        '\0' => "\\0",
                        '\n' => "\\n",
                        '\r' => "\\r",
                        '\x1a' => "\\Z",
                        _ => c.ToString(),
                    });
                }

                sql.Append('\'');
                break;
        }
    }
}
