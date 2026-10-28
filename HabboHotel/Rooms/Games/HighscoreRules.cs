using Plus.HabboHotel.Items.DataFormat;

namespace Plus.HabboHotel.Rooms.Games;

internal sealed record HighscoreTeam(IReadOnlyList<string> Users, int Points);

/// <summary>How a finished round changes a high score board, by its score type.</summary>
internal static class HighscoreRules
{
    private const uint PerTeam = 0;
    private const uint MostWins = 1;
    private const uint Classic = 2;
    private const uint FastestTime = 3;
    private const uint LongestTime = 4;

    internal static void Apply(HighscoreDataFormat board, IReadOnlyList<HighscoreTeam> teams, int seconds)
    {
        var entries = board.Entries.ToList();
        // A team wins with the most points, and only with points.
        var best = teams.Count == 0 ? 0 : teams.Max(team => team.Points);

        foreach (var team in teams) {
            switch (board.ScoreType) {
                case Classic when team.Points > 0:
                    entries.Add(new(team.Points, [.. team.Users]));
                    break;
                case PerTeam when team.Points > 0:
                    Merge(entries, team.Users, team.Points, Math.Max);
                    break;
                case MostWins when team.Points > 0 && team.Points == best:
                    Merge(entries, team.Users, 1, (old, _) => old == int.MaxValue ? old : old + 1);
                    break;
                case FastestTime:
                    Merge(entries, team.Users, seconds, Math.Min);
                    break;
                case LongestTime:
                    Merge(entries, team.Users, seconds, Math.Max);
                    break;
            }
        }

        board.Entries = [.. board.ScoreType == FastestTime ? entries.OrderBy(entry => entry.Score) : entries.OrderByDescending(entry => entry.Score)];
    }

    // The same players keep one entry, whatever their team colour.
    private static void Merge(List<HighscoreEntry> entries, IReadOnlyList<string> users, int score, Func<int, int, int> combine)
    {
        var index = entries.FindIndex(entry => entry.Users.Length == users.Count && !entry.Users.Except(users, StringComparer.Ordinal).Any());

        if (index < 0) {
            entries.Add(new(score, [.. users]));
        }
        else {
            entries[index] = entries[index] with { Score = combine(entries[index].Score, score) };
        }
    }
}
