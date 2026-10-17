using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Handshake;

/// <summary>
/// AIR WIN63-202610151745-318700621 package_30/class_2929.as:30: account id, suggested login actions (shorts), identity id.
/// Accounts have no separate identity, so the user id fills both ids and no login actions are suggested.
/// </summary>
public class AuthenticationOkComposer(int userId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.AuthenticationOkComposer;

    public void Compose(IOutgoingPacket packet)
    {
        short[] suggestedLoginActions = [];

        packet.WriteInteger(userId);
        packet.WriteInteger(suggestedLoginActions.Length);

        foreach (var action in suggestedLoginActions) {
            packet.WriteShort(action);
        }

        packet.WriteInteger(userId);
    }
}
