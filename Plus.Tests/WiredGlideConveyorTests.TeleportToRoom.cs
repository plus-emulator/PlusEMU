using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Teleport to Room glows the user with effect 234 from the firing and takes them into the linked room about 950 ms later.
    [Fact]
    public void TeleportToRoomGlowsTheUserBeforeTheForward()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_teleport_to_room", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            Text = "42",
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
            (22, 4, 12, 0.65, 0, "wf_act_teleport_to_room", "native:" + JsonSerializer.Serialize(native))
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var packets = CapturePublication(rider);
        var wakes = new List<long>();
        var switches = 0;
        using var switchRoom = WiredRoomForwarding.UseRoomSwitch(_ => switches++);
        f.SetEngineCallback("<ActionDue>k__BackingField", (Action<long>)wakes.Add);
        bool Forwarded() => switches > 0;

        var firedAt = f.Engine.Now;
        f.Owned(() => rider.OnChat(0, "pulse", false));

        // The forward wakes the room at its own due time instead of waiting for the next 50 ms step.
        Assert.Contains(firedAt + 950, wakes);
        f.Advance(200);
        Assert.Equal(234, rider.CurrentEffect);
        Assert.False(Forwarded());

        f.Advance(650);
        Assert.False(Forwarded());
        f.Advance(150);
        Assert.True(Forwarded());
        Assert.DoesNotContain(packets, bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.RoomForwardComposer);
        Assert.Equal(0, rider.GetClient()!.GetHabbo()!.ForwardArrivalRestoreEffect);

        // The server takes the user away with the glow on: leaving does not put the old effect back.
        Assert.Equal(234, rider.CurrentEffect);
        f.Advance(2500);
        Assert.Equal(234, rider.CurrentEffect);
    }

    private sealed class LinkedPairs(uint partnerRoom = 0) : IItemTravelStore
    {
        public readonly Dictionary<uint, uint> Partners = [];
        public uint FindOtherHopperRoom(uint roomId) => 0;
        public uint FindHopper(uint roomId) => 0;
        public uint FindLinkedTeleporter(uint itemId) => Partners.GetValueOrDefault(itemId);
        public uint FindItemRoom(uint itemId) => partnerRoom;
        public void RegisterHopper(uint itemId, uint roomId) { }
        public void RemoveHopper(uint itemId, uint roomId) { }
    }

    private static (Fixture Fixture, RoomUser Rider, Func<int> Switches, List<byte[]> Packets, IDisposable Scope) TeleportToRoomWithLinkers(uint[] picks, uint partnerRoom = 0)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_teleport_to_room", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            PrimaryItems = [.. picks.Select(id => new WiredNativeItemReference(id, false))],
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            Delay = 0
        };
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (21, 4, 12, 0.0, 0, "wf_trg_says_something", JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = "pulse" })),
            (22, 4, 12, 0.65, 0, "wf_act_teleport_to_room", "native:" + JsonSerializer.Serialize(native)),
            (31, 5, 6, 0.0, 2, "wired_room_linker", null),
            (32, 9, 6, 0.0, 5, "wired_room_linker", null)
        };
        var travel = new LinkedPairs(partnerRoom) { Partners = { [31] = 32, [32] = 31 } };
        var f = new Fixture(layout, live: true, travel: travel);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var packets = CapturePublication(rider);
        var switches = 0;
        var scope = WiredRoomForwarding.UseRoomSwitch(_ => switches++);

        return (f, rider, () => switches, packets, scope);
    }

    private static (int X, int Y, int Head, int Body)? LastUserUpdate(IEnumerable<byte[]> packets, int virtualId)
    {
        (int, int, int, int)? last = null;

        foreach (var bytes in packets.Where(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.UserUpdateComposer)) {
            var packet = new FlashIncomingPacket { Buffer = bytes.AsMemory(6) };
            var count = packet.ReadInt();

            for (var index = 0; index < count; index++) {
                var id = packet.ReadInt();
                var (x, y) = (packet.ReadInt(), packet.ReadInt());
                packet.ReadString();
                var (head, body) = (packet.ReadInt(), packet.ReadInt());
                packet.ReadInt();
                packet.ReadString();

                if (id == virtualId) {
                    last = (x, y, head, body);
                }
            }
        }

        return last;
    }

    // A linker whose partner stands in the same room moves the user onto the partner tile, facing the partner, without a room switch.
    [Fact]
    public void TeleportToRoomMovesTheUserToASameRoomPartnerLinker()
    {
        var (f, rider, switches, packets, scope) = TeleportToRoomWithLinkers([31]);
        using var switchRoom = scope;
        f.Owned(() => rider.OnChat(0, "pulse", false));

        f.Advance(900);
        Assert.Equal((4, 11), (rider.X, rider.Y));
        Assert.Equal(234, rider.CurrentEffect);

        f.Advance(100);
        Assert.Equal((9, 6), (rider.X, rider.Y));
        Assert.Equal((5, 5), (rider.RotHead, rider.RotBody));
        Assert.Equal((9, 6, 5, 5), LastUserUpdate(packets, rider.VirtualId));
        Assert.Equal(0, switches());
        Assert.DoesNotContain(packets, bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.RoomForwardComposer);
        Assert.False(rider.GetClient()!.GetHabbo()!.IsTeleporting);

        f.Advance(2500);
        Assert.NotEqual(234, rider.CurrentEffect);
        Assert.Equal((9, 6), (rider.X, rider.Y));
    }

    // The stored order is the Java `HashSet` order: id 32 sits in bucket 0 and id 31 in bucket 15, so 31 is last and its partner (32) is the landing.
    [Theory]
    [InlineData(new uint[] { 31, 32 })]
    [InlineData(new uint[] { 32, 31 })]
    public void TeleportToRoomLandsOnThePartnerOfTheLastLinkerInStoredOrder(uint[] picks)
    {
        var (f, rider, switches, _, scope) = TeleportToRoomWithLinkers(picks);
        using var switchRoom = scope;
        f.Owned(() => rider.OnChat(0, "pulse", false));
        f.Advance(1000);

        Assert.Equal((9, 6, 5), (rider.X, rider.Y, rider.RotBody));
        Assert.Equal(0, switches());
    }

    [Fact]
    public void TeleportToRoomStillForwardsWhenThePartnerLinkerIsInAnotherRoom()
    {
        var (f, rider, switches, packets, scope) = TeleportToRoomWithLinkers([31], partnerRoom: 99);
        using var switchRoom = scope;
        f.Items[32].RoomId = 99;
        f.Items.TryRemove(32, out _);
        f.Owned(() => rider.OnChat(0, "pulse", false));

        f.Advance(900);
        Assert.Equal(0, switches());
        f.Advance(100);
        Assert.Equal(1, switches());
        Assert.Equal((4, 11), (rider.X, rider.Y));
        Assert.DoesNotContain(packets, bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.RoomForwardComposer);
    }
}
