using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

/// <summary>One RecolorUpdateData: furni that now share one colour.</summary>
public sealed record FurniRecolorUpdate(IReadOnlyList<uint> FurniIds, int Red, int Green, int Blue);

/// <summary>Int count, then per update int idCount, int ids, short r, g, b. The client rewrites each furni's r, g and b.</summary>
public sealed class FurniRecolorUpdateComposer(IReadOnlyList<FurniRecolorUpdate> updates) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.FurniRecolorUpdateComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(updates.Count);

        foreach (var update in updates) {
            packet.WriteInteger(update.FurniIds.Count);

            foreach (var id in update.FurniIds) {
                packet.WriteUInteger(BuildersClubItemIds.ToWire(id));
            }

            packet.WriteShort((short)update.Red);
            packet.WriteShort((short)update.Green);
            packet.WriteShort((short)update.Blue);
        }
    }
}
