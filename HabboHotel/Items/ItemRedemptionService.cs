using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.AvatarEffect;
using Plus.Communication.Packets.Outgoing.Inventory.AvatarEffects;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Notifications;
using Plus.Core.Settings;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Clothing.Parts;
using Plus.HabboHotel.Users.Effects;

namespace Plus.HabboHotel.Items;

public interface IItemRedemptionStore
{
    void DeleteExchange(uint itemId, int ownerId, uint roomId);
    IReadOnlyList<ClothingParts> ConsumeClothing(uint itemId, int ownerId, uint roomId, string name, IReadOnlyCollection<int> partIds);
    AvatarEffect ConsumeEffectBox(uint itemId, int ownerId, uint roomId, int spriteId, DateTimeOffset now);
}

public sealed class ItemRedemptionStore(IDatabase database) : IItemRedemptionStore
{
    public void DeleteExchange(uint itemId, int ownerId, uint roomId)
    {
        using var connection = database.Connection();
        var deleted = connection.Execute(
            "DELETE FROM items WHERE id=@itemId AND user_id=@ownerId AND room_id=@roomId LIMIT 1",
            new { itemId, ownerId, roomId });

        if (deleted != 1) {
            throw new InvalidOperationException("Exchange furniture was not deleted.");
        }
    }

    public IReadOnlyList<ClothingParts> ConsumeClothing(uint itemId, int ownerId, uint roomId, string name, IReadOnlyCollection<int> partIds)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var deleted = connection.Execute(
            "DELETE FROM items WHERE id=@itemId AND user_id=@ownerId AND room_id=@roomId LIMIT 1",
            new { itemId, ownerId, roomId }, transaction);

        if (deleted != 1) {
            throw new InvalidOperationException("Clothing furniture was not deleted.");
        }

        if (connection.ExecuteScalar<int?>("SELECT id FROM users WHERE id=@ownerId FOR UPDATE", new { ownerId }, transaction) == null) {
            throw new InvalidOperationException("Clothing owner no longer exists.");
        }

        var result = new List<ClothingParts>();

        foreach (var partId in partIds.Distinct()) {
            var existing = connection.QuerySingleOrDefault<ClothingRow>(
                "SELECT id,part_id AS PartId,part AS Part FROM user_clothing WHERE user_id=@ownerId AND part_id=@partId LIMIT 1",
                new { ownerId, partId }, transaction);

            if (existing != null) {
                result.Add(new(existing.Id, existing.PartId, existing.Part));
                continue;
            }

            var id = connection.ExecuteScalar<int>(
                "INSERT INTO user_clothing(user_id,part_id,part) VALUES(@ownerId,@partId,@name); SELECT LAST_INSERT_ID()",
                new { ownerId, partId, name }, transaction);
            result.Add(new(id, partId, name));
        }

        transaction.Commit();

        return result;
    }

    // The client lists effects by type, so a user keeps one row per effect: an effect the user already has becomes permanent.
    public AvatarEffect ConsumeEffectBox(uint itemId, int ownerId, uint roomId, int spriteId, DateTimeOffset now)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var deleted = connection.Execute(
            "DELETE FROM items WHERE id=@itemId AND user_id=@ownerId AND room_id=@roomId LIMIT 1",
            new { itemId, ownerId, roomId }, transaction);

        if (deleted != 1) {
            throw new InvalidOperationException("Effect box furniture was not deleted.");
        }

        var stamp = now.UtcDateTime;
        var existing = connection.QuerySingleOrDefault<EffectRow>(
            "SELECT id,quantity FROM user_effects WHERE user_id=@ownerId AND effect_id=@spriteId ORDER BY id LIMIT 1 FOR UPDATE",
            new { ownerId, spriteId }, transaction);
        int id;
        var quantity = Math.Max(existing?.Quantity ?? 1, 1);

        if (existing != null) {
            id = existing.Id;
            connection.Execute(
                "UPDATE user_effects SET total_duration=0,is_activated=1,activated_stamp=@stamp,quantity=@quantity WHERE id=@id",
                new { stamp, quantity, id }, transaction);
        }
        else {
            id = connection.ExecuteScalar<int>(
                "INSERT INTO user_effects(user_id,effect_id,total_duration,is_activated,activated_stamp,quantity) VALUES(@ownerId,@spriteId,0,1,@stamp,1); SELECT LAST_INSERT_ID()",
                new { ownerId, spriteId, stamp }, transaction);
        }

        transaction.Commit();

        return new(id, ownerId, spriteId, 0, true, now, quantity, new AvatarEffectStore(database));
    }

    private sealed class ClothingRow
    {
        public int Id { get; set; }
        public int PartId { get; set; }
        public string Part { get; set; } = "";
    }

    private sealed class EffectRow
    {
        public int Id { get; set; }
        public int Quantity { get; set; }
    }
}

public interface IItemRedemptionService
{
    void RedeemCredits(Room room, GameClient session, uint itemId);
    void RedeemClothing(GameClient session, uint itemId);
    void OpenEffectBox(GameClient session, Item item, DateTimeOffset now);
}

