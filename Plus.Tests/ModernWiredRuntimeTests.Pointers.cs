using System.Reflection;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A pointer variable read and written by boxes saved from their native editor rows and fired through the room's engine. Observed on AIR
/// WIN63-202610201032-107964540: the context accessor holds the box id of a variable of the pointer's own kind, and the pointer reads that
/// variable for the holder the reading box selects.
/// </summary>
public partial class ModernWiredRuntimeTests
{
    private const uint PointedOwner = 5;
    private const uint UserA = 610, UserB = 611, UserFlag = 612, UserC = 613, FurniA = 620, GlobalA = 630, GlobalB = 631, ContextT = 640, Accessor = 641;
    private const uint UserPointer = 650, FurniPointer = 651, GlobalPointer = 652, ContextPointer = 653;

    // AIR variable targets: 0 furni, 1 user, -10 global, -20 context.
    private const int AirFurni = 0, AirUser = 1, AirGlobal = -10, AirContext = -20;

    private sealed class PointerCatalog : IWiredVariableDirectory
    {
        private readonly Dictionary<uint, WiredVariableDefinition> _definitions = new()
        {
            [UserA] = Plain(UserA, "pu_a", WiredVariableTarget.User),
            [UserB] = Plain(UserB, "pu_b", WiredVariableTarget.User),
            [UserFlag] = Plain(UserFlag, "pu_flag", WiredVariableTarget.User) with { HasValue = false },
            [UserC] = Plain(UserC, "pu_c", WiredVariableTarget.User),
            [FurniA] = Plain(FurniA, "pf_a", WiredVariableTarget.Furni),
            [GlobalA] = Plain(GlobalA, "pg_a", WiredVariableTarget.Global),
            [GlobalB] = Plain(GlobalB, "pg_b", WiredVariableTarget.Global),
            [ContextT] = Plain(ContextT, "pc_t", WiredVariableTarget.Context),
            [Accessor] = Plain(Accessor, "pctx", WiredVariableTarget.Context),
            [UserPointer] = Pointer(UserPointer, "pp_u", AirUser),
            [FurniPointer] = Pointer(FurniPointer, "pp_f", AirFurni),
            [GlobalPointer] = Pointer(GlobalPointer, "pp_g", AirGlobal),
            [ContextPointer] = Pointer(ContextPointer, "pp_c", AirContext)
        };

        private static WiredVariableDefinition Plain(uint id, string name, WiredVariableTarget target) =>
            new(id, 1, PointedOwner, name, target, WiredVariableAvailability.RoomActive, true);

        private static WiredVariableDefinition Pointer(uint id, string name, int airTarget)
        {
            Assert.True(WiredVariableDefinitions.TryDecode("wf_var_pointer", id, 1, PointedOwner,
                new() { IntParams = [airTarget], VariableIds = [$"ctx:{Accessor}"], Text = name }, out var definition, out var error), error);

            return definition!;
        }

        public WiredVariableDefinition? Find(uint id) => _definitions.GetValueOrDefault(id);
        public uint? GetRoomOwner(uint roomId) => PointedOwner;
    }

    private sealed class PointerStack : IDisposable
    {
        public readonly SelectorStack S = new();
        public readonly WiredVariableModule Module;
        private uint _next = 500;
        private double _z;

        public PointerStack()
        {
            Module = new WiredVariableModule(1, new PointerCatalog(), new MemoryWiredVariableStore(), TimeProvider.System,
                new RoomWiredBuiltinVariables(S.F.Room, S.F.Room.GetWired().ReadBuiltin));
            typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(S.F.Room.GetWired().Variables, Module);
            Native("wf_xtra_exec_in_order", [], []);
        }

        public void Set(uint variable, WiredVariableHolder holder, long value) => Assert.True(Module.Mutate(
            new(holder.Target, $"custom:{variable}"), holder,
            holder.Target == WiredVariableTarget.Global ? WiredVariableMutation.Set : WiredVariableMutation.Replace, value, new(1, [holder])));

