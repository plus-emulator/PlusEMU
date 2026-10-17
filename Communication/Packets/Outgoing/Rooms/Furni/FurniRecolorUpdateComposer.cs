using Plus.HabboHotel.GameClients;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni;

/// <summary>One AIR RecolorUpdateData: furni that now share one colour.</summary>
public sealed record FurniRecolorUpdate(IReadOnlyList<uint> FurniIds, int Red, int Green, int Blue);

/// <summary>
/// AIR WIN63-202610131318-860371703 package_64/class_3777 (incoming 181 there, 3469 on WIN63-202610121709-785561319), parsed by
/// package_71/class_4153.as:22 and RecolorUpdateData.as:16: int count, then per update int idCount, int ids, short r, g, b.
/// The room engine rewrites each furni's r, g and b (room/class_1993.as:767).
/// </summary>
public sealed class FurniRecolorUpdateComposer(IReadOnlyList<FurniRecolorUpdate> updates) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.FurniRecolorUpdateComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteInteger(updates.Count);

        foreach (var update in updates) {
            packet.WriteInteger(update.FurniIds.Count);

            foreach (var id in update.FurniIds) {
                packet.WriteUInteger(id);
            }

            packet.WriteShort((short)update.Red);
            packet.WriteShort((short)update.Green);
            packet.WriteShort((short)update.Blue);
        }
    }
}
