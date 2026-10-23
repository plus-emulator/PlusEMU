using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.HabboHotel.Items.Wired.Chests;

// Chest settings are not a packet: the client reads them from the chest's map stuff data (WiredChestRoom.Refresh).

/// <summary>The client answers with ChestOpenEvent.</summary>
public sealed class WiredChestOpenComposer(uint chestId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestOpenComposer;
    public void Compose(IOutgoingPacket packet) => packet.WriteUInt(chestId);
}

/// <summary>A saved settings form; the client closes that form.</summary>
public sealed class WiredChestSettingsUpdatedComposer(uint chestId, bool notificationPreferences) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestSettingsUpdatedComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInt(chestId);
        packet.WriteBool(notificationPreferences);
    }
}

/// <summary>Result 0 is success.</summary>
public sealed class WiredChestUpgradeComposer(uint chestId, int result) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestUpgradeComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInt(chestId);
        packet.WriteInt(result);
    }
}

/// <summary>With update false the client opens the coin chest view; with true it updates the open view.</summary>
public sealed class WiredChestCoinsComposer(uint chestId, int coins, bool update) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestCoinsComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInt(chestId);
        packet.WriteInt(coins);
        packet.WriteBool(update);
    }
}

/// <summary>One fragment of a furni chest being opened.</summary>
public sealed class WiredChestFurniChunkComposer(uint chestId, int pages, int page, InventoryItem[] items, IReadOnlyDictionary<uint, long>? depositTransactions = null) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestFurniChunkComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInt(chestId);
        packet.WriteInt(pages);
        packet.WriteInt(page);
        WiredChestWire.Storages(packet, items, depositTransactions);
    }
}

/// <summary>Contents change of the furni chest a viewer has open.</summary>
public sealed class WiredChestFurniUpdateComposer(uint chestId, uint[] removed, InventoryItem[] added, IReadOnlyDictionary<uint, long>? depositTransactions = null) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestFurniUpdateComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInt(chestId);
        packet.WriteInt(removed.Length);

        foreach (var id in removed) {
            packet.WriteUInt(id);
        }

        WiredChestWire.Storages(packet, added, depositTransactions);
    }
}

/// <summary>The client shows the text wired_transactions.notification.fail.N.</summary>
public sealed class WiredTransactionFailComposer(WiredChestFailure failure) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredTransactionFailComposer;
    public void Compose(IOutgoingPacket packet) => packet.WriteInt((int)failure);
}

public enum WiredTransactionSuccess
{
    Deposit = 0, Withdraw = 1, Reward = 2, Payment = 3, Trade = 4
}

/// <summary>Type 2 carries the reward rule, text and popup flag.</summary>
public sealed class WiredTransactionSuccessComposer(WiredTransactionSuccess type, WiredChestNode[]? reward = null, string text = "", bool open = false) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredTransactionSuccessComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInt((int)type);

        if (type == WiredTransactionSuccess.Reward && reward != null) {
            WiredChestWire.Rule(packet, reward);
            packet.WriteString(text);
            packet.WriteBool(open);
        }
    }

    public static WiredTransactionSuccessComposer Rewarded(WiredChestTransferResult result, string text, bool open)
    {
        var nodes = result.Received.GroupBy(WiredChestItemType.Of).Select(group => new WiredChestNode(group.Count(), group.Key)).ToList();

        if (result.Figures.WithdrawalCoins > 0) {
            nodes.Insert(0, new(result.Figures.WithdrawalCoins));
        }

        return new(WiredTransactionSuccess.Reward, nodes.ToArray(), text, open);
    }
}

public enum WiredTradeError
{
    InvalidItem = 0, TooManyItems = 1
}

/// <summary>The client shows the text wired_transactions.notification.trade_error.N.</summary>
public sealed class WiredTradeErrorComposer(WiredTradeError error) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredTradeErrorComposer;
    public void Compose(IOutgoingPacket packet) => packet.WriteInt((int)error);
}

/// <summary>
/// TradeRequirement, showRequirementsImmediate, overridePreviousTrade, timeoutSeconds.
/// A plain deposit is "any coins" (0), "any furni" (1) or "anything" (2) by the chests' kinds; a contract with rules sends
/// them (4), unscaled, with the rule type (0 normal, 1 multiplier, 2 auto) and its multiplier.
/// </summary>
public sealed class WiredChestTradeOpenComposer(WiredChestContract? contract, IReadOnlyCollection<WiredChestKind> kinds, int mode, int multiplier, int timeout, bool replacement) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestTradeOpenComposer;
    public void Compose(IOutgoingPacket packet)
    {
        // A plain deposit and a payment contract in mode 0 ("Anything (donation)") take whatever the chests can hold.
        if (contract == null || contract is { Kind: WiredContractKind.Payment, PaymentMode: 0 }) {
            packet.WriteInt(kinds.All(kind => kind == WiredChestKind.Coins) ? 0 : kinds.All(kind => kind == WiredChestKind.Furni) ? 1 : 2);
            packet.WriteString(contract?.ReceiveText ?? "");
            packet.WriteString(contract?.Layout ?? "generic");
        }
        else {
            packet.WriteInt(4);
            packet.WriteString(contract.ReceiveText);
            packet.WriteString(contract.Layout);
            packet.WriteBool(contract.Payment != null);

            if (contract.Payment != null) {
                packet.WriteInt(contract.Payment.Length);

                foreach (var rule in contract.Payment) {
                    WiredChestWire.Rule(packet, rule);
                }
            }

            var reward = contract.Kind == WiredContractKind.Trade ? contract.Reward : null;
            packet.WriteBool(reward != null);

            if (reward != null) {
                WiredChestWire.Rule(packet, reward);
            }

            packet.WriteInt(mode is 1 or 2 ? mode : 0);

            if (mode is 1 or 2) {
                packet.WriteInt(multiplier);
            }
        }

        packet.WriteBool(true);
        packet.WriteBool(replacement);
        packet.WriteInt(timeout);
    }
}

