using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Catalog;

/// <summary>
/// [furni count] (AIR WIN63-202610151745-318700621, package_4/class_2878.as:22, read by HabboCatalog.onBuildersClubFurniCount,
/// com/sulake/habbo/catalog/HabboCatalog.as:3723): the Builders Club furni the user has placed. Observed on hhus after the
/// BUILDERS_CLUB index and after every Builders Club furni placed or removed.
/// </summary>
public sealed class BuildersClubFurniCountComposer(int count) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.BuildersClubFurniCountComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(count);
}
