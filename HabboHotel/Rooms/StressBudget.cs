namespace Plus.HabboHotel.Rooms;

/// <summary>Caps the stress entities (players, horses and bots) that all rooms hold together.</summary>
internal static class StressBudget
{
    public const int GlobalMax = 4000;

    private static readonly object Sync = new();
    private static readonly Dictionary<object, int> Held = new(ReferenceEqualityComparer.Instance);

    public static bool TryHold(object owner, int entities)
    {
        lock (Sync) {
            var others = Held.Where(entry => !ReferenceEquals(entry.Key, owner)).Sum(entry => entry.Value);

            if (others + entities > GlobalMax) {
                return false;
            }

            Set(owner, entities);

            return true;
        }
    }

    internal static void Reset()
    {
        lock (Sync) {
            Held.Clear();
        }
    }

    public static void Hold(object owner, int entities)
    {
        lock (Sync) {
            Set(owner, entities);
        }
    }

    private static void Set(object owner, int entities)
    {
        if (entities <= 0) {
            Held.Remove(owner);
        }
        else {
            Held[owner] = entities;
        }
    }
}
