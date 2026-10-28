namespace Plus.HabboHotel.Users;

public sealed record ChatDisplayPreferences(int ChatSize, int ChatMode, int BubbleWidth, int ScrollSpeed, int OnlineIndicator)
{
    public static readonly ChatDisplayPreferences Default = new(0, 0, 1, 1, 0);
}
