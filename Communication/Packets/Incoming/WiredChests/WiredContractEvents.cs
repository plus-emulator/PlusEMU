using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Chests;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.WiredChests;

/// <summary>The complete packet is parsed before room-authorized access.</summary>
public sealed class GetWiredContractContentsEvent : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var id = packet.ReadUInt();

        return module => WiredNativeContractEditor.Contents(module, room, session, id);
    }
}

/// <summary>Only configuration changes; no visitor transfers.</summary>
public sealed class UpdateWiredContractEvent(IItemDataManager definitions) : WiredChestPacketEvent
{
    protected override Action<WiredChestRoom>? Read(Room room, GameClient session, IIncomingPacket packet)
    {
        var contract = WiredNativeContractWire.Read(packet);

        return module => WiredNativeContractEditor.Save(module, room, session, contract, definitions);
    }
}
