namespace Plus.HabboHotel.Items.Wired;

internal enum WiredEngineLimit
{
    ExecutionBudget, PendingStacks, Depth
}

internal sealed class WiredEngineLimits
{
    public int MaxDepth { get; init; } = 20;
    public int MaxExecutionsPerPass { get; init; } = 10000;
    public int MaxPendingStacks { get; init; } = 10000;
    // A hotel-wide override. Without it a room's cap follows its owner's account level.
    public double? ExecutionCostCap { get; init; }

    public static WiredEngineLimits FromSettings(Func<string, string> setting) => new()
    {
        MaxDepth = ReadSetting(setting, "wired.max_depth", 20),
        MaxExecutionsPerPass = ReadSetting(setting, "wired.max_executions_per_pass", 10000),
        MaxPendingStacks = ReadSetting(setting, "wired.max_pending_stacks", 10000),
        ExecutionCostCap = PositiveOrNull(setting("wired.execution_cost_cap"))
    };

    private static int Positive(string value, int fallback) =>
        int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;

    private static double? PositiveOrNull(string value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
        && parsed > 0 && double.IsFinite(parsed) ? parsed : null;

    // SettingsManager returns "0" for absent keys, so existing installations need no migration.
    private static int ReadSetting(Func<string, string> setting, string key, int fallback) =>
        Positive(setting(key), fallback);
}
