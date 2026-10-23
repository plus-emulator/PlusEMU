using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

/// <summary>[seconds left, furni limit, max furni limit, seconds left with grace]</summary>
public sealed class BuildersClubSubscriptionStatusComposer(BuildersClubStatus status) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.BuildersClubSubscriptionStatusComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(status.SecondsLeft);
        packet.WriteInteger(status.FurniLimit);
        packet.WriteInteger(status.MaxFurniLimit);
        packet.WriteInteger(status.SecondsLeftWithGrace);
    }
}
