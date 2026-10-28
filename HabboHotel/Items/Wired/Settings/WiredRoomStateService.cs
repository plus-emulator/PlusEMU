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
/// Everyone is sent out at once and forwarded back two seconds later.
/// </summary>
public sealed class WiredRoomStateService(IRoomManager roomManager, TimeProvider clock) : IWiredRoomStateService
{
    private const short ReloadCloseReason = 2;
    private static readonly TimeSpan ForwardDelay = TimeSpan.FromSeconds(2);

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
            client => Forward(client, roomId));
    }

    private void Forward(GameClient client, uint roomId)
    {
        ITimer? timer = null;
        timer = clock.CreateTimer(_ =>
        {
            timer?.Dispose();
            client.Send(new RoomForwardComposer(roomId));
        }, null, ForwardDelay, Timeout.InfiniteTimeSpan);
    }
}
