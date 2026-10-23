using System.Globalization;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Links;

/// <summary>
/// The WIRED Link window. Only the furni's owner
/// edits it; networks belong to that owner and may join furni in any of their rooms.
/// </summary>
public static class WiredLinkController
{
    // WiredLinkController operation constants, echoed in the result packet. Result codes are WiredLinkResult.
    public const int Save = 1;
    public const int Create = 2;
    public const int Rename = 3;
    public const int Delete = 4;
    public const int Connections = 5;

    private static bool InRoom(Room room, GameClient session) => ReferenceEquals(session.GetHabbo()?.CurrentRoom, room);

    /// <summary>7 when the furni is no WIRED Link of this room, 1 when the user does not own it (wiredlink.error.7, .1).</summary>
    private static WiredLinkResult Owned(Room room, GameClient session, uint furniId, out Item item, out int ownerId)
    {
        ownerId = session.GetHabbo()?.Id ?? 0;
        item = room.GetRoomItemHandler().GetItem(furniId)!;

        return item == null || !WiredLinkTransport.IsLink(item) ? WiredLinkResult.LinkUnavailable
            : ownerId <= 0 || item.OwnerId != (uint)ownerId ? WiredLinkResult.OwnerOnly : WiredLinkResult.Ok;
    }

    private static bool TryOwned(Room room, GameClient session, uint furniId, out Item item, out int ownerId) =>
        Owned(room, session, furniId, out item, out ownerId) == WiredLinkResult.Ok && InRoom(room, session);

    /// <summary>An operation's admission: the furni and its owner, then the network id; a refusal is answered with its code.</summary>
    private static bool Admit(Room room, GameClient session, uint furniId, int operation, string? networkId, out Item item, out int ownerId,
        out uint network)
    {
        network = 0;

        if (!InRoom(room, session)) {
            item = null!;
            ownerId = 0;

            return false;
        }

        var result = Owned(room, session, furniId, out item, out ownerId);

        // wiredlink.error.6: an id that is not a canonical number, or no network where one is needed.
        if (result == WiredLinkResult.Ok && networkId != null
            && (!TryParseNetwork(networkId, out network) || network == 0 && operation != Save)) {
            result = WiredLinkResult.InvalidNetworkId;
        }

        if (result == WiredLinkResult.Ok) {
            return true;
        }

        // A furni the user may not configure gets no networks back.
        session.Send(new WiredLinkOperationResultComposer(room.Id, furniId, operation, result, null, "",
            result == WiredLinkResult.InvalidNetworkId ? room.GetWired().LinkStore.Networks(ownerId, room.Id) : []));

        return false;
    }

    public static bool TryParseNetwork(string id, out uint networkId) =>
        uint.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out networkId) && networkId.ToString(CultureInfo.InvariantCulture) == id;

    /// <summary>The furni was used: its owner's client opens the window and asks for the configuration.</summary>
    public static void Open(GameClient session, Item item)
    {
        if (session.GetHabbo() is { } habbo && item.OwnerId == (uint)habbo.Id) {
            session.Send(new WiredLinkOpenComposer(item.Id));
        }
    }

    public static void SendConfiguration(Room room, GameClient session, uint furniId)
    {
        if (!TryOwned(room, session, furniId, out var item, out var ownerId)) {
            return;
        }

        var store = room.GetWired().LinkStore;
        var endpoint = store.Endpoint(item.Id);
        session.Send(new WiredLinkConfigurationComposer(room.Id, item.Id, (endpoint?.NetworkId ?? 0).ToString(CultureInfo.InvariantCulture),
            endpoint?.ReceiveLocal ?? false, store.Networks(ownerId, room.Id)));
    }

    public static void SaveSelection(Room room, GameClient session, uint furniId, string networkId, bool receiveLocal)
    {
        if (!Admit(room, session, furniId, Save, networkId, out var item, out var ownerId, out var network)) {
            return;
        }

        var result = room.GetWired().LinkStore.Select(ownerId, item.Id, room.Id, network, receiveLocal);
        Reply(room, session, item, ownerId, Save, result, "");
    }

    public static void CreateNetwork(Room room, GameClient session, uint furniId, string name)
    {
        if (!Admit(room, session, furniId, Create, null, out var item, out var ownerId, out _)) {
            return;
        }

        Reply(room, session, item, ownerId, Create, room.GetWired().LinkStore.Create(ownerId, name, out _), "");
    }

    public static void RenameNetwork(Room room, GameClient session, uint furniId, string networkId, string name)
    {
        if (!Admit(room, session, furniId, Rename, networkId, out var item, out var ownerId, out var network)) {
            return;
        }

        Reply(room, session, item, ownerId, Rename, room.GetWired().LinkStore.Rename(ownerId, network, name), "");
    }

    public static void DeleteNetwork(Room room, GameClient session, uint furniId, string networkId)
    {
        if (!Admit(room, session, furniId, Delete, networkId, out var item, out var ownerId, out var network)) {
            return;
        }

        var result = room.GetWired().LinkStore.Delete(ownerId, network, out var detached);
        Reply(room, session, item, ownerId, Delete, result, result == WiredLinkResult.Ok ? networkId : "");

        // Other open windows on the network's furni learn that it is gone.
        foreach (var connection in detached.Where(connection => connection.ItemId != item.Id)) {
            session.Send(new WiredLinkNetworkInvalidatedComposer(connection.RoomId, connection.ItemId, networkId));
        }
    }

    public static void SendConnections(Room room, GameClient session, uint furniId, string networkId)
    {
        if (!Admit(room, session, furniId, Connections, networkId, out var item, out var ownerId, out var network)) {
            return;
        }

        session.Send(new WiredLinkConnectionsComposer(room.Id, item.Id, networkId, room.GetWired().LinkStore.Connections(ownerId, network)));
    }

    private static void Reply(Room room, GameClient session, Item item, int ownerId, int operation, WiredLinkResult result, string removed)
    {
        var store = room.GetWired().LinkStore;
        session.Send(new WiredLinkOperationResultComposer(room.Id, item.Id, operation, result,
            store.Endpoint(item.Id) ?? new(item.Id, 0, false), removed, store.Networks(ownerId, room.Id)));
    }
}
