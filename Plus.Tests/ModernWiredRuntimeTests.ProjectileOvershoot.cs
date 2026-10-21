using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    // A projectile moves the target one tile and the packets the room got are read back: the WiredMovements entries as
    // (overshoot flag, overshoot value), their animation times and whether an ObjectUpdate came.
    private static (List<(bool Has, int Value)> Movements, bool ObjectUpdate, (int X, int Y) At, List<int> Animations) ShootOneTile(
        WiredProjectileDistance mode, int tiles, int speedIncrease = 0)
    {
        using var f = new TeleportFixture();
        f.Target.Definition.Stackable = true;
        var replies = Capture(f.Habbo.Client);
        var context = Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User]);
        context.Policy.Addons.Projectile = new WiredProjectilePolicy(new HashSet<uint>(), null, 0, 50, mode, tiles)
        {
            AppliesToAllMovedFurniture = true,
            TimePerTileMs = 400,
            SpeedIncrease = speedIncrease,
            MeasureX = true,
            MeasureY = true
        };

        Assert.True(new WiredRoomMovement(f.Room.GetWired().DispatchWalkTransition).MoveFurniture(context, f.Target, 2, 1, 0, null));
        var movements = new List<(bool, int)>();
        var animations = new List<int>();

        foreach (var reply in replies.Where(reply => reply.Header == ServerPacketHeader.WiredMovementsComposer)) {
            var packet = new WireReader(reply.Body);
            Assert.Equal(1, packet.Int());
            Assert.Equal(1, packet.Int());
            packet.Skip(4);
            packet.String();
            packet.String();
            packet.Int();
            animations.Add(packet.Int());
            packet.Skip(1);
            var has = packet.Bool();
            movements.Add((has, has ? packet.Int() : 0));
        }

        return (movements, replies.Any(reply => reply.Header == ServerPacketHeader.ObjectUpdateComposer), (f.Target.GetX, f.Target.GetY), animations);
    }

    // Observed on AIR WIN63-202610201032-107964540: the overshoot modes send an overshoot value with the movement: 800 for two more tiles
    // at 400 ms a tile.
    [Theory]
    [InlineData(WiredProjectileDistance.Overshoot, 2)]
    [InlineData(WiredProjectileDistance.Fixed, 3)]
    public void AnOvershootingProjectileSendsTheOvershootValue(WiredProjectileDistance mode, int tiles)
    {
        var shot = ShootOneTile(mode, tiles);

        Assert.Equal((true, 800), Assert.Single(shot.Movements));
        Assert.Equal((2, 1), shot.At);
    }

    // Observed on AIR WIN63-202610201032-107964540: a shot sends one packet. It holds the animation time of the first tile and the time of
    // the rest of the flight as the overshoot. The whole flight takes the time per tile for every tile, less the speed increase
    // (400 ms a tile, 6 tiles: 2400 ms; speed increase 100 takes 1800 ms), shared by the tiles of the flight. The largest speed
    // increase changes nothing.
    [Theory]
    [InlineData(WiredProjectileDistance.Fixed, 6, 0, 400, 2000)]
    [InlineData(WiredProjectileDistance.Fixed, 6, 100, 300, 1500)]
    [InlineData(WiredProjectileDistance.Fixed, 6, 200, 367, 1833)]
    [InlineData(WiredProjectileDistance.Fixed, 6, 300, 389, 1945)]
    [InlineData(WiredProjectileDistance.Fixed, 6, 100000, 400, 2000)]
    [InlineData(WiredProjectileDistance.Fixed, 8, 0, 400, 2800)]
    [InlineData(WiredProjectileDistance.Overshoot, 4, 300, 387, 1547)]
    [InlineData(WiredProjectileDistance.Overshoot, 2, 0, 400, 800)]
    public void AShotSendsTheFirstTileAndTheRestOfTheFlightAsOneOvershoot(WiredProjectileDistance mode, int tiles, int speedIncrease, int animation, int overshoot)
    {
        var shot = ShootOneTile(mode, tiles, speedIncrease);

        Assert.Equal((true, overshoot), Assert.Single(shot.Movements));
        Assert.Equal(animation, Assert.Single(shot.Animations));
    }

    // One move step with no overshoot is a single tile: the speed increase does not apply to it.
    [Fact]
    public void ANormalShotWithASpeedIncreaseKeepsItsTileTime()
    {
        var shot = ShootOneTile(WiredProjectileDistance.Normal, 0, 300);

        Assert.Equal((false, 0), Assert.Single(shot.Movements));
        Assert.Equal(400, Assert.Single(shot.Animations));
    }

    [Fact]
    public void ANormalProjectileSendsNoOvershoot() => Assert.Equal((false, 0), Assert.Single(ShootOneTile(WiredProjectileDistance.Normal, 0).Movements));

    // Observed on AIR WIN63-202610201032-107964540: always shooting a negative number of tiles moves the furni at once with one
    // ObjectUpdate and no WiredMovements.
    [Fact]
    public void ANegativeAlwaysShootDistanceMovesTheFurniAtOnce()
    {
        var shot = ShootOneTile(WiredProjectileDistance.Fixed, -2);

        Assert.Empty(shot.Movements);
        Assert.True(shot.ObjectUpdate);
        Assert.Equal((2, 1), shot.At);
    }
}
