using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Chat;

/// <summary>
/// Chat, shout and whisper share one parser: user id, text,
/// gesture, style, links, tracking id, then the optional receiver room index, bubble width, hideUsername and location. The tracking
/// id is -1, the receiver is the viewing client's own room index and the width is -1 without an override. An unset hideUsername and an unset location are left out so the parser keeps its defaults.
/// </summary>
internal static class RoomChatPacket
{
    public const int NoBubbleWidth = -1;
    private const int TrackingId = -1;

    public static void Write(IOutgoingPacket packet, int virtualId, string message, int gesture, int style, int receiverVirtualId,
        int bubbleWidth = NoBubbleWidth, bool hideUsername = false, (int X, int Y)? location = null)
    {
        packet.WriteInteger(virtualId);
        packet.WriteString(message);
        packet.WriteInteger(gesture);
        packet.WriteInteger(style);
        packet.WriteInteger(0);
        packet.WriteInteger(TrackingId);
        packet.WriteInteger(receiverVirtualId);
        packet.WriteInteger(bubbleWidth);

        // User and Wired chat end after the bubble width; the parser reads hideUsername and the location
        // only while bytes remain, so they are written only when set.
        if (hideUsername || location != null) {
            packet.WriteBoolean(hideUsername);
        }

        if (location is var (x, y)) {
            packet.WriteInteger(x);
            packet.WriteInteger(y);
        }
    }
}
