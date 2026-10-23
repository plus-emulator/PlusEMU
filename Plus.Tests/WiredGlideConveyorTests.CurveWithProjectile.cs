using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint CpTile = 81, CpSwitch = 82, CpTrigger = 83, CpMove = 84, CpCurve = 85, CpProjectile = 86;

    // A Projectile editor frame with no rotation. Only the curve strength (index 18, 50 on a new box) matters to these tests.
    private static readonly int[] CpProjectileFrame = [0, 0, 0, 0, 500, -20, 1, 1, 0, 4, 0, 0, 1, 1, 0, 0, 1, -20, 50];

    [Theory]
    [InlineData("curve,projectile", 50, 50, 500)]
    [InlineData("projectile,curve", 50, 50, 500)]
    [InlineData("projectile", 50, 50, 500)]
    [InlineData("curve", 500, 50, 500)]
    [InlineData("curve,projectile", 200, 200, 500)]
    [InlineData("projectile", 200, 200, 500)]
    [InlineData("curve,projectile", 500, 0, 500)]
    [InlineData("curve,projectile", 50, 50, 100)]
    [InlineData("curve,projectile", 50, 50, 0)]
    [InlineData("curve,projectile", 50, 50, -100)]
    [InlineData("curve,projectile", -100, -100, 500)]
    [InlineData("curve", -100, 50, -100)]
    public async Task AClickedStackMovesTheTileWithTheProjectileCurveWhenItHasBothCurveAddons(string addons, int strength, int projectileCurve, int curve)
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
                ? [(int)CpCurve, 3, 0, curve, 0, "", 1, (int)CpTile, 1, 100, 1, 0, 1, "0", 0]
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
