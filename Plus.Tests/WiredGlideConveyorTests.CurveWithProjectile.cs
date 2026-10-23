using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: a User Clicks Furni stack with Move to Direction on a colour tile. With a Movement Curve
// add-on (strength 500) and a Projectile add-on with its default settings (curve strength 50) on one stack, the tile moves with curve
// strength 50, the same as with the Projectile add-on alone, in either stacking order. A projectile curve strength of 200 is used over 500
// the same way, and a projectile curve strength of 0 leaves the Movement Curve add-on in charge (3 of 3 runs). The Movement Curve add-on
// alone moves it with 500.
public sealed partial class WiredGlideConveyorTests
{
    private const uint CpTile = 81, CpSwitch = 82, CpTrigger = 83, CpMove = 84, CpCurve = 85, CpProjectile = 86;

    // The Projectile editor frame as the client sends it unchanged: no rotation, 500 ms a tile, curve strength 50 (index 18).
    private static readonly int[] CpProjectileFrame = [0, 0, 0, 0, 500, -20, 1, 1, 0, 4, 0, 0, 1, 1, 0, 0, 1, -20, 50];

    [Theory]
    [InlineData("curve,projectile", 50, 50)]
    [InlineData("projectile,curve", 50, 50)]
    [InlineData("projectile", 50, 50)]
    [InlineData("curve", 500, 50)]
    [InlineData("curve,projectile", 200, 200)]
    [InlineData("projectile", 200, 200)]
    [InlineData("curve,projectile", 500, 0)]
    public async Task AClickedStackMovesTheTileWithTheProjectileCurveWhenItHasBothCurveAddons(string addons, int strength, int projectileCurve)
    {
        var f = new Fixture(
        [
            (CpTile, 5, 9, 0.0, 0, "wf_colortile", null),
            (CpSwitch, 8, 13, 0.0, 2, "wf_floor_switch1", null),
            (CpTrigger, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(CpSwitch))
        ], live: true);
        f.Room.OwnerId = 1;
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";
        f.PlaceWired(CpMove, 4, 12, 0.65, "wf_act_move_to_dir");
        var z = 1.3;

        foreach (var addon in addons.Split(',')) {
            f.PlaceWired(addon == "curve" ? CpCurve : CpProjectile, 4, 12, z, addon == "curve" ? "wf_xtra_mov_curve" : "wf_xtra_rotate_to_dir");
            z += 0.65;
        }

        var owner = f.Walker(1, 7, 10, 7, 11);
        var frames = CaptureFlashFrames(owner);
        var service = MatchService();

        // Effect save: item, ints, text, picks, delay, furni sources, user sources, variables, secondary picks.
        Send(f, owner, frames, new SaveWiredEffectConfigEvent(service), [(int)CpMove, 3, 2, 0, 1, "", 1, (int)CpTile, 0, 1, 100, 0, 0, 0]);
        Assert.Equal("ok", SaveAnswer(frames));

        // Add-on save: item, ints, text, picks, furni sources, user sources, variables, secondary picks.
        foreach (var addon in addons.Split(',')) {
            object[] body = addon == "curve"
                ? [(int)CpCurve, 3, 0, 500, 0, "", 1, (int)CpTile, 1, 100, 1, 0, 1, "0", 0]
                : [(int)CpProjectile, CpProjectileFrame.Length, .. CpProjectileFrame[..^1].Cast<object>(), projectileCurve, "", 0, 3, 901, 0, 0, 3, 0, 0, 0, 2, "0", "0", 0];
            Send(f, owner, frames, new SaveWiredAddonConfigEvent(service), body);
            Assert.Equal("ok", SaveAnswer(frames));
        }

        f.Advance(1000);
        frames.Clear();

        await new ClickFurniEvent(new FurnitureUseService(null!, null!)).Parse(f.Room, owner.GetClient()!, ClickPacket((int)CpSwitch, 0));
        f.Advance(1000);

        var movement = Assert.Single(frames, frame => frame.Header == ServerPacketHeader.WiredMovementsComposer);
        var packet = new FlashIncomingPacket { Buffer = movement.Payload };
        Assert.Equal(1, packet.ReadInt());
        Assert.Equal(1, packet.ReadInt());
        Assert.Equal((5, 9), (packet.ReadInt(), packet.ReadInt()));
        packet.ReadInt();
        packet.ReadInt();
        packet.ReadString();
        packet.ReadString();
        Assert.Equal((int)CpTile, packet.ReadInt());
        packet.ReadInt();
        packet.ReadInt();
        Assert.False(packet.ReadBool());
        Assert.True(packet.ReadBool());
        Assert.Equal(strength, packet.ReadInt());
    }
}
