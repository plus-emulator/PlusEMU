using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Inventory.AvatarEffect;

public class AvatarEffectAddedComposer : IServerPacket
{
    private readonly int _spriteId;
    private readonly int _duration;
    private readonly bool _permanent;
    public uint MessageId => ServerPacketHeader.AvatarEffectAddedComposer;

    public AvatarEffectAddedComposer(int spriteId, int duration, bool permanent = false)
    {
        _spriteId = spriteId;
        _duration = duration;
        _permanent = permanent;
    }

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(_spriteId);
        packet.WriteInteger(0); //Types
        packet.WriteInteger(_duration);
        packet.WriteBoolean(_permanent);
    }
}
