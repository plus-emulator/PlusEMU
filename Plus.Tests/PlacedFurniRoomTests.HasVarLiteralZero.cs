using System.Reflection;
using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

// The transported native 40/41 shape: owned [1], F100, U0, quantifier 0, variable id "0", empty text, picks and secondary.
// Positive output 0 and negative output 3 are bounded output constraints, not a meaning of the literal 0.
public partial class PlacedFurniRoomTests
{
    private const uint HasVarId = 702;

    // Control: the shape is structurally compilable, so a rejection below is not a compile fault.
    [Theory]
    [InlineData("wf_cnd_has_var")]
    [InlineData("wf_cnd_neg_has_var")]
    public void NativeSavedHasVarLiteralZeroRowCompilesThroughTheRealSchema2Store(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        var config = HasVarStore(name).Load(HasVarId, descriptor);

        Assert.NotNull(config);
        Assert.NotNull(config.Origin?.Native);
        Assert.Equal([0, 0, 100, 0], config.IntParams.ToArray());
        Assert.Equal(["0"], config.VariableIds.ToArray());
        Assert.Equal(("", 0, 0), (config.Text, config.SelectedItems.Length, config.SecondarySelectedItems.Length));
    }

    // Normal admission, load: a saved row that compiles is admitted by the real room load like any other card.
    [Theory]
    [InlineData("wf_cnd_has_var")]
    [InlineData("wf_cnd_neg_has_var")]
    public void NormalRoomLoadAdmitsTheNativeSavedHasVarLiteralZeroRow(string name)
    {
        var (wired, _) = PrepareSpeech(new SpeechClock());
        SetHasVarStore(wired, name);
        var item = HasVarItem(name, 1, 1);

        var loaded = wired.LoadWiredBox(item);

        Assert.NotNull(loaded);
        Assert.True(wired.TryGet(HasVarId, out _), "the room did not register the saved has-var box");
    }

