using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>Opens the WIRED Link window: [furni id]; the owner's client asks for the configuration.</summary>
public sealed class WiredLinkOpenComposer(uint furniId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkOpenComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteUInteger(BuildersClubItemIds.ToWire(furniId));
}

/// <summary>[room, furni, selected network, receive local, networks].</summary>
public sealed class WiredLinkConfigurationComposer(uint roomId, uint furniId, string selectedNetworkId, bool receiveLocal,
    IReadOnlyList<WiredLinkNetwork> networks) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkConfigurationComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
        packet.WriteUInteger(BuildersClubItemIds.ToWire(furniId));
        packet.WriteString(selectedNetworkId);
        packet.WriteBoolean(receiveLocal);
        WriteNetworks(packet, networks);
    }

    // [id, name, connection count, linked in this room].
    internal static void WriteNetworks(IOutgoingPacket packet, IReadOnlyList<WiredLinkNetwork> networks)
    {
        packet.WriteInteger(networks.Count);

        foreach (var network in networks) {
            packet.WriteString(network.WireId);
            packet.WriteString(network.Name);
            packet.WriteInteger(network.ConnectionCount);
            packet.WriteBoolean(network.LinkedInCurrentRoom);
        }
    }
}

/// <summary>
/// [room, furni, operation (1 save, 2 create, 3 rename, 4 delete, 5 connections),
/// result (0 ok), has configuration, [selected network, receive local], removed network, networks].
/// </summary>
public sealed class WiredLinkOperationResultComposer(uint roomId, uint furniId, int operation, WiredLinkResult result,
    WiredLinkEndpoint? configuration, string removedNetworkId, IReadOnlyList<WiredLinkNetwork> networks) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkOperationResultComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
        packet.WriteUInteger(BuildersClubItemIds.ToWire(furniId));
        packet.WriteInteger(operation);
        packet.WriteInteger((int)result);
        packet.WriteBoolean(configuration != null);

        if (configuration != null) {
            packet.WriteString(configuration.NetworkId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            packet.WriteBoolean(configuration.ReceiveLocal);
        }

        packet.WriteString(removedNetworkId);
        WiredLinkConfigurationComposer.WriteNetworks(packet, networks);
    }
}

/// <summary>[room, furni, network, connections (room, room name, furni, builders club, receive local)].</summary>
public sealed class WiredLinkConnectionsComposer(uint roomId, uint furniId, string networkId, IReadOnlyList<WiredLinkConnection> connections) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkConnectionsComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
        packet.WriteUInteger(BuildersClubItemIds.ToWire(furniId));
        packet.WriteString(networkId);
        packet.WriteInteger(connections.Count);

        foreach (var connection in connections) {
            packet.WriteUInteger(connection.RoomId);
            packet.WriteString(connection.RoomName);
            var wireId = BuildersClubItemIds.ToWire(connection.ItemId);
            packet.WriteUInteger(wireId);
            packet.WriteBoolean(BuildersClubItemIds.Contains(wireId));
            packet.WriteBoolean(connection.ReceiveLocal);
        }
    }
}

/// <summary>[room, furni, deleted network].</summary>
public sealed class WiredLinkNetworkInvalidatedComposer(uint roomId, uint furniId, string deletedNetworkId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkNetworkInvalidatedComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
        packet.WriteUInteger(BuildersClubItemIds.ToWire(furniId));
        packet.WriteString(deletedNetworkId);
    }
}
