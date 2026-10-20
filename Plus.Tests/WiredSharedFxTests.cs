using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;
using static Plus.Tests.SharedAddonIds;

namespace Plus.Tests;

/// <summary>
/// Variable FX travel with the Share and Import add-ons. Observed on AIR WIN63-202610201032-107964540: a room that imports them sends the
/// FX config under the id -1 and draws it over the reference, only while the Import add-on has variable FX on.
/// </summary>
public sealed class WiredSharedFxTests
{
    [Fact]
    public void ImportedVariableFxAreDrawnForTheReferenceUnderNegativeConfigIds()
    {
        var scene = new SharedAddonScene();
        scene.A.AddMetadata(Fx, "wf_xtra_var_fx_number");
        Assert.Single(scene.A.Variables.FxBindings());
        scene.Synchronize();

        var binding = Assert.Single(scene.B.Variables.FxBindings());
        Assert.Equal(-1, binding.Config.Id);
        Assert.Equal(new WiredVariableReference(WiredVariableTarget.User, $"custom:{Reference}"), binding.Variable);
        Assert.True(binding.Config.UserFx);

        SharedAddonRig.Change(scene.ImportBox, [1, 1, 0]);
        Assert.Empty(scene.B.Variables.FxBindings());
        SharedAddonRig.Change(scene.ImportBox, [0, 0, 1]);
        Assert.Single(scene.B.Variables.FxBindings());
    }

    [Fact]
    public void AnImportedFxIsPublishedOnlyWhileTheShareHasVariableFxOn()
    {
        var scene = new SharedAddonScene(share: [1, 1, 0]);
        scene.A.AddMetadata(Fx, "wf_xtra_var_fx_number");
        scene.Synchronize();

        Assert.Empty(scene.B.Variables.FxBindings());
    }

    [Fact]
    public void ASynchronizeMarksTheReferencingRoomForAFreshFxPass()
    {
        var scene = new SharedAddonScene();
        scene.B.Variables.FxFlushed();
        Assert.False(scene.B.Variables.FxDirty);
        scene.Synchronize();

        Assert.True(scene.B.Variables.FxDirty);
    }
}