    // Normal admission, save: the real editor save path accepts and persists the same native shape.
    [Theory]
    [InlineData("wf_cnd_has_var")]
    [InlineData("wf_cnd_neg_has_var")]
    public async Task NormalEditorSaveAdmitsTheNativeHasVarLiteralZero(string name)
    {
        var (box, store) = CanonicalBox(name, null!);
        var handler = new SaveWiredConditionConfigEvent(CanonicalService(store));

        await handler.Parse(_client, ClientPacket(601, 1, 1, "", 0, 0, 1, 100, 1, 0, 1, "0", 0));

        var saved = Assert.Single(store.Saves);
        Assert.Equal(["0"], saved.VariableIds.ToArray());
        Assert.Same(saved, box.Configuration);
        Assert.Contains(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
    }

    // Control: the same save packet with a catalog-shaped variable id is accepted by the real save path, so the rejection above is the literal "0" token and not the save fixture.
    [Theory]
    [InlineData("wf_cnd_has_var")]
    [InlineData("wf_cnd_neg_has_var")]
    public async Task NormalEditorSaveAcceptsTheSameShapeWithACatalogShapedVariableId(string name)
    {
        var (box, store) = CanonicalBox(name, null!);
        var handler = new SaveWiredConditionConfigEvent(CanonicalService(store));

        await handler.Parse(_client, ClientPacket(601, 1, 1, "", 0, 0, 1, 100, 1, 0, 1, "user:1", 0));

        var saved = Assert.Single(store.Saves);
        Assert.Equal(["user:1"], saved.VariableIds.ToArray());
        Assert.Same(saved, box.Configuration);
    }

    // Direct adapter boundary, not the normal load: the compiled configuration is applied straight onto the configured adapter,
    // skipping admission, and a real speech trigger drains the stack. Positive stays quiet, negative lets the action run.
    [Theory]
    [InlineData("wf_cnd_has_var", "pulse")]
    [InlineData("wf_cnd_neg_has_var", "A")]
    public void DirectAdapterWithoutAdmissionRunsTheStackLikeTheBoundedOfficialOutputs(string name, string expected)
    {
        var (wired, actor) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "pulse" }, 0);
        var condition = WiredBox(HasVarId, name, 1, 1);
        condition.Item.GetZ = 1;
        var config = HasVarStore(name).Load(HasVarId, condition.Descriptor);
        Assert.NotNull(config);
        condition.ApplyConfiguration(config);
        Assert.True(wired.AddBox(condition));
        AddSpeechBox(303, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "A" }, 2);
        _client.Packets.Clear();

        actor.OnChat(0, "pulse", false);
        wired.OnFastCycle();

        Assert.Equal([expected], SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    // Normal admission and execution together: the card is admitted by the real load from its schema 2 row, and a real speech trigger drains the stack.
    // Positive stays quiet (the echo is kept) and negative lets the action run, which are the bounded output constraints, not a meaning of the literal 0.
    [Theory]
    [InlineData("wf_cnd_has_var", "pulse")]
    [InlineData("wf_cnd_neg_has_var", "A")]
    public void NormalLoadAdmittedHasVarLiteralZeroDrivesTheStackLikeTheBoundedOfficialOutputs(string name, string expected)
    {
        var (wired, actor) = PrepareSpeech(new SpeechClock());
        SetHasVarStore(wired, name);
        AddSpeechBox(301, "wf_trg_says_something", new() { IntParams = [1, 1, 0], Text = "pulse" }, 0);
        var item = HasVarItem(name, 1, 1);
        item.GetZ = 1;
        Assert.NotNull(wired.LoadWiredBox(item));
        AddSpeechBox(303, "wf_act_show_message", new() { IntParams = [0, 1, 34, -1], Text = "A" }, 2);
        _client.Packets.Clear();

        actor.OnChat(0, "pulse", false);
        wired.OnFastCycle();

        Assert.Equal([expected], SpeechMessages(ServerPacketHeader.ChatComposer));
    }

    // Guard: with no live trigger user (a game-start trigger carries none) both cards fail, so the action never runs for either.
    [Theory]
    [InlineData("wf_cnd_has_var")]
    [InlineData("wf_cnd_neg_has_var")]
    public void HasVarLiteralZeroWithoutALiveTriggerUserFailsBothCards(string name)
    {
        var (wired, _) = PrepareSpeech(new SpeechClock());
        AddSpeechBox(301, "wf_trg_game_starts", new(), 0);
        var condition = WiredBox(HasVarId, name, 1, 1);
        condition.Item.GetZ = 1;
        var config = HasVarStore(name).Load(HasVarId, condition.Descriptor);
        Assert.NotNull(config);
        condition.ApplyConfiguration(config);
        Assert.True(wired.AddBox(condition));
        AddSpeechBox(303, "wf_act_log", new() { IntParams = [1, 0], Text = "ran" }, 2);

        wired.TriggerEvent(WiredBoxType.TriggerGameStarts);
        wired.OnFastCycle();
        wired.OnCycle();

        Assert.DoesNotContain("ran", wired.ReadLogs(0, 100).Entries.Select(entry => entry.Message));
    }

    // Guard: only the exact measured has-var shape is admitted. Other targets, sources, quantifier, picks and other ids stay rejected by the real load.
    [Theory]
    [InlineData("wf_cnd_has_var", "target-furni")]
    [InlineData("wf_cnd_neg_has_var", "target-context")]
    [InlineData("wf_cnd_has_var", "furni-source-200")]
    [InlineData("wf_cnd_neg_has_var", "user-source-200")]
    [InlineData("wf_cnd_has_var", "quantifier-1")]
    [InlineData("wf_cnd_neg_has_var", "pick")]
    [InlineData("wf_cnd_has_var", "id-1")]
    [InlineData("wf_cnd_neg_has_var", "id-00")]
    [InlineData("wf_cnd_has_var", "id-minus-0")]
    [InlineData("wf_cnd_neg_has_var", "id-0-space")]
    [InlineData("wf_cnd_has_var", "id-0x0")]
    [InlineData("wf_cnd_neg_has_var", "id-empty-second")]
    public void OnlyTheExactMeasuredHasVarLiteralZeroShapeIsAdmittedByTheRealLoad(string name, string change)
    {
        var native = HasVarNative(name);
        native = change switch
        {
            "target-furni" => native with { OwnedIntParams = [0] },
            "target-context" => native with { OwnedIntParams = [-20] },
            "furni-source-200" => native with { FurniSourceTypes = [200] },
            "user-source-200" => native with { UserSourceTypes = [200] },
            "quantifier-1" => native with { Quantifier = 1 },
            "pick" => native with { PrimaryItems = [new(601, false)] },
            "id-1" => native with { VariableIds = ["1"] },
            "id-00" => native with { VariableIds = ["00"] },
            "id-minus-0" => native with { VariableIds = ["-0"] },
            "id-0-space" => native with { VariableIds = ["0 "] },
            "id-0x0" => native with { VariableIds = ["0x0"] },
            "id-empty-second" => native with { VariableIds = ["0", ""] },
            _ => throw new ArgumentException(change)
        };
        var (wired, _) = PrepareSpeech(new SpeechClock());
        SetStore(wired, new(HasVarId, name, 2, JsonSerializer.Serialize(native)));
        var item = HasVarItem(name, 1, 1);

        Assert.Null(wired.LoadWiredBox(item));
        Assert.False(wired.TryGet(HasVarId, out _));
    }

    // Guard: another variable card carrying the same literal 0 is not admitted.
    [Fact]
    public void AnotherVariableCardWithTheLiteralZeroStaysRejectedByTheRealLoad()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_cnd_var_age_match", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [1, 0, 0, 0, 0, 1],
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            VariableIds = ["0"],
            Quantifier = 0
        };
        var (wired, _) = PrepareSpeech(new SpeechClock());
        SetStore(wired, new(HasVarId, "wf_cnd_var_age_match", 2, JsonSerializer.Serialize(native)));
        Assert.True(WiredNativeEditorProjection.TryCompile(HasVarId, descriptor, native, out _), "the other card's row must compile so the rejection is the token");

        Assert.Null(wired.LoadWiredBox(HasVarItem("wf_cnd_var_age_match", 1, 1)));
    }

    private static void SetStore(Plus.HabboHotel.Rooms.Instance.WiredComponent wired, ModernWiredRuntimeTests.StoredRuntimeRow row) =>
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new WiredConfigurationStore(new ModernWiredRuntimeTests.StoredRuntimeRowsDatabase([row])));

    private static WiredNativeEditorConfiguration HasVarNative(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        return new()
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [1],
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            VariableIds = ["0"],
            Quantifier = 0
        };
    }

    private static WiredConfigurationStore HasVarStore(string name) => new(new ModernWiredRuntimeTests.StoredRuntimeRowsDatabase(
        [new(HasVarId, name, 2, JsonSerializer.Serialize(HasVarNative(name)))]));

    private static void SetHasVarStore(Plus.HabboHotel.Rooms.Instance.WiredComponent wired, string name) =>
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, HasVarStore(name));

    private Item HasVarItem(string name, int x, int y)
    {
        var item = Furni(HasVarId, InteractionType.None, WiredBoxType.None);
        item.Definition.InteractionName = item.Definition.ItemName = name;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, x, y, 0, true, false, false));

        return item;
    }
}
