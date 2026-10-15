using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>
/// Whether the user plays the room's wired game: package_44/class_3256 -> class_3332 reads [playing] (room/class_1993.as:324
/// onPlayingGame, WIN63-202610131318-860371703). Observed on hhus (r90): 7 bytes on room entry, and on join team / leave team.
/// </summary>
public sealed class YouArePlayingGameComposer(bool playing) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.YouArePlayingGameComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteBoolean(playing);
}
