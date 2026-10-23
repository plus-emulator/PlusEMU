using Dapper;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items;

public readonly record struct FurnitureUseRequest(uint ItemId, int Parameter);
public readonly record struct FurnitureClickRequest(uint ItemId, bool IsWall);

public interface IFurnitureUseStore
{
    void SetTonerEnabled(uint itemId, uint roomId, bool enabled);
}

public sealed class FurnitureUseStore(IDatabase database) : IFurnitureUseStore
{
    public void SetTonerEnabled(uint itemId, uint roomId, bool enabled)
    {
        using var connection = database.Connection();

        if (connection.Execute("""
            UPDATE room_items_toner toner JOIN items item ON item.id=toner.id
            SET toner.enabled=@enabled WHERE toner.id=@itemId AND item.room_id=@roomId
            """, new { itemId, roomId, enabled }) != 1) {
            throw new InvalidOperationException("Toner state was not persisted.");
        }
    }
}

public interface IFurnitureUseService
{
    void Use(Room room, GameClient session, FurnitureUseRequest request);
    void Click(Room room, GameClient session, FurnitureClickRequest request);
    void TurnOffDice(Room room, GameClient session, uint itemId);
    void RollDice(Room room, GameClient session, FurnitureUseRequest request);
    void UseOneWayGate(Room room, GameClient session, uint itemId);
    void UseWall(Room room, GameClient session, FurnitureUseRequest request);
    void UseHabboWheel(Room room, GameClient session, uint itemId);
}

public sealed class FurnitureUseService(IFurnitureUseStore store, IQuestManager quests) : IFurnitureUseService
{
    public void TurnOffDice(Room room, GameClient session, uint itemId)
    {
        var item = FindPermanent(room, session, itemId);

        if (item == null) {
            return;
        }

        Trigger(room, session, item, -1, room.CheckRights(session));
    }

    public void RollDice(Room room, GameClient session, FurnitureUseRequest request)
    {
        var item = FindPermanent(room, session, request.ItemId);

        if (item == null) {
            return;
        }

        Trigger(room, session, item, request.Parameter, room.CheckRights(session, false, true));
    }

    public void UseOneWayGate(Room room, GameClient session, uint itemId)
    {
        var item = FindPermanent(room, session, itemId);

        if (item?.Definition.InteractionType != InteractionType.OneWayGate) {
            return;
        }

        Trigger(room, session, item, -1, room.CheckRights(session));
    }

    public void UseWall(Room room, GameClient session, FurnitureUseRequest request)
    {
        if (!ReferenceEquals(session.GetHabbo().CurrentRoom, room)) {
            return;
        }

        var item = room.GetRoomItemHandler().GetItem(request.ItemId);

        if (item == null || !item.IsWallItem) {
            return;
        }

        if (item.IsTemporary) {
            Use(room, session, request);

            return;
        }

        // The writes are reported before the use runs its stacks, which may write the state again.
        Trigger(room, session, item, request.Parameter, room.CheckRights(session, false, true));
        room.GetWired().TriggerEvent(WiredBoxType.TriggerStateChanges, session.GetHabbo(), item);
        quests.ProgressUserQuest(session, QuestType.ExploreFindItem, (int)item.Definition.Id);
    }

    // AIR WIN63-202610151745-318700621 class_1854.as:2598 sends ROFCAE_USE_HABBOWHEEL as its own message with only the id.
    public void UseHabboWheel(Room room, GameClient session, uint itemId)
    {
        if (FindPermanent(room, session, itemId)?.Definition.InteractionType != InteractionType.HabboWheel) {
            return;
        }

        UseWall(room, session, new(itemId, 0));
    }

    // The state writes the interactor makes for this request are the user's, reported once each, in order,
    // also when the interactor then fails: its exception still propagates.
    private static void Trigger(Room room, GameClient session, Item item, int request, bool hasRights)
    {
        var actor = FurnitureStateEvents.Actor(room, session);
        var capture = FurnitureStateEvents.Capture(room);

        try {
            item.Interactor.OnTrigger(session, item, request, hasRights);
        }
        finally {
            capture.Dispose();
            FurnitureStateEvents.Publish(room, actor, capture.Transitions);
        }
    }

