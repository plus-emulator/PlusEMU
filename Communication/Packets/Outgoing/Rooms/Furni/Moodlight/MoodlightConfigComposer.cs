using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Data.Moodlight;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Moodlight;

public class MoodlightConfigComposer : IServerPacket
{
    private readonly MoodlightConfigSnapshot _snapshot;

    public uint MessageId => ServerPacketHeader.MoodlightConfigComposer;

    public MoodlightConfigComposer(MoodlightConfigSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_snapshot.Presets.Length);
        packet.WriteInteger(_snapshot.CurrentPreset);
        var i = 1;

        foreach (var preset in _snapshot.Presets) {
            packet.WriteInteger(i);
            packet.WriteInteger(preset.BackgroundOnly ? 2 : 1);
            packet.WriteString(preset.ColorCode);
            packet.WriteInteger(preset.ColorIntensity);
            i++;
        }

        // AIR WIN63-202610151745-318700621 package_173/class_3442.as:59 reads isOn and the moodlight's item id after the presets.
        packet.WriteBoolean(_snapshot.Enabled);
        packet.WriteUInteger(_snapshot.ItemId);
    }
}
