using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Subscriptions;

namespace Plus.HabboHotel.Rooms.Chat.Styles;

public sealed class ChatStyle
{
    public ChatStyle(int id, string name, string requiredPermission, bool requiresHc = false, bool enabled = true, bool requiresOwnership = false)
    {
        Id = id;
        Name = name;
        RequiredPermission = requiredPermission;
        RequiresHc = requiresHc;
        Enabled = enabled;
        RequiresOwnership = requiresOwnership;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string RequiredPermission { get; set; }
    public bool RequiresHc { get; set; }
    public bool Enabled { get; set; }
    public bool RequiresOwnership { get; set; }

    public bool CanUse(UserAccess access) => !RequiresOwnership && Enabled && (!RequiresHc || ClubAccess.LevelFor(access) > 0) &&
        (RequiredPermission.Length == 0 || access.Can(RequiredPermission));
    public bool CanUse(Plus.HabboHotel.Users.Habbo habbo) => RequiresOwnership
        ? Enabled && habbo.PurchasedChatStyles.ContainsKey(Id) && (!RequiresHc || ClubAccess.LevelFor(habbo.Access) > 0)
            && (RequiredPermission.Length == 0 || habbo.Access.Can(RequiredPermission))
        : CanUse(habbo.Access);
}
