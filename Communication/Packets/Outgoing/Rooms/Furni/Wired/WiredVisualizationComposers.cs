using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>Set Furni Transparency on furni: class_2988 reads [n, furni id x n, transparency, click through, easing] (L34-48).</summary>
public sealed class WiredFurniTransparencyComposer(IReadOnlyList<int> furniIds, int transparency, bool allowClickThrough, int easing) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredFurniTransparencyComposer;

    public void Compose(IOutgoingPacket packet)
    {
        WriteIds(packet, [.. furniIds.Select(id => BuildersClubItemIds.ToWire(id))]);
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
        WiredFurniTransparencyComposer.WriteIds(packet, [.. furniIds.Select(id => BuildersClubItemIds.ToWire(id))]);
        packet.WriteInteger(type);
        packet.WriteInteger(strength);
        packet.WriteInteger(color);
        packet.WriteInteger(animation);
        packet.WriteInteger(easing);
    }
}

/// <summary>Set Furni Highlight on users: class_3603 -> class_2412 reads [n, room index x n, type, strength, colour, animation, easing] (L37-57).</summary>
public sealed class WiredUserHighlightComposer(IReadOnlyList<int> roomIndices, int type, int strength, int color, int animation, int easing) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredUserHighlightComposer;

    public void Compose(IOutgoingPacket packet)
    {
        WiredFurniTransparencyComposer.WriteIds(packet, roomIndices);
        packet.WriteInteger(type);
        packet.WriteInteger(strength);
        packet.WriteInteger(color);
        packet.WriteInteger(animation);
        packet.WriteInteger(easing);
    }
}

/// <summary>A furni's or user's transparency: id (furni id or room index), transparency, click through, easing.</summary>
public sealed record WiredTransparencyState(int Id, int Transparency, bool AllowClickThrough, int Easing);

/// <summary>A furni's or user's highlight: id (furni id or room index), type, strength, colour, animation, easing.</summary>
public sealed record WiredHighlightState(int Id, int Type, int Strength, int Color, int Animation, int Easing);

/// <summary>
/// The visual state a user entering the room starts from: class_3560 -> class_3015 (furni) and class_3459 -> class_2803 (users) read
/// [n, (id, transparency, click through, easing) x n, m, (id, type, strength, colour, animation, easing) x m] (parse L25-52); the room
/// applies them without easing (class_56.initializeWiredFurniVisualizationStates L2994, initializeWiredUserVisualizationStates L3060).
/// </summary>
public sealed class WiredVisualizationStateComposer(bool users, IReadOnlyList<WiredTransparencyState> transparencies,
    IReadOnlyList<WiredHighlightState> highlights) : IServerPacket
{
    public uint MessageId => users ? ServerPacketHeader.WiredUserVisualizationStateComposer : ServerPacketHeader.WiredFurniVisualizationStateComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(transparencies.Count);

        foreach (var state in transparencies) {
            packet.WriteInteger(users ? state.Id : BuildersClubItemIds.ToWire(state.Id));
            packet.WriteInteger(state.Transparency);
            packet.WriteBoolean(state.AllowClickThrough);
            packet.WriteInteger(state.Easing);
        }

        packet.WriteInteger(highlights.Count);

        foreach (var state in highlights) {
            packet.WriteInteger(users ? state.Id : BuildersClubItemIds.ToWire(state.Id));
            packet.WriteInteger(state.Type);
            packet.WriteInteger(state.Strength);
            packet.WriteInteger(state.Color);
            packet.WriteInteger(state.Animation);
            packet.WriteInteger(state.Easing);
        }
    }
}
