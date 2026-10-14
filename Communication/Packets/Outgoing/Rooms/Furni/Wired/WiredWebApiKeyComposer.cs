using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>A generated Web API key: class_3530 -> class_4132 reads [wired id, read key?, key] (VariablesWebApiAddon.as:133-148).</summary>
public sealed class WiredWebApiKeyComposer(uint wiredId, bool readKey, string key) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredWebApiKeyComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(wiredId);
        packet.WriteBoolean(readKey);
        packet.WriteString(key);
    }
}
