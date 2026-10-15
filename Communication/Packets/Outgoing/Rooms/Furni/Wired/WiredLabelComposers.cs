using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Labels;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>All labels of the room: class_3043 -> class_3418 reads [n, entry x n] and replaces what the client had.</summary>
public sealed class WiredLabelsComposer(IReadOnlyList<WiredLabel> labels) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLabelsComposer;

    public void Compose(IOutgoingPacket packet) => WriteLabels(packet, labels);

    // class_4102.parse: [type 0 label, furni id, text, bubble colour, visibility, always open]. Regions (type 1) are not sent.
    internal static void WriteLabels(IOutgoingPacket packet, IReadOnlyList<WiredLabel> labels)
    {
        packet.WriteInteger(labels.Count);

        foreach (var label in labels) {
            packet.WriteInteger(0);
            packet.WriteUInteger(label.FurniId);
            packet.WriteString(label.Text);
            packet.WriteInteger(label.BubbleColor);
            packet.WriteInteger(label.Visibility);
            packet.WriteBoolean(label.AlwaysOpen);
        }
    }
}

/// <summary>Changed labels: class_2856 -> class_3772 reads [n, entry x n, m, removed furni id x m].</summary>
public sealed class WiredLabelsUpdateComposer(IReadOnlyList<WiredLabel> changed, IReadOnlyList<uint> removed) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLabelsUpdateComposer;

    public void Compose(IOutgoingPacket packet)
    {
        WiredLabelsComposer.WriteLabels(packet, changed);
        packet.WriteInteger(removed.Count);

        foreach (var id in removed) {
            packet.WriteUInteger(id);
        }
    }
}
