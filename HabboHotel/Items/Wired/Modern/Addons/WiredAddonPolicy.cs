using Plus.HabboHotel.Items.Wired.Modern.Selectors;

namespace Plus.HabboHotel.Items.Wired.Modern.Addons;

public enum WiredConditionEvaluation
{
    All, Any, SomeButNotAll, None, LessThan, Exactly, MoreThan
}
public sealed record WiredConditionPolicy(WiredConditionEvaluation Mode, int Source, int Count,
    IReadOnlySet<uint> ConditionIds);

public interface IWiredActionPicker
{
    IReadOnlyList<uint> Pick(IReadOnlyList<uint> actionIds);
    void Reset();
}

public sealed record WiredCarryPolicy(bool SameTile, IReadOnlySet<int> UserIds);
public sealed record WiredPhysicsPolicy(bool KeepAltitude, IReadOnlySet<uint> ThroughFurni,
    IReadOnlySet<int> ThroughUsers, IReadOnlySet<uint> BlockingFurni);
public sealed record WiredCurvePolicy(int Type, int Intensity, int Strength);
public enum WiredProjectileDistance
{
    Normal, Overshoot, Fixed
}
public sealed record WiredProjectilePolicy(IReadOnlySet<uint> ItemIds, int? DirectionSystem,
    int RotationOffset, int? CurveStrength, WiredProjectileDistance Distance, int DistanceTiles)
{
    /// <summary>Source 901 applies projectile movement to every furniture item moved by the stack.</summary>
    public const int StackMovedSource = 901;

    public bool AppliesToAllMovedFurniture { get; init; }
    public bool AppliesTo(uint itemId) => AppliesToAllMovedFurniture || ItemIds.Contains(itemId);

    public int VariableMask { get; init; }
    public int? TimePerTileMs { get; init; }
    public int SpeedIncrease { get; init; }
    public bool MeasureX { get; init; }
    public bool MeasureY { get; init; }
    public bool MeasureAltitude { get; init; }
    public int? ShooterSource { get; init; }
    public bool BunnyHop { get; init; }
    internal bool ShooterAimed { get; set; }

    internal int Duration(long dx, long dy, double dz, int stackDuration)
    {
        if (TimePerTileMs is not { } time) {
            return stackDuration;
        }

        // Enabled-axis Euclidean distance, with separately truncated time and acceleration.
        var x = MeasureX ? (double)dx : 0;
        var y = MeasureY ? (double)dy : 0;
        var z = MeasureAltitude ? dz : 0;
        var distance = Math.Sqrt(x * x + y * y + z * z);
        var total = Math.Floor(time * distance);

        if (distance >= 2 && SpeedIncrease > 0 && SpeedIncrease < time) {
            var acceleratedDistance = Math.Min(distance, (double)time / SpeedIncrease);
            total -= Math.Floor(SpeedIncrease * acceleratedDistance * (acceleratedDistance - 1) / 2);
        }

        return (int)Math.Clamp(total, 0, 100000);
    }
}

/// <summary>One firing's typed options. Stateful pickers belong to the placed addon, not this policy.</summary>
public sealed class WiredAddonPolicy
{
    public WiredConditionPolicy? Conditions { get; set; }
    public IWiredActionPicker? ActionPicker { get; set; }
    public bool ExecuteInOrder { get; set; }
    public bool DisableAnimation { get; set; }
    public int AnimationTimeMs { get; set; } = 500;
    public int? FurniLimit { get; set; }
    public int? UserLimit { get; set; }
    // When set, a furniture limit that truncates keeps a random subset of the eligible selection instead of its first entries. Users and unset policies keep the prefix.
    internal Random? FurniFilterRandom { get; set; }
    public WiredCarryPolicy? Carry { get; set; }
    public WiredPhysicsPolicy? Physics { get; set; }
    public WiredCurvePolicy? Curve { get; set; }
    public WiredProjectilePolicy? Projectile { get; set; }
    public IList<Func<WiredAddonInputs, string, string>> TextFormatters { get; } = new List<Func<WiredAddonInputs, string, string>>();

    public string FormatText(WiredAddonInputs input, string text)
    {
        foreach (var formatter in TextFormatters) {
            text = formatter(input, text);
        }

        return text;
    }

    public WiredSelectedIds FilterSelection(WiredSelectedIds selection,
        IEnumerable<uint>? furniOrder = null, IEnumerable<int>? userOrder = null)
    {
        var result = selection.Copy();
        Limit(result.FurniIds, FurniLimit, furniOrder, FurniFilterRandom);
        Limit(result.UserIds, UserLimit, userOrder);

        return result;
    }

    private static void Limit<T>(HashSet<T> values, int? limit, IEnumerable<T>? order, Random? random = null)
    {
        if (limit is not >= 0 || values.Count <= limit.Value) {
            return;
        }

        var candidates = random is null || limit.Value == 0
            ? (order ?? values).Where(values.Contains).Take(limit.Value).ToArray()
            : Sample((order ?? values).Where(values.Contains).Distinct().ToArray(), limit.Value, random);
        values.Clear();
        values.UnionWith(candidates);
    }

    // A partial Fisher-Yates shuffle: the first "count" entries are drawn without replacement from the eligible candidates; nothing is drawn unless they truncate.
    private static T[] Sample<T>(T[] eligible, int count, Random random)
    {
        if (eligible.Length <= count) {
            return eligible;
        }

        for (var index = 0; index < count; index++) {
            var other = random.Next(index, eligible.Length);
            (eligible[index], eligible[other]) = (eligible[other], eligible[index]);
        }

        return eligible[..count];
    }
}

/// <summary>Counts logical OR groups as one requirement; exception handling stays with the engine.</summary>
public static class WiredConditionPolicyEvaluator
{
    public static bool Matches(WiredConditionEvaluation mode, int matched, int total, int count)
    {
        if (total <= 0) {
            return true;
        }

        count = Math.Clamp(count, 0, 1000);

        return mode switch
        {
            WiredConditionEvaluation.All => matched >= total,
            WiredConditionEvaluation.Any => matched > 0,
            WiredConditionEvaluation.SomeButNotAll => matched < total,
            WiredConditionEvaluation.None => matched == 0,
            WiredConditionEvaluation.LessThan => matched < count,
            WiredConditionEvaluation.Exactly => matched == count,
            WiredConditionEvaluation.MoreThan => matched > count,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
