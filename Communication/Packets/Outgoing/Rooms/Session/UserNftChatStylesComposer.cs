using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Session;

/// <summary>
/// The NFT chat style ids the user owns. Room opening sends it before
/// RoomReady; no styles exist here, so the list is empty.
/// </summary>
public sealed class UserNftChatStylesComposer : IServerPacket
{
    public uint MessageId => ServerPacketHeader.UserNftChatStylesComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteInteger(0);
}
