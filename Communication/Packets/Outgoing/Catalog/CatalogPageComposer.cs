using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

public class CatalogPageComposer : IServerPacket
{
    private readonly CatalogPageSnapshot _page;
    public uint MessageId => ServerPacketHeader.CatalogPageComposer;

    public CatalogPageComposer(CatalogPageSnapshot page)
    {
        _page = page;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_page.Id);
        packet.WriteString(_page.Mode);
        packet.WriteString(_page.Layout);
        packet.WriteInteger(_page.Strings1.Length);

        foreach (var s in _page.Strings1) {
            packet.WriteString(s);
        }

        packet.WriteInteger(_page.Strings2.Length);

        foreach (var s in _page.Strings2) {
            packet.WriteString(s);
        }

        packet.WriteInteger(_page.Offers.Length);

        foreach (var offer in _page.Offers) {
            CatalogOfferWriter.Write(packet, offer);
        }

        // AIR WIN63-202610151745-318700621 package_4/class_1794.as:86, front page items package_5/class_2181.as:39
        packet.WriteInteger(_page.PreselectOfferId);
        packet.WriteBoolean(false);
        packet.WriteInteger(_page.Promotions.Length);

        foreach (var promotion in _page.Promotions) {
            packet.WriteInteger(promotion.Position);
            packet.WriteString(promotion.Title ?? string.Empty);
            packet.WriteString(promotion.Image ?? string.Empty);
            packet.WriteInteger(promotion.ItemType);

            switch (promotion.ItemType) {
                case CatalogPromotion.ProductOfferItem:
                    packet.WriteInteger(promotion.OfferId);
                    break;
                case CatalogPromotion.ProductCodeItem:
                    packet.WriteString(promotion.ProductCode ?? string.Empty);
                    break;
                case CatalogPromotion.CataloguePageItem:
                    packet.WriteString(promotion.PageLink ?? string.Empty);
                    break;
            }

            packet.WriteInteger(promotion.SecondsLeft);
        }
    }
}
