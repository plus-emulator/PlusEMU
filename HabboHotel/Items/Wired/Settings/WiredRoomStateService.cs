using Plus.Communication.Packets.Outgoing.Rooms.Session;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Settings;

public interface IWiredRoomStateService
{
    void Update(Room room, GameClient session, bool rollback);
}

/// <summary>
/// The Room state buttons of the Wired menu. Both send everyone in the room out and back in, so the room loads again from storage.
/// A rollback first puts every furni back to its place and state from the last room load.
/// </summary>
public sealed class WiredRoomStateService(IRoomManager roomManager) : IWiredRoomStateService
{
    private const short ReloadCloseReason = 2;

    public void Update(Room room, GameClient session, bool rollback)
    {
        var settings = room.GetWired().Settings;

        if (rollback ? !settings.CanManage(session) : !settings.CanModify(session)) {
            return;
        }

        var userManager = room.GetRoomUserManager();
        var clients = userManager.GetRoomUsers().Select(user => user?.GetClient()).OfType<GameClient>().ToList();

        if (rollback) {
            room.GetRoomItemHandler().RestoreLoadedStateOnDispose();
        }

        var roomId = room.Id;
        FloorPlanUpdateService.ReturnConnectedClients(
            clients,
            (client, notifyUser) => userManager.RemoveUserFromRoom(client, notifyUser, closeReason: ReloadCloseReason),
            () => { },
            () => roomManager.UnloadRoom(roomId),
            client => client.Send(new RoomForwardComposer(roomId)));
    }
}
