using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Chat;

/// <summary>
/// Chat, shout and whisper share one parser (AIR WIN63-202610151745-318700621 package_167/class_2856.as:101-140): user id, text,
/// gesture, style, links, tracking id, then the optional receiver room index, bubble width, hideUsername and location. Observed on
/// AIR WIN63-202610151745-318700621: the tracking id is -1, the receiver is the viewing client's own room index and the width is -1
/// without an override. An unset hideUsername and the location are left out so the parser keeps its defaults.
/// </summary>
internal static class RoomChatPacket
{
    public const int NoBubbleWidth = -1;
    private const int TrackingId = -1;

    public static void Write(IOutgoingPacket packet, int virtualId, string message, int gesture, int style, int receiverVirtualId,
        int bubbleWidth = NoBubbleWidth, bool hideUsername = false)
    {
        packet.WriteInteger(virtualId);
        packet.WriteString(message);
        packet.WriteInteger(gesture);
        packet.WriteInteger(style);
        packet.WriteInteger(0);
        packet.WriteInteger(TrackingId);
        packet.WriteInteger(receiverVirtualId);
        packet.WriteInteger(bubbleWidth);

        // Observed on AIR WIN63-202610151745-318700621: user and Wired chat end after the bubble width; the parser reads hideUsername
        // only while bytes remain, so it is written only when set.
        if (hideUsername) {
            packet.WriteBoolean(true);
        }
    }
}
