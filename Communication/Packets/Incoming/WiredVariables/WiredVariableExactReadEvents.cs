using Plus.Communication.Packets.Incoming.Rooms;
using Plus.Communication.Packets.Outgoing.WiredVariables;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.Communication.Packets.Incoming.WiredVariables;

public sealed class WiredUserVariablesRequest64Event(IWiredVariableMenuService menus) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (WiredExactReadRequest.TryRead(packet, 0, out _)) {
            menus.ShowExactSnapshot(room, session);
        }

        return Task.CompletedTask;
    }
}

public sealed class WiredVariableHoldersRequest64Event(IWiredVariableMenuService menus) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (WiredExactReadRequest.TryRead(packet, 1, out var request)) {
            menus.ShowExactHolders(room, session, request.Id);
        }

        return Task.CompletedTask;
    }
}

public sealed class WiredVariableHoldersPage64Event(IWiredVariableMenuService menus) : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        if (WiredExactReadRequest.TryRead(packet, 2, out var request)) {
            menus.ShowExactHolderPage(room, session, request.Id, request.Page, request.Size, request.Sort, request.UserType);
        }

        return Task.CompletedTask;
    }
}

internal readonly record struct WiredExactReadRequest(string Id, int Page, int Size, int Sort, int UserType)
{
    internal static bool TryRead(IIncomingPacket packet, int kind, out WiredExactReadRequest request)
    {
        request = default;

        try {
            if (packet.ReadInt() != WiredVariableWireProtocol.Version) {
                return false;
            }

            var id = kind == 0 ? "" : packet.ReadString();
            var page = kind == 2 ? packet.ReadInt() : 0;
            var size = kind == 2 ? packet.ReadInt() : 0;
            var sort = kind == 2 ? packet.ReadInt() : 0;
            var userType = kind == 2 ? packet.ReadInt() : WiredVariableHolderFilters.AllTypes;

            if (kind != 0 && id.Length is < 1 or > 64 || !WiredVariableHolderFilters.IsValid(sort, userType) || packet.HasDataRemaining()) {
                return false;
            }

            request = new(id, page, size, sort, userType);

            return true;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or IOException) {
            return false;
        }
    }
}
