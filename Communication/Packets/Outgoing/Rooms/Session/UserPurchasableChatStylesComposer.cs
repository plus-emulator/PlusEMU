using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Session;

public sealed class UserPurchasableChatStylesComposer(IEnumerable<int> styles) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.UserPurchasableChatStylesComposer;
    public void Compose(IOutgoingPacket packet)
    {
        var ids = styles.Distinct().Order().ToArray();
        packet.WriteInteger(ids.Length);

        foreach (var id in ids) {
            packet.WriteInteger(id);
        }
    }
}

public sealed class UserPurchasableChatStyleChangedComposer(bool added, int styleId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.UserPurchasableChatStyleChangedComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteBoolean(added);
        packet.WriteInteger(styleId);
    }
}
