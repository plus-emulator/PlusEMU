using System.Text.Json;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed class WiredLinkEditorStoredTests
{
    private sealed class Rows(Dictionary<uint, WiredNativeEditorConfiguration> rows) : IWiredConfigurationStore
    {
        public WiredConfiguration? Load(uint itemId, WiredBoxDescriptor descriptor)
        {
            if (!rows.TryGetValue(itemId, out var native)) {
                return null;
            }

            var stored = JsonSerializer.Deserialize<WiredNativeEditorConfiguration>(JsonSerializer.Serialize(native))!;

            return WiredNativeEditorProjection.TryCompile(itemId, descriptor, stored, out var runtime, storedRow: true)
                ? runtime : throw new InvalidDataException($"Invalid native Wired configuration for item {itemId}.");
        }

        public void Save(uint itemId, WiredBoxDescriptor descriptor, WiredConfiguration configuration) { }

        public void Reset(IReadOnlyCollection<uint> itemIds) { }
    }

    [Theory]
    [InlineData("[100]", true)]
    [InlineData("[100]", false)]
    public void ALinkSpeakerSavedWithAFurniSourceStillLoads(string sources, bool withLinkPick)
    {
        var rows = new Dictionary<uint, WiredNativeEditorConfiguration>();
        using var world = new G1World(new Rows(rows));
        var item = world.AddItem("wf_xtra_link_speaker", InteractionTypes.GetTypeFromString("wf_xtra_link_speaker"));
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_link_speaker", out var descriptor));
        var pick = world.AddItem("wired_link", InteractionType.None);
        rows[item.Id] = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [1, 1],
            FurniSourceTypes = [.. JsonSerializer.Deserialize<int[]>(sources)!],
            PrimaryItems = withLinkPick ? [new WiredNativeItemReference(pick.Id, false)] : []
        };

        var box = Assert.IsAssignableFrom<IWiredConfiguredItem>(world.Wired.LoadWiredBox(item));
        Assert.Equal(withLinkPick ? [pick.Id] : [], box.Configuration.SelectedItems.ToArray());
    }

    private static WiredEditorSnapshot FreshSnapshot(string name)
    {
        using var world = new G1World();
        var item = world.AddItem(name, InteractionTypes.GetTypeFromString(name));
        var box = world.Wired.CreateConfiguredBox(item)!;
        Assert.True(world.Wired.AddBox((IWiredItem)box));

        return WiredEditorSnapshot.Capture(box);
    }

    [Theory]
    [InlineData("wf_trg_receive_dataframe")]
    [InlineData("wf_act_send_dataframe")]
    public void AFreshDataframeEditorOpensWithAnEmptyText(string name) => Assert.Equal("", FreshSnapshot(name).Native!.Text);

    [Fact]
    public void SendDataframeCarriesTheTextPlaceholderContext() => Assert.NotNull(FreshSnapshot("wf_act_send_dataframe").TextPlaceholders);

    [Theory]
    [InlineData("wf_trg_receive_message", "5", "txt", "wiredfurni.error.require_link_furni")]
    [InlineData("wf_xtra_link_speaker", "2,5", "", "ok")]
    public void LinkBoxSaveAnswers(string name, string ints, string text, string expected)
    {
        using var save = new WiredRawSave(name);
        var plain = save.Furni("tile", InteractionType.None);
        var link = save.Furni("wired_link", InteractionType.None);
        var result = save.Save([.. ints.Split(',').Select(int.Parse)], text, name == "wf_xtra_link_speaker" ? [] : null,
            picks: name == "wf_xtra_link_speaker" ? [link] : [plain]);

        Assert.Equal(expected, result.Accepted ? "ok" : result.Key ?? result.Answer);
    }

    [Fact]
    public void ReceiveDataframeAcceptsAnEmptyVariableList()
    {
        using var save = new WiredRawSave("wf_trg_receive_dataframe");
        var link = save.Furni("wired_link", InteractionType.None);
        var result = save.Save([1, .. Enumerable.Repeat(0, 12)], "[[\"num\",\"numph\"]]", variables: [], picks: [link]);

        Assert.True(result.Accepted, result.ToString());
    }
}
