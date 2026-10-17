using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Navigator.New;

// AIR WIN63-202610151745-318700621 package_28/class_4100.as:23 reads a list of collapsed category names.
public class NavigatorCollapsedCategoriesComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.NavigatorCollapsedCategoriesComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(0); // No collapsed categories.
}
