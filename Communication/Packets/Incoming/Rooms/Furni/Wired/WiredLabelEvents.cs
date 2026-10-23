using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;

/// <summary>[furni id, text]: the label bubble's text, saved with wired write permission. The hotel word filter applies.</summary>
public sealed class SaveWiredLabelTextEvent(IWordFilterManager wordFilter) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        try {
            var furniId = BuildersClubItemIds.FromWire(packet.ReadUInt());
            var text = packet.ReadString();

            if (!packet.HasDataRemaining()) {
                var filtered = wordFilter.CheckMessage(text);
                room.GetWired().SaveLabelText(session, furniId, filtered == text.TrimEnd(' ') ? text : filtered);
            }
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException) {
        }

        return Task.CompletedTask;
    }
}

/// <summary>[furni id, bubble colour, visibility, always open]: the label settings view.</summary>
public sealed class SaveWiredLabelSettingsEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        try {
            var furniId = BuildersClubItemIds.FromWire(packet.ReadUInt());
            var color = packet.ReadInt();
            var visibility = packet.ReadInt();
            var alwaysOpen = packet.ReadBool();

            if (!packet.HasDataRemaining()) {
                room.GetWired().SaveLabelSettings(session, furniId, color, visibility, alwaysOpen);
            }
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException) {
        }

        return Task.CompletedTask;
    }
}
