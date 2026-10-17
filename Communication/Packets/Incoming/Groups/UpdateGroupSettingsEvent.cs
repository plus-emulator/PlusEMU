using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Groups;

internal class UpdateGroupSettingsEvent(IGroupSettingsService settings) : IPacketEvent
{
    // AIR WIN63-202610151745-318700621 package_25/class_3302.as:11 sends (groupId, state, furniOptions).
    // Volt appends the forum flag only when its forum toggle changed.
    public Task Parse(GameClient session, IIncomingPacket packet) => settings.Update(session, new(
        packet.ReadInt(), packet.ReadInt(), packet.ReadInt(), packet.HasDataRemaining() ? packet.ReadBool() : null));
}
