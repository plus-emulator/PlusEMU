namespace Plus.HabboHotel.Items;

/// <summary>
/// The id band the client reserves for Wired temporary furniture. Observed on AIR WIN63-202610131318-860371703
/// com/sulake/habbo/utils/class_1846.as:26-29 (isTempId); Builders Club ids start right after it (:21-24).
/// </summary>
public static class TemporaryItemIds
{
    public const uint First = 2147401728;
    public const uint Last = 2147418111;

    private const uint WallTag = 0x80000000;

    // Floor and wall copies share wire numbers but must not share runtime or variable identities.
    public static uint FromWire(uint id, bool wall) => wall && Contains(id) ? id | WallTag : id;
    public static uint ToWire(uint id) => IsWallIdentity(id) ? id & ~WallTag : id;
    public static bool IsWallWireId(int id) => id < 0 && id != int.MinValue && Contains((uint)-id);
    public static int FromWiredEntityId(int id) => IsWallWireId(id)
        ? unchecked((int)FromWire((uint)-id, true)) : BuildersClubItemIds.FromWire(id);
    public static int ToWiredEntityId(int id) => IsWallIdentity(unchecked((uint)id))
        ? -checked((int)ToWire(unchecked((uint)id))) : BuildersClubItemIds.ToWire(id);
    public static bool IsWallIdentity(uint id) => (id & WallTag) != 0 && Contains(id & ~WallTag);

    public static bool Contains(uint id) => id is >= First and <= Last;
}
