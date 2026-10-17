using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Rooms.Chat.Commands.Moderator.Fun;

internal class ForceSitCommand : ITargetChatCommand
{
    public string Key => "forcesit";

    public string Parameters => "%username%";

    public string Description => "Force another to user sit.";

    public bool MustBeInSameRoom => true;

    public Task Execute(GameClient session, Room room, Habbo target, string[] parameters)
    {
        if (!session.GetHabbo().Access.Outranks(target.Access)) {
            return Task.CompletedTask;
        }

        var user = room.GetRoomUserManager().GetRoomUserByHabbo(target.Id);

        if (user == null) {
            return Task.CompletedTask;
        }

        if (user.Statusses.ContainsKey("lie") || user.IsLying || user.RidingHorse || user.IsWalking) {
            return Task.CompletedTask;
        }

        if (!user.Statusses.ContainsKey("sit")) {
            if (user.RotBody % 2 == 0) {
                if (user == null) {
                    return Task.CompletedTask;
                }

                try {
                    user.Statusses.Add("sit", "0.55 1");
                    user.IsSitting = true;
                    user.UpdateNeeded = true;
                }
                catch { }
            }
            else {
                user.RotBody--;
                user.Statusses.Add("sit", "0.55 1");
                user.IsSitting = true;
                user.UpdateNeeded = true;
            }
        }
        else if (user.IsSitting) {
            user.Statusses.Remove("sit");
            user.IsSitting = false;
            user.UpdateNeeded = true;
        }

        return Task.CompletedTask;
    }
}
