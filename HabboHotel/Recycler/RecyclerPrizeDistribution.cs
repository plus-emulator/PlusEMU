namespace Plus.HabboHotel.Recycler;

internal static class RecyclerPrizeDistribution
{
    public static (int Total, (RecyclerLevel Level, int Weight)[] Levels)? Build(RecyclerConfiguration configuration)
    {
        if (configuration.Levels.Length == 0 || configuration.Levels.Count(level => level.Id == 1 && level.Chance == 1) != 1
            || configuration.Levels.Any(level => level.Chance <= 0)) {
            return null;
        }

        long total = 1;

        foreach (var level in configuration.Levels.Where(level => level.Id != 1)) {
            long a = total, b = level.Chance;

            while (b != 0) {
                (a, b) = (b, a % b);
            }

            total = total / a * level.Chance;

            if (total > int.MaxValue) {
                return null;
            }
        }

        var weights = configuration.Levels.OrderByDescending(level => level.Id)
            .Select(level => (Level: level, Weight: level.Id == 1 ? 0 : (int)(total / level.Chance))).ToArray();
        var assigned = weights.Sum(level => (long)level.Weight);

        if (assigned >= total) {
            return null;
        }

        var common = Array.FindIndex(weights, level => level.Level.Id == 1);
        weights[common].Weight = (int)(total - assigned);

        return ((int)total, weights);
    }
}
