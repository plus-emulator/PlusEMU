using Plus.HabboHotel.Rooms.AI;

namespace Plus.HabboHotel.Rooms;

public sealed record AvatarChangeSnapshot(int VirtualId, string Look, string Gender, string Motto, int AchievementPoints)
{
    public static AvatarChangeSnapshot Capture(RoomUser user, bool self)
    {
        var habbo = user.GetClient()?.GetHabbo() ?? throw new InvalidOperationException("Room user has no active account.");

        return new(self ? -1 : user.VirtualId, user.LookFor(habbo), habbo.Gender, habbo.Motto, habbo.HabboStats?.AchievementPoints ?? 0);
    }

    // A bot's UserChange carries its gender in lower case ("m"), as in Users.
    public static AvatarChangeSnapshot Capture(RoomBot bot) => new(bot.VirtualId, bot.Look, bot.Gender.ToLowerInvariant(), bot.Motto, 0);
}
