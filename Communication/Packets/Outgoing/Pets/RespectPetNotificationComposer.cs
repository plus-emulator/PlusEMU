using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Pets;

public class RespectPetNotificationComposer : IServerPacket
{
    private readonly int _virtualId;
    private readonly int _petId;
    private readonly string _name;
    private readonly string _colour;

    public uint MessageId => ServerPacketHeader.RespectPetNotificationComposer;

    public RespectPetNotificationComposer(int virtualId, int petId, string name, string colour)
    {
        _virtualId = virtualId;
        _petId = petId;
        _name = name;
        _colour = colour;
    }

    // AIR WIN63-202610151745-318700621 package_37/class_2618.as:30 reads respect, owner id, then the pet data of
    // package_123/class_2538.as:19 (id, name, figure of class_3869.as:21, level, rarity).
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_virtualId);
        packet.WriteInteger(_virtualId);
        packet.WriteInteger(_petId);
        packet.WriteString(_name);
        packet.WriteInteger(0);
        packet.WriteInteger(0);
        packet.WriteString(_colour);
        packet.WriteInteger(0);
        packet.WriteInteger(0);
        packet.WriteInteger(1);
        packet.WriteInteger(-1);
    }
}
