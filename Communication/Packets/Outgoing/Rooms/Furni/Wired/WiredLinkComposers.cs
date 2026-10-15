using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Links;

namespace Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;

/// <summary>Opens the WIRED Link window: package_82/class_3692 -> class_4274 reads [furni id]; the owner's client asks for the configuration.</summary>
public sealed class WiredLinkOpenComposer(uint furniId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkOpenComposer;

    public void Compose(IOutgoingPacket packet) => packet.WriteUInteger(furniId);
}

/// <summary>package_82/class_3192 -> package_113/class_3325: [room, furni, selected network, receive local, networks].</summary>
public sealed class WiredLinkConfigurationComposer(uint roomId, uint furniId, string selectedNetworkId, bool receiveLocal,
    IReadOnlyList<WiredLinkNetwork> networks) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkConfigurationComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
        packet.WriteUInteger(furniId);
        packet.WriteString(selectedNetworkId);
        packet.WriteBoolean(receiveLocal);
        WriteNetworks(packet, networks);
    }

    // package_82/WiredLinkNetwork.as:5 [id, name, connection count, linked in this room].
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
/// package_82/class_3722 -> package_113/class_2472: [room, furni, operation (1 save, 2 create, 3 rename, 4 delete, 5 connections),
/// result (0 ok), has configuration, [selected network, receive local], removed network, networks].
/// </summary>
public sealed class WiredLinkOperationResultComposer(uint roomId, uint furniId, int operation, WiredLinkResult result,
    WiredLinkEndpoint? configuration, string removedNetworkId, IReadOnlyList<WiredLinkNetwork> networks) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkOperationResultComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
        packet.WriteUInteger(furniId);
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

/// <summary>package_82/class_2742 -> package_113/class_3701: [room, furni, network, connections (room, room name, furni, builders club, receive local)].</summary>
public sealed class WiredLinkConnectionsComposer(uint roomId, uint furniId, string networkId, IReadOnlyList<WiredLinkConnection> connections) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkConnectionsComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
        packet.WriteUInteger(furniId);
        packet.WriteString(networkId);
        packet.WriteInteger(connections.Count);

        foreach (var connection in connections) {
            packet.WriteUInteger(connection.RoomId);
            packet.WriteString(connection.RoomName);
            packet.WriteUInteger(connection.ItemId);
            // PlusEMU has no Builders Club furni.
            packet.WriteBoolean(false);
            packet.WriteBoolean(connection.ReceiveLocal);
        }
    }
}

/// <summary>package_82/class_2356 -> package_113/class_2440: [room, furni, deleted network].</summary>
public sealed class WiredLinkNetworkInvalidatedComposer(uint roomId, uint furniId, string deletedNetworkId) : IServerPacket
{
    public uint MessageId => ServerPacketHeader.WiredLinkNetworkInvalidatedComposer;

    public void Compose(IOutgoingPacket packet)
    {
        packet.WriteUInteger(roomId);
        packet.WriteUInteger(furniId);
        packet.WriteString(deletedNetworkId);
    }
}
