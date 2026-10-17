using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;

/// <summary>WIRED Link window requests (package_129, WIN63-202610131318-860371703). A body with extra or missing fields is ignored.</summary>
public abstract class WiredLinkEvent : RoomPacketEvent
{
    public override Task Parse(Room room, GameClient session, IIncomingPacket packet)
    {
        try {
            var furniId = BuildersClubItemIds.FromWire(packet.ReadUInt());
            var handle = Read(packet);

            if (!packet.HasDataRemaining()) {
                handle(room, session, furniId);
            }
        }
        catch (Exception error) when (error is ArgumentException or IndexOutOfRangeException) {
        }

        return Task.CompletedTask;
    }

    protected abstract Action<Room, GameClient, uint> Read(IIncomingPacket packet);
}

/// <summary>class_2716: [furni id].</summary>
public sealed class WiredLinkRequestConfigEvent : WiredLinkEvent
{
    protected override Action<Room, GameClient, uint> Read(IIncomingPacket packet) => WiredLinkController.SendConfiguration;
}

/// <summary>class_2533: [furni id, network id, receive local].</summary>
public sealed class WiredLinkSaveSelectionEvent : WiredLinkEvent
{
    protected override Action<Room, GameClient, uint> Read(IIncomingPacket packet)
    {
        var network = packet.ReadString();
        var receiveLocal = packet.ReadBool();

        return (room, session, furni) => WiredLinkController.SaveSelection(room, session, furni, network, receiveLocal);
    }
}

/// <summary>class_3715: [furni id, name].</summary>
public sealed class WiredLinkCreateNetworkEvent : WiredLinkEvent
{
    protected override Action<Room, GameClient, uint> Read(IIncomingPacket packet)
    {
        var name = packet.ReadString();

        return (room, session, furni) => WiredLinkController.CreateNetwork(room, session, furni, name);
    }
}

/// <summary>class_3841: [furni id, network id, new name].</summary>
public sealed class WiredLinkRenameNetworkEvent : WiredLinkEvent
{
    protected override Action<Room, GameClient, uint> Read(IIncomingPacket packet)
    {
        var network = packet.ReadString();
        var name = packet.ReadString();

        return (room, session, furni) => WiredLinkController.RenameNetwork(room, session, furni, network, name);
    }
}

/// <summary>class_3622: [furni id, network id].</summary>
public sealed class WiredLinkDeleteNetworkEvent : WiredLinkEvent
{
    protected override Action<Room, GameClient, uint> Read(IIncomingPacket packet)
    {
        var network = packet.ReadString();

        return (room, session, furni) => WiredLinkController.DeleteNetwork(room, session, furni, network);
    }
}

/// <summary>class_3524: [furni id, network id].</summary>
public sealed class WiredLinkGetConnectionsEvent : WiredLinkEvent
{
    protected override Action<Room, GameClient, uint> Read(IIncomingPacket packet)
    {
        var network = packet.ReadString();

        return (room, session, furni) => WiredLinkController.SendConnections(room, session, furni, network);
    }
}
