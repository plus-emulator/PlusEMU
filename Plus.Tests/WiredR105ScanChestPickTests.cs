using Plus.HabboHotel.Items.Wired.Chests;
using Xunit;
using static Plus.Tests.WiredR104Save;

namespace Plus.Tests;

// Chest Item Type Scanner picks as Habbo stores them. A new save whose chest list is a picked list must pick only Furni Chests,
// and anything else is answered with the furni chest key.
public sealed class WiredR105ScanChestPickTests
{
    private const string Name = "wf_xtra_scan_chest_furni_by_type";
    private const string Key = "wiredfurni.error.require_furni_chests";

    [Fact]
    public void AFurniChestIsAccepted()
    {
        using var world = new G1World();

        Assert.True(Save(world, Name, furni: [100, 100], picks: [world.AddChest(WiredChestKind.Furni).Id]).Accepted);
    }

    [Fact]
    public void ASampleThatIsNotAChestIsAnsweredWithTheFurniChestKey()
    {
        using var world = new G1World();
        var notAChest = world.AddItem("wf_colortile", Plus.HabboHotel.Items.InteractionType.None).Id;

        Assert.Equal(Key, Save(world, Name, furni: [100, 100], picks: [notAChest]).Key);
    }

    [Fact]
    public void ACreditChestOrAMixOfBothIsAnsweredWithTheFurniChestKey()
    {
        using var world = new G1World();

        Assert.Equal(Key, Save(world, Name, furni: [100, 100], picks: [world.AddChest(WiredChestKind.Coins).Id]).Key);
        Assert.Equal(Key, Save(world, Name, furni: [100, 100],
            picks: [world.AddChest(WiredChestKind.Furni).Id, world.AddChest(WiredChestKind.Coins).Id]).Key);
    }

    [Fact]
    public void ASourceThatIsNotAPickedListIsNotChecked()
    {
        using var world = new G1World();
        var coins = world.AddChest(WiredChestKind.Coins).Id;

        Assert.True(Save(world, Name, furni: [0, 0], picks: [coins]).Accepted);
        Assert.True(Save(world, Name, furni: [0, 200], picks: [coins]).Accepted);
    }

    [Fact]
    public void NoPicksAreAccepted()
    {
        using var world = new G1World();

        Assert.True(Save(world, Name, furni: [100, 100]).Accepted);
    }
}
