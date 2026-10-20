using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Settings;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;

public sealed class WiredRoomSettingsRequestEvent(IWiredRoomSettingsService settings) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (!packet.HasDataRemaining()) {
            settings.Reload(room, session, WiredRoomSettingsReply.Custom);
        }

        return Task.CompletedTask;
    }
}

public sealed class WiredRoomSettingsSaveEvent(IWiredRoomSettingsService settings) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        int inspect, modify;

        try {
            inspect = packet.ReadInt();
            modify = packet.ReadInt();
        }
        catch (ArgumentException) {
            return Task.CompletedTask;
        }

        if (packet.HasDataRemaining()) {
            return Task.CompletedTask;
        }

        settings.Save(room, session, inspect, modify, null, WiredRoomSettingsReply.Custom);

        return Task.CompletedTask;
    }
}

/// <summary>Habbo's own settings request. Observed on AIR WIN63-202610151745-318700621: it has no fields and gets WiredRoomSettings back.</summary>
public sealed class WiredGetRoomSettingsEvent(IWiredRoomSettingsService settings) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (!packet.HasDataRemaining()) {
            settings.Reload(room, session, WiredRoomSettingsReply.Native);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Habbo's own settings save. Observed on AIR WIN63-202610151745-318700621: modify mask, read mask, then the timezone.
/// It gets WiredRoomSettings back, also when the save is rejected.
/// </summary>
public sealed class WiredSetRoomSettingsEvent(IWiredRoomSettingsService settings) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        int modify, inspect;
        string timezone;

        try {
            modify = packet.ReadInt();
            inspect = packet.ReadInt();
            timezone = packet.ReadString();
        }
        catch (ArgumentException) {
            return Task.CompletedTask;
        }

        if (packet.HasDataRemaining()) {
            return Task.CompletedTask;
        }

        settings.Save(room, session, inspect, modify, timezone, WiredRoomSettingsReply.Native);

        return Task.CompletedTask;
    }
}
