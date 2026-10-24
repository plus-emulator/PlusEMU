using System.Data;
using System.Globalization;
using Dapper;
using Plus.Database;

namespace Plus.HabboHotel.Items.Wired.Links;

/// <summary>A WIRED Link network as the link window lists it.</summary>
public sealed record WiredLinkNetwork(uint Id, string Name, int ConnectionCount, bool LinkedInCurrentRoom)
{
    public string WireId => Id.ToString(CultureInfo.InvariantCulture);
}

/// <summary>One WIRED Link furni joined to a network.</summary>
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

/// <summary>
/// Result codes of the link window. The client shows them as ${wiredlink.error.N}.
/// </summary>
public enum WiredLinkResult
{
    Ok = 0,
    OwnerOnly = 1,
    NetworkMissing = 2,
    InvalidName = 3,
    DuplicateName = 4,
    AlreadyConnected = 5,
    InvalidNetworkId = 6,
    LinkUnavailable = 7,
    NetworkLimit = 8
}

/// <summary>
/// Owner-scoped networks of WIRED Link furni, which may stand in different rooms. Endpoint rooms are read from the items table,
/// so a picked-up or moved furni follows its row; a furni deleted from the hotel leaves its network through the foreign key. A deleted network keeps its endpoints without a network.
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

        return connection.QuerySingleOrDefault<EndpointRow>("SELECT item_id AS ItemId, COALESCE(network_id, 0) AS NetworkId, receive_local AS ReceiveLocal "
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
            connection.Execute("INSERT INTO wired_link_endpoints (item_id, network_id, receive_local) VALUES (@ItemId, NULL, @ReceiveLocal) "
                + "ON DUPLICATE KEY UPDATE network_id = NULL, receive_local = @ReceiveLocal", new { ItemId = itemId, ReceiveLocal = receiveLocal }, transaction);

            return WiredLinkResult.Ok;
        }

        if (connection.ExecuteScalar<int?>("SELECT owner_id FROM wired_link_networks WHERE id = @Id FOR UPDATE", new { Id = networkId }, transaction) != ownerId) {
            return WiredLinkResult.NetworkMissing;
        }

        // A room joins a network through one furni only.
        if (connection.ExecuteScalar<int>("SELECT COUNT(*) FROM wired_link_endpoints e JOIN items i ON i.id = e.item_id "
            + "WHERE e.network_id = @NetworkId AND i.room_id = @RoomId AND e.item_id <> @ItemId", new { NetworkId = networkId, RoomId = roomId, ItemId = itemId }, transaction) > 0) {
            return WiredLinkResult.AlreadyConnected;
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
                return WiredLinkResult.NetworkLimit;
            }

            if (NameTaken(connection, transaction, ownerId, name.Trim(), 0)) {
                return WiredLinkResult.DuplicateName;
            }

            created = connection.ExecuteScalar<uint>("INSERT INTO wired_link_networks (owner_id, name) VALUES (@OwnerId, @Name); SELECT LAST_INSERT_ID();",
                new { OwnerId = ownerId, Name = name.Trim() }, transaction);

            return WiredLinkResult.Ok;
        }) : WiredLinkResult.InvalidName;
        networkId = created;

        return result;
    }

    public WiredLinkResult Rename(int ownerId, uint networkId, string name) => !ValidName(name) ? WiredLinkResult.InvalidName
        : InTransaction((connection, transaction) =>
        {
            if (connection.ExecuteScalar<int?>("SELECT owner_id FROM wired_link_networks WHERE id = @Id FOR UPDATE", new { Id = networkId }, transaction) != ownerId) {
                return WiredLinkResult.NetworkMissing;
            }

            if (NameTaken(connection, transaction, ownerId, name.Trim(), networkId)) {
                return WiredLinkResult.DuplicateName;
            }

            connection.Execute("UPDATE wired_link_networks SET name = @Name WHERE id = @Id", new { Name = name.Trim(), Id = networkId }, transaction);

            return WiredLinkResult.Ok;
        });

    public WiredLinkResult Delete(int ownerId, uint networkId, out IReadOnlyList<WiredLinkConnection> detached)
    {
        IReadOnlyList<WiredLinkConnection> members = [];
        var result = InTransaction((connection, transaction) =>
        {
            if (connection.ExecuteScalar<int?>("SELECT owner_id FROM wired_link_networks WHERE id = @Id FOR UPDATE", new { Id = networkId }, transaction) != ownerId) {
                return WiredLinkResult.NetworkMissing;
            }

            members = connection.Query<ConnectionRow>(MemberSelect + "WHERE e.network_id = @NetworkId", new { NetworkId = networkId }, transaction).Select(row => row.Connection).ToArray();
            connection.Execute("DELETE FROM wired_link_networks WHERE id = @Id", new { Id = networkId }, transaction);

            return WiredLinkResult.Ok;
        });
        detached = members;

        return result;
    }

    // The column's utf8mb4_unicode_ci collation compares names without case, as the duplicate-name text expects.
    private static bool NameTaken(IDbConnection connection, IDbTransaction transaction, int ownerId, string name, uint exceptId) =>
        connection.ExecuteScalar<int>("SELECT COUNT(*) FROM wired_link_networks WHERE owner_id = @OwnerId AND name = @Name AND id <> @ExceptId",
            new { OwnerId = ownerId, Name = name, ExceptId = exceptId }, transaction) > 0;

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
