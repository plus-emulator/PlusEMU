using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

// A count of (name, alias) string pairs.
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
