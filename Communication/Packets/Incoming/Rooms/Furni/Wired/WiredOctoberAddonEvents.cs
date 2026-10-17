using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;

/// <summary>class_2618 [wired id, read key?]: the Web API editor's generate button; the new key comes back on class_3530.</summary>
public sealed class WiredWebApiKeyRequestEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        try {
            var wiredId = BuildersClubItemIds.FromWire(packet.ReadUInt());
            var read = packet.ReadBool();

            if (!packet.HasDataRemaining() && room.GetWired().IssueWebApiKey(session, wiredId, read, out var key)) {
                session.Send(new WiredWebApiKeyComposer(wiredId, read, key));
            }
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException) {
        }

        return Task.CompletedTask;
    }
}

/// <summary>class_3048 [wired id]: the Share add-on's Synchronize button.</summary>
public sealed class WiredVariableShareSynchronizeEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        try {
            var wiredId = BuildersClubItemIds.FromWire(packet.ReadUInt());

            if (!packet.HasDataRemaining()) {
                room.GetWired().SynchronizeShare(session, wiredId);
            }
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException) {
        }

        return Task.CompletedTask;
    }
}
