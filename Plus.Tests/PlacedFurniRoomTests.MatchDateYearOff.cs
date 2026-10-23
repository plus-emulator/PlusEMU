using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// A fresh Date Matches frame holds year minimum and maximum 0, and a save with "Use Year" off keeps them at 0. The year range is only read when "Use Year" is on.
public partial class PlacedFurniRoomTests
{
    private const uint MatchDateId = 703;

    [Fact]
    public void AFreshMatchDateOpensWithBothYearsAtZero()
    {
        using var save = new WiredRawSave("wf_cnd_match_date");

        Assert.Equal([0, 0, 127, 1, 1, 4095, 0, 0], save.Stored().Ints);
    }

    [Fact]
    public void SavingTheFreshFrameWithUseYearOffReopensWithBothYearsAtZero()
    {
        using var save = new WiredRawSave("wf_cnd_match_date");
        var ints = save.Stored().Ints;
        ints[0] = 1;
        ints[3] = 21;
        ints[4] = 21;

        var saved = save.Save(ints, "UTC");

        Assert.True(saved.Accepted, saved.ToString());
        Assert.Equal([1, 0, 127, 21, 21, 4095, 0, 0], save.Stored().Ints);
    }

    // The 2026 years are a row the earlier fresh frame wrote; with "Use Year" off it must still load and ignore the year.
    [Theory]
    [InlineData(0, 0, 21, "matched")]
    [InlineData(2026, 2026, 21, "matched")]
    [InlineData(0, 0, 22, "pulse")]
    [InlineData(2026, 2026, 22, "pulse")]
    public void AStoredMatchDateWithUseYearOffIgnoresTheYearInTheRoomCycle(int yearMinimum, int yearMaximum, int today, string expected)
    {
        var (wired, actor) = PrepareSpeech(new SpeechClock { Now = new(2031, 3, today, 12, 0, 0, TimeSpan.Zero) });
        Assert.True(WiredBoxRegistry.TryGet("wf_cnd_match_date", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [1, 0, 127, 21, 21, 4095, yearMinimum, yearMaximum],
            Text = "UTC"
        };
        SetStore(wired, new(MatchDateId, "wf_cnd_match_date", 2, JsonSerializer.Serialize(native)));
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "pulse" }, 0);
        var item = Furni(MatchDateId, InteractionType.None, WiredBoxType.None);
        item.Definition.InteractionName = item.Definition.ItemName = "wf_cnd_match_date";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, 1, 1, 0, true, false, false));
        item.GetZ = 1;
        Assert.NotNull(wired.LoadWiredBox(item));
        AddSpeechBox(303, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "matched" }, 2);
        _client.Packets.Clear();

        actor.OnChat(0, "pulse", false);
        wired.OnFastCycle();

        Assert.Equal([expected], SpeechMessages(ServerPacketHeader.ChatComposer));
    }
}
