using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;
using static Plus.Tests.SharedAddonIds;

namespace Plus.Tests;

/// <summary>
/// A room reads the add-ons published to its references on every tick, so it loads them from the store once and again only after
/// something changed: a Synchronize in another room, or a reference or Import add-on loaded or removed.
/// </summary>
public sealed class WiredSharedAddonCacheTests
{
    private sealed class CountingStore(IWiredSharedAddonStore inner) : IWiredSharedAddonStore
    {
        public int Loads { get; private set; }
        public WiredSharedAddons? Load(uint referenceItemId)
        {
            Loads++;

            return inner.Load(referenceItemId);
        }

        public void Save(uint referenceItemId, WiredSharedAddons? addons) => inner.Save(referenceItemId, addons);
    }

    private static (SharedAddonScene Scene, CountingStore Store) Synchronized()
    {
        var scene = new SharedAddonScene();
        scene.A.AddMetadata(Level, "wf_xtra_var_lvlup_system", text: LevelSystem);
        scene.Synchronize();
        var store = new CountingStore(scene.Store);
        scene.B.Variables.SharedAddons = store;

        return (scene, store);
    }

    [Fact]
    public void RepeatedReadsLoadTheAddonsOnce()
    {
        var (scene, store) = Synchronized();

        for (var i = 0; i < 5; i++) {
            Assert.Equal(3, scene.B.Parts("shref").Length);
        }

        var first = store.Loads;
        Assert.True(first > 0);

        for (var i = 0; i < 5; i++) {
            Assert.Equal(3, scene.B.Parts("shref").Length);
        }

        Assert.Equal(first, store.Loads);
    }

    [Fact]
    public void ARefreshFromAnotherRoomLoadsTheAddonsAgain()
    {
        var (scene, store) = Synchronized();
        Assert.Equal(3, scene.B.Parts("shref").Length);
        var before = store.Loads;

        scene.Store.Save(Reference, null);
        WiredSharedVariableBroadcast.Refresh(scene.B.RoomId);

        Assert.Empty(scene.B.Parts("shref"));
        Assert.True(store.Loads > before);
    }

    [Fact]
    public void LoadingAnImportAddonLoadsTheAddonsAgain()
    {
        var (scene, store) = Synchronized();
        Assert.Equal(3, scene.B.Parts("shref").Length);
        var before = store.Loads;

        scene.B.AddMetadata(OtherImport, "wf_xtra_var_import");
        Assert.Equal(3, scene.B.Parts("shref").Length);

        Assert.True(store.Loads > before);
    }

    [Fact]
    public void RemovingTheReferenceLoadsTheAddonsAgain()
    {
        var (scene, store) = Synchronized();
        Assert.Equal(3, scene.B.Parts("shref").Length);
        var before = store.Loads;

        scene.B.Variables.ItemDetached(Reference);
        scene.B.AddReference(Reference, "shref", SharedVariable);
        Assert.Equal(3, scene.B.Parts("shref").Length);

        Assert.True(store.Loads > before);
    }
}
