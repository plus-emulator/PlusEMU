using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Users.Collectibles;

namespace Plus.Communication.Packets.Outgoing.Collectibles;

/// <summary>
/// A product: type, item type id, score, the subtype's own fields, pet figure, figure set ids, product code and
/// rarity. A collection item adds its amount right after the score.
/// </summary>
internal static class NftProductWriter
{
    public static void Write(IOutgoingPacket packet, CollectibleProduct product, int? amount = null)
    {
        packet.WriteShort(product.ProductType);
        packet.WriteString(product.ItemTypeId);
        packet.WriteInteger(product.Score);

        if (amount is { } owned) {
            packet.WriteInteger(owned);
        }

        packet.WriteString(product.PetFigure);
        packet.WriteInteger(product.FigureSetIds.Length);

        foreach (var setId in product.FigureSetIds) {
            packet.WriteInteger(setId);
        }

        packet.WriteString(product.ProductCode);
        packet.WriteString(product.Rarity);
    }
}
