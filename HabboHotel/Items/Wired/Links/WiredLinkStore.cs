using System.Data;
using System.Globalization;
using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Items.Wired.Links;

/// <summary>A WIRED Link network as the link window lists it (package_82/WiredLinkNetwork.as:5).</summary>
public sealed record WiredLinkNetwork(uint Id, string Name, int ConnectionCount, bool LinkedInCurrentRoom)
{
    public string WireId => Id.ToString(CultureInfo.InvariantCulture);
}

/// <summary>One WIRED Link furni joined to a network (package_82/WiredLinkConnection.as:5).</summary>
public sealed record WiredLinkConnection(uint RoomId, string RoomName, uint ItemId, bool ReceiveLocal);

/// <summary>The network a WIRED Link furni is joined to; network 0 is none.</summary>
public sealed record WiredLinkEndpoint(uint ItemId, uint NetworkId, bool ReceiveLocal);

public interface IWiredLinkStore
{
    IReadOnlyList<WiredLinkNetwork> Networks(int ownerId, uint roomId);
    WiredLinkEndpoint? Endpoint(uint itemId);
    IReadOnlyList<WiredLinkConnection> Connections(int ownerId, uint networkId);
    IReadOnlyList<WiredLinkConnection> Members(uint networkId);
    WiredLinkResult Select(int ownerId, uint itemId, uint roomId, uint networkId, bool receiveLocal);
    WiredLinkResult Create(int ownerId, string name, out uint networkId);
    WiredLinkResult Rename(int ownerId, uint networkId, string name);
    WiredLinkResult Delete(int ownerId, uint networkId, out IReadOnlyList<WiredLinkConnection> detached);
}

/// <summary>Result codes the link window reads (WiredLinkController): 0 ok, 3 invalid name, 5 cannot connect; others are refusals.</summary>
public enum WiredLinkResult
{
    Ok = 0,
    NotFound = 1,
    LimitReached = 2,
    InvalidName = 3,
    Denied = 4,
    CannotConnect = 5
}

/// <summary>
/// Owner-scoped networks of WIRED Link furni, which may stand in different rooms. Endpoint rooms are read from the items table,
/// so a picked-up or moved furni follows its row; a furni deleted from the hotel leaves its network through the foreign key.
/// </summary>
public sealed class DatabaseWiredLinkStore(IDatabase database) : IWiredLinkStore
{
    public const int MaxNetworksPerOwner = 100;
    public const int MaxNameLength = 255;

    private const string MemberSelect = "SELECT i.room_id AS RoomId, COALESCE(r.caption, '') AS RoomName, e.item_id AS ItemId, e.receive_local AS ReceiveLocal "
        + "FROM wired_link_endpoints e JOIN items i ON i.id = e.item_id LEFT JOIN rooms r ON r.id = i.room_id ";

    public static bool ValidName(string name) => name.Length <= MaxNameLength && name.Trim().Length > 0;

    public IReadOnlyList<WiredLinkNetwork> Networks(int ownerId, uint roomId)
    {
        using var connection = database.Connection();

        return connection.Query<NetworkRow>("SELECT n.id AS Id, n.name AS Name, "
            + "(SELECT COUNT(*) FROM wired_link_endpoints e WHERE e.network_id = n.id) AS ConnectionCount, "
            + "EXISTS(SELECT 1 FROM wired_link_endpoints e JOIN items i ON i.id = e.item_id WHERE e.network_id = n.id AND i.room_id = @RoomId) AS Linked "
            + "FROM wired_link_networks n WHERE n.owner_id = @OwnerId ORDER BY n.id", new { OwnerId = ownerId, RoomId = roomId })
            .Select(row => new WiredLinkNetwork(row.Id, row.Name, (int)row.ConnectionCount, row.Linked)).ToArray();
    }

    public WiredLinkEndpoint? Endpoint(uint itemId)
    {
        using var connection = database.Connection();

        return connection.QuerySingleOrDefault<EndpointRow>("SELECT item_id AS ItemId, network_id AS NetworkId, receive_local AS ReceiveLocal "
            + "FROM wired_link_endpoints WHERE item_id = @ItemId", new { ItemId = itemId }) is { } row
            ? new(row.ItemId, row.NetworkId, row.ReceiveLocal) : null;
    }

    public IReadOnlyList<WiredLinkConnection> Connections(int ownerId, uint networkId)
    {
        using var connection = database.Connection();

        return connection.Query<ConnectionRow>(MemberSelect + "JOIN wired_link_networks n ON n.id = e.network_id "
            + "WHERE e.network_id = @NetworkId AND n.owner_id = @OwnerId ORDER BY i.room_id, e.item_id", new { NetworkId = networkId, OwnerId = ownerId })
            .Select(row => row.Connection).ToArray();
    }

    public IReadOnlyList<WiredLinkConnection> Members(uint networkId)
    {
        using var connection = database.Connection();

        return connection.Query<ConnectionRow>(MemberSelect + "WHERE e.network_id = @NetworkId AND i.room_id > 0 ORDER BY e.item_id",
            new { NetworkId = networkId }).Select(row => row.Connection).ToArray();
    }