        public void SetUser(uint variable, Plus.HabboHotel.Rooms.RoomUser user, long value) =>
            Set(variable, WiredVariableRuntimeFrames.UserHolder(user), value);

        public void SetFurni(uint variable, Plus.HabboHotel.Items.Item item, long value) =>
            Set(variable, WiredVariableRuntimeFrames.FurniHolder(item), value);

        public void SetGlobal(uint variable, long value) => Set(variable, new(WiredVariableTarget.Global, 0, 0), value);

        /// <summary>A box saved from its native editor row on the trigger's tile; each new box stands above the last one.</summary>
        public void Native(string name, int[] owned, string[] variables, string text = "", int[]? furni = null, int[]? users = null,
            uint[]? picks = null, Func<uint, IReadOnlyDictionary<int, string>>? connector = null, int delay = 0)
        {
            var id = _next++;
            var item = S.Place(id, name, 0, 0);
            item.GetZ = _z++;
            Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
            var box = connector is null ? S.F.Room.GetWired().CreateConfiguredBox(item, descriptor)!
                : new WiredVariableAddonBox(S.F.Room, item, descriptor, Module, connector);
            var native = WiredNativeEditorProjection.DefaultNative(descriptor);
            native = native with
            {
                OwnedIntParams = [.. owned],
                Text = text,
                VariableIds = [.. variables],
                FurniSourceTypes = furni is null ? native.FurniSourceTypes : [.. furni],
                UserSourceTypes = users is null ? native.UserSourceTypes : [.. users],
                PrimaryItems = [.. (picks ?? []).Select(pick => new WiredNativeItemReference(pick, false))],
                Delay = delay > 0 ? delay : native.Delay
            };
            Assert.True(WiredNativeEditorProjection.TryCompile(id, descriptor, native, out var runtime, WiredNativeEditorProjection.AnyTriggerContext), name);
            Assert.True(box.TryValidateConfiguration(runtime, out var validated, out var error), error);
            box.ApplyConfiguration(validated);
            Assert.True(S.F.Engine.Add(box));
        }

        /// <summary>Give Variable on the context accessor: the box id the pointer should follow.</summary>
        public void Point(uint box, int delay = 0) => Give(AirContext, $"ctx:{Accessor}", box, delay: delay);

        public void Give(int airTarget, string variable, long value, uint[]? picks = null, int delay = 0) => Native("wf_act_give_var",
            [airTarget, 0, (int)value, 1, 0, -10, 0], [variable, "n"], furni: picks is null ? [0, 200] : [100, 200], users: [0, 200], picks: picks, delay: delay);

        /// <summary>The stack fires, then the clock runs on so that its delayed actions run.</summary>
        public WiredRuntimeContext FireAndWait(int milliseconds)
        {
            var context = S.Fire();
            S.F.Advance(milliseconds);

            return context;
        }

        public long? Read(int airTarget, string token, Plus.HabboHotel.Items.Item? furni = null)
        {
            var target = airTarget == AirUser ? WiredVariableTarget.User : airTarget == AirFurni ? WiredVariableTarget.Furni : WiredVariableTarget.Global;
            var holder = airTarget == AirUser ? WiredVariableRuntimeFrames.UserHolder(S.F.User) : airTarget == AirFurni ? WiredVariableRuntimeFrames.FurniHolder(furni!) : new(WiredVariableTarget.Global, 0, 0);

            return Module.Read(new(target, token), holder, new(1, [holder]))?.Value;
        }

        /// <summary>Text Output Variable: number or text mode, the first holder's value, the triggering user or the given sources.</summary>
        public void Output(string placeholder, int airTarget, string variable, bool textMode = false, int furni = 0, int user = 0,
            uint[]? picks = null, Func<uint, IReadOnlyDictionary<int, string>>? connector = null) =>
            Native("wf_xtra_text_output_variable", [0, airTarget, textMode ? 1 : 0], [variable], placeholder, [furni], [user], picks, connector);