    private static Item? FindPermanent(Room room, GameClient session, uint itemId)
    {
        if (!ReferenceEquals(session.GetHabbo().CurrentRoom, room)) {
            return null;
        }

        var item = room.GetRoomItemHandler().GetItem(itemId);

        return item is { IsTemporary: false } ? item : null;
    }

    public void Click(Room room, GameClient session, FurnitureClickRequest request)
    {
        var actor = room.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (actor == null || actor.IsBot) {
            return;
        }

        var item = room.GetRoomItemHandler().GetItem(request.ItemId);

        if (item == null || request.IsWall && !item.IsWallItem || !request.IsWall && !item.IsFloorItem) {
            return;
        }

        if (item.IsTemporary && !room.GetRoomItemHandler().OwnsTemporary(item)) {
            return;
        }

        room.GetWired().Dispatch(new(WiredEventKind.ClickFurni) { Actor = actor, EventItem = item });
    }

    public void Use(Room room, GameClient session, FurnitureUseRequest request)
    {
        var habbo = session.GetHabbo();

        if (habbo.CurrentRoom != room) {
            return;
        }

        var item = room.GetRoomItemHandler().GetItem(request.ItemId);

        if (item == null || item.RoomId != room.Id || item.Definition == null) {
            return;
        }

        // Observed on AIR WIN63-202610131318-860371703: a user with rights uses a Place Furni copy (its state goes 0 to 1);
        // without rights it does nothing. Only a plain state switch is used, since a copy never reaches storage.
        if (item.IsTemporary) {
            if (room.GetRoomItemHandler().OwnsTemporary(item) && room.CheckRights(session, false, true)
                && item.Interactor is Plus.HabboHotel.Items.Interactor.InteractorGenericSwitch) {
                Trigger(room, session, item, request.Parameter, true);
                room.GetWired().TriggerEvent(WiredBoxType.TriggerStateChanges, habbo, item);
            }

            return;
        }

        if (item.Definition.InteractionType == InteractionType.Banzaitele || RoomItemSnapshot.IsNeverUsed(item.Definition)) {
            return;
        }

        if (item.Definition.InteractionType == InteractionType.Toner) {
            lock (room.NavigationSync) {
                if (habbo.CurrentRoom != room || !room.CheckRights(session, true) ||
                    room.GetRoomItemHandler().GetItem(item.Id) != item || room.TonerData?.ItemId != item.Id) {
                    return;
                }

                var enabled = room.TonerData.Enabled == 0;
                store.SetTonerEnabled(item.Id, room.Id, enabled);
                room.TonerData.Enabled = enabled ? 1 : 0;
                Plus.HabboHotel.Items.Data.Toner.TonerState.Synchronize(room, item);
                var furnitureSnapshot = RoomItemSnapshot.Capture(item);
                room.SendPacket(new ObjectUpdateComposer(furnitureSnapshot));
                room.SendPacket(new FurnitureMetadataComposer([furnitureSnapshot]));
                item.UpdateState();
            }

            return;
        }

        var hasRights = room.CheckRights(session, false, true);

        if (item.Definition.InteractionType == InteractionType.GnomeBox && item.OwnerId == habbo.Id) {
            session.Send(new GnomeBoxComposer(item.Id));
        }

        // Using a Wired chest only asks the client to open it (WiredChestOpen); it is no state change.
        var toggle = !Plus.HabboHotel.Items.Wired.Chests.WiredChestFurniture.IsChest(item.Definition);

        if (item.Definition.InteractionType is InteractionType.WfFloorSwitch1 or InteractionType.WfFloorSwitch2) {
            var user = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);

            if (user == null) {
                return;
            }

            toggle = Gamemap.TilesTouching(item.GetX, item.GetY, user.X, user.Y);
        }

        Trigger(room, session, item, request.Parameter, hasRights);

        if (toggle) {
            room.GetWired().TriggerEvent(WiredBoxType.TriggerStateChanges, habbo, item);
        }

        quests.ProgressUserQuest(session, QuestType.ExploreFindItem, (int)item.Definition.Id);
    }
}