/// <summary>
/// Trading items (first = the player's offer, second = what the chests give),
/// canAccept, and how many times the requirement is met.
/// </summary>
public sealed class WiredChestTradeItemsComposer(int userId, InventoryItem[] offer, InventoryItem[] reward, int rewardCoins, bool canAccept, int times) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestTradeItemsComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInt(userId);
        WiredChestWire.TradeItems(packet, offer);
        packet.WriteInt(offer.Length);
        packet.WriteInt(offer.Where(item => item.Definition.InteractionType == InteractionType.Exchange).Sum(item => item.Definition.BehaviourData));
        packet.WriteInt(0);
        WiredChestWire.TradeItems(packet, reward);
        packet.WriteInt(reward.Length);
        packet.WriteInt(rewardCoins);
        packet.WriteBool(canAccept);
        packet.WriteInt(times);
    }
}

/// <summary>The wired trade window closes with this reason.</summary>
public sealed class WiredChestTradeCancelledComposer(WiredChestFailure failure) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestTradeCancelledComposer;
    public void Compose(IOutgoingPacket packet) => packet.WriteInt((int)failure);
}

/// <summary>The wired trade went through; no body.</summary>
public sealed class WiredChestTradeCompletedComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredChestTradeCompletedComposer;
    public void Compose(IOutgoingPacket packet) { }
}

internal static class WiredChestWire
{
    /// <summary>TradeRequirementRule: int count, TradeRequirementNode[].</summary>
    public static void Rule(IOutgoingPacket packet, WiredChestNode[] nodes)
    {
        packet.WriteInt(nodes.Length);

        foreach (var node in nodes) {
            Node(packet, node);
        }
    }

    /// <summary>TradeRequirementNode: byte type (0 coins, 1 furni), int amount, furni: ChestItemType.</summary>
    public static void Node(IOutgoingPacket packet, WiredChestNode node)
    {
        packet.WriteByte((byte)(node.ItemType == null ? 0 : 1));
        packet.WriteInt(node.Amount);

        if (node.ItemType is { } type) {
            packet.WriteBool(type.Wall);
            packet.WriteInt(type.SpriteId);
            packet.WriteString(type.PosterId);
        }
    }

    /// <summary>int count, ChestStorage[].</summary>
    public static void Storages(IOutgoingPacket packet, InventoryItem[] items, IReadOnlyDictionary<uint, long>? depositTransactions)
    {
        packet.WriteInt(items.Length);

        foreach (var item in items) {
            var type = WiredChestItemType.Of(item);
            packet.WriteUInt(item.Id);
            packet.WriteInt(0);
            var deposit = depositTransactions?.GetValueOrDefault(item.Id) ?? 0;
            packet.WriteInt(unchecked((int)(deposit >> 32)));
            packet.WriteInt(unchecked((int)deposit));
            packet.WriteBool(type.Wall);
            packet.WriteInt(type.SpriteId);
            packet.WriteString(type.PosterId);
            packet.WriteBool(item.Definition.AllowInventoryStack);
            packet.WriteInt((int)item.Definition.Category);
            FurnitureDataSerializer.Write(packet, FurnitureDataSnapshot.Capture(item.ExtraData), item.UniqueNumber, item.UniqueSeries);

            if (!type.Wall) {
                packet.WriteInt(0);
            }
        }
    }

    /// <summary>int count, item[] (the trading items).</summary>
    public static void TradeItems(IOutgoingPacket packet, InventoryItem[] items)
    {
        packet.WriteInt(items.Length);

        foreach (var item in items) {
            packet.WriteUInt(item.Id);
            packet.WriteString(item.IsWallItem ? "I" : "S");
            packet.WriteUInt(item.Id);
            packet.WriteInt(item.Definition.SpriteId);
            packet.WriteInt((int)item.Definition.Category);
            packet.WriteBool(item.Definition.AllowInventoryStack);
            FurnitureDataSerializer.Write(packet, FurnitureDataSnapshot.Capture(item.ExtraData), item.UniqueNumber, item.UniqueSeries);
            packet.WriteInt(0);
            packet.WriteInt(0);
            packet.WriteInt(0);

            if (!item.IsWallItem) {
                packet.WriteInt(0);
            }
        }
    }
}
