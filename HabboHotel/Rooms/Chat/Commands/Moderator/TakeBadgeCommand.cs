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

    public async Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (target.Id != session.GetHabbo().Id && !session.GetHabbo().Access.Outranks(target.Access)) {
            return;
        }

        var badgeCode = parameters.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(badgeCode)) {
            session.SendWhisper("Please enter the code of the badge you'd like to remove!");

            return;
        }

        if (target.Inventory is not { } inventory) {
            session.SendWhisper($"Oops, that user's badges are not loaded ({badgeCode}) !");

            return;
        }

        if (!inventory.Badges.HasBadge(badgeCode)) {
            session.SendWhisper($"Oops, that user does not have this badge ({badgeCode}) !");

            return;
        }

        await _badgeManager.RemoveBadge(target, badgeCode);

        if (target.Id != session.GetHabbo().Id) {
            session.SendWhisper($"You have removed the badge {badgeCode} from {target.Username}!");
            target.Client?.SendNotification("A badge has been removed from you!");
        }
        else {
            session.SendWhisper($"You have removed your badge {badgeCode}!");
        }
    }
}
