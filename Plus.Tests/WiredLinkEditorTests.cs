using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed class WiredLinkEditorTests
{
    private static WiredNativeEditorMetadata Meta(string name) => WiredNativeEditorProjection.Metadata(name);

    private static WiredNativeEditorConfiguration Fresh(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        return WiredNativeEditorProjection.DefaultNative(descriptor);
    }

    [Theory]
    [InlineData("wf_trg_receive_message")]
    [InlineData("wf_trg_receive_dataframe")]
    public void ReceiveBoxesOfferPickedAndSelectedFurniOnly(string name) =>
        Assert.Equal([[100, 200]], Meta(name).FurniAllowed.Select(group => group.ToArray()).ToArray());

    [Fact]
    public void SendMessageOpensOnTriggerText() => Assert.Equal([0], Fresh("wf_act_send_message").OwnedIntParams.ToArray());

    [Fact]
    public void SendDataframeFreshFrame()
    {
        var meta = Meta("wf_act_send_dataframe");
        Assert.Equal([0, .. Enumerable.Range(0, 12).SelectMany(_ => new[] { 0, 0, -20 })], Fresh("wf_act_send_dataframe").OwnedIntParams.ToArray());
        Assert.Equal([100, .. Enumerable.Repeat(200, 12)], meta.FurniDefaults.ToArray());
        Assert.All(meta.FurniAllowed, group => Assert.Equal([0, 100, 101, 200, 201], group.ToArray()));
        Assert.True(meta.AllowWall);
    }

    [Fact]
    public void LinkSpeakerHasNoFurniSource() => Assert.Empty(Meta("wf_xtra_link_speaker").FurniAllowed);

    [Theory]
    [InlineData("wf_trg_receive_message", "", "", 0, false, "ok")]
    [InlineData("wf_trg_receive_message", "", "", 1, true, "wiredfurni.error.require_link_furni")]
    [InlineData("wf_trg_receive_message", "", "", 0, false, "wiredfurni.error.invalid_furni_source", 201)]
    [InlineData("wf_act_send_message", "2", "hello", 0, false, "wiredfurni.error.link.invalid_dataframe")]
    [InlineData("wf_act_send_message", "1", "hello", 1, true, "wiredfurni.error.require_link_furni")]
    [InlineData("wf_act_send_message", "1", "", 0, false, "ok")]
    [InlineData("wf_trg_receive_dataframe", "2", "[[\"a\",\"pa\"],[\"a\",\"pb\"]]", 0, false, "wiredfurni.error.variables.name_uniq")]
    [InlineData("wf_trg_receive_dataframe", "2", "[[\"a\",\"pa\"],[\"b\",\"pa\"]]", 0, false, "wiredfurni.error.link.destination_conflict")]
    [InlineData("wf_trg_receive_dataframe", "1", "[[\"a-b\",\"pa\"]]", 0, false, "wiredfurni.error.variables.name_syntax")]
    [InlineData("wf_trg_receive_dataframe", "1", "[[\"\",\"pa\"]]", 0, false, "wiredfurni.error.link.key_length")]
    [InlineData("wf_trg_receive_dataframe", "1", "[[\"k\",\"trigger_text\"]]", 0, false, "wiredfurni.error.placeholder.reserved")]
    [InlineData("wf_trg_receive_dataframe", "1", "[[\"k\",\"\"]]", 0, false, "wiredfurni.error.placeholder.name_length")]
    [InlineData("wf_trg_receive_dataframe", "1,1", "[[\"k\",\"\"]]", 0, false, "wiredfurni.error.link.invalid_capture_variable")]
    [InlineData("wf_trg_receive_dataframe", "2", "[[\"a\",\"pa\"]]", 0, false, "wiredfurni.error.link.invalid_dataframe")]
    [InlineData("wf_xtra_link_speaker", "1,1", "", 1, true, "wiredfurni.error.require_link_furni")]
    [InlineData("wf_xtra_link_speaker", "1,1", "", 0, false, "wiredfurni.error.invalid_furni_source_count", 100)]
    public void SaveAnswers(string name, string ints, string text, int extraPicks, bool nonLink, string expected, int source = -1)
    {
        using var save = new WiredRawSave(name);
        var link = save.Furni("wired_link", Plus.HabboHotel.Items.InteractionType.None);
        var fresh = Fresh(name);
        int[] owned = ints.Length == 0 ? [.. fresh.OwnedIntParams] : [.. ints.Split(',').Select(int.Parse)];

        if (name == "wf_trg_receive_dataframe") {
            owned = [.. owned, .. Enumerable.Repeat(0, 13 - owned.Length)];
        }

        uint[] picks = nonLink ? [link, save.Furni("other", Plus.HabboHotel.Items.InteractionType.None)] : name == "wf_xtra_link_speaker" || source == 201 ? [] : [link];
        int[]? furni = source == -1 ? null : [source];

        if (name == "wf_xtra_link_speaker" && source == -1) {
            furni = [];
        }

        var result = save.Save(owned, text, furni, picks: picks);

        Assert.Equal(expected, result.Accepted ? "ok" : result.Key ?? result.Answer);
    }

    [Fact]
    public void ReceiveDataframeStoresNormalisedKeyAndLowerCasePlaceholder()
    {
        using var save = new WiredRawSave("wf_trg_receive_dataframe");
        var link = save.Furni("wired_link", Plus.HabboHotel.Items.InteractionType.None);
        var result = save.Save([1, .. Enumerable.Repeat(0, 12)], "[[\"A b\",\"PH\"]]", picks: [link]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal("[[\"a_b\",\"ph\"]]", result.Text);
    }

    private static int[] SendRows(int count, params (int Mode, int Literal, int Target)[] rows) =>
        [count, .. Enumerable.Range(0, 12).SelectMany(row => row < rows.Length ? new[] { rows[row].Mode, rows[row].Literal, rows[row].Target } : new[] { 0, 0, -20 })];

    [Theory]
    [InlineData("[[\"a\",\"\"],[\"a\",\"\"]]", 2, -20, "wiredfurni.error.variables.name_uniq")]
    [InlineData("[[\"a\",\"\"]]", 1, 7, "wiredfurni.error.link.invalid_dataframe")]
    [InlineData("[[\"a\",\"\"]]", 2, -20, "wiredfurni.error.link.invalid_dataframe")]
    [InlineData("[[\"a\",\"\"]]", 1, -20, "ok")]
    public void SendDataframeSaveAnswers(string text, int count, int target, string expected)
    {
        using var save = new WiredRawSave("wf_act_send_dataframe");
        var link = save.Furni("wired_link", Plus.HabboHotel.Items.InteractionType.None);
        var result = save.Save(SendRows(count, (0, 5, target)), text, picks: [link]);

        Assert.Equal(expected, result.Accepted ? "ok" : result.Key ?? result.Answer);
    }

    [Theory]
    [InlineData("wf_trg_receive_dataframe")]
    [InlineData("wf_act_send_dataframe")]
    public void DataframeWithoutRowsReopensWithEmptyArrayText(string name)
    {
        using var save = new WiredRawSave(name);
        var link = save.Furni("wired_link", Plus.HabboHotel.Items.InteractionType.None);
        var result = save.Save(picks: [link]);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal("[]", result.Text);
    }

    [Theory]
    [InlineData(1000, true, "1000")]
    [InlineData(1001, false, "1000")]
    public void SendMessageTextLimitIs1000Bytes(int length, bool accepted, string limit)
    {
        using var save = new WiredRawSave("wf_act_send_message");
        var result = save.Save([1], new string('a', length));

        Assert.Equal(accepted, result.Accepted);

        if (!accepted) {
            Assert.Equal("wiredfurni.error.too_much_text", result.Key);
            Assert.Equal(limit, result.ErrorParameters!["limit"]);
        }
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void SendDataframeRowTextLimitIs200Bytes(int length, bool accepted)
    {
        using var save = new WiredRawSave("wf_act_send_dataframe");
        var result = save.Save(SendRows(1, (3, 0, -20)), "[[\"msg\",\"" + new string('a', length) + "\"]]");

        Assert.Equal(accepted, result.Accepted);

        if (!accepted) {
            Assert.Equal("wiredfurni.error.too_much_text", result.Key);
            Assert.Equal("200", result.ErrorParameters!["limit"]);
        }
    }
}
