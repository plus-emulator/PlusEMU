namespace Plus.HabboHotel.Items.Wired;

/// <summary>The monitor's statistics rows: wired usage, heavy state, furni counts and permanent variable counts, each with its cap.</summary>
public sealed record WiredRoomStats(double ExecutionCost, double ExecutionCostCap, bool IsHeavy, int FloorItems, int FloorItemCap,
    int WallItems, int WallItemCap, int PermanentFurniVariables, int MaxPermanentFurniVariables, int PermanentUserVariables,
    int MaxPermanentUserVariables, int PermanentGlobalVariables, int MaxPermanentGlobalVariables)
{
    // Floor and wall furni are both capped at 4000.
    public const int FloorItemLimit = 4000;
    public const int WallItemLimit = 4000;

    // The execution cost cap of a room varies; until the rule is known every room uses this default or wired.execution_cost_cap.
    public const double DefaultExecutionCostCap = 3125;

    public static WiredRoomStats Create(double cost, double cap, int floorItems, int wallItems, int furniVariables, int userVariables,
        int globalVariables) => new(cost, cap, IsHeavyAt(cost, cap), floorItems, FloorItemLimit, wallItems, WallItemLimit,
        furniVariables, MaxFurniVariables(cap), userVariables, MaxUserVariables(cap), globalVariables, MaxGlobalVariables(cap));

    // The permanent variable caps follow the cost cap exactly (3125 -> 15 / 62 / 46, 4687.5 -> 23 / 93 / 70, 2500 -> 12 / 50 / 37, 6563 -> 32 furni).
    public static int MaxFurniVariables(double cap) => (int)Math.Floor(cap / 200);
    public static int MaxUserVariables(double cap) => (int)Math.Floor(cap / 50);
    public static int MaxGlobalVariables(double cap) => (int)Math.Floor(cap * 3 / 200);

    // No room was seen heavy; reaching the cap is the assumed threshold.
    public static bool IsHeavyAt(double cost, double cap) => cost >= cap;
}
