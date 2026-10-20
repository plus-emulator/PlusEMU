using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Handshake;

public class UserPerksComposer(bool cameraAllowed) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.UserPerksComposer;

    public void Compose(IOutgoingPacket packet)
    {
        // TODO @80O: Pass perks via constructor.
        // TODO @80O: Store perks in the database.
        // Observed on AIR WIN63-202610201032-107964540: Habbo sends these 12 perks in this order.
        // BUILDER_AT_WORK stays denied so floor plans keep the normal size limit.
        // HABBO_CLUB_OFFER_BETA stays denied because this hotel has no Habbo Club offer.
        (string Code, string DenialKey, bool Allowed)[] perks =
        [
            ("JUDGE_CHAT_REVIEWS", "requirement.unfulfilled.helper_level_6", true),
            ("NAVIGATOR_PHASE_TWO_2014", "", true),
            ("TRADE", "requirement.unfulfilled.citizenship_level_3", true),
            ("CALL_ON_HELPERS", "", false),
            ("CITIZEN", "", true),
            ("BUILDER_AT_WORK", "requirement.unfulfilled.group_membership", false),
            ("HABBO_CLUB_OFFER_BETA", "", false),
            ("USE_GUIDE_TOOL", "requirement.unfulfilled.helper_level_4", false),
            ("MOUSE_ZOOM", "", true),
            ("NAVIGATOR_ROOM_THUMBNAIL_CAMERA", "", cameraAllowed),
            ("VOTE_IN_COMPETITIONS", "requirement.unfulfilled.helper_level_2", true),
            ("CAMERA", "", cameraAllowed),
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
