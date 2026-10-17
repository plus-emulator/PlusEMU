using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

// One catalog offer as the client parses it, shared by catalog pages, single product offers and club gifts.
// AIR WIN63-202610151745-318700621 package_5/class_1948.as:35 (products: package_5/class_2305.as:33)
public static class CatalogOfferWriter
{
    public static void Write(IOutgoingPacket packet, CatalogOfferSnapshot offer)
    {
        packet.WriteInteger(offer.OfferId);
        packet.WriteString(offer.LocalizationId);
        packet.WriteBoolean(false); //IsRentable
        packet.WriteInteger(offer.CostCredits);
        packet.WriteInteger(offer.Price);
        packet.WriteInteger(offer.PriceType);
        packet.WriteInteger(0); // Price in silver.
        packet.WriteBoolean(offer.CanGift);
        packet.WriteInteger(offer.Products.Length);

        foreach (var product in offer.Products) {
            WriteProduct(packet, product);
        }

        packet.WriteInteger(offer.ClubLevel);
        packet.WriteBoolean(offer.CanSelectAmount);
        packet.WriteBoolean(false); // Pet offer flag.
        packet.WriteString(offer.PreviewImage); // e.g. catalogue/pet_lion.png
    }

    private static void WriteProduct(IOutgoingPacket packet, CatalogProductSnapshot product)
    {
        packet.WriteString(product.ProductType);

        if (product.ProductType == "b") {
            packet.WriteString(product.ExtraParam);

            return;
        }

        packet.WriteInteger(product.ClassId);
        packet.WriteString(product.ExtraParam);
        packet.WriteInteger(product.Amount);
        packet.WriteBoolean(product.IsLimited);

        if (product.IsLimited) {
            packet.WriteUInteger(product.LimitedStack);
            packet.WriteUInteger(product.LimitedRemaining);
        }
    }
}
