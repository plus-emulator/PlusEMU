using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>Set Furni Transparency on furni: class_2988 reads [n, furni id x n, transparency, click through, easing] (L34-48).</summary>
public sealed class WiredFurniTransparencyComposer(IReadOnlyList<int> furniIds, int transparency, bool allowClickThrough, int easing) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredFurniTransparencyComposer;

    public void Compose(IOutgoingPacket packet)
    {
        WriteIds(packet, furniIds);
        packet.WriteInteger(transparency);
        packet.WriteBoolean(allowClickThrough);
        packet.WriteInteger(easing);
    }

    internal static void WriteIds(IOutgoingPacket packet, IReadOnlyList<int> ids)
    {
        packet.WriteInteger(ids.Count);

        foreach (var id in ids) {
            packet.WriteInteger(id);
        }
    }
}

/// <summary>Set Furni Transparency on users: class_2441 reads [n, room index x n, transparency, click through, easing].</summary>
public sealed class WiredUserTransparencyComposer(IReadOnlyList<int> roomIndices, int transparency, bool allowClickThrough, int easing) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredUserTransparencyComposer;

    public void Compose(IOutgoingPacket packet)
    {
        WiredFurniTransparencyComposer.WriteIds(packet, roomIndices);
        packet.WriteInteger(transparency);
        packet.WriteBoolean(allowClickThrough);
        packet.WriteInteger(easing);
    }
}

/// <summary>Set Furni Highlight: class_3488 reads [n, furni id x n, type, strength, colour, animation, easing] (L40-56).</summary>
public sealed class WiredFurniHighlightComposer(IReadOnlyList<int> furniIds, int type, int strength, int color, int animation, int easing) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredFurniHighlightComposer;

    public void Compose(IOutgoingPacket packet)
    {
        WiredFurniTransparencyComposer.WriteIds(packet, furniIds);
        packet.WriteInteger(type);
        packet.WriteInteger(strength);
        packet.WriteInteger(color);
        packet.WriteInteger(animation);
        packet.WriteInteger(easing);
    }
}
