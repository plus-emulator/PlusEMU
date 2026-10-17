using System.Collections.Immutable;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Navigator.New;

// AIR WIN63-202610151745-318700621 package_28/class_2230.as:24: top-level contexts, each with its saved searches
// (id, search code, filter, localization). Saved searches are not sent yet, so every context has none.
public class NavigatorMetaDataParserComposer(ImmutableArray<string> searchCodes) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NavigatorMetaDataParserComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(searchCodes.Length); //Count

        foreach (var searchCode in searchCodes) {
            //TopLevelContext
            packet.WriteString(searchCode); //Search code
            packet.WriteInteger(0); // Saved search count.
        }
    }
}
