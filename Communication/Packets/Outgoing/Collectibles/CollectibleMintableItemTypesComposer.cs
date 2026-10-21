using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>A furni that can be minted: its type ("s" floor, "i" wall), sprite, the product it becomes, price in mint tokens and window.</summary>
public sealed record MintableItem(int Id, string ItemType, int SpriteId, string ProductCode, int TokenPrice, DateTime? StartsAt, DateTime? EndsAt,
    bool RegionLocked, bool LimitedEdition);

/// <summary>
/// AIR collectibles/class_4328.as:26-30 and class_4398.as:23-47: per item its sprite, start and end in unix seconds (0 = none), region lock,
/// price, limited edition and type (0 floor, 1 wall, 2 clothing).
/// </summary>
public sealed class CollectibleMintableItemTypesComposer(ImmutableArray<MintableItem> items) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.CollectibleMintableItemTypesComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(items.Length);

        foreach (var item in items) {
            packet.WriteInteger(item.SpriteId);
            packet.WriteInteger(UnixSeconds(item.StartsAt));
            packet.WriteInteger(UnixSeconds(item.EndsAt));
            packet.WriteBoolean(item.RegionLocked);
            packet.WriteInteger(item.TokenPrice);
            packet.WriteBoolean(item.LimitedEdition);
            packet.WriteShort((short)(item.ItemType == "i" ? 1 : 0));
        }
    }

    private static int UnixSeconds(DateTime? stored) => stored is { } time ? (int)CollectibleTime.Utc(time).ToUnixTimeSeconds() : 0;
}
