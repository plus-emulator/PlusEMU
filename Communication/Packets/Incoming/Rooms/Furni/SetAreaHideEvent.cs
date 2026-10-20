using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// The area hider widget saves: int id, int rootX, int rootY, int width, int length, bool invisibility, bool wallItems, bool invert.
/// </summary>
internal sealed class SetAreaHideEvent : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet) => Task.CompletedTask;
}
