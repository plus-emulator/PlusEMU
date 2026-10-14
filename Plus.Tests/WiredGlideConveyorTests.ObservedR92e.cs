using System.Buffers.Binary;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.WiredVariables;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed r92e room 14 (AIR WIN63-202610131318-860371703): a Fast Queue of sprite 3666 colour tiles with the owner riding.
    // Each ~200ms pulse the tiles are reported home->home for 500ms in the same WiredMovements packet as the rider's step
    // (own user duration = the movement addon's animation time, 300ms there); the colour tiles never appear to move east.
    private static (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[] ColourTileFastQueueLayout(int animationMs = 300) =>
        FastQueueLayout(2).Select(item => item.Name == "bc_tile_1" ? item with { Name = "wf_colortile" }
            : item.Name == "wf_xtra_anim_time" ? item with { Json = System.Text.Json.JsonSerializer.Serialize(new WiredConfiguration { IntParams = [animationMs] }) }
            : item).ToArray();

    [Fact]
    public async Task ColourTileFastQueuePublishesTheTilesHomeToHomeInTheRidersPacket()
    {
        var f = new Fixture(ColourTileFastQueueLayout(), live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var room = f.MovementContext().Room;
        room.Type = "private";
        room.OwnerName = rider.GetUsername();
        var packets = CapturePublication(rider);
        var request = new WiredUserVariablesRequestEvent(new WiredVariableMenuService());

        for (var pulse = 1; pulse <= 4; pulse++) {
            await request.Parse(room, rider.GetClient()!, new FlashIncomingPacket { Buffer = Array.Empty<byte>() });
            packets.Clear();

            f.Advance(pulse == 1 ? 250 : 200);

            var groups = packets.Where(IsMovementBody).Select(bytes => ReadPublication([bytes]).ToArray()).ToList();
            var group = Assert.Single(groups);
            var furniture = group.Where(move => move.Type == 1).ToArray();
            Assert.Equal(pulse == 1 ? 7 : 6, furniture.Length);
            Assert.All(furniture, move =>
            {
                Assert.Equal(move.FromX, move.ToX);
                Assert.Equal(11, move.ToY);
                Assert.Equal(500, move.Duration);
            });
            Assert.All(furniture, move => Assert.Equal(("0", "0"), (move.FromZ, move.ToZ)));
            var carry = Assert.Single(group.Where(move => move.Type == 0));
            Assert.Equal((3 + pulse, 4 + pulse, 300), (carry.FromX, carry.ToX, carry.Duration));
            Assert.Equal(0, group[^1].Type); // The rider's entry follows the tiles' entries, as in the official packet.
            Assert.Equal((4 + pulse, 11), (rider.X, rider.Y));
            Assert.Equal(0, f.Engine.ReadStats().Pending);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ColourTileFastQueueStillDispatchesWalkOnTriggersOfTheTilesItCarriesOnto(bool silentObserver)
    {
        var layout = ColourTileFastQueueLayout().ToList();
        layout.Add((19, 7, 12, 0, 0, "wf_trg_walks_on_furni", System.Text.Json.JsonSerializer.Serialize(new WiredConfiguration
        { IntParams = [100], SelectedItems = [2] })));
        var f = new Fixture(layout.ToArray(), live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var packets = CapturePublication(rider);
        var observed = 0;
        f.Engine.ObserveEvent = (evt, _) =>
        {
            if (evt.Kind == WiredEventKind.WalkOn) {
                Assert.NotEmpty(ReadPublication(packets).Where(move => move.Type == 1));
                observed++;
            }
        };
        f.Engine.PublicationObserverIsSilent = _ => silentObserver;

        f.Advance(250);

        Assert.True(observed > 0);
        Assert.Equal(14, ReadPublication(packets).Count(move => move.Type == 1));
        Assert.Equal(5, rider.X);
        Assert.Equal(0, f.Engine.ReadStats().Pending);
    }
}
