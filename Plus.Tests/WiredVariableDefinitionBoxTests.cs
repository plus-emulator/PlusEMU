using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed class WiredVariableDefinitionBoxTests
{
    [Theory]
    // Give, Change, Value and Age drafts follow the October editors' fresh-box payloads, so they carry the reference fields.
    // Observed on hhus (AIR WIN63-202610131318-860371703): Give and Change save with no variable picked; they still change nothing.
    [InlineData("wf_act_give_var", 8, true)]
    [InlineData("wf_act_remove_var", 3, false)]
    [InlineData("wf_act_change_var_val", 10, true)]
    // A fresh Has Variable opens on the hotel's literal "0" slot, the measured shape that saves but never resolves (IsUnresolvedLiteralZero).
    [InlineData("wf_cnd_has_var", 4, true)]
    [InlineData("wf_cnd_neg_has_var", 4, true)]
    [InlineData("wf_cnd_var_val_match", 11, false)]
    [InlineData("wf_cnd_var_age_match", 10, false)]
    public void ScalarDraftOpensWithClientShapeButCannotExecuteWithoutSelection(string name, int count, bool saves)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        room.Id = 1;
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var module = new WiredVariableModule(1, new EmptyDirectory(), new MemoryWiredVariableStore(), new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1)));
        var box = new WiredVariableConfiguredBox(room, new Item { Id = 10 }, descriptor, new(module, new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1))));
        Assert.Equal(count, box.Configuration.IntParams.Length);
        Assert.False(box.HasPersistedConfiguration);
        Assert.Equal(saves, box.TryValidateConfiguration(box.Configuration, out _, out _));
        Assert.False(box.Execute(new WiredVariableFrame(1, [])));
        Assert.Empty(module.DrainChanges());
    }
    private sealed class EmptyDirectory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint id) => null;
        public uint? GetRoomOwner(uint roomId) => 5;
    }
    [Theory]
    [InlineData("wf_var_user", "score", 1, 10)]
    [InlineData("wf_var_furni", "score", 1, 1)]
    [InlineData("wf_var_room", "score", 10, 7)]
    [InlineData("wf_var_context", "score", 1, -1)]
    [InlineData("wf_var_echo", "{\"variableName\":\"alias\",\"sourceTargetType\":0,\"sourceVariableToken\":\"custom:20\"}", -1, -1)]
    [InlineData("wf_var_reference", "{\"variableName\":\"alias\",\"sourceTargetType\":0,\"sourceRoomId\":2,\"sourceVariableItemId\":20,\"readOnly\":true}", -1, -1)]
    public void PassiveDefinitionValidatesWithoutPublishingOrExecutingAnAction(string name, string text, int first, int second)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        room.Id = 1;
        room.OwnerId = 5;
        var item = new Item { Id = 10, Definition = new() { ItemName = "arbitrary_catalog_name", InteractionName = name } };
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var box = new WiredVariableDefinitionBox(room, item, descriptor);
        var draft = box.Configuration;
        Assert.False(box.HasPersistedConfiguration);
        Assert.False(box.TryValidateConfiguration(draft, out _, out _));
        var native = WiredNativeTestSupport.FromLegacyVariableDraft(descriptor, new WiredConfiguration { Text = text, IntParams = first < 0 ? [] : second < 0 ? [first] : [first, second] });
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out var proposed));
        Assert.True(box.TryValidateConfiguration(proposed, out var valid, out var error), error);
        Assert.Equal(draft, box.Configuration); // Pure validation has no publication or persistence side effects.
        box.ApplyConfiguration(valid);
        Assert.Equal(proposed, box.Configuration);
        Assert.False(box.Execute());
        Assert.True(box.HasPersistedConfiguration);
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { Text = "invalid-name" }, out var invalid));
        Assert.False(box.TryValidateConfiguration(invalid, out _, out _));
        Assert.Equal(proposed, box.Configuration);
    }
}
