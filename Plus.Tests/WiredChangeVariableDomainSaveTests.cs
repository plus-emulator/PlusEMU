using Xunit;

namespace Plus.Tests;

public sealed class WiredChangeVariableDomainSaveTests
{
    private const string FurniAltitude = "-123";

    // Observed on AIR WIN63-202610201032-107964540: Change Variable Value saves a furni variable (Altitude) with the user target and with
    // the furni target. The box with the user target changes nothing.
    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void AFurniVariableSavesWithTheUserAndTheFurniTarget(int target)
    {
        using var save = new WiredRawSave("wf_act_change_var_val");
        var result = save.Save(ints: [target, 0, 0, 0, 30, -10, 0], furni: [200, 200], users: [0, 200], variables: [FurniAltitude, "0"]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([target, 0, 0, 0, 30, -10, 0], result.Ints);
        Assert.Equal([FurniAltitude, "0"], result.Variables);
    }
}
