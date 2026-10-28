using Plus.Communication.Packets.Incoming.Rooms;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.WiredVariables;

public sealed class WiredGetAllVariableHoldersEvent(IWiredVariableHolderService holders) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (WiredVariableHolderRequests.TryReadId(packet, out var id)) {
            holders.ShowAllHolders(room, session, id);
        }

        return Task.CompletedTask;
    }
}

public sealed class WiredDeleteAllVariableHoldersEvent(IWiredVariableHolderService holders) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (WiredVariableHolderRequests.TryReadId(packet, out var id)) {
            holders.DeleteAllHolders(room, session, id);
        }

        return Task.CompletedTask;
    }
}

public sealed class WiredGetVariableOwnersPageEvent(IWiredVariableHolderService holders) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        string id;
        int page, size, sort, userType;

        try {
            id = packet.ReadString();
            page = packet.ReadInt();
            size = packet.ReadInt();
            sort = packet.ReadInt();
            userType = packet.ReadInt();
        }
        catch (ArgumentException) {
            return Task.CompletedTask;
        }

        if (id.Length <= 64 && !packet.HasDataRemaining()) {
            holders.ShowOwnersPage(room, session, id, page, size, sort, userType);
        }

        return Task.CompletedTask;
    }
}

public sealed class WiredGetUserPermanentVariablesEvent(IWiredVariableHolderService holders) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        int entityType, entityId;

        try {
            entityType = packet.ReadInt();
            entityId = packet.ReadInt();
        }
        catch (ArgumentException) {
            return Task.CompletedTask;
        }

        if (!packet.HasDataRemaining()) {
            holders.ShowPermanentVariables(room, session, entityType, entityId);
        }

        return Task.CompletedTask;
    }
}

public sealed class WiredSetUserPermanentVariableEvent(IWiredVariableHolderService holders) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        int entityType, entityId, value, action;
        string id;

        try {
            entityType = packet.ReadInt();
            entityId = packet.ReadInt();
            id = packet.ReadString();
            value = packet.ReadInt();
            action = packet.ReadInt();
        }
        catch (ArgumentException) {
            return Task.CompletedTask;
        }

        if (id.Length <= 64 && !packet.HasDataRemaining()) {
            holders.SetPermanentVariable(room, session, new(entityType, entityId, id, value, action));
        }

        return Task.CompletedTask;
    }
}

internal static class WiredVariableHolderRequests
{
    public static bool TryReadId(IIncomingPacket packet, out string id)
    {
        id = "";

        try {
            id = packet.ReadString();
        }
        catch (ArgumentException) {
            return false;
        }

        return id.Length <= 64 && !packet.HasDataRemaining();
    }
}
