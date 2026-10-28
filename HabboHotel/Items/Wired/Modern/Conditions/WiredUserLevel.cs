using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Conditions;

/// <summary>The level User Level compares and the @level user variable reads: the account level shown on the profile.</summary>
public static class WiredUserLevel
{
    public static int? Of(RoomUser user) => user.GetClient()?.GetHabbo()?.AccountLevel;
}