        public string Shown(string message)
        {
            var context = S.Fire();

            return context.Policy.FormatText(context, message);
        }

        public void Dispose() => S.Dispose();
    }

    // Observed on AIR WIN63-202610201032-107964540 (r111 targets c0-c4): every target reads the variable whose box id the accessor holds.
    [Theory]
    [InlineData(AirUser, "user:650", UserA, "p=7")]
    [InlineData(AirFurni, "furni:651", FurniA, "p=21")]
    [InlineData(AirGlobal, "room:652", GlobalA, "p=33")]
    [InlineData(AirContext, "ctx:653", ContextT, "p=44")]
    [InlineData(AirContext, "ctx:653", Accessor, "p=641")]
    public void APointerOfEveryTargetReadsTheVariableItsAccessorNames(int airTarget, string pointer, uint pointed, string expected)
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.SetFurni(FurniA, p.S.Blocks[0], 21);
        p.SetGlobal(GlobalA, 33);
        p.Give(AirContext, $"ctx:{ContextT}", 44);
        p.Point(pointed);
        p.Output("p", airTarget, pointer, furni: 100, picks: [p.S.Blocks[0].Id]);

        Assert.Equal(expected, p.Shown("p=$(p)"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r111 targets c5-c7, c15): a holder without the variable prints null, a global never
    // set prints 0.
    [Theory]
    [InlineData(AirFurni, "furni:651", FurniA, "p=null")]
    [InlineData(AirGlobal, "room:652", GlobalB, "p=0")]
    [InlineData(AirContext, "ctx:653", ContextT, "p=null")]
    public void APointerToAVariableWithoutAValuePrintsNullOrZeroForAGlobal(int airTarget, string pointer, uint pointed, string expected)
    {
        using var p = new PointerStack();
        p.SetFurni(FurniA, p.S.Blocks[0], 21);
        p.Point(pointed);
        p.Output("p", airTarget, pointer, furni: 100, picks: [p.S.Blocks[1].Id]);

        Assert.Equal(expected, p.Shown("p=$(p)"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r111 targets c8-c14): a box of another kind, or a pointer box, leaves the placeholder.
    [Theory]
    [InlineData(AirFurni, "furni:651", UserA)]
    [InlineData(AirGlobal, "room:652", FurniA)]
    [InlineData(AirUser, "user:650", GlobalA)]
    [InlineData(AirContext, "ctx:653", UserA)]
    [InlineData(AirUser, "user:650", ContextT)]
    [InlineData(AirUser, "user:650", GlobalPointer)]
    [InlineData(AirGlobal, "room:652", GlobalPointer)]
    [InlineData(AirContext, "ctx:653", 0u)]
    public void APointerToABoxOfAnotherKindLeavesThePlaceholder(int airTarget, string pointer, uint pointed)
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.SetFurni(FurniA, p.S.Blocks[0], 21);
        p.SetGlobal(GlobalA, 33);
        p.Give(AirContext, $"ctx:{ContextT}", 44);
        p.Point(pointed);
        p.Output("p", airTarget, pointer, furni: 100, picks: [p.S.Blocks[0].Id]);

        Assert.Equal("p=$(p)", p.Shown("p=$(p)"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r111 writes c5-c9): Give and Change through a furni, global or context pointer change
    // nothing, create nothing and raise no Variable Changed.
    [Fact]
    public void WritesThroughFurniGlobalAndContextPointersChangeNothing()
    {
        using var p = new PointerStack();
        p.SetFurni(FurniA, p.S.Blocks[0], 21);
        p.SetGlobal(GlobalA, 33);
        p.Module.DrainChanges();
        uint[] block = [p.S.Blocks[0].Id];
        p.Point(FurniA);
        p.Native("wf_act_give_var", [AirFurni, 0, 5, 1, 0, -10, 0], ["furni:651", "n"], furni: [100, 200], users: [0, 200], picks: block);
        p.Native("wf_act_change_var_val", [AirFurni, 1, 0, 0, 1, -10, 0], ["furni:651", "n"], furni: [100, 200], users: [0, 200], picks: block);
        p.Output("f", AirFurni, $"furni:{FurniA}", furni: 100, picks: block);
        p.Output("pf", AirFurni, "furni:651", furni: 100, picks: block);
        p.Output("pg", AirGlobal, "room:652");
        p.Output("pc", AirContext, "ctx:653");

        var shown = p.Shown("f=$(f) pf=$(pf) pg=$(pg) pc=$(pc)");

        Assert.Equal("f=21 pf=21 pg=$(pg) pc=$(pc)", shown);
        Assert.DoesNotContain(p.Module.DrainChanges(), change => change.Key.DefinitionId is FurniA or FurniPointer);
    }

    [Fact]
    public void AChangeThroughAGlobalPointerLeavesTheGlobal()
    {
        using var p = new PointerStack();
        p.SetGlobal(GlobalA, 33);
        p.Module.DrainChanges();
        p.Point(GlobalA);
        p.Native("wf_act_change_var_val", [AirGlobal, 0, 0, 0, 9, -10, 0], ["room:652", "n"]);
        p.Output("g", AirGlobal, $"room:{GlobalA}");
        p.Output("p", AirGlobal, "room:652");

        Assert.Equal("g=33 p=33", p.Shown("g=$(g) p=$(p)"));
        Assert.DoesNotContain(p.Module.DrainChanges(), change => change.Key.DefinitionId is GlobalA or GlobalPointer);
    }

    [Fact]
    public void AGiveThroughAContextPointerCreatesNothing()
    {
        using var p = new PointerStack();
        p.Point(ContextT);
        p.Give(AirContext, "ctx:653", 5);
        p.Output("c", AirContext, $"ctx:{ContextT}");
        p.Output("p", AirContext, "ctx:653");

        Assert.Equal("c=null p=null", p.Shown("c=$(c) p=$(p)"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r111 varchanged L71): a variable without a value read through a pointer prints null.
    [Fact]
    public void AVariableWithoutAValueReadThroughAPointerPrintsNull()
    {
        using var p = new PointerStack();
        p.SetUser(UserFlag, p.S.F.User, 1);
        p.Point(UserFlag);
        p.Output("p", AirUser, "user:650");

        Assert.Equal("p=null", p.Shown("p=$(p)"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r111 p13): in text mode a pointer shows the label of the pointed variable's Text
    // Connector; a value without a label shows the number.
    [Theory]
    [InlineData(7, "t=seven")]
    [InlineData(9, "t=9")]
    public void TextModeThroughAPointerUsesTheConnectorOfThePointedVariable(long value, string expected)
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, value);
        p.Point(UserA);
        p.Output("t", AirUser, "user:650", textMode: true,
            connector: id => id == UserA ? new Dictionary<int, string> { [7] = "seven" } : new Dictionary<int, string>());

        Assert.Equal(expected, p.Shown("t=$(t)"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r111 p11): the holder comes from the reading box's user source, not the triggering user.
    [Fact]
    public void APointerReadsTheUserTheReadingBoxSelects()
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.SetUser(UserA, p.S.Bob, 42);
        p.S.Selector("wf_slc_users_area", UsersArea(2, 0, 1, 1));
        p.Point(UserA);
        p.Output("pt", AirUser, "user:650");
        p.Output("po", AirUser, "user:650", user: 200);

        Assert.Equal("pt=7 po=42", p.Shown("pt=$(pt) po=$(po)"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r111 readers c1-c3, reader_controls c5-c7, p02 a and c): Give Variable and Change
    // Variable never read a pointer as their value source, while the same message still reads the pointer.
    [Fact]
    public void GiveVariableIgnoresAPointerAsTheValueSource()
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.SetUser(UserB, p.S.F.User, 2);
        p.Point(UserA);
        p.Native("wf_act_give_var", [AirUser, 0, 0, 1, 1, AirUser, 0], [$"user:{UserB}", "user:650"], furni: [0, 0], users: [0, 0]);
        p.Output("b", AirUser, $"user:{UserB}");
        p.Output("p", AirUser, "user:650");

        Assert.Equal("b=2 p=7", p.Shown("b=$(b) p=$(p)"));
    }

    [Theory]
    [InlineData(0, AirUser, "user:650", UserA)]
    [InlineData(1, AirUser, "user:650", UserA)]
    [InlineData(0, AirGlobal, "room:652", GlobalA)]
    public void ChangeVariableIgnoresAPointerAsTheValueSource(int operation, int airSource, string pointer, uint pointed)
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.SetUser(UserB, p.S.F.User, 2);
        p.SetGlobal(GlobalA, 33);
        p.Point(pointed);
        p.Native("wf_act_change_var_val", [AirUser, operation, 1, 0, 0, airSource, 0], [$"user:{UserB}", pointer], furni: [0, 0], users: [0, 0]);
        p.Output("b", AirUser, $"user:{UserB}");

        Assert.Equal("b=2", p.Shown("b=$(b)"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r124 delay 1 and 2 operands): a Change Variable or Give Variable that runs after a delay
    // reads a pointer as its value source, like the variable it points at.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ADelayedChangeVariableReadsAPointerAsTheValueSource(int delay)
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.SetUser(UserB, p.S.F.User, 100);
        p.Point(UserA);
        p.Native("wf_act_change_var_val", [AirUser, 1, 1, 0, 0, AirUser, 0], [$"user:{UserB}", "user:650"], furni: [0, 0], users: [0, 0], delay: delay);

        p.FireAndWait(delay * 500 + 500);

        Assert.Equal(107, p.Read(AirUser, $"custom:{UserB}"));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ADelayedGiveVariableReadsAPointerAsTheValueSource(int delay)
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.SetUser(UserB, p.S.F.User, 2);
        p.Point(UserA);
        p.Native("wf_act_give_var", [AirUser, 0, 0, 1, 1, AirUser, 0], [$"user:{UserB}", "user:650"], furni: [0, 0], users: [0, 0], delay: delay);

        p.FireAndWait(delay * 500 + 500);

        Assert.Equal(7, p.Read(AirUser, $"custom:{UserB}"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r124 readtime e1 and e3): the delayed box reads the pointer when it runs, with the
    // accessor the stack holds then, not the one it held when the box was reached.
    [Fact]
    public void ADelayedReadFollowsTheAccessorAsItIsWhenTheBoxRuns()
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.SetUser(UserB, p.S.F.User, 2);
        p.SetUser(UserC, p.S.F.User, 100);
        p.Point(UserA);
        p.Native("wf_act_change_var_val", [AirUser, 1, 1, 0, 0, AirUser, 0], [$"user:{UserC}", "user:650"], furni: [0, 0], users: [0, 0], delay: 2);
        p.Point(UserB, delay: 1);

        p.FireAndWait(1500);

        Assert.Equal(102, p.Read(AirUser, $"custom:{UserC}"));
    }

    // Observed on AIR WIN63-202610201032-107964540 (r124 delay 1 destinations, delay targets d1-d5): Give, Change and Remove that run after a
    // delay write through a pointer of any target to the variable it points at.
    [Fact]
    public void ADelayedWriteThroughAUserPointerChangesThePointedVariable()
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.Point(UserA);
        p.Native("wf_act_change_var_val", [AirUser, 1, 0, 0, 5, -10, 0], ["user:650", "n"], furni: [0, 0], users: [0, 0], delay: 1);

        p.FireAndWait(1000);

        Assert.Equal(12, p.Read(AirUser, $"custom:{UserA}"));
    }

    [Fact]
    public void ADelayedGiveThroughAUserPointerGivesThePointedVariable()
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.Point(UserA);
        p.Native("wf_act_give_var", [AirUser, 0, 5, 1, 0, -10, 0], ["user:650", "n"], furni: [0, 200], users: [0, 200], delay: 1);

        p.FireAndWait(1000);

        Assert.Equal(5, p.Read(AirUser, $"custom:{UserA}"));
    }

    [Fact]
    public void ADelayedRemoveThroughAUserPointerRemovesThePointedVariable()
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.Point(UserA);
        p.Native("wf_act_remove_var", [AirUser], ["user:650"], delay: 1);

        p.FireAndWait(1000);

        Assert.Null(p.Read(AirUser, $"custom:{UserA}"));
    }

    [Fact]
    public void ADelayedChangeThroughAFurniPointerChangesThePointedFurniVariable()
    {
        using var p = new PointerStack();
        p.SetFurni(FurniA, p.S.Blocks[0], 21);
        p.Point(FurniA);
        p.Native("wf_act_change_var_val", [AirFurni, 1, 0, 0, 1, -10, 0], ["furni:651", "n"], furni: [100, 200], users: [0, 200],
            picks: [p.S.Blocks[0].Id], delay: 1);

        p.FireAndWait(1000);

        Assert.Equal(22, p.Read(AirFurni, $"custom:{FurniA}", p.S.Blocks[0]));
    }

    [Fact]
    public void ADelayedChangeThroughAGlobalPointerChangesThePointedGlobal()
    {
        using var p = new PointerStack();
        p.SetGlobal(GlobalA, 33);
        p.Point(GlobalA);
        p.Native("wf_act_change_var_val", [AirGlobal, 0, 0, 0, 9, -10, 0], ["room:652", "n"], delay: 1);

        p.FireAndWait(1000);

        Assert.Equal(9, p.Read(AirGlobal, $"custom:{GlobalA}"));
    }

    [Fact]
    public void ADelayedChangeThroughAContextPointerChangesThePointedContextVariable()
    {
        using var p = new PointerStack();
        p.Give(AirContext, $"ctx:{ContextT}", 44);
        p.Point(ContextT);
        p.Native("wf_act_change_var_val", [AirContext, 1, 0, 0, 1, -10, 0], ["ctx:653", "n"], delay: 1);

        var context = p.FireAndWait(1000);

        Assert.Equal(45, context.VariableFrame!.Context.Read(new(ContextT, WiredVariableTarget.Context, 0))?.Value);
    }

    // Observed on AIR WIN63-202610201032-107964540 (r124 delay targets): a delayed box reads a global or context pointer as its value source.
    [Theory]
    [InlineData(AirGlobal, "room:652", GlobalA, 133)]
    [InlineData(AirContext, "ctx:653", ContextT, 144)]
    public void ADelayedChangeReadsAGlobalOrContextPointerAsTheValueSource(int airSource, string pointer, uint pointed, long expected)
    {
        using var p = new PointerStack();
        p.SetUser(UserB, p.S.F.User, 100);
        p.SetGlobal(GlobalA, 33);
        p.Give(AirContext, $"ctx:{ContextT}", 44);
        p.Point(pointed);
        p.Native("wf_act_change_var_val", [AirUser, 1, 1, 0, 0, airSource, 0], [$"user:{UserB}", pointer], furni: [0, 0], users: [0, 0], delay: 1);

        p.FireAndWait(1000);

        Assert.Equal(expected, p.Read(AirUser, $"custom:{UserB}"));
    }

    // A pointer that points at nothing stays a no-op after a delay too.
    [Fact]
    public void ADelayedWriteThroughAPointerThatPointsAtNothingChangesNothing()
    {
        using var p = new PointerStack();
        p.SetUser(UserA, p.S.F.User, 7);
        p.Native("wf_act_give_var", [AirUser, 0, 5, 1, 0, -10, 0], ["user:650", "n"], furni: [0, 200], users: [0, 200], delay: 1);

        p.FireAndWait(1000);

        Assert.Equal(7, p.Read(AirUser, $"custom:{UserA}"));
        Assert.Null(p.Read(AirUser, $"custom:{UserB}"));
    }
}
