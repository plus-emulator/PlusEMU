using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>Whether the user plays the room's wired game: [playing]. Sent on room entry, and on join team and leave team.</summary>
public sealed class YouArePlayingGameComposer(bool playing) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.YouArePlayingGameComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteBoolean(playing);
}
