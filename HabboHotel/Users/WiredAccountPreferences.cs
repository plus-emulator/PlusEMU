namespace Plus.HabboHotel.Users;

public sealed record WiredAccountPreferences(
    bool MenuButton,
    bool InspectButton,
    bool PlayTestMode,
    bool WhisperDisabled,
    bool ShowAllNotifications,
    string UiStyle)
{
    public const int MaxUiStyleLength = 30000;

    public static readonly WiredAccountPreferences Default = new(false, false, false, false, false, "");
}
