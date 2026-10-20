using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Settings;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

public sealed class WiredRoomSettingsComposer(WiredRoomSettingsView view) : IServerPacket
{
    public uint MessageId => throw new NotImplementedException();
    public void Compose(IOutgoingPacket packet) => throw new NotImplementedException();
}