public sealed class ItemRedemptionService(IItemRedemptionStore store, ISettingsManager settings) : IItemRedemptionService
{
    public void RedeemCredits(Room room, GameClient session, uint itemId)
    {
        var habbo = session.GetHabbo();

        if (habbo.CurrentRoom != room || !room.CheckRights(session, true)) {
            return;
        }

        if (settings.TryGetValue("room.item.exchangeables.enabled") != "1") {
            session.SendNotification("The hotel managers have temporarilly disabled exchanging!");

            return;
        }

        lock (habbo.WalletSync) {
            if (habbo.WalletClosed || habbo.CurrentRoom != room || !room.CheckRights(session, true)) {
                return;
            }

            var item = room.GetRoomItemHandler().GetItem(itemId);

            if (item == null || item.IsTemporary || item.RoomId != room.RoomId || item.OwnerId != habbo.Id || item.Definition?.InteractionType != InteractionType.Exchange) {
                return;
            }

            var value = item.Definition.BehaviourData;

            if (value <= 0) {
                return;
            }

            int balance;

            try {
                balance = checked(habbo.Credits + value);
            }
            catch (OverflowException) {
                return;
            }

            store.DeleteExchange(item.Id, habbo.Id, room.RoomId);
            habbo.Credits = balance;
            room.GetRoomItemHandler().RemoveFurniture(null, item.Id);
            // The exchange item was placed in the room; this only drops a stale inventory copy, which an unloaded inventory cannot hold.
            habbo.Inventory?.Furniture.RemoveItem(item.Id);
            session.Send(new CreditBalanceComposer(balance));
            session.Send(new FurniListUpdateComposer());
            session.Send(new FurniListRemoveComposer(item.Id));
        }
    }

    public void RedeemClothing(GameClient session, uint itemId)
    {
        var habbo = session.GetHabbo();
        var room = habbo.CurrentRoom;

        // The wardrobe is loaded at login; without it the redeemed parts would have nowhere to go.
        if (room == null || habbo.Clothing is not { } wardrobe) {
            return;
        }

        lock (habbo.WalletSync) {
            if (habbo.WalletClosed || habbo.CurrentRoom != room) {
                return;
            }

            var item = room.GetRoomItemHandler().GetItem(itemId);

            if (item == null || item.IsTemporary || item.RoomId != room.RoomId || item.OwnerId != habbo.Id) {
                return;
            }

            if (item.Definition?.InteractionType != InteractionType.PurchasableClothing) {
                session.SendNotification("Oops, this item isn't set as a sellable clothing item!");

                return;
            }

            if (item.Definition.FigureSetIds.Count == 0) {
                session.SendNotification("Oops, this item doesn't have a linking clothing configuration, please report it!");

                return;
            }

            var committed = store.ConsumeClothing(item.Id, habbo.Id, room.RoomId, item.Definition.ItemName, item.Definition.FigureSetIds);
            wardrobe.PublishCommitted(committed);
            room.GetRoomItemHandler().RemoveFurniture(session, item.Id);
            session.Send(new FigureSetIdsComposer(wardrobe.GetClothingParts.ToArray()));
            session.Send(new RoomNotificationComposer("figureset.redeemed.success"));
        }
    }

    // Only the owner gets the effect box dialog, and its OK button sends the plain use packet. The box works once: the owner wears its effect, which stays in the effects list for good,
    // and the box plays its opening state before it leaves the room. A box left in state 1 by the old toggle still opens.
    public void OpenEffectBox(GameClient session, Item item, DateTimeOffset now)
    {
        var habbo = session.GetHabbo();
        var room = habbo.CurrentRoom;

        // Effects are loaded at login; without them the effect would have nowhere to go.
        if (room == null || habbo.Effects is not { } effects || !TryGetBoxEffect(item.Definition, out var spriteId)) {
            return;
        }

        lock (habbo.WalletSync) {
            if (habbo.WalletClosed || habbo.CurrentRoom != room || item.IsTemporary || item.RoomId != room.RoomId || item.OwnerId != habbo.Id
                || item.InteractingUser != 0 || !ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), item)) {
                return;
            }

            var effect = store.ConsumeEffectBox(item.Id, habbo.Id, room.RoomId, spriteId, now);
            item.InteractingUser = habbo.Id;

            if (effects.GetAllEffects.FirstOrDefault(owned => owned.Id == effect.Id) is { } owned) {
                owned.Duration = effect.Duration;
                owned.Activated = true;
                owned.ActivatedAt = effect.ActivatedAt;
                owned.Quantity = effect.Quantity;
            }
            else {
                effects.TryAdd(effect);
                session.Send(new AvatarEffectAddedComposer(spriteId, AvatarEffectService.WireDuration(effect), true));
            }

            session.Send(new AvatarEffectActivatedComposer(new AvatarEffectActivation(spriteId, AvatarEffectService.WireDuration(effect), true)));
            room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id)?.ApplyEffect(spriteId);
            // The box row is already deleted, so the opening state only goes to the room, as one data update like a used switch.
            item.LegacyDataString = "1";
            item.UpdateState(false, false);
            room.SendObjectData(item, item.LegacyDataString, false);
            room.SendPacket(new FurnitureMetadataComposer([RoomItemSnapshot.Capture(item)]));
            // The next room cycle takes the box away (Item.ProcessUpdates), after the client started the opening animation.
            item.RequestUpdate(1, true);
        }
    }

    // Habbo names each effect box after its effect: fxbox_fx16 gives effect 16.
    private static bool TryGetBoxEffect(ItemDefinition? definition, out int spriteId)
    {
        const string prefix = "fxbox_fx";
        spriteId = 0;

        return definition?.ItemName is { } name && name.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(name.AsSpan(prefix.Length), System.Globalization.NumberStyles.None, null, out spriteId) && spriteId > 0;
    }
}
