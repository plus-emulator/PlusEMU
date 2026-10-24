using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;
using static Plus.Tests.SharedAddonIds;

namespace Plus.Tests;

/// <summary>The "_=" fallback of a text connector travels through Share and Import like the number labels.</summary>
public sealed class WiredSharedAddonFallbackTests
{
    private const string WithFallback = "1=one\n2=two\n_=other";

    private static SharedAddonScene Scene()
    {
        var scene = new SharedAddonScene();
        SharedAddonRig.Change(scene.ConnectorBox, text: WithFallback);
        scene.A.Variables.ConfigurationLoaded(scene.ConnectorBox);

        return scene;
    }

    private static string? Fallback(SharedAddonRig rig, string name) => rig.Row(name)?.TextConnectorFallback;

    [Fact]
    public void TheSharedVariableKeepsItsOwnFallback()
    {
        var scene = Scene();
        scene.Synchronize();

        Assert.Equal("other", Fallback(scene.A, "shv"));
    }

    [Fact]
    public void SynchronizeCarriesTheFallbackToTheReference()
    {
        var scene = Scene();
        scene.Synchronize();

        Assert.Equal(["1=one", "2=two"], scene.B.Connector("shref"));
        Assert.Equal("other", Fallback(scene.B, "shref"));
    }

    [Fact]
    public void AMappingWithoutFallbackRemovesItAfterSynchronize()
    {
        var scene = Scene();
        scene.Synchronize();
        SharedAddonRig.Change(scene.ConnectorBox, text: "1=uno\n_=changed");
        scene.A.Variables.ConfigurationLoaded(scene.ConnectorBox);
        scene.Synchronize();
        Assert.Equal("changed", Fallback(scene.B, "shref"));

        SharedAddonRig.Change(scene.ConnectorBox, text: "1=uno\n3=tres");
        scene.A.Variables.ConfigurationLoaded(scene.ConnectorBox);
        scene.Synchronize();

        Assert.Equal(["1=uno", "3=tres"], scene.B.Connector("shref"));
        Assert.Null(Fallback(scene.B, "shref"));
    }

    [Fact]
    public void ImportTextConnectorsOffAndOnTakesAndGivesBackTheFallback()
    {
        var scene = Scene();
        scene.Synchronize();

        SharedAddonRig.Change(scene.ImportBox, [0, 1, 1]);
        scene.B.Variables.ConfigurationLoaded(scene.ImportBox);
        Assert.Empty(scene.B.Connector("shref"));
        Assert.Null(Fallback(scene.B, "shref"));

        SharedAddonRig.Change(scene.ImportBox, [1, 1, 1]);
        scene.B.Variables.ConfigurationLoaded(scene.ImportBox);
        Assert.Equal("other", Fallback(scene.B, "shref"));
    }

    [Fact]
    public void ShareFlagsOffThenSynchronizeRemovesConnectorAndFallback()
    {
        var scene = Scene();
        scene.Synchronize();
        Assert.Equal("other", Fallback(scene.B, "shref"));

        SharedAddonRig.Change(scene.ShareBox, [0, 0, 0]);
        scene.A.Variables.ConfigurationLoaded(scene.ShareBox);
        scene.Synchronize();
        Assert.Empty(scene.B.Connector("shref"));
        Assert.Null(Fallback(scene.B, "shref"));

        SharedAddonRig.Change(scene.ShareBox, [1, 1, 1]);
        scene.A.Variables.ConfigurationLoaded(scene.ShareBox);
        scene.Synchronize();
        Assert.Equal("other", Fallback(scene.B, "shref"));
    }

    [Fact]
    public void PublishedFallbackSurvivesTheStoredCopy()
    {
        var scene = Scene();
        scene.Synchronize();
        var stored = scene.Store.Load(Reference);
        Assert.NotNull(stored);

        var json = stored!.ToJson();

        Assert.Contains("other", json);
    }
}
