using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public partial class ModernWiredRuntimeTests
{
    // A projectile moves the target one tile and the packets the room got are read back: the WiredMovements entries as
    // (overshoot flag, overshoot value) and whether an ObjectUpdate came.
    private static (List<(bool Has, int Value)> Movements, bool ObjectUpdate, (int X, int Y) At) ShootOneTile(WiredProjectileDistance mode, int tiles)
    {
        using var f = new TeleportFixture();
        f.Target.Definition.Stackable = true;
        var replies = Capture(f.Habbo.Client);
        var context = Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User]);
        context.Policy.Addons.Projectile = new WiredProjectilePolicy(new HashSet<uint>(), null, 0, 50, mode, tiles)
        {
            AppliesToAllMovedFurniture = true,
            TimePerTileMs = 400,
            MeasureX = true,
            MeasureY = true
        };

        Assert.True(new WiredRoomMovement(f.Room.GetWired().DispatchWalkTransition).MoveFurniture(context, f.Target, 2, 1, 0, null));
        var movements = new List<(bool, int)>();

        foreach (var reply in replies.Where(reply => reply.Header == ServerPacketHeader.WiredMovementsComposer)) {
            var packet = new WireReader(reply.Body);
            Assert.Equal(1, packet.Int());
            Assert.Equal(1, packet.Int());
            packet.Skip(4);
            packet.String();
            packet.String();
            packet.Int();
            packet.Skip(2);
            var has = packet.Bool();
            movements.Add((has, has ? packet.Int() : 0));
        }

        return (movements, replies.Any(reply => reply.Header == ServerPacketHeader.ObjectUpdateComposer), (f.Target.GetX, f.Target.GetY));
    }

    // Observed on AIR WIN63-202610201032-107964540: the overshoot modes send an overshoot value of 800 with the movement, whatever the
    // distance.
    [Theory]
    [InlineData(WiredProjectileDistance.Overshoot, 2)]
    [InlineData(WiredProjectileDistance.Fixed, 3)]
    public void AnOvershootingProjectileSendsTheOvershootValue(WiredProjectileDistance mode, int tiles)
    {
        var shot = ShootOneTile(mode, tiles);

        Assert.Equal((true, 800), Assert.Single(shot.Movements));
        Assert.Equal((2, 1), shot.At);
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
