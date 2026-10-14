using System.Globalization;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Links;

/// <summary>
/// The WIRED Link window (roomevents/wired_link/WiredLinkController.as:41, WIN63-202610131318-860371703). Only the furni's owner
/// edits it (ownsTarget, :130); networks belong to that owner and may join furni in any of their rooms.
/// </summary>
public static class WiredLinkController
{
    // WiredLinkController operation constants, echoed in the result packet.
    public const int Save = 1;
    public const int Create = 2;
    public const int Rename = 3;
    public const int Delete = 4;
    public const int Connections = 5;

    private static bool TryOwned(Room room, GameClient session, uint furniId, out Item item, out int ownerId)
    {
        ownerId = session.GetHabbo()?.Id ?? 0;
        item = room.GetRoomItemHandler().GetItem(furniId)!;

        return item != null && WiredLinkTransport.IsLink(item) && ownerId > 0 && item.OwnerId == (uint)ownerId
            && ReferenceEquals(session.GetHabbo().CurrentRoom, room);
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
        if (!TryOwned(room, session, furniId, out var item, out var ownerId) || !TryParseNetwork(networkId, out var network)) {
            return;
        }

        var result = room.GetWired().LinkStore.Select(ownerId, item.Id, room.Id, network, receiveLocal);
        Reply(room, session, item, ownerId, Save, result, "");
    }

    public static void CreateNetwork(Room room, GameClient session, uint furniId, string name)
    {
        if (!TryOwned(room, session, furniId, out var item, out var ownerId)) {
            return;
        }

        Reply(room, session, item, ownerId, Create, room.GetWired().LinkStore.Create(ownerId, name, out _), "");
    }

    public static void RenameNetwork(Room room, GameClient session, uint furniId, string networkId, string name)
    {
        if (!TryOwned(room, session, furniId, out var item, out var ownerId) || !TryParseNetwork(networkId, out var network) || network == 0) {
            return;
        }

        Reply(room, session, item, ownerId, Rename, room.GetWired().LinkStore.Rename(ownerId, network, name), "");
    }

    public static void DeleteNetwork(Room room, GameClient session, uint furniId, string networkId)
    {
        if (!TryOwned(room, session, furniId, out var item, out var ownerId) || !TryParseNetwork(networkId, out var network) || network == 0) {
            return;
        }

        var result = room.GetWired().LinkStore.Delete(ownerId, network, out var detached);
        Reply(room, session, item, ownerId, Delete, result, result == WiredLinkResult.Ok ? networkId : "");

        // Other open windows on the network's furni learn that it is gone (package_82/class_2356).
        foreach (var connection in detached.Where(connection => connection.ItemId != item.Id)) {
            session.Send(new WiredLinkNetworkInvalidatedComposer(connection.RoomId, connection.ItemId, networkId));
        }
    }

    public static void SendConnections(Room room, GameClient session, uint furniId, string networkId)
    {
        if (!TryOwned(room, session, furniId, out var item, out var ownerId) || !TryParseNetwork(networkId, out var network) || network == 0) {
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
