using System.Reflection;
using System.Text.Json;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// Selector stacks saved through the native editor rows (AIR WIN63-202610131318-860371703, UpdateSelectorMessageComposer.as:11-47):
/// a first selector seeds the selection and a second one with "Filter existing selection" narrows it.
/// </summary>
public partial class ModernWiredRuntimeTests
{
    private const uint Block = 900;

    private sealed class SelectorStack : IDisposable
    {
        public readonly TeleportFixture F = new();
        public readonly SelectionCapture Capture;
        public readonly Item[] Blocks;
        public readonly RoomUser Bob;
        public readonly WiredVariableModule Module;
        private uint _next = 300;

        public SelectorStack()
        {
            F.Room.Id = 1;
            // Three blocks of one type on row 2 and a second player; the fixture's owner stands at (0, 0) on the trigger.
            var definition = MakeItem(Block, "block").Definition;
            definition.Id = Block;
            Blocks = [.. Enumerable.Range(0, 3).Select(x => Place((uint)(201 + x), "block", x, 2, definition))];
            Bob = F.AddPlayer(8, 2, 0);
            F.Engine.Remove(F.Action.Item.Id);
            F.Items.TryRemove(F.Action.Item.Id, out _);
            Capture = new(F.Room, Place(100, "wf_act_toggle_state", 0, 0));
            Assert.True(F.Engine.Add(Capture));
            Module = new WiredVariableModule(1, new SelectorVariableCatalog(), new MemoryWiredVariableStore(), TimeProvider.System,
                new RoomWiredBuiltinVariables(F.Room, F.Room.GetWired().ReadBuiltin));
            typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(F.Room.GetWired().Variables, Module);
        }

        public void Give(Item item, long value) => Assert.True(Module.Mutate(new(WiredVariableTarget.Furni, "custom:10"),
            WiredVariableRuntimeFrames.FurniHolder(item), WiredVariableMutation.Give, value, new(1, [WiredVariableRuntimeFrames.FurniHolder(item)])));

        public void Give(RoomUser user, long value) => Assert.True(Module.Mutate(new(WiredVariableTarget.User, "custom:11"),
            WiredVariableRuntimeFrames.UserHolder(user), WiredVariableMutation.Give, value, new(1, [WiredVariableRuntimeFrames.UserHolder(user)])));

        public Item Place(uint id, string name, int x, int y, ItemDefinition? definition = null, bool temporary = false)
        {
            var made = MakeItem(id, name);
            var item = temporary ? new Item { Id = id, ExtraData = made.ExtraData, Definition = made.Definition, IsTemporary = true } : made;

            if (definition != null) {
                item.Definition = definition;
            }

            typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, F.Room);
            item.RoomId = F.Room.Id;
            item.SetState(x, y, 0, Gamemap.GetAffectedTiles(1, 1, x, y, 0));
            F.Items[id] = item;

            return item;
        }

        /// <summary>A selector saved from the given native row, on the trigger's tile unless placed elsewhere (a stack without trigger).</summary>
        public IWiredConfiguredItem Selector(string name, string row, int x = 0, int y = 0)
        {
            var id = _next++;
            var item = Place(id, name, x, y);
            Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
            var box = F.Room.GetWired().CreateConfiguredBox(item, descriptor)!;
            var native = JsonSerializer.Deserialize<WiredNativeEditorConfiguration>(row.Replace("@", id.ToString(System.Globalization.CultureInfo.InvariantCulture)))!;
            Assert.True(WiredNativeEditorProjection.TryCompile(id, descriptor, native, out var runtime), name);
            box.ApplyConfiguration(runtime);
            Assert.True(F.Engine.Add(box));

            return box;
        }

