using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

// AIR WIN63-202610151745-318700621 package_71/class_2892.as:48 reads a count of (name, alias) string pairs.
public class FurnitureAliasesComposer : IServerPacket
{
    private static readonly IReadOnlyDictionary<string, string> Aliases = new Dictionary<string, string>();

    public uint MessageId => ServerPacketHeader.FurnitureAliasesComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(Aliases.Count);

        foreach (var (name, alias) in Aliases) {
            packet.WriteString(name);
            packet.WriteString(alias);
        }
    }
}
