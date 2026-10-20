using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Handshake;

public class UserPerksComposer(bool cameraAllowed) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.UserPerksComposer;

    public void Compose(IOutgoingPacket packet)
    {
        // TODO @80O: Pass perks via constructor.
        // TODO @80O: Store perks in the database.
        (string Code, string DenialKey, bool Allowed)[] perks =
        [
            ("USE_GUIDE_TOOL", "", false),
            ("GIVE_GUIDE_TOURS", "requirement.unfulfilled.helper_le", false),
            ("JUDGE_CHAT_REVIEWS", "", true),
            ("VOTE_IN_COMPETITIONS", "", true),
            ("CALL_ON_HELPERS", "", false),
            ("CITIZEN", "", true),
            ("TRADE", "", true),
            ("HEIGHTMAP_EDITOR_BETA", "", false),
            ("EXPERIMENTAL_CHAT_BETA", "requirement.unfulfilled.helper_level_2", true),
            ("EXPERIMENTAL_TOOLBAR", "", true),
            ("NAVIGATOR_PHASE_ONE_2014", "", false),
            ("CAMERA", "", cameraAllowed),
            ("NAVIGATOR_PHASE_TWO_2014", "", true),
            ("MOUSE_ZOOM", "", true),
            ("NAVIGATOR_ROOM_THUMBNAIL_CAMERA", "", cameraAllowed),
        ];
        packet.WriteInteger(perks.Length);
        // Each perk contains its code, optional denial-message localization key, and allowed flag.
        foreach (var (code, denialKey, allowed) in perks) {
            packet.WriteString(code);
            packet.WriteString(denialKey);
            packet.WriteBoolean(allowed);
        }
    }
}