        public WiredRuntimeContext Fire()
        {
            var fired = F.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.Enter) { Actor = F.User });
            F.Engine.OnFastCycle();
            Assert.True(fired, string.Join("\n", F.Errors));
            Assert.Empty(F.Errors);

            return Assert.Single(Capture.Contexts);
        }

        public uint[] Furni(WiredRuntimeContext context) => [.. context.Selected.FurniIds.Order()];
        public int[] Users(WiredRuntimeContext context) => [.. context.Selected.UserIds.Order()];

        public void Dispose() => F.Dispose();
    }

    private sealed class SelectorVariableCatalog : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 5;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            10 => new(10, 1, 5, "points", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true),
            11 => new(11, 1, 5, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            _ => null
        };
    }

    private static string Row(int code, string owned, bool filter, bool invert, string furni = "[]", string users = "[]",
        string picks = "[]", string variables = "[]", string text = "") =>
        $$$"""{"Version":2,"Category":3,"NativeCode":{{{code}}},"OwnedIntParams":{{{owned}}},"Text":"{{{text}}}","PrimaryItems":{{{picks}}},"SecondaryItems":[],"FurniSourceTypes":{{{furni}}},"UserSourceTypes":{{{users}}},"VariableIds":{{{variables}}},"Delay":null,"Quantifier":null,"Filter":{{{(filter ? "true" : "false")}}},"Inverse":{{{(invert ? "true" : "false")}}},"SavedState":{"Snapshots":[]}}""";

    private static string Pick(uint id) => $$"""[{"ItemId":{{id}},"Wall":false}]""";

    // Furni of the picked block's type (class_3961.as:34-48).
    private static string BlocksByType(bool filter = false, bool invert = false) => Row(0, "[0]", filter, invert, furni: "[100]", picks: Pick(201));

    // Users By Type (class_4313.as:34-47): Habbo 1, Pet 2, Bot 4.
    private static string Habbos(bool filter = false, bool invert = false) => Row(2, "[1]", filter, invert);

    private static string UsersByName(string names, bool filter = false, bool invert = false) => Row(11, "[]", filter, invert, text: names);

    // Users in an area (InArea.as:100-123): x, y, width, length.
    private static string UsersArea(int x, int y, int width, int length, bool filter = false, bool invert = false) =>
        Row(13, $"[{x},{y},{width},{length}]", filter, invert);

    // With Variable (WithVariable.as:144-150): "select by value" equals the value, or every holder of the variable.
    private static string Points(bool filter, bool invert, int? value = 2) =>
        Row(17, value is { } v ? $"[1,1,0,{v},-10,0]" : "[2,0,0,0,-10,0]", filter, invert, furni: "[100]", users: "[0]", variables: """["furni:10","n"]""");

    private static string Score(bool filter, bool invert, int? value = 2) =>
        Row(18, value is { } v ? $"[1,1,0,{v},-10,0]" : "[2,0,0,0,-10,0]", filter, invert, furni: "[100]", users: "[0]", variables: """["user:11","n"]""");

    // Remote Selection (class_4144.as:38-60): [type (Union 0, Intersection 1), random amount (0 = all stacks)], stacks picked from source 100.
    private static string Remote(uint[] stacks, int type = 0, int amount = 0, bool filter = false, bool invert = false) =>
        Row(19, $"[{type},{amount}]", filter, invert, furni: "[100]",
            picks: "[" + string.Join(",", stacks.Select(id => $$"""{"ItemId":{{id}},"Wall":false}""")) + "]");

    // Observed on hhus (R95-C5, AIR WIN63-202610131318-860371703): a filter narrows the selection of the other selectors of its kind,
    // wherever it stands, and Invert keeps the non-matching ones. Variable filters read each candidate's own value.
    [Theory]
    [InlineData(false, false, true, new uint[] { 203 })]
    [InlineData(false, true, true, new uint[] { 201, 202 })]
    [InlineData(true, false, true, new uint[] { 203 })]
    [InlineData(false, false, false, new uint[] { 202, 203 })]
    [InlineData(false, true, false, new uint[] { 201 })]
    public void FurniWithVariableFilterNarrowsTheTypeSelection(bool filterFirst, bool invert, bool byValue, uint[] expected)
    {
        using var s = new SelectorStack();
        s.Give(s.Blocks[1], 1);
        s.Give(s.Blocks[2], 2);

        if (filterFirst) {
            s.Selector("wf_slc_furni_with_var", Points(true, invert, byValue ? 2 : null));
            s.Selector("wf_slc_furni_bytype", BlocksByType());
        }
        else {
            s.Selector("wf_slc_furni_bytype", BlocksByType());
            s.Selector("wf_slc_furni_with_var", Points(true, invert, byValue ? 2 : null));
        }

        Assert.Equal(expected, s.Furni(s.Fire()));
    }

    [Theory]
    [InlineData(false, new[] { 8 })]
    [InlineData(true, new[] { 7 })]
    public void UserWithVariableFilterNarrowsTheTypeSelection(bool invert, int[] expected)
    {
        using var s = new SelectorStack();
        s.Give(s.F.User, 1);
        s.Give(s.Bob, 2);
        s.Selector("wf_slc_users_bytype", Habbos());
        s.Selector("wf_slc_users_with_var", Score(true, invert));

        Assert.Equal(expected, s.Users(s.Fire()));
    }

    [Fact]
    public void ANameFilterNarrowsTheAreaSelection()
    {
        using var s = new SelectorStack();
        s.Selector("wf_slc_users_area", UsersArea(0, 0, 3, 1));
        s.Selector("wf_slc_users_byname", UsersByName("alice", filter: true));

        Assert.Equal(new[] { 7 }, s.Users(s.Fire()));
    }

    // A furni filter leaves the users of a user selector alone, and a user filter the furni (127-3).
    [Fact]
    public void FiltersOnlyNarrowTheirOwnKind()
    {
        using var s = new SelectorStack();
        s.Give(s.Blocks[2], 2);
        s.Selector("wf_slc_furni_bytype", BlocksByType());
        s.Selector("wf_slc_users_bytype", Habbos());
        s.Selector("wf_slc_furni_with_var", Points(true, false));
        s.Selector("wf_slc_users_byname", UsersByName("alice", filter: true));

        var context = s.Fire();

        Assert.Equal(new uint[] { 203 }, s.Furni(context));
        Assert.Equal(new[] { 7 }, s.Users(context));
    }

    // Observed on hhus (AIR WIN63-202610131318-860371703, r96 S2): Union keeps what each stack selected; Intersection works per kind
    // across every picked stack, so a furni-only stack intersected with a users-only stack selects neither kind.
    [Theory]
    [InlineData(0, new uint[] { 201, 202, 203 }, new[] { 7 })]
    [InlineData(1, new uint[0], new int[0])]
    public void RemoteCombinesTheReferencedStacksForBothKinds(int type, uint[] furni, int[] users)
    {
        using var s = new SelectorStack();
        var blocks = s.Selector("wf_slc_furni_bytype", BlocksByType(), 2, 1);
        var area = s.Selector("wf_slc_users_area", UsersArea(0, 0, 1, 1), 0, 1);
        s.Selector("wf_slc_remote", Remote([blocks.Item.Id, area.Item.Id], type));

        var context = s.Fire();

        Assert.Equal(furni, s.Furni(context));
        Assert.Equal(users, s.Users(context));
    }

    [Fact]
    public void RemoteIntersectionKeepsWhatEveryStackSelected()
    {
        using var s = new SelectorStack();
        s.Give(s.Blocks[2], 2);
        var all = s.Selector("wf_slc_furni_bytype", BlocksByType(), 2, 1);
        var valued = s.Selector("wf_slc_furni_with_var", Points(false, false), 0, 1);
        s.Selector("wf_slc_remote", Remote([all.Item.Id, valued.Item.Id], 1));

        Assert.Equal(new uint[] { 203 }, s.Furni(s.Fire()));
    }

    // Observed on hhus (r96 S1): a picked stack without a selector is an empty operand, and picking any box of a stack selects that
    // whole stack.
    [Theory]
    [InlineData(0, new[] { 7 })]
    [InlineData(1, new int[0])]
    public void ARemotePickWithoutASelectorCountsAsAnEmptyStack(int type, int[] users)
    {
        using var s = new SelectorStack();
        var area = s.Selector("wf_slc_users_area", UsersArea(0, 0, 1, 1), 0, 1);
        var plain = s.Place(260, "wf_cnd_has_furni_on", 4, 1);
        s.Selector("wf_slc_remote", Remote([area.Item.Id, plain.Id], type));

        Assert.Equal(users, s.Users(s.Fire()));
    }

    [Fact]
    public void PickingAnyBoxOfAStackSelectsTheWholeStack()
    {
        using var s = new SelectorStack();
        s.Selector("wf_slc_furni_bytype", BlocksByType(), 2, 1);
        var condition = s.Place(261, "wf_cnd_has_furni_on", 2, 1);
        s.Selector("wf_slc_remote", Remote([condition.Id]));

        Assert.Equal(new uint[] { 201, 202, 203 }, s.Furni(s.Fire()));
    }

    // Observed on hhus (r96 S2 n23): Invert on a remote over a furni-only stack inverts both kinds, so the empty user set becomes every
    // user.
    [Fact]
    public void InvertedRemoteOverAFurniStackInvertsBothKinds()
    {
        using var s = new SelectorStack();
        var stack = s.Selector("wf_slc_furni_bytype", BlocksByType(), 2, 1);
        s.Selector("wf_slc_remote", Remote([stack.Item.Id], invert: true));

        var context = s.Fire();

        Assert.Equal(new[] { 7, 8 }, s.Users(context));
        Assert.DoesNotContain(201u, context.Selected.FurniIds);
        Assert.Contains(1u, context.Selected.FurniIds);
    }

    // Observed on hhus (r96 S2 n31-n34): Filter on the remote keeps the matches, Filter + Invert keeps the rest of the existing
    // selection, and a remote whose stacks select no furni empties the furni pool.
    [Theory]
    [InlineData(false, new[] { 7 })]
    [InlineData(true, new[] { 8 })]
    public void FilteringRemoteNarrowsBothKinds(bool invert, int[] users)
    {
        using var s = new SelectorStack();
        var area = s.Selector("wf_slc_users_area", UsersArea(0, 0, 1, 1), 2, 1);
        s.Selector("wf_slc_users_bytype", Habbos());
        s.Selector("wf_slc_furni_bytype", BlocksByType());
        s.Selector("wf_slc_remote", Remote([area.Item.Id], filter: true, invert: invert));

        var context = s.Fire();

        Assert.Equal(users, s.Users(context));

        if (!invert) {
            Assert.Empty(s.Furni(context));
        }
    }

    // Observed on hhus (r96 S4): Invert without Filter selects every Wired box and every temporary copy in the room, on the stack itself
    // and through a remote.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvertedFurniSelectionIncludesWiredBoxesAndTemporaryFurni(bool remote)
    {
        using var s = new SelectorStack();
        s.Place(250, "temp_block", 1, 0, temporary: true);
        IWiredConfiguredItem selector;

        if (remote) {
            var stack = s.Selector("wf_slc_furni_bytype", BlocksByType(invert: true), 2, 1);
            selector = s.Selector("wf_slc_remote", Remote([stack.Item.Id]));
        }
        else {
            selector = s.Selector("wf_slc_furni_bytype", BlocksByType(invert: true));
        }

        var furni = s.Furni(s.Fire());

        Assert.Contains(1u, furni);
        Assert.Contains(250u, furni);
        Assert.Contains(100u, furni);
        Assert.Contains(selector.Item.Id, furni);
        Assert.DoesNotContain(201u, furni);
    }

    private sealed class SelectionCapture(Room room, Item item) : WiredModernBox(room, item,
        WiredBoxRegistry.All.Single(entry => entry.CanonicalName == "wf_act_toggle_state")), IWiredContextualAction
    {
        public List<WiredRuntimeContext> Contexts { get; } = [];
        public bool IsNegative => false;
        public override bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
        {
            validated = proposed;
            error = string.Empty;

            return true;
        }

        public override bool Execute(WiredRuntimeContext context)
        {
            Contexts.Add(context);

            return true;
        }
    }
}
public partial class ModernWiredRuntimeTests
{
    // Built-in variables read each candidate too: @position.x (-120) equals 2, and the saved row of an inverted "has
    // @projectile.animation.is_travelling" (-2106) filter, which keeps the blocks that are not flying.
    [Theory]
    [InlineData("-120", "[1,1,0,2,-10,0]", false, new uint[] { 203 })]
    [InlineData("-120", "[1,1,0,2,-10,0]", true, new uint[] { 201, 202 })]
    [InlineData("-2106", "[2,0,0,0,-10,0]", true, new uint[] { 201, 202, 203 })]
    [InlineData("-2106", "[2,0,0,0,-10,0]", false, new uint[0])]
    public void BuiltinVariableFilterReadsEachCandidate(string variable, string owned, bool invert, uint[] expected)
    {
        using var s = new SelectorStack();
        s.Selector("wf_slc_furni_bytype", BlocksByType());
        s.Selector("wf_slc_furni_with_var", Row(17, owned, true, invert, furni: "[100]", users: "[0]", variables: $$"""["{{variable}}","n"]"""));

        Assert.Equal(expected, s.Furni(s.Fire()));
    }
}
