using System.Text.Json;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// Triggerer Matches saved through the real packet path, then read back from its stored row as the editor shows it on open.
public sealed class WiredTriggererMatchesSourcesTests
{
    private sealed class RowStore : IWiredConfigurationStore
    {
        private readonly Dictionary<uint, ModernWiredRuntimeTests.StoredRuntimeRow> _rows = [];

        public WiredConfiguration? Load(uint itemId, WiredBoxDescriptor descriptor) =>
            new WiredConfigurationStore(new ModernWiredRuntimeTests.StoredRuntimeRowsDatabase(_rows.Values)).Load(itemId, descriptor);

        public void Save(uint itemId, WiredBoxDescriptor descriptor, WiredConfiguration configuration)
        {
            var native = configuration.Origin!.Native;
            _rows[itemId] = new(itemId, descriptor.CanonicalName, native.Version, JsonSerializer.Serialize(native));
        }

        public void Reset(IReadOnlyCollection<uint> itemIds) { }
    }

    [Theory]
    [InlineData("wf_cnd_triggerer_match", 0, 101, "")]
    [InlineData("wf_cnd_triggerer_match", 0, 101, "Objecto")]
    [InlineData("wf_cnd_triggerer_match", 0, 200, "")]
    [InlineData("wf_cnd_triggerer_match", 0, 201, "")]
    [InlineData("wf_cnd_triggerer_match", 0, 0, "")]
    [InlineData("wf_cnd_triggerer_match", 200, 200, "Objecto")]
    [InlineData("wf_cnd_triggerer_match", 201, 101, "")]
    [InlineData("wf_cnd_triggerer_match", 200, 0, "")]
    [InlineData("wf_cnd_not_triggerer_match", 0, 200, "")]
    [InlineData("wf_cnd_not_triggerer_match", 201, 201, "Objecto")]
    public void EverySourceChoiceIsKeptAfterASaveAndAReload(string name, int users, int comparison, string text)
    {
        var store = new RowStore();
        using var save = new WiredRawSave(name, store);

        var result = save.Save(ints: [1], text: text, users: [users, comparison]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal([users, comparison], result.Users);
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var reloaded = store.Load(save.Item.Id, descriptor);
        Assert.NotNull(reloaded);
        var frame = WiredNativeEditorProjection.HotelFrame(descriptor, reloaded.Origin!.Native);
        Assert.Equal([users, comparison], frame.UserSourceTypes.ToArray());
        Assert.Equal(text, frame.Text);
        Assert.True(save.World.Wired.TryRemove(save.Item.Id));
        Assert.NotNull(save.World.Wired.LoadWiredBox(save.Item));
    }
}
