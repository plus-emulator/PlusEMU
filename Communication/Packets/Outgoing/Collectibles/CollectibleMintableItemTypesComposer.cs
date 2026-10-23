using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>A furni that can be minted: its type ("s" floor, "i" wall), sprite, the product it becomes, price in Emeralds and window.</summary>
public sealed record MintableItem(int Id, string ItemType, int SpriteId, string ProductCode, int EmeraldPrice, DateTimeOffset? StartsAt,
    DateTimeOffset? EndsAt, bool RegionLocked, bool LimitedEdition);

/// <summary>
/// Per item its sprite, start and end in unix seconds (0 = none), region lock,
/// price, limited edition and type (0 floor, 1 wall, 2 clothing). The client reads the price as mint tokens; this hotel sends Emeralds.
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
            packet.WriteInteger(item.EmeraldPrice);
            packet.WriteBoolean(item.LimitedEdition);
            packet.WriteShort((short)(item.ItemType == "i" ? 1 : 0));
        }
    }

    private static int UnixSeconds(DateTimeOffset? time) => (int)(time?.ToUnixTimeSeconds() ?? 0);
}
