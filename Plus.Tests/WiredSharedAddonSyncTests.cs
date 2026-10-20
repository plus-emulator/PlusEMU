using System.Buffers.Binary;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;
using static Plus.Tests.SharedAddonIds;

namespace Plus.Tests;

/// <summary>
/// The Share and Import add-ons carry a shared variable's text connector to its references. Observed on AIR WIN63-202610201032-107964540:
/// a Share add-on stacked with a shared variable publishes that stack's add-ons only when it is synchronized, to every reference that
/// exists then. The Import add-on stacked with a reference picks the categories it shows, and changes at once. Both work per stack.
/// </summary>
public sealed class WiredSharedAddonSyncTests
{
    [Fact]
    public void NothingIsPublishedUntilTheShareIsSynchronized()
    {
        var scene = new SharedAddonScene();

        Assert.NotNull(scene.B.Row("shref"));
        Assert.Empty(scene.B.Connector("shref"));
    }

    [Fact]
    public void SynchronizeGivesAReferenceTheTextConnectorOfItsSharedVariable()
    {
        var scene = new SharedAddonScene();
        scene.Synchronize();

        Assert.Equal(["1=one", "2=two"], scene.B.Connector("shref"));
    }

    [Fact]
    public async Task TheSynchronizeButtonPublishesAndTheHotelAnswersWithSaveSuccess()
    {
        var scene = new SharedAddonScene();
        await Press(scene, Connector);
        Assert.Empty(scene.B.Connector("shref"));
        Assert.DoesNotContain(scene.A.World.Packets, sent => sent.Header == ServerPacketHeader.HideWiredConfigComposer);

        await Press(scene, Share);
        Assert.Equal(["1=one", "2=two"], scene.B.Connector("shref"));
        Assert.Single(scene.A.World.Packets, sent => sent.Header == ServerPacketHeader.HideWiredConfigComposer);
    }

    private static Task Press(SharedAddonScene scene, uint item)
    {
        var body = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(body, item);

        return new WiredVariableShareSynchronizeEvent().Parse(scene.A.World.Room, scene.A.World.Client, new FlashIncomingPacket { Buffer = body });
    }

    [Fact]
    public void OnlyAShareAddonCanBeSynchronized()
    {
        var scene = new SharedAddonScene();

        Assert.False(scene.A.Variables.Synchronize(scene.A.Floor[Connector]));
        Assert.Empty(scene.B.Connector("shref"));
    }

    [Fact]
    public void AReferenceMadeAfterTheLastSynchronizeHasNothingUntilTheNextOne()
    {
        var scene = new SharedAddonScene(reference: false);
        scene.Synchronize();
        scene.B.AddReference(Reference, "shref", SharedVariable);

        Assert.Empty(scene.B.Connector("shref"));
        scene.Synchronize();
        Assert.Equal(["1=one", "2=two"], scene.B.Connector("shref"));
    }

    [Fact]
    public void ImportChoosesTheCategoriesAtOnceWithoutASynchronize()
    {
        var scene = new SharedAddonScene();
        scene.Synchronize();
        SharedAddonRig.Change(scene.ImportBox, [0, 0, 0]);
        Assert.Empty(scene.B.Connector("shref"));
        SharedAddonRig.Change(scene.ImportBox, [1, 0, 0]);
        Assert.Equal(["1=one", "2=two"], scene.B.Connector("shref"));
        SharedAddonRig.Change(scene.ImportBox, [0, 1, 1]);
        Assert.Empty(scene.B.Connector("shref"));
    }

    [Fact]
    public void WithoutAnImportAddonAReferenceShowsNothingImported()
    {
        var scene = new SharedAddonScene();
        scene.Synchronize();
        scene.B.Variables.ItemDetached(scene.ImportBox.Item);

        Assert.Empty(scene.B.Connector("shref"));
    }

