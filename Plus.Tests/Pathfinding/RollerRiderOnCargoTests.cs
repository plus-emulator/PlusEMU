using System.Globalization;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // The roller is 0.5 high and the cargo 0.25 high.
    [Fact]
    public void ARollerCarriesAUserStandingOnItsCargoInOneSlideAtTheCarriedHeight()
    {
        ExecutorRoller(10, 0, 1);
        var cargo = ExecutorFloor(12, 0, 1, z: 0.5, height: 0.25);
        var actor = ExecutorRollerActor(0, 1, 0.75);
        EnableExecutorRollers();
        ExecutorTick();
        var slides = RollerBundles();
        var slide = Assert.Single(slides);
        Assert.Equal(((0, 1), (1, 1), 10u), (slide.From, slide.To, slide.Roller));
        Assert.Equal([(12u, "0.5", "0.0")], slide.Objects);
        Assert.Equal((actor.VirtualId, "0.75", "0.25"), slide.Avatar);
        Assert.Equal((1, 1, 0.25), (actor.X, actor.Y, Math.Round(actor.Z, 3)));
        Assert.Equal((1, 1, 0d), (cargo.GetX, cargo.GetY, cargo.GetZ));
    }

    // Several cargo of one roller slide in one bundle, lowest first, when no user rides them.
    [Fact]
    public void ARollerSlidesSeveralCargoWithoutARiderInOneBundle()
    {
        ExecutorRoller(10, 0, 1);
        ExecutorFloor(12, 0, 1, z: 0.5, height: 0.25);
        ExecutorFloor(13, 0, 1, z: 0.75, height: 0.25);
        ExecutorFloor(14, 0, 1, z: 1, height: 0.25);
        ExecutorActor(3, 3);
        EnableExecutorRollers();
        ExecutorTick();
        var slide = Assert.Single(RollerBundles());
        Assert.Equal(((0, 1), (1, 1), 10u), (slide.From, slide.To, slide.Roller));
        Assert.Equal([(12u, "0.5", "0.0"), (13u, "0.75", "0.25"), (14u, "1.0", "0.5")], slide.Objects);
        Assert.Null(slide.Avatar);
    }

    // A seat is 1 high: its sitter rides at the seat top.
    [Fact]
    public void ARollerCarriesAUserSittingOnItsCargoSeat()
    {
        ExecutorRoller(10, 0, 1);
        var seat = Furni(12, InteractionType.None, WiredBoxType.None);
        seat.Definition.Height = 1;
        seat.Definition.IsSeat = true;
        seat.Definition.Stackable = false;
        seat.Definition.Walkable = false;
        seat.Definition.Directions = [0, 2, 4, 6];
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, seat, 0, 1, 2, true, false, false, height: 0.5));
        var actor = ExecutorRollerActor(0, 1, 1.5);
        EnableExecutorRollers();
        ExecutorTick();
        var slide = Assert.Single(RollerBundles());
        Assert.Equal([(12u, "0.5", "0.0")], slide.Objects);
        Assert.Equal((actor.VirtualId, "1.5", "1.0"), slide.Avatar);
        Assert.Equal((1, 1, 1d), (actor.X, actor.Y, actor.Z));
    }

    private List<((int X, int Y) From, (int X, int Y) To, uint Roller, List<(uint Id, string FromZ, string ToZ)> Objects, (int Index, string FromZ, string ToZ)? Avatar)> RollerBundles()
    {
        var bundles = new List<((int, int), (int, int), uint, List<(uint, string, string)>, (int, string, string)?)>();

        foreach (var sent in _client.Packets.Where(p => p.Header == ServerPacketHeader.SlideObjectBundleComposer)) {
            var packet = new FlashIncomingPacket { Buffer = sent.Body.ToArray() };
            var from = (packet.ReadInt(), packet.ReadInt());
            var to = (packet.ReadInt(), packet.ReadInt());
            var objects = new List<(uint, string, string)>();

            for (var count = packet.ReadInt(); count > 0; count--) {
                objects.Add((packet.ReadUInt(), packet.ReadString(), packet.ReadString()));
            }

            var roller = packet.ReadUInt();
            (int, string, string)? avatar = null;

            if (packet.HasDataRemaining()) {
                Assert.Equal(2, packet.ReadInt());
                avatar = (packet.ReadInt(), packet.ReadString(), packet.ReadString());
            }

            bundles.Add((from, to, roller, objects, avatar));
        }

        return bundles;
    }
}
