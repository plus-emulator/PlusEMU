using Plus.HabboHotel.Badges;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator;

internal class TakeBadgeCommand : ITargetChatCommand
{
    private readonly IBadgeManager _badgeManager;
    public string Key => "takebadge";

    public string Parameters => "%username% %badge%";

    public string Description => "Remove a badge from another user.";

    public bool MustBeInSameRoom => false;

    public TakeBadgeCommand(IBadgeManager badgeManager)
    {
        _badgeManager = badgeManager;
    }

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters) =>
        throw new NotImplementedException();
}
