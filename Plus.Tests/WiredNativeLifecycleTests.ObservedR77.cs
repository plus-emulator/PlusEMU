using System.Collections.Immutable;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;

namespace Plus.Tests;

/// <summary>Observed on hhus build WIN63-202610131318, r77 live-check batches 01 and 02 and r62 batches 04 and 05.</summary>
public sealed partial class WiredNativeLifecycleTests
{
    // Join Team saved [team 2, type 1] (Battle Banzai) and delivered AvatarEffect 34; [team 3, type 2] (Freeze) delivered 42.
    [Theory]
    [InlineData(1, 2, 34)]
    [InlineData(2, 3, 42)]
    [InlineData(0, 1, 223)]
    [InlineData(1, 1, 33)]
    [InlineData(2, 4, 43)]
    [InlineData(0, 4, 226)]
    public void JoinTeamSavesMapToTheObservedEffectIds(int type, int team, int effect)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_join_team", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [team, type] };
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out var runtime));
        Assert.Equal(type, runtime.IntParams[0]);
        Assert.Equal(team, runtime.IntParams[1]);
        Assert.Equal(effect, WiredGameState.TeamEffect(runtime.IntParams[0], (Team)runtime.IntParams[1]));
    }

    // r77 range windows: 1767225600..1798761600 (2026-01-01 to 2027-01-01 UTC) delivered the message, 1577836800..1609459200 (2020) did not.
    [Theory]
    [InlineData(1767225600, 1798761600, 0, true)]
    [InlineData(1767225600, 1798761600, 9, true)]
    [InlineData(1767225600, 1798761600, -7, true)]
    [InlineData(1577836800, 1609459200, 0, false)]
    [InlineData(1577836800, 1609459200, 9, false)]
    public void DateRangeConditionComparesUnixSecondsAgainstTheInstantInAnyOffset(int start, int end, int offsetHours, bool expected)
    {
        var f = new World();
        var now = new DateTimeOffset(2026, 10, 13, 20, 15, 42, TimeSpan.Zero).ToOffset(TimeSpan.FromHours(offsetHours));
        var item = f.Item(9);
        item.Definition.InteractionName = "wf_cnd_date_rng_active";
        Assert.True(WiredBoxRegistry.TryGet("wf_cnd_date_rng_active", out var descriptor));
        var condition = new WiredModernCondition(f.Room, item, descriptor, TestGroupManager.Empty, _ => null, () => now);
        WiredNativeTestSupport.Install(condition, WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [start, end] });
        Assert.Equal([start, end], condition.Configuration.IntParams.ToArray());
        Assert.Equal(expected, condition.Execute(f.Context()));
    }

    [Fact]
    public void DateRangeEndsAreInclusiveToTheSecond()
    {
        var config = new WiredConfiguration { IntParams = [1767225600, 1798761600] };
        Assert.True(WiredTimeConditions.MatchesRange(config, DateTimeOffset.FromUnixTimeSeconds(1798761600)));
        Assert.False(WiredTimeConditions.MatchesRange(config, DateTimeOffset.FromUnixTimeSeconds(1798761601)));
        Assert.False(WiredTimeConditions.MatchesRange(config, DateTimeOffset.FromUnixTimeSeconds(1767225599)));
    }

    // Show Message radio 0 arrived as a Whisper and radio 1 as a Chat, both to the owner; the saved ints were [radio, 202, -1].
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void ShowMessageVisibilityRadioSelectsWhisperOrRoomChat(int radio, bool whisper)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_show_message", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [radio, 202, -1], Text = "vis-r77" };
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out var runtime));
        Assert.Equal([0, radio, 202, -1], runtime.IntParams.ToArray());
        Assert.Equal(whisper, runtime.IntParams[1] == 0);
        var packet = new WiredChatComposer(1, "vis-r77", 202, -1, runtime.IntParams[1] == 0);
        Assert.Equal(whisper ? ServerPacketHeader.WhisperComposer : ServerPacketHeader.ChatComposer, packet.MessageId);
    }

    // The unchosen reference ("0") the official initial editor sends, with a valid name, was refused with this key and no parameters.
    [Fact]
    public void VariableReferenceWithoutAChosenReferenceIsRefusedWithTheObservedKey()
    {
        var f = new World();
        f.Room.OwnerId = 5;
        var item = f.Item(4);
        item.Definition.InteractionName = "wf_var_reference";
        var box = f.Wired.CreateConfiguredBox(item)!;
        var unchosen = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with
        {
            OwnedIntParams = [1],
            VariableIds = ["0"],
            Text = "r70_probe_reference"
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, box.Descriptor, unchosen, out var runtime));
        Assert.False(WiredConfigurationSave.TrySave(box, runtime, TestWiredConfigurationStore.Instance, out var error));
        Assert.Equal("wiredfurni.error.variables.reference.valid", error);
        var chosen = unchosen with { VariableIds = ["shared:2:room:4"] };
        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, box.Descriptor, chosen, out runtime));
        Assert.True(box.TryValidateConfiguration(runtime, out _, out error), error);
    }

    // The unchanged-form save (empty name, owned [0], id "0") was refused with the placeholder length key and no parameters.
    [Theory]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("##", true)]
    [InlineData("a", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmn", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmno", true)]
    public void TextInputVariableNameLengthOutsideOneToFortyIsRefusedWithTheObservedKey(string name, bool tooLong)
    {
        var f = new World();
        var item = f.Item(4);
        item.Definition.InteractionName = "wf_xtra_text_input_variable";
        var box = f.Wired.CreateConfiguredBox(item)!;
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with { OwnedIntParams = [0], VariableIds = ["0"], Text = name };
        Assert.True(WiredNativeEditorProjection.TryCompile(item.Id, box.Descriptor, native, out var runtime));
        Assert.False(WiredConfigurationSave.TrySave(box, runtime, TestWiredConfigurationStore.Instance, out var error));
        Assert.Equal(tooLong, error == "wiredfurni.error.placeholder.name_length");
    }

    // User Clicks Tile with a plain own furni picked was refused with this key; the invisible click tile saved.
    [Fact]
    public void ClickTileTriggerRefusesAPickThatIsNotAnInvisibleClickTile()
    {
        var f = new World();
        var plain = f.Item(2);
        plain.Definition.InteractionName = "";
        var tile = f.Item(3);
        tile.Definition.InteractionName = "room_invisible_click_tile";
        var triggerItem = f.Item(1);
        triggerItem.Definition.InteractionName = "wf_trg_click_tile";
        var trigger = f.Wired.CreateConfiguredBox(triggerItem)!;
        var native = WiredNativeEditorProjection.DefaultNative(trigger.Descriptor);
        Assert.True(WiredNativeEditorProjection.TryCompile(triggerItem.Id, trigger.Descriptor,
            native with { PrimaryItems = [new(plain.Id, false)] }, out var runtime));
        Assert.False(WiredConfigurationSave.TrySave(trigger, runtime, TestWiredConfigurationStore.Instance, out var error));
        Assert.Equal("wiredfurni.error.require_click_tiles", error);
        Assert.True(WiredNativeEditorProjection.TryCompile(triggerItem.Id, trigger.Descriptor,
            native with { PrimaryItems = [new(tile.Id, false), new(plain.Id, false)] }, out runtime));
        Assert.False(WiredConfigurationSave.TrySave(trigger, runtime, TestWiredConfigurationStore.Instance, out error));
        Assert.Equal("wiredfurni.error.require_click_tiles", error);
        Assert.True(WiredNativeEditorProjection.TryCompile(triggerItem.Id, trigger.Descriptor,
            native with { PrimaryItems = [new(tile.Id, false)] }, out runtime));
        Assert.True(WiredConfigurationSave.TrySave(trigger, runtime, TestWiredConfigurationStore.Instance, out error), error);
    }

    // Client text (WIN63-202610131318): wiredfurni.error.require_room_linker = "Can only select wired room linkers!".
    [Fact]
    public void TeleportToRoomRefusesAPickThatIsNotARoomLinker()
    {
        var f = new World();
        var tele = f.Item(2);
        tele.Definition.InteractionName = "teleport";
        tele.Definition.InteractionType = Plus.HabboHotel.Items.InteractionType.Teleport;
        var linker = f.Item(3);
        linker.Definition.InteractionName = "wired_room_linker";
        linker.Definition.InteractionType = Plus.HabboHotel.Items.InteractionType.WiredRoomLinker;
        var actionItem = f.Item(1);
        actionItem.Definition.InteractionName = "wf_act_teleport_to_room";
        var action = f.Wired.CreateConfiguredBox(actionItem)!;
        var native = WiredNativeEditorProjection.DefaultNative(action.Descriptor);
        Assert.True(WiredNativeEditorProjection.TryCompile(actionItem.Id, action.Descriptor,
            native with { PrimaryItems = [new(linker.Id, false), new(tele.Id, false)] }, out var runtime));
        Assert.False(WiredConfigurationSave.TrySave(action, runtime, TestWiredConfigurationStore.Instance, out var error));
        Assert.Equal("wiredfurni.error.require_room_linker", error);
        Assert.True(WiredNativeEditorProjection.TryCompile(actionItem.Id, action.Descriptor,
            native with { PrimaryItems = [new(linker.Id, false)] }, out runtime));
        Assert.True(WiredConfigurationSave.TrySave(action, runtime, TestWiredConfigurationStore.Instance, out error), error);
    }
}
