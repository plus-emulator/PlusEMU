using Plus.HabboHotel.Camera;
using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Incoming.Camera;

/// <summary>
/// The client sends purchase, publish and competition with an empty body. Volt adds the checkout media id as one string, so the id is
/// read only when the body is exactly that string; an empty body has no media id and the checkout is rejected.
/// </summary>
internal static class CameraCheckoutPacket
{
    internal static Guid? ReadMediaId(IIncomingPacket packet) =>
        TryReadMediaId(packet, out var id) ? id : null;

    internal static bool TryReadMediaId(IIncomingPacket packet, out Guid id)
    {
        id = Guid.Empty;
        var bytes = packet.Buffer;

        if (bytes.Length < 2) {
            return false;
        }

        int count = (bytes.Span[0] << 8) | bytes.Span[1];

        if (bytes.Length != count + 2) {
            return false;
        }

        return CameraMediaPath.TryReadId(packet.ReadString(), out id);
    }
}
