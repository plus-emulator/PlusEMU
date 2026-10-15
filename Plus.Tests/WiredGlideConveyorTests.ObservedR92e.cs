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
        var flashes = new List<string>();

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
            // Movement Physics, Carry Users and Animation Time take part in each pulse, but a box flips once per 500 ms window
            // (observed r92e r92-room14-def: boxes 600/400 ms apart under the 200 ms repeater).
            flashes.Add(string.Join(",", new uint[] { 15, 16, 17, 18 }.Select(id => f.Items[id].LegacyDataString).Distinct()));
            Assert.Equal(0, f.Engine.ReadStats().Pending);
        }

        Assert.Equal(["1", "1", "0", "0"], flashes);
    }

    // Observed r92e r92-room14-def and r92f r92-room14-slow / r92-chase: the ObjectsDataUpdate with the flashed boxes goes out
    // before the WiredMovements packet of the same firing.
    [Fact]
    public void ColourTileFastQueueSendsTheBoxFlashesBeforeTheMovementPacket()
    {
        var f = new Fixture(ColourTileFastQueueLayout(), live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var headers = new List<uint>();
        rider.GetClient()!.SendCallback = args =>
        {
            headers.Add(BinaryPrimitives.ReadUInt32BigEndian(args.MemoryBuffer.ToArray()));

            return false;
        };

        for (var pulse = 1; pulse <= 6; pulse++) {
            headers.Clear();
            f.Advance(pulse == 1 ? 250 : 200);
            var relevant = headers.Where(header => header is ServerPacketHeader.ObjectsDataUpdateComposer
                or ServerPacketHeader.WiredMovementsComposer).ToArray();
            var movement = Array.IndexOf(relevant, ServerPacketHeader.WiredMovementsComposer);
            Assert.True(movement >= 0, $"pulse {pulse}: " + string.Join(",", relevant));
            Assert.All(relevant.Skip(movement + 1), header => Assert.Equal(ServerPacketHeader.WiredMovementsComposer, header));
        }
    }

    [Theory]
    [InlineData(false, 900, 10)]
    [InlineData(true, 0, 10)]
    [InlineData(true, 900, 11)]
    public void ColourTileQueueCarriesOntoAnOccupiedEndOnlyWhenForwardPhysicsPassesThatUser(bool passUsers, int userSource, int expectedX)
    {
        var layout = Configure(ColourTileFastQueueLayout(), (16, [1, 0, passUsers ? 1 : 0, 0, 900, 900, userSource]));
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var occupant = f.Walker(2, 11, 11);
        f.Advance(2000);
        Assert.Equal((expectedX, 11), (rider.X, rider.Y));
        Assert.Equal((11, 11), (occupant.X, occupant.Y));
        Assert.Equal(Enumerable.Range(4, 8), Enumerable.Range(1, 8).Select(id => f.Items[(uint)id].GetX));
    }

    // Room 14 also has an unused collision trigger, an unrelated walk-on area and variable metadata.
    // None of those may split a blocked queue step from its same-pulse snapshot restore.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ColourTileQueueStaysStationaryWithOtherWiredInTheRoom(bool unrelatedWalkStack)
    {
        var layout = ColourTileFastQueueLayout().ToList();
        layout.Add((19, 8, 15, 0, 0, "wf_trg_collision", null));
        layout.Add((20, 2, 4, 0, 0, "wf_var_echo", null));
        layout.Add((21, 2, 4, 1, 0, "wf_xtra_var_text_connector", null));
        layout.Add((25, 10, 16, 0, 0, "wf_trg_periodically", System.Text.Json.JsonSerializer.Serialize(new WiredConfiguration
        { IntParams = [1] })));

        if (unrelatedWalkStack) {
            layout.Add((22, 8, 16, 0, 0, "wf_trg_walks_on_furni", System.Text.Json.JsonSerializer.Serialize(new WiredConfiguration
            { IntParams = [200] })));
            layout.Add((23, 8, 16, 1, 0, "wf_slc_furni_area", System.Text.Json.JsonSerializer.Serialize(new WiredConfiguration
            { IntParams = [2, 2, 1, 1] })));
            layout.Add((24, 8, 16, 2, 0, "wf_xtra_text_output_variable", "native:" +
                """{"Version":2,"Category":4,"NativeCode":15,"OwnedIntParams":[0,1,0],"Text":"direction\t, ","PrimaryItems":[],"SecondaryItems":[],"FurniSourceTypes":[100],"UserSourceTypes":[0],"VariableIds":["-232"],"Delay":null,"Quantifier":null,"Filter":null,"Inverse":null,"SavedState":{"Snapshots":[]}}"""));
        }

        var f = new Fixture(layout.ToArray(), live: true);
        _ = f.Room.GetWired().Chests; // Room 14 has idle chests; opening their component must not split a pulse.
        var rider = f.Walker(1, 4, 10, 4, 11);
        var packets = CapturePublication(rider);
        var errors = new List<Exception>();
        f.SetEngineCallback("_error", (Action<Exception>)errors.Add);

        if (unrelatedWalkStack) {
            f.SetBoxField(24, "_variables", new WiredVariableModule(14, new QueueVariableDirectory(),
                new MemoryWiredVariableStore(), TimeProvider.System));
        }

        for (var pulse = 1; pulse <= 30; pulse++) {
            packets.Clear();
            f.Advance(pulse == 1 ? 250 : 200);
            Assert.Empty(errors);
            var furniture = ReadPublication(packets).Where(move => move.Type == 1).ToArray();
            Assert.NotEmpty(furniture);
            Assert.All(furniture, move => Assert.Equal(move.FromX, move.ToX));
            Assert.Equal(Math.Min(4 + pulse, 11), rider.X);
        }
    }

    private sealed class QueueVariableDirectory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint itemId) => null;
        public uint? GetRoomOwner(uint roomId) => roomId == 14 ? 1u : null;
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
