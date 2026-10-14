using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Chests;

/// <summary>AIR25 class_2536 / AbstractContract: short kind, nullable rule sets, byte node kinds, optional furniture tuples.</summary>
public static class WiredNativeContractWire
{
    public static WiredChestContract Read(IIncomingPacket packet)
    {
        var id = packet.ReadUInt();
        var kind = packet.ReadShort();
        if (kind is < 0 or > 2) throw new InvalidDataException("Invalid contract kind.");
        WiredChestNode[][]? payment = null;
        if (packet.ReadBool()) {
            var count = packet.ReadInt();
            if (count is < 0 or > 3) throw new InvalidDataException("Invalid contract alternatives.");
            payment = new WiredChestNode[count][];
            for (var i = 0; i < count; i++) payment[i] = ReadRule(packet);
        }
        var reward = packet.ReadBool() ? ReadRule(packet) : null;
        var result = new WiredChestContract { Id = id, Kind = (WiredContractKind)kind, Payment = payment, Reward = reward };
        if (result.Kind == WiredContractKind.Payment) result = result with {
            PaymentMode = packet.ReadShort(), ReceiveText = packet.ReadString(), Layout = packet.ReadString()
        };
        if (result.Kind == WiredContractKind.Reward) result = result with {
            RewardCategory = packet.ReadShort(), ShowDialog = packet.ReadBool(), RewardText = packet.ReadString()
        };
        return result;
    }

    private static WiredChestNode[] ReadRule(IIncomingPacket packet)
    {
        var count = packet.ReadInt();
        if (count is < 0 or > 5) throw new InvalidDataException("Invalid contract nodes.");
        var nodes = new WiredChestNode[count];
        for (var i = 0; i < count; i++) {
            var type = packet.ReadByte();
            if (type is not (0 or 1)) throw new InvalidDataException("Invalid contract node kind.");
            var amount = packet.ReadInt();
            WiredChestItemType? itemType = type == 1 ? new(packet.ReadBool(), packet.ReadInt(), packet.ReadString()) : null;
            if (itemType is { PosterId.Length: > 20 }) throw new InvalidDataException("Invalid poster id.");
            nodes[i] = new(amount, itemType);
        }
        return nodes;
    }

    public static void Write(IOutgoingPacket packet, WiredChestContract contract)
    {
        packet.WriteUInt(contract.Id);
        packet.WriteShort((short)contract.Kind);
        packet.WriteBool(contract.Payment != null);
        if (contract.Payment != null) {
            packet.WriteInt(contract.Payment.Length);
            foreach (var rule in contract.Payment) WriteRule(packet, rule);
        }
        packet.WriteBool(contract.Reward != null);
        if (contract.Reward != null) WriteRule(packet, contract.Reward);
        if (contract.Kind == WiredContractKind.Payment) {
            packet.WriteShort((short)contract.PaymentMode);
            packet.WriteString(contract.ReceiveText);
            packet.WriteString(contract.Layout);
        }
        if (contract.Kind == WiredContractKind.Reward) {
            packet.WriteShort((short)contract.RewardCategory);
            packet.WriteBool(contract.ShowDialog);
            packet.WriteString(contract.RewardText);
        }
    }

    private static void WriteRule(IOutgoingPacket packet, WiredChestNode[] nodes)
    {
        packet.WriteInt(nodes.Length);
        foreach (var node in nodes) {
            packet.WriteByte((byte)(node.ItemType == null ? 0 : 1));
            packet.WriteInt(node.Amount);
            if (node.ItemType is { } type) {
                packet.WriteBool(type.Wall);
                packet.WriteInt(type.SpriteId);
                packet.WriteString(type.PosterId);
            }
        }
    }
}

public sealed class WiredContractOpenComposer(uint contractId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredContractOpenComposer;
    public void Compose(IOutgoingPacket packet) => packet.WriteUInt(contractId);
}
public sealed class WiredContractContentsComposer(WiredChestContract contract) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredContractContentsComposer;
    public void Compose(IOutgoingPacket packet) => WiredNativeContractWire.Write(packet, contract);
}
public sealed class WiredContractUpdateResultComposer(uint contractId, bool success) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredContractUpdateResultComposer;
    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInt(contractId);
        packet.WriteBool(success);
        packet.WriteString(success ? "" : "invalid_rules");
    }
}

public static class WiredNativeContractEditor
{
    private static Item? Find(Room room, GameClient client, uint id, bool edit) =>
        client.GetHabbo().CurrentRoom == room
        && (edit ? room.GetWired().Settings.CanModify(client) : room.GetWired().Settings.CanInspect(client))
        && room.GetRoomItemHandler().GetItem(id) is { IsTemporary: false } item
        && WiredChestFurniture.IsContract(item.Definition) ? item : null;

    public static void Open(Room room, GameClient client, uint id)
    {
        if (Find(room, client, id, false) != null) client.Send(new WiredContractOpenComposer(id));
    }

    public static void Contents(WiredChestRoom module, Room room, GameClient client, uint id)
    {
        if (Find(room, client, id, false) is { } item && module.Store.LoadContract(item) is { } contract)
            client.Send(new WiredContractContentsComposer(contract with { Id = item.Id, Kind = WiredChestFurniture.ContractKind(item.Definition) }));
    }

    public static void Save(WiredChestRoom module, Room room, GameClient client, WiredChestContract contract, IItemDataManager definitions)
    {
        if (Find(room, client, contract.Id, true) is not { } item) return;
        var valid = contract.Kind == WiredChestFurniture.ContractKind(item.Definition)
            && (contract.Kind != WiredContractKind.Payment || contract.Reward == null)
            && (contract.Kind != WiredContractKind.Reward || contract.Payment == null)
            && WiredChestContracts.Valid(contract, definitions);
        var saved = valid && module.Store.SaveContract(item, contract);
        client.Send(new WiredContractUpdateResultComposer(contract.Id, saved));
    }
}
