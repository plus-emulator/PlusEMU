using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;
using static Plus.Tests.SharedAddonIds;

namespace Plus.Tests;

/// <summary>
/// The sub-variables of a level-up system travel with the Share and Import add-ons. Observed on AIR WIN63-202610201032-107964540: a
/// reference lists them while its Import add-on has subvariable creators on, and they go away at once when it does not.
/// </summary>
public sealed class WiredSharedLevelTests
{
    [Fact]
    public void SubvariableCreatorsShowTheLevelPartsOfTheSharedVariableOnlyWhileImported()
    {
        var scene = new SharedAddonScene();
        scene.A.AddMetadata(Level, "wf_xtra_var_lvlup_system", text: LevelSystem);
        scene.Synchronize();

        Assert.Equal(["shref.current_level", "shref.current_xp", "shref.is_maxed"], scene.B.Parts("shref"));
        SharedAddonRig.Change(scene.ImportBox, [1, 0, 1]);
        Assert.Empty(scene.B.Parts("shref"));
        SharedAddonRig.Change(scene.ImportBox, [0, 1, 0]);
        Assert.Equal(["shref.current_level", "shref.current_xp", "shref.is_maxed"], scene.B.Parts("shref"));
    }

    [Fact]
    public void ALevelSystemIsNotPublishedWhileTheShareHasSubvariableCreatorsOff()
    {
        var scene = new SharedAddonScene(share: [1, 0, 1]);
        scene.A.AddMetadata(Level, "wf_xtra_var_lvlup_system", text: LevelSystem);
        scene.Synchronize();

        Assert.Empty(scene.B.Parts("shref"));
    }

    [Fact]
    public void ALevelPartReadsTheLevelOfTheSharedValue()
    {
        var scene = new SharedAddonScene();
        scene.A.AddMetadata(Level, "wf_xtra_var_lvlup_system", text: LevelSystem);
        scene.Synchronize();
        var part = scene.B.Row("shref.current_level")!;
        var id = WiredRoomVariables.SyntheticId(WiredVariableTarget.User, Reference, 0, false)!.Value;

        Assert.Equal($"user:{id}", part.Id);
        Assert.Equal((2, 999, 1), (part.Type, part.Availability, part.Target));
    }
}
