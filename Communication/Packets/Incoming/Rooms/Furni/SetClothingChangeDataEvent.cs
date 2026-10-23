using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.FootballGate;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// Item id, gender, figure. The avatar editor passes the figure and its gender, and the widget sends them gender first.
/// </summary>
internal sealed class SetClothingChangeDataEvent(IFootballGateService gates) : IPacketEvent
{
    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        var itemId = packet.ReadUInt();
        var gender = packet.ReadString();
        var look = packet.ReadString();
        gates.SaveLook(session, new(itemId, gender, look));

        return Task.CompletedTask;
    }
}