    public WiredLinkResult Select(int ownerId, uint itemId, uint roomId, uint networkId, bool receiveLocal) => InTransaction((connection, transaction) =>
    {
        if (networkId == 0) {
            connection.Execute("DELETE FROM wired_link_endpoints WHERE item_id = @ItemId", new { ItemId = itemId }, transaction);

            return WiredLinkResult.Ok;
        }

        if (connection.ExecuteScalar<int?>("SELECT owner_id FROM wired_link_networks WHERE id = @Id FOR UPDATE", new { Id = networkId }, transaction) != ownerId) {
            return WiredLinkResult.NotFound;
        }

        // A room joins a network through one furni only (WiredLinkController.canConnect).
        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM wired_link_endpoints e JOIN items i ON i.id = e.item_id "
            + "WHERE e.network_id = @NetworkId AND i.room_id = @RoomId AND e.item_id <> @ItemId", new { NetworkId = networkId, RoomId = roomId, ItemId = itemId }, transaction) > 0) {
            return WiredLinkResult.CannotConnect;
        }

        connection.Execute("INSERT INTO wired_link_endpoints (item_id, network_id, receive_local) VALUES (@ItemId, @NetworkId, @ReceiveLocal) "
            + "ON DUPLICATE KEY UPDATE network_id = @NetworkId, receive_local = @ReceiveLocal",
            new { ItemId = itemId, NetworkId = networkId, ReceiveLocal = receiveLocal }, transaction);

        return WiredLinkResult.Ok;
    });

    public WiredLinkResult Create(int ownerId, string name, out uint networkId)
    {
        uint created = 0;
        var result = ValidName(name) ? InTransaction((connection, transaction) =>
        {
            if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM wired_link_networks WHERE owner_id = @OwnerId FOR UPDATE", new { OwnerId = ownerId }, transaction)
                >= MaxNetworksPerOwner) {
                return WiredLinkResult.LimitReached;
            }

            created = connection.ExecuteScalar<uint>("INSERT INTO wired_link_networks (owner_id, name) VALUES (@OwnerId, @Name); SELECT LAST_INSERT_ID();",
                new { OwnerId = ownerId, Name = name.Trim() }, transaction);

            return WiredLinkResult.Ok;
        }) : WiredLinkResult.InvalidName;
        networkId = created;

        return result;
    }

    public WiredLinkResult Rename(int ownerId, uint networkId, string name)
    {
        if (!ValidName(name)) {
            return WiredLinkResult.InvalidName;
        }

        using var connection = database.Connection();

        return connection.Execute("UPDATE wired_link_networks SET name = @Name WHERE id = @Id AND owner_id = @OwnerId",
            new { Name = name.Trim(), Id = networkId, OwnerId = ownerId }) > 0 || Owned(connection, ownerId, networkId)
            ? WiredLinkResult.Ok : WiredLinkResult.NotFound;
    }

    public WiredLinkResult Delete(int ownerId, uint networkId, out IReadOnlyList<WiredLinkConnection> detached)
    {
        IReadOnlyList<WiredLinkConnection> members = [];
        var result = InTransaction((connection, transaction) =>
        {
            if (connection.ExecuteScalar<int?>("SELECT owner_id FROM wired_link_networks WHERE id = @Id FOR UPDATE", new { Id = networkId }, transaction) != ownerId) {
                return WiredLinkResult.NotFound;
            }

            members = connection.Query<ConnectionRow>(MemberSelect + "WHERE e.network_id = @NetworkId", new { NetworkId = networkId }, transaction).Select(row => row.Connection).ToArray();
            connection.Execute("DELETE FROM wired_link_endpoints WHERE network_id = @Id", new { Id = networkId }, transaction);
            connection.Execute("DELETE FROM wired_link_networks WHERE id = @Id", new { Id = networkId }, transaction);

            return WiredLinkResult.Ok;
        });
        detached = members;

        return result;
    }

    private static bool Owned(IDbConnection connection, int ownerId, uint networkId) =>
        connection.ExecuteScalar<int>("SELECT COUNT(*) FROM wired_link_networks WHERE id = @Id AND owner_id = @OwnerId", new { Id = networkId, OwnerId = ownerId }) > 0;

    private WiredLinkResult InTransaction(Func<IDbConnection, IDbTransaction, WiredLinkResult> work)
    {
        using var connection = database.Connection();

        if (connection.State != ConnectionState.Open) {
            connection.Open();
        }

        using var transaction = connection.BeginTransaction();
        var result = work(connection, transaction);

        if (result == WiredLinkResult.Ok) {
            transaction.Commit();
        }

        return result;
    }

    private sealed class NetworkRow
    {
        public uint Id { get; set; }
        public string Name { get; set; } = "";
        public long ConnectionCount { get; set; }
        public bool Linked { get; set; }
    }

    private sealed class ConnectionRow
    {
        public long RoomId { get; set; }
        public string RoomName { get; set; } = "";
        public long ItemId { get; set; }
        public bool ReceiveLocal { get; set; }
        public WiredLinkConnection Connection => new((uint)Math.Max(0, RoomId), RoomName, (uint)ItemId, ReceiveLocal);
    }

    private sealed class EndpointRow
    {
        public uint ItemId { get; set; }
        public uint NetworkId { get; set; }
        public bool ReceiveLocal { get; set; }
    }
}
