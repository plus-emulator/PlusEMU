namespace Plus.HabboHotel.Items;

/// <summary>
/// The id band the client reserves for Wired temporary furniture. Observed on AIR WIN63-202610131318-860371703
/// com/sulake/habbo/utils/class_1846.as:26-29 (isTempId); Builders Club ids start right after it (:21-24).
/// </summary>
public static class TemporaryItemIds
{
    public const uint First = 2147401728;
    public const uint Last = 2147418111;

    public static bool Contains(uint id) => id is >= First and <= Last;
}
