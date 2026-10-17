using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

internal class SitCommand : IChatCommand
{
    public string Key => "sit";

    public string Parameters => "";

    public string Description => "Allows you to sit down in your current spot.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        var user = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (user == null) {
            return;
        }

        if (user.Statusses.ContainsKey("lie") || user.IsLying || user.RidingHorse || user.IsWalking || user.IsSwimming) {
            return;
        }

        if (!user.Statusses.ContainsKey("sit")) {
            if (user.RotBody % 2 == 0) {
                if (user == null) {
                    return;
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
    }
}
