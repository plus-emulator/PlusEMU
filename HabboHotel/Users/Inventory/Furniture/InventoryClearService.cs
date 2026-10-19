using System.Text.Json;
using Dapper;
using Plus.Communication.Packets.Outgoing.Inventory.Furni;
using Plus.Communication.Packets.Outgoing.Inventory.Purse;
using Plus.Database;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Music;
using Plus.HabboHotel.Users.Authentication;

namespace Plus.HabboHotel.Users.Inventory.Furniture;

/// <summary>
/// What a clear did: the deleted items, the foreign discs it released, the wallet after chest coins were refunded,
/// and the chests it kept because their coins would overflow the wallet.
/// </summary>
public sealed record InventoryClearResult(IReadOnlyList<uint> Deleted, IReadOnlyList<InventoryItem> ReleasedDiscs, int Credits,
    long RefundedCredits, int RefundedChests, int KeptChests);

public interface IInventoryClearStore
{
    InventoryClearResult DeleteAll(int userId, int liveCredits);
    InventoryItem? AvailableDisc(uint discId, uint ownerId);
}

public sealed class InventoryClearStore(IDatabase database, IItemDataManager definitions) : IInventoryClearStore
{
    public InventoryItem? AvailableDisc(uint discId, uint ownerId)
    {
        using var connection = database.Connection();

        return RoomMusicStore.ReadAvailableDisc(connection, definitions, discId, ownerId);
    }

    public InventoryClearResult DeleteAll(int userId, int liveCredits)
    {
        using var connection = database.Connection();
        connection.Open();
        using var transaction = connection.BeginTransaction();

        if (connection.ExecuteScalar<int?>("SELECT id FROM users WHERE id=@userId FOR UPDATE", new { userId }, transaction) == null) {
            throw new InvalidOperationException("Inventory owner no longer exists.");
        }

        // Capture the set before a deleted player's cascading links expose its discs again.
        var ids = connection.Query<uint>("""
            SELECT id FROM items WHERE room_id=0 AND user_id=@userId
                AND NOT EXISTS (SELECT 1 FROM room_music_playlist link WHERE link.disc_id=items.id)
            ORDER BY id FOR UPDATE
            """, new { userId }, transaction).ToArray();

        if (ids.Length == 0) {
            return new([], [], liveCredits, 0, 0, 0);
        }

        // Chest coins are credits, so a deleted chest pays them out like a withdrawal. A chest whose coins would
        // overflow the wallet is kept instead of losing them.
        var credits = (long)liveCredits;
        var refunded = new List<ChestRow>();
        var kept = new HashSet<uint>();

        foreach (var chest in connection.Query<ChestRow>("SELECT item_id AS Id,coins FROM wired_chests WHERE item_id IN @ids ORDER BY item_id FOR UPDATE", new { ids }, transaction)) {
            if (credits + chest.Coins > int.MaxValue) {
                kept.Add(chest.Id);

                continue;
            }

            credits += chest.Coins;
            refunded.Add(chest);
        }

        ids = ids.Where(id => !kept.Contains(id)).ToArray();

        var discs = connection.Query<DiscRow>("""
            SELECT item.id,item.user_id AS OwnerId,item.base_item AS BaseItem,item.extra_data AS ExtraData,
                   item.limited_number AS LimitedNumber,item.limited_stack AS LimitedStack
            FROM room_music_playlist link JOIN items item ON item.id=link.disc_id
            WHERE link.player_id IN @ids AND item.room_id=0 FOR UPDATE
            """, new { ids }, transaction).ToArray();
        connection.Execute("DELETE FROM room_music_playlist WHERE player_id IN @ids", new { ids }, transaction);
        Refund(connection, transaction, userId, refunded, credits);
        connection.Execute("DELETE FROM items WHERE id IN @ids AND room_id=0 AND user_id=@userId", new { ids, userId }, transaction);
        var map = definitions.Items;
        var released = discs.Where(disc => map.TryGetValue(disc.BaseItem, out var definition) && RoomMusicDefinition.IsDisc(definition))
            .Select(disc => RoomMusicDefinition.Inventory(disc.Id, disc.OwnerId, map[disc.BaseItem], disc.ExtraData, disc.LimitedNumber, disc.LimitedStack)).ToArray();
        transaction.Commit();

        return new(ids, released, (int)credits, credits - liveCredits, refunded.Count, kept.Count);
    }

