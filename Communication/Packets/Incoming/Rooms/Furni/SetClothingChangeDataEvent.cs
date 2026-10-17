using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.FootballGate;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni;

/// <summary>
/// AIR WIN63-202610151745-318700621 outgoing 2545, package_66/class_3476.as:15-30: item id, gender, figure. The avatar editor passes
/// the figure and its gender (HabboAvatarEditor.as:362-368) and the widget sends them gender first
/// (FurnitureClothingChangeWidgetHandler.as:141-147).
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
