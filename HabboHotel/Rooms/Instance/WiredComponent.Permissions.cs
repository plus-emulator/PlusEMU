using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;

namespace Plus.HabboHotel.Rooms.Instance;

public partial class WiredComponent
{
    /// <summary>
    /// The user's Wired permissions in this room (canModify, then canRead). Sent on room entry, and again whenever a
    /// rights change or a settings save can change them. The client takes the latest values.
    /// </summary>
    public void SendPermissions(GameClient session) => session.Send(new WiredPermissionsComposer(Settings.CanModify(session), Settings.CanInspect(session)));
}
