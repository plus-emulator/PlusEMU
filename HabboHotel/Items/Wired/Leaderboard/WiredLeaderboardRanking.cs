using System.Globalization;
using Dapper;
using Plus.Database;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Items.Wired.Leaderboard;

/// <summary>A player's name and figure for leaderboard rows, including players who are offline.</summary>
public interface IWiredLeaderboardProfiles
{
    IReadOnlyDictionary<long, (string Username, string Figure)> Find(IReadOnlyCollection<long> userIds);
}

public sealed class DatabaseWiredLeaderboardProfiles(IDatabase database) : IWiredLeaderboardProfiles
{
    public IReadOnlyDictionary<long, (string Username, string Figure)> Find(IReadOnlyCollection<long> userIds)
    {
        if (userIds.Count == 0) {
            return new Dictionary<long, (string, string)>();
        }

        using var connection = database.Connection();

        return connection.Query<(int Id, string Username, string Look)>("SELECT id, username, look FROM users WHERE id IN @Ids", new { Ids = userIds.ToArray() })
            .ToDictionary(row => (long)row.Id, row => (row.Username, row.Look ?? ""));
    }
}

/// <summary>
/// Ranks a user variable's holders the way the overlay presents them. Sort modes are VariableLeaderboardAddon's (0 highest value,
/// 1 lowest, 2 oldest creation, 3 latest creation, 4 oldest update, 5 latest update); equal keys keep the lower user id first.
/// The filter keeps holders that also hold the filter variable; with a comparison the filter variable's value must equal the
/// literal or global operand (the editor names no operator, so equality is inferred).
/// </summary>
public static class WiredLeaderboardRanking
{
    public static IReadOnlyList<(long UserId, WiredVariableValue Value)> Rank(WiredLeaderboardConfig config,
        IReadOnlyDictionary<WiredVariableKey, WiredVariableValue> holders,
        IReadOnlyDictionary<WiredVariableKey, WiredVariableValue>? filter, long? operand)
    {
        var filterValues = filter?.Where(pair => pair.Key.Target == WiredVariableTarget.User)
            .ToDictionary(pair => pair.Key.HolderId, pair => pair.Value.Value);
        var rows = holders.Where(pair => pair.Key.Target == WiredVariableTarget.User && pair.Key.HolderId > 0)
            .Where(pair => !config.Filter || filterValues != null && filterValues.TryGetValue(pair.Key.HolderId, out var value)
                && (!config.Comparison || operand == value))
            .Select(pair => (UserId: pair.Key.HolderId, pair.Value));
        static long Time(DateTimeOffset? time) => time?.ToUnixTimeMilliseconds() ?? 0;

        var sorted = config.Sort switch
        {
            1 => rows.OrderBy(row => row.Value.Value),
            2 => rows.OrderBy(row => Time(row.Value.CreatedAt)),
            3 => rows.OrderByDescending(row => Time(row.Value.CreatedAt)),
            4 => rows.OrderBy(row => Time(row.Value.UpdatedAt)),
            5 => rows.OrderByDescending(row => Time(row.Value.UpdatedAt)),
            _ => rows.OrderByDescending(row => row.Value.Value)
        };

        return [.. sorted.ThenBy(row => row.UserId)];
    }

    /// <summary>Chunk <paramref name="index"/> of the ranking: 50 rows, ranks counted from the top of the whole list.</summary>
    // Observed on AIR WIN63-202610151745-318700621: a row carries its creation and last-update times only when the board shows a
    // column of that kind; otherwise both are 0.
    public static IReadOnlyList<WiredLeaderboardRow> Chunk(IReadOnlyList<(long UserId, WiredVariableValue Value)> ranked, int index,
        WiredLeaderboardChoice choice, IWiredLeaderboardProfiles profiles, WiredLeaderboardConfig? config = null)
    {
        var showsUpdate = config == null || config.Columns.Any(column => column.Type == 2);
        var showsCreation = config == null || config.Columns.Any(column => column.Type == 3);
        var slice = ranked.Skip(index * WiredLeaderboardChunk.Size).Take(WiredLeaderboardChunk.Size).ToArray();
        var names = profiles.Find([.. slice.Select(row => row.UserId)]);

        return [.. slice.Select((row, offset) =>
        {
            var value = row.Value.Value;
            var (username, figure) = names.GetValueOrDefault(row.UserId, ("", ""));

            return new WiredLeaderboardRow(index * WiredLeaderboardChunk.Size + offset + 1, (int)row.UserId, username, figure,
                choice.Description.HasValue ? value.ToString(CultureInfo.InvariantCulture) : "",
                showsCreation ? row.Value.CreatedAt?.ToUnixTimeMilliseconds() ?? 0 : 0, showsUpdate ? row.Value.UpdatedAt?.ToUnixTimeMilliseconds() ?? 0 : 0,
                choice.LevelSystem?.Level(value).Level.ToString(CultureInfo.InvariantCulture),
                ConnectorText(choice.TextConnector, value));
        })];
    }

    // Observed on AIR WIN63-202610201032-107964540: a value with a label shows it, any other value shows the "_=" fallback (empty when the
    // fallback is empty), and a connector without a fallback line gives the row no text.
    private static string? ConnectorText(IReadOnlyDictionary<int, string> connector, long value) =>
        value is >= int.MinValue and <= int.MaxValue && connector.TryGetValue((int)value, out var text) ? text
        : (connector as WiredTextConnector)?.Fallback;
}
