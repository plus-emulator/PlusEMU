using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

internal class SetSpeedCommand : IChatCommand
{
    public string Key => "setspeed";

    public string Parameters => "%value%";

    public string Description => "Set the speed of the rollers in the current room.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!room.CheckRights(session, true)) {
            return;
        }

        if (!parameters.Any()) {
            session.SendWhisper("Please enter a value for the roller speed.");

            return;
        }

        // Speeds 0 to 3 match the roller speed controller, one roller step every 500 to 2000 ms.
        if (int.TryParse(parameters[0], out var speed) && speed is >= 0 and <= 3) {
            session.GetHabbo().CurrentRoom.GetRoomItemHandler().SetSpeed(speed);
        }
        else {
            session.SendWhisper("Invalid amount, please enter a valid number.");
        }
    }
}