    [Fact]
    public void ChangesInTheShareOrTheConnectorWaitForTheNextSynchronize()
    {
        var scene = new SharedAddonScene();
        scene.Synchronize();
        WiredNativeTestSupport.InstallLegacyVariableDraft(scene.ConnectorBox, new() { Text = "1=uno\n3=tres" });
        Assert.Equal(["1=one", "2=two"], scene.B.Connector("shref"));
        scene.Synchronize();
        Assert.Equal(["1=uno", "3=tres"], scene.B.Connector("shref"));

        SharedAddonRig.Change(scene.ShareBox, [0, 0, 0]);
        Assert.Equal(["1=uno", "3=tres"], scene.B.Connector("shref"));
        scene.Synchronize();
        Assert.Empty(scene.B.Connector("shref"));
    }

    [Fact]
    public void ASynchronizeUpdatesEveryReferenceThatExists()
    {
        var scene = new SharedAddonScene();
        scene.B.AddReference(ThirdReference, "other", SharedVariable, x: 5);
        scene.B.AddMetadata(OtherImport, "wf_xtra_var_import", x: 5);

        Assert.True(scene.Directory.Rows[ThirdReference].Link!.Source.Token == $"custom:{SharedVariable}");
        scene.Synchronize();

        Assert.Equal(["1=one", "2=two"], scene.B.Connector("shref"));
        Assert.Equal(["1=one", "2=two"], scene.B.Connector("other"));
    }

    [Fact]
    public void AShareOnlyPublishesTheVariablesOfItsOwnStack()
    {
        var scene = new SharedAddonScene();
        // A second shared variable and its reference, each in a stack of their own (tile 1): the first Share does not reach them.
        scene.A.AddShared(OtherVariable, "shv2", x: 1);
        scene.A.AddMetadata(OtherConnector, "wf_xtra_var_text_connector", text: "5=five", x: 1);
        scene.B.AddReference(OtherReference, "shref2", OtherVariable, x: 1);
        scene.B.AddMetadata(OtherImport, "wf_xtra_var_import", x: 1);
        scene.Synchronize();

        Assert.Equal(["1=one", "2=two"], scene.B.Connector("shref"));
        Assert.Empty(scene.B.Connector("shref2"));

        scene.A.AddMetadata(OtherShare, "wf_xtra_var_share", x: 1);
        scene.Synchronize(OtherShare);
        Assert.Equal(["5=five"], scene.B.Connector("shref2"));
    }

    [Fact]
    public void AnImportAddonOfAnotherStackDoesNotCount()
    {
        var scene = new SharedAddonScene();
        scene.A.AddShared(OtherVariable, "shv2", x: 1);
        scene.A.AddMetadata(OtherConnector, "wf_xtra_var_text_connector", text: "5=five", x: 1);
        scene.A.AddMetadata(OtherShare, "wf_xtra_var_share", x: 1);
        // The reference sits on tile 1 and the only Import add-on on tile 0.
        scene.B.AddReference(OtherReference, "shref2", OtherVariable, x: 1);
        scene.Synchronize(OtherShare);

        Assert.Empty(scene.B.Connector("shref2"));
    }

    [Fact]
    public void PublishedAddonsSurviveAJsonRoundTrip()
    {
        var addons = new WiredSharedAddons
        {
            TextConnector = [new(1, "one"), new(-2, "minus two")],
            Level = LevelSystem,
            Fx = [new("wf_xtra_var_fx_number", [1, 2, 3], ["0", "n", "user:5"], "gems")]
        };
        var read = WiredSharedAddons.FromJson(addons.ToJson())!;

        Assert.Equal(addons.TextConnector.ToArray(), read.TextConnector.ToArray());
        Assert.Equal(addons.Level, read.Level);
        var fx = Assert.Single(read.Fx);
        Assert.Equal(("wf_xtra_var_fx_number", "gems"), (fx.Box, fx.Text));
        Assert.Equal([1, 2, 3], fx.Ints.ToArray());
        Assert.Equal(["0", "n", "user:5"], fx.VariableIds.ToArray());
        Assert.Null(WiredSharedAddons.FromJson("not json"));
    }
}
