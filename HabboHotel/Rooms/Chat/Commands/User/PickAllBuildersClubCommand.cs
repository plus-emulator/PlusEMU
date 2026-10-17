using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Chat.Commands.User;

/// <summary>
/// :pickallbc returns every Builders Club furni in the room to the warehouse. AIR WIN63-202610151745-318700621 asks for it with
/// room.confirm.pick_all_bc and then says ":pickallbc" (com/sulake/habbo/ui/handler/ChatInputWidgetHandler.as:325-327,
/// com/sulake/habbo/session/SessionDataManager.as pickAllBuilderFurniture). A Builders Club furni is deleted on pickup, which frees
/// its place in the placer's furni limit.
/// </summary>
internal sealed class PickAllBuildersClubCommand(IBuildersClubService buildersClub) : IChatCommand
{
    public string Key => "pickallbc";

    public string Parameters => "";

    public string Description => "Picks up all of the Builders Club furniture from your room.";

    public void Execute(GameClient session, Room room, string[] parameters)
    {
        if (!room.CheckRights(session, true)) {
            return;
        }

        foreach (var item in room.GetRoomItemHandler().GetWallAndFloor.Where(item => item.IsBuildersClub).ToArray()) {
            buildersClub.TryPickUp(session, room, item);
        }
    }
}
