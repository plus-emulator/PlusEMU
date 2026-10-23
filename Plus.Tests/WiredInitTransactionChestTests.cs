using Xunit;

namespace Plus.Tests;

// Raw saves of Initiate Transaction through the real save packet. A save without a chest is refused with the chest key and no
// parameters.
public sealed class WiredInitTransactionChestTests
{
    private const string RequireChests = "wiredfurni.error.require_chests";

    [Fact]
    public void ASaveWithoutAPickedChestIsRefused()
    {
        using var save = new WiredRawSave("wf_act_init_transaction");

        var result = save.Save(picks: []);

        Assert.Equal(("error", RequireChests), (result.Answer, result.Key));
        Assert.Empty(result.ErrorParameters!);
    }

    [Fact]
    public void ASaveWithOnlyAFurniThatIsNoChestIsRefused()
    {
        using var save = new WiredRawSave("wf_act_init_transaction");

        var result = save.Save(picks: [save.Furni()]);

        Assert.Equal(("error", RequireChests), (result.Answer, result.Key));
    }

    [Fact]
    public void ASaveWithAPickedChestIsAccepted()
    {
        using var save = new WiredRawSave("wf_act_init_transaction");

        var result = save.Save(picks: [save.FurniChest()]);

        Assert.True(result.Accepted, result.ToString());
    }
}
