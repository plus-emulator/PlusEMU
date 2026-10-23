using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// What a user pointer variable does in a stack. The context variable
/// holds the id of another variable's box, and the pointer reads that variable for the same user.
/// </summary>
public sealed class WiredPointerRuntimeTests
{
    private const uint Owner = 5;
    private const uint First = 10, Second = 11, NeverGiven = 12, Ctx = 40, Furni = 20, Global = 30, Pointer = 50;
    private static readonly WiredVariableReference ContextVariable = new(WiredVariableTarget.Context, $"custom:{Ctx}");
    private static readonly WiredVariableReference PointerVariable = new(WiredVariableTarget.User, $"custom:{Pointer}");

    private sealed class Directory : IWiredVariableDirectory
    {
        private readonly Dictionary<uint, WiredVariableDefinition> _definitions = new()
        {
            [First] = Plain(First, "ptgt", WiredVariableTarget.User),
            [Second] = Plain(Second, "ptgt2", WiredVariableTarget.User),
            [NeverGiven] = Plain(NeverGiven, "ptgt3", WiredVariableTarget.User),
            [Furni] = Plain(Furni, "pfurni", WiredVariableTarget.Furni),
            [Global] = Plain(Global, "pglob", WiredVariableTarget.Global),
            [Ctx] = Plain(Ctx, "pctx", WiredVariableTarget.Context),
            [Pointer] = Decode(Pointer, "ptrv", "ctx:" + Ctx)
        };

        private static WiredVariableDefinition Plain(uint id, string name, WiredVariableTarget target) =>
            new(id, 1, Owner, name, target, WiredVariableAvailability.RoomActive, true);

        private static WiredVariableDefinition Decode(uint id, string name, string accessor)
        {
            Assert.True(WiredVariableDefinitions.TryDecode("wf_var_pointer", id, 1, Owner,
                new() { IntParams = [1], VariableIds = [accessor], Text = name }, out var definition, out var error), error);

            return definition!;
        }

        public WiredVariableDefinition? Find(uint id) => _definitions.GetValueOrDefault(id);
        public uint? GetRoomOwner(uint roomId) => Owner;
    }

    private sealed class Operations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }

    private sealed class World
    {
        public Room Room { get; }
        public WiredRuntimeContext Context { get; }
        public WiredVariableModule Module { get; }
        public WiredVariableFrame Frame { get; }
        public WiredVariableHolder Player { get; }
        public World()
        {
            Room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
            Room.Id = 1;
            var user = new RoomUser(901, 0, 1, Room, null, TestChatEmotions.Unused, TestRewardProgress.Unused);
            Context = new WiredRuntimeContext(Room, new(WiredEventKind.Enter), new(() => [], () => [user]), new Operations());
            Context.SelectorPool.UserIds.Add(user.VirtualId);
            Context.SelectorUserOrder.Add(user.VirtualId);
            Context.SelectorKinds = WiredSelectionKind.Users;
            Context.Selected.UserIds.Add(user.VirtualId);
            Frame = WiredVariableRuntimeFrames.Create(Context);
            Context.VariableFrame = Frame;
            Module = new(1, new Directory(), new MemoryWiredVariableStore(), new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1000)));
            Player = Frame.Holders.Single(holder => holder.Target == WiredVariableTarget.User);
            Give(First, 7);
            Give(Second, 9);
            Module.DrainChanges();
        }

        public void Give(uint variable, long value) =>
            Assert.True(Module.Mutate(new(WiredVariableTarget.User, $"custom:{variable}"), Player, WiredVariableMutation.Give, value, Frame));

        public void Point(long id)
        {
            Assert.True(Module.Mutate(ContextVariable, new(WiredVariableTarget.Context, 0, 0), WiredVariableMutation.Replace, id, Frame));
            Module.DrainChanges();
        }

        public long? Read(WiredVariableReference reference) => Module.Read(reference, Player, Frame)?.Value;

        /// <summary>Show Message text: "$(pb)" is the pointer, filled the way a Text Output Variable add-on fills a placeholder.</summary>
        public string Message()
        {
            Assert.True(WiredBoxRegistry.TryGet("wf_xtra_text_output_variable", out var descriptor));
            var box = new WiredVariableAddonBox(Room, new Item { Id = 90 }, descriptor, Module, _ => new Dictionary<int, string>());
            WiredNativeTestSupport.Install(box, WiredNativeTestSupport.FromLegacyVariableAddon(descriptor,
                new() { IntParams = [0, 1, 1, 200, 0], Text = $"custom:{Pointer}\tpb\t," }));
            Assert.True(box.Apply(Context));

            return Context.Policy.FormatText(Context, "b=$(pb)");
        }
    }

    [Fact]
    public void ThePointerReadsTheVariableWhoseBoxIdTheContextHolds()
    {
        var world = new World();
        world.Point(First);
        Assert.Equal(7, world.Read(PointerVariable));
        Assert.Equal("b=7", world.Message());

        world.Point(Second);
        Assert.Equal(9, world.Read(PointerVariable));
        Assert.Equal("b=9", world.Message());
    }

    [Fact]
    public void ThePointerFollowsTheValueOfItsTarget()
    {
        var world = new World();
        world.Point(First);
        Assert.True(world.Module.Mutate(new(WiredVariableTarget.User, $"custom:{First}"), world.Player, WiredVariableMutation.Replace, 12, world.Frame));

        Assert.Equal("b=12", world.Message());
    }

    [Fact]
    public void AValidPointerToAVariableTheUserDoesNotHoldReadsNull()
    {
        var world = new World();
        world.Point(NeverGiven);

        Assert.Null(world.Read(PointerVariable));
        Assert.Equal("b=null", world.Message());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(999999999)]
    [InlineData(Pointer)]
    [InlineData(Furni)]
    [InlineData(Global)]
    [InlineData(Ctx)]
    public void AnIdThatIsNotAUserVariableLeavesThePlaceholderInTheText(long id)
    {
        var world = new World();
        world.Point(id);

        Assert.Null(world.Read(PointerVariable));
        Assert.Equal("b=$(pb)", world.Message());
    }

    [Fact]
    public void AnEmptyContextLeavesThePlaceholderInTheText()
    {
        var world = new World();

        Assert.Null(world.Read(PointerVariable));
        Assert.Equal("b=$(pb)", world.Message());
    }

    [Theory]
    [InlineData(WiredVariableMutation.Give)]
    [InlineData(WiredVariableMutation.Replace)]
    [InlineData(WiredVariableMutation.Set)]
    [InlineData(WiredVariableMutation.Remove)]
    public void WritingThroughAPointerChangesNothing(WiredVariableMutation mutation)
    {
        var world = new World();
        world.Point(First);

        Assert.False(world.Module.Mutate(PointerVariable, world.Player, mutation, 5, world.Frame));
        Assert.False(world.Module.Change(PointerVariable, world.Player, mutation, value => value + 1, world.Frame));
        Assert.Equal(7, world.Read(new(WiredVariableTarget.User, $"custom:{First}")));
        Assert.Empty(world.Module.DrainChanges());
    }

    [Fact]
    public void AGiveThroughAPointerDoesNotCreateTheTarget()
    {
        var world = new World();
        world.Point(NeverGiven);

        Assert.False(world.Module.Mutate(PointerVariable, world.Player, WiredVariableMutation.Give, 5, world.Frame));
        Assert.Null(world.Read(new(WiredVariableTarget.User, $"custom:{NeverGiven}")));
    }
}
