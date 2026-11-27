using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Engine;

internal sealed class WalkPreviewComposer(int nonce, int roomId, int x, int y, int startX, int startY,
    (int X, int Y)[]? route) : IServerPacket, IClientExtensionPacket
{
    public uint MessageId => ServerPacketHeader.WalkPreviewComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(nonce);
        packet.WriteInteger(roomId);
        packet.WriteInteger(x);
        packet.WriteInteger(y);
        packet.WriteInteger(startX);
        packet.WriteInteger(startY);
        packet.WriteInteger(route?.Length ?? -1);

        if (route == null) {
            return;
        }

        foreach (var tile in route) {
            packet.WriteInteger(tile.X);
            packet.WriteInteger(tile.Y);
        }
    }
}
