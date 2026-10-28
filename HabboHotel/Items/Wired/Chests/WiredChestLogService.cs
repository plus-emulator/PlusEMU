using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Chests;

public interface IWiredChestLogService
{
    void ShowRoom(WiredChestRoom module, Room room, GameClient session, int amount, int page);
    void ShowChest(WiredChestRoom module, Room room, GameClient session, uint chestId, int amount, int page);
}

/// <summary>Answers transaction log pages to users who may read Wired in the room; everyone else gets the read-rights refusal.</summary>
public sealed class WiredChestLogService(IWiredRequestGateService gates) : IWiredChestLogService
{
    private const string NoReadRights = "wiredfurni.error.no_read_rights";

    public void ShowRoom(WiredChestRoom module, Room room, GameClient session, int amount, int page) =>
        Show(module, room, session, WiredChestLogQuery.Room(room.Id, amount, page));

    public void ShowChest(WiredChestRoom module, Room room, GameClient session, uint chestId, int amount, int page)
    {
        var chest = room.GetRoomItemHandler().GetItem(chestId);

        if (chest is not { IsTemporary: false } || !WiredChestFurniture.IsChest(chest.Definition)) {
            session.Send(new WiredValidationErrorComposer(NoReadRights));

            return;
        }

        Show(module, room, session, WiredChestLogQuery.Chest(room.Id, chestId, amount, page));
    }

    private void Show(WiredChestRoom module, Room room, GameClient session, WiredChestLogQuery query)
    {
        if (!room.GetWired().Settings.CanInspect(session)) {
            session.Send(new WiredValidationErrorComposer(NoReadRights));

            return;
        }

        if (gates.TryPass(session, WiredRequestKind.ChestLog)) {
            session.Send(new WiredTransactionLogListComposer(module.Store.ReadLog(query)));
        }
    }
}
