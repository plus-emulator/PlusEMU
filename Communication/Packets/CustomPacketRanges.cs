namespace Plus.Communication.Packets;

public enum PacketRangeDirection
{
    ClientToServer,
    ServerToClient
}

public sealed record CustomPacketRange(string Name, PacketRangeDirection Direction, uint Start, uint End, string Feature)
{
    public bool Contains(uint id) => id >= Start && id <= End;
}

public sealed record ReservedClientPacket(string RendererConstant, uint Id, string Reason);

/// <summary>
/// Header ids that are not official in WIN63-202610121709-785561319. Every non-placeholder id below 65000 in
/// <see cref="Incoming.ClientPacketHeader"/> and <see cref="Outgoing.ServerPacketHeader"/> that the client build does not
/// register belongs to one of these ranges. The same ranges are the custom entries of Volt-Renderer
/// protocol/packet-field-contracts.json (registry.ranges); none of them holds an official id of 202610121709 or 202610131318.
/// </summary>
public static class CustomPacketRanges
{
    /// <summary>Ids of 65000 and above are placeholders for packets the client build does not carry.</summary>
    public const uint PlaceholderStart = 65000;

    public static readonly IReadOnlyList<CustomPacketRange> All =
    [
        new("client_extensions_client", PacketRangeDirection.ClientToServer, 4080, 4099, "client_extensions"),
        new("messenger_client", PacketRangeDirection.ClientToServer, 4900, 4999, "messenger"),
        new("custom_gameplay_client", PacketRangeDirection.ClientToServer, 6000, 6099, "custom_gameplay"),
        new("room_extras_client", PacketRangeDirection.ClientToServer, 6200, 6299, "room_extras"),
        new("social_client", PacketRangeDirection.ClientToServer, 7000, 7099, "social_extensions"),
        new("youtube_client", PacketRangeDirection.ClientToServer, 8000, 8099, "youtube_room"),
        new("housekeeping_client", PacketRangeDirection.ClientToServer, 9100, 9199, "housekeeping"),
        new("custom_features_client", PacketRangeDirection.ClientToServer, 9300, 9399, "custom_features"),
        new("custom_features_high_client", PacketRangeDirection.ClientToServer, 9400, 9499, "custom_features"),
        new("catalog_tools_client", PacketRangeDirection.ClientToServer, 10000, 10099, "catalog_tools"),
        new("reward_track_tools_client", PacketRangeDirection.ClientToServer, 10100, 10199, "reward_track_tools"),
        new("client_extensions_server", PacketRangeDirection.ServerToClient, 4050, 4099, "client_extensions"),
        new("messenger_server", PacketRangeDirection.ServerToClient, 4900, 4999, "messenger"),
        new("snowwar_server", PacketRangeDirection.ServerToClient, 5000, 5099, "snowwar"),
        new("room_tools_server", PacketRangeDirection.ServerToClient, 5100, 5199, "room_tools"),
        new("room_extras_server", PacketRangeDirection.ServerToClient, 5200, 5299, "room_extras"),
        new("custom_gameplay_server", PacketRangeDirection.ServerToClient, 6000, 6099, "custom_gameplay"),
        new("social_server", PacketRangeDirection.ServerToClient, 7000, 7099, "social_extensions"),
        new("youtube_server", PacketRangeDirection.ServerToClient, 8000, 8099, "youtube_room"),
        new("housekeeping_server", PacketRangeDirection.ServerToClient, 9200, 9299, "housekeeping"),
        new("custom_features_server", PacketRangeDirection.ServerToClient, 9300, 9399, "custom_features"),
        new("custom_features_high_server", PacketRangeDirection.ServerToClient, 9400, 9499, "custom_features"),
        new("catalog_tools_server", PacketRangeDirection.ServerToClient, 10000, 10099, "catalog_tools"),
        new("reward_track_tools_server", PacketRangeDirection.ServerToClient, 10100, 10199, "reward_track_tools")
    ];

    /// <summary>
    /// Custom ids Volt-Renderer can send that this server does not handle yet. They stay reserved so no other incoming
    /// header takes them; the packets are dropped until the feature exists here.
    /// </summary>
    public static readonly IReadOnlyList<ReservedClientPacket> ReservedClientPackets =
    [
        new("WIRED_CHEST_ROOM_LOGS", 9328, "Wired chest transaction log page; no transaction log is stored."),
        new("WIRED_CHEST_TRANSACTION_DETAILS", 9334, "Furni breakdown of one logged chest transaction; no transaction log is stored."),
        new("WIRED_USER_INSPECT_MOVE", 10027, "Creator tools: move an inspected user; not implemented."),
        new("WIRED_FURNI_RUNTIME_STATE_REQUEST", 10028, "Creator tools: read or write @gravity / @opacity; not implemented."),
        new("WIRED_FEATURE_CAPABILITIES", 10029, "Client capability flags; WiredFurniMoveStyleComposer is sent without a handshake."),
        new("WIRED_ARRAY_INSPECTION_REQUEST", 10034, "Array variable inspection; not implemented."),
        new("WIRED_ARRAY_INSPECTION_UPDATE", 10035, "Array variable inspection edit; not implemented.")
    ];

    public static CustomPacketRange? Find(PacketRangeDirection direction, uint id) =>
        All.FirstOrDefault(range => range.Direction == direction && range.Contains(id));
}
