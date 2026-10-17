using Plus.HabboHotel.Catalog.BuildersClub;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

/// <summary>
/// [seconds left, furni limit, max furni limit, seconds left with grace] (AIR WIN63-202610151745-318700621, package_4/class_2148.as:28-42,
/// read by HabboCatalog.onBuildersClubSubscriptionStatus, com/sulake/habbo/catalog/HabboCatalog.as:3694). Observed on hhus at login:
/// [617961, 500, 2000000, 617961] for a member.
/// </summary>
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
