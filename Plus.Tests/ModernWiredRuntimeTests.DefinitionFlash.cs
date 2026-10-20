using System.Collections.Immutable;
using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A variable definition shows the kind of the last write through a real stack, with the stack's changes batched (default order)
/// or written one by one (Execute In Order).
/// </summary>
public partial class ModernWiredRuntimeTests
{
    // Observed on AIR WIN63-202610151745-318700621: give 5 shows 6, give 5 again 7, add 1 shows 2, add 1 again 3, remove 8, a remove
    // with nothing to remove sends no update, give 6, add 1 shows 2.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADefinitionShowsTheChangeStateWhenAStacksChangeMovesTheValue(bool ordered)
    {
        using var stack = new DefinitionFlashStack(ordered);
        Assert.Equal(("6", 5L), stack.Run("wf_act_give_var", [1, 0, 5, 1, 0, -10, 0]));
        Assert.Equal(("7", 5L), stack.Run("wf_act_give_var", [1, 0, 5, 1, 0, -10, 0]));
        Assert.Equal(("2", 6L), stack.Run("wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0]));
        Assert.Equal(("3", 7L), stack.Run("wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0]));
        Assert.Equal(("8", (long?)null), stack.Run("wf_act_remove_var", [1]));
        Assert.Equal(("8", (long?)null), stack.Run("wf_act_remove_var", [1]));
        Assert.Equal(("6", 5L), stack.Run("wf_act_give_var", [1, 0, 5, 1, 0, -10, 0]));
        Assert.Equal(("2", 6L), stack.Run("wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0]));
    }

    // Observed on AIR WIN63-202610151745-318700621 (r97_definer_same): give 5 shows 6, assign 5 shows 0, assign 5 again 1, give 6,
    // assign 5 shows 0.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADefinitionFlipsBetweenZeroAndOneWhenAStacksChangeKeepsTheValue(bool ordered)
    {
        using var stack = new DefinitionFlashStack(ordered);
        Assert.Equal(("6", 5L), stack.Run("wf_act_give_var", [1, 0, 5, 1, 0, -10, 0]));
        Assert.Equal(("0", 5L), stack.Run("wf_act_change_var_val", [1, 0, 0, 0, 5, -10, 0]));
        Assert.Equal(("1", 5L), stack.Run("wf_act_change_var_val", [1, 0, 0, 0, 5, -10, 0]));
        Assert.Equal(("6", 5L), stack.Run("wf_act_give_var", [1, 0, 5, 1, 0, -10, 0]));
        Assert.Equal(("0", 5L), stack.Run("wf_act_change_var_val", [1, 0, 0, 0, 5, -10, 0]));
    }

    // Observed on AIR WIN63-202610151745-318700621: give 5 shows 6, subtract 1 shows 4, subtract 1 again 5, add 1 shows 2, add 1
    // again 3, subtract 1 shows 4.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADefinitionShowsTheDecreaseStateWhenAStacksChangeLowersTheValue(bool ordered)
    {
        using var stack = new DefinitionFlashStack(ordered);
        Assert.Equal(("6", 5L), stack.Run("wf_act_give_var", [1, 0, 5, 1, 0, -10, 0]));
        Assert.Equal(("4", 4L), stack.Run("wf_act_change_var_val", [1, 2, 0, 0, 1, -10, 0]));
        Assert.Equal(("5", 3L), stack.Run("wf_act_change_var_val", [1, 2, 0, 0, 1, -10, 0]));
        Assert.Equal(("2", 4L), stack.Run("wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0]));
        Assert.Equal(("3", 5L), stack.Run("wf_act_change_var_val", [1, 1, 0, 0, 1, -10, 0]));
        Assert.Equal(("4", 4L), stack.Run("wf_act_change_var_val", [1, 2, 0, 0, 1, -10, 0]));
    }

    // Observed on AIR WIN63-202610151745-318700621: give 5 and add 3 in one execution show the give state, 6 and then 7 on the next
    // execution, with or without Execute In Order. A change in a later execution shows its own state again.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ADefinitionShowsTheGiveStateWhenOneExecutionGivesAndChangesTheVariable(bool ordered)
    {
        using var stack = new DefinitionFlashStack(ordered);
        (string, int[]) give = ("wf_act_give_var", [1, 0, 5, 1, 0, -10, 0]);
        (string, int[]) add = ("wf_act_change_var_val", [1, 1, 0, 0, 3, -10, 0]);
        Assert.Equal(("6", 8L), stack.Run(give, add));
        Assert.Equal("7", stack.Run(give, add).State);
        Assert.Equal("2", stack.Run(add).State);
    }

    // Only the first write of each firing counts in a flash batch, also when another firing writes in between.
    [Fact]
    public void AFiringsLaterWriteDoesNotCountAgainAfterAnotherFiringWritesInTheSameBatch()
    {
        using var f = new TeleportFixture();
        var definition = MakeItem(10, "wf_var_user");
        object first = new(), second = new();
        var wired = f.Room.GetWired();

        wired.FlashDefinition(definition, Plus.HabboHotel.Items.Wired.WiredFlashStates.DefinitionGiven, first);
        wired.FlashDefinition(definition, Plus.HabboHotel.Items.Wired.WiredFlashStates.DefinitionChanged, second);
        wired.FlashDefinition(definition, Plus.HabboHotel.Items.Wired.WiredFlashStates.DefinitionChanged, first);

        Assert.Equal("2", definition.LegacyDataString);
    }

    private sealed class DefinitionFlashCatalog : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 5;
        public WiredVariableDefinition? Find(uint id) => id == 10
            ? new(10, 1, 5, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true) : null;
    }

    /// <summary>The enter-room stack with one variable action at a time; the user variable's definition (10) stands on its own tile.</summary>
    private sealed class DefinitionFlashStack : IDisposable
    {
        private readonly TeleportFixture _f = new();
        private readonly WiredVariableModule _module;
        private readonly Item _definition;
        private readonly List<uint> _actions = [];
        private uint _action = 200;

        public DefinitionFlashStack(bool ordered)
        {
            _f.Room.Id = 1;
            _f.Engine.Remove(_f.Action.Item.Id);
            _f.Items.TryRemove(_f.Action.Item.Id, out _);
            _module = new WiredVariableModule(1, new DefinitionFlashCatalog(), new MemoryWiredVariableStore(), TimeProvider.System);
            typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(_f.Room.GetWired().Variables, _module);
            _definition = Place(10, "wf_var_user", 2, 2);

            if (ordered) {
                Assert.True(WiredBoxRegistry.TryGet("wf_xtra_exec_in_order", out var descriptor));
                var addon = _f.Room.GetWired().CreateConfiguredBox(Place(99, "wf_xtra_exec_in_order", 0, 0), descriptor)!;
                WiredNativeTestSupport.Install(addon, WiredNativeEditorProjection.DefaultNative(descriptor));
                Assert.True(_f.Engine.Add(addon));
            }
        }

        /// <summary>Fires the stack with only this action and returns the definition's state and the user's value.</summary>
        public (string State, long? Value) Run(string name, int[] owned) => Run((name, owned));

        /// <summary>Fires the stack with only these actions, in this order under Execute In Order.</summary>
        public (string State, long? Value) Run(params (string Name, int[] Owned)[] actions)
        {
            foreach (var id in _actions) {
                _f.Engine.Remove(id);
                _f.Items.TryRemove(id, out _);
            }

            _actions.Clear();

            foreach (var (name, owned) in actions) {
                _action++;
                Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
                var box = new WiredVariableConfiguredBox(_f.Room, Place(_action, name, 0, 0), descriptor, new(_module, TimeProvider.System));
                WiredNativeTestSupport.Install(box, WiredNativeEditorProjection.DefaultNative(descriptor) with
                {
                    OwnedIntParams = ImmutableArray.Create(owned),
                    VariableIds = name == "wf_act_remove_var" ? ["user:10"] : ["user:10", "n"]
                });
                Assert.True(_f.Engine.Add(box));
                _actions.Add(_action);
            }

            _f.Fire();
            Assert.Empty(_f.Errors);
            var holder = WiredVariableRuntimeFrames.UserHolder(_f.User);

            return (_definition.LegacyDataString, _module.Read(new(WiredVariableTarget.User, "custom:10"), holder, new(1, [holder]))?.Value);
        }

        private Item Place(uint id, string name, int x, int y)
        {
            var item = MakeItem(id, name);
            typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, _f.Room);
            item.RoomId = _f.Room.Id;
            item.SetState(x, y, 0);
            _f.Items[id] = item;

            return item;
        }

        public void Dispose() => _f.Dispose();
    }
}
