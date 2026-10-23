using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Groups;

namespace Plus.Communication.Packets.Incoming.Groups;

internal class UpdateGroupSettingsEvent(IGroupSettingsService settings) : IPacketEvent
{
    // The client sends (groupId, state, furniOptions).
    // Volt appends the forum flag only when its forum toggle changed.
    public Task Parse(GameClient session, IIncomingPacket packet) => settings.Update(session, new(
        packet.ReadInt(), packet.ReadInt(), packet.ReadInt(), packet.HasDataRemaining() ? packet.ReadBool() : null));
}