    private static void Refund(System.Data.IDbConnection connection, System.Data.IDbTransaction transaction, int userId, List<ChestRow> chests, long credits)
    {
        if (chests.Count == 0) {
            return;
        }

        var chestIds = chests.Select(chest => chest.Id).ToArray();
        connection.Execute("INSERT INTO wired_chest_transactions (operation_id,user_id,room_id,source_id,result,created_at) VALUES (@operation,@userId,0,0,@result,UTC_TIMESTAMP(6))",
            new { operation = Guid.NewGuid().ToString("N"), userId, result = JsonSerializer.Serialize(new { Credits = credits, InventoryCleared = true }) }, transaction);
        var logId = connection.ExecuteScalar<long>("SELECT LAST_INSERT_ID()", transaction: transaction);

        foreach (var chest in chests.Where(chest => chest.Coins > 0)) {
            connection.Execute("INSERT INTO wired_chest_transaction_entries (transaction_id,chest_id,item_id,is_deposit,coins) VALUES (@logId,@Id,NULL,0,@Coins)",
                new { logId, chest.Id, chest.Coins }, transaction);
        }

        // Stored stock goes with its chest, and must go first: it holds the chest row in place.
        connection.Execute("DELETE FROM items WHERE chest_item_id IN @chestIds AND user_id=0", new { chestIds }, transaction);
        connection.Execute("DELETE FROM wired_chests WHERE item_id IN @chestIds", new { chestIds }, transaction);

        if (chests.Any(chest => chest.Coins > 0)) {
            connection.Execute("UPDATE users SET credits=@credits WHERE id=@userId", new { credits, userId }, transaction);
        }
    }

    private sealed class ChestRow
    {
        public uint Id { get; set; }
        public long Coins { get; set; }
    }
    private sealed class DiscRow
    {
        public uint Id { get; set; }
        public uint OwnerId { get; set; }
        public uint BaseItem { get; set; }
        public string ExtraData { get; set; } = "";
        public uint LimitedNumber { get; set; }
        public uint LimitedStack { get; set; }
    }
}

public interface IInventoryClearService
{
    bool TryClear(GameClient session, Room room);
}

public sealed class InventoryClearService(IInventoryClearStore store, IAccountSessionGate accounts,
    IGameClientManager clients) : IInventoryClearService
{
    public bool TryClear(GameClient session, Room room)
    {
        var habbo = session.GetHabbo();
        InventoryClearResult cleared;

        using (accounts.Enter(habbo.Id)) {
            lock (habbo.WalletSync) {
                // Clearing empties the loaded inventory together with storage, so nothing is deleted without one.
                if (habbo.AccessClosed || !ReferenceEquals(habbo.Client, session) || !ReferenceEquals(habbo.CurrentRoom, room)
                    || habbo.Inventory is not { } inventory) {
                    return false;
                }

                var roomUser = room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id);

                if (roomUser == null || roomUser.IsTrading) {
                    return false;
                }

                cleared = store.DeleteAll(habbo.Id, habbo.Credits);
                habbo.Credits = cleared.Credits;

                foreach (var id in cleared.Deleted) {
                    inventory.Furniture.RemoveItem(id);
                }
            }
        }

        // A foreign disc keeps its original owner; resolve the current session after taking that owner's gate.
        foreach (var disc in cleared.ReleasedDiscs) {
            RoomMusicComponent.PublishReturned(disc, accounts, clients, store.AvailableDisc);
        }

        session.Send(new FurniListUpdateComposer());

        if (cleared.RefundedCredits > 0) {
            session.Send(new CreditBalanceComposer(cleared.Credits));
        }

        session.SendNotification("Your inventory has been cleared!");

        if (cleared.RefundedCredits > 0) {
            var chests = cleared.RefundedChests == 1 ? "the chest that was" : $"the {cleared.RefundedChests} chests that were";
            session.SendNotification($"{cleared.RefundedCredits} credits were added to your wallet.\n\nThey were the coins inside {chests} deleted from your inventory.");
        }

        if (cleared.KeptChests > 0) {
            var one = cleared.KeptChests == 1;
            session.SendNotification((one ? "1 chest was" : $"{cleared.KeptChests} chests were") + " kept in your inventory.\n\n"
                + $"The coins inside {(one ? "it" : "them")} would take your credits above the maximum of {int.MaxValue}, so they could not be added "
                + $"to your wallet. Spend some credits or take coins out of the {(one ? "chest" : "chests")}, then type :emptyitems yes again.");
        }

        return true;
    }
}
