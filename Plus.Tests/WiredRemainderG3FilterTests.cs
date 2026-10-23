using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Group g3, Filter Users by Variable (wf_xtra_filter_users_by_var): the add-on trims the stack's selected users to the N highest or lowest
// (or oldest or latest) holders of a user variable.
public sealed class WiredRemainderG3FilterTests
{
    private const string Value = "custom:10";
    private const string Reference = "custom:11";
    private const string Plain = "custom:12";

    private sealed class Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class Directory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 5;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            10 or 11 => new(id, 1, 5, "v" + id, WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            12 => new(12, 1, 5, "plain", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, false),
            _ => null
        };
    }

    private sealed class Rig
    {
        public Room Room { get; }
        public WiredRuntimeContext Context { get; }
        public WiredVariableModule Module { get; }
        public Clock Clock { get; } = new(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        public RoomUser[] Users { get; }
        public WiredVariableHolder[] Holders { get; }

        public Rig()
        {
            Room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
            Room.Id = 1;
            Users = Enumerable.Range(1, 3).Select(i => new RoomUser(900 + i, 0, i, Room, null, TestChatEmotions.Unused, TestRewardProgress.Unused)).ToArray();
            Context = new WiredRuntimeContext(Room, new(WiredEventKind.Enter), new(() => [], () => Users), new Operations());
            Context.SelectorPool.UserIds.UnionWith(Users.Select(user => user.VirtualId));
            Context.SelectorUserOrder.AddRange(Users.Select(user => user.VirtualId));
            Context.SelectorKinds = WiredSelectionKind.Users;
            Context.Selected.UserIds.UnionWith(Users.Select(user => user.VirtualId));
            Context.VariableFrame = WiredVariableRuntimeFrames.Create(Context);
            Module = new(1, new Directory(), new MemoryWiredVariableStore(), Clock);
            Holders = Context.VariableFrame.Holders.OrderBy(holder => holder.EntityId).ToArray();
        }

        public void Give(string token, int user, long value, int secondsLater = 0)
        {
            Clock.Now = Clock.Now.AddSeconds(secondsLater);
            Assert.True(Module.Mutate(new(WiredVariableTarget.User, token), Holders[user], WiredVariableMutation.Give, value, Context.VariableFrame!));
        }

        public void Set(string token, int user, long value, int secondsLater)
        {
            Clock.Now = Clock.Now.AddSeconds(secondsLater);
            Assert.True(Module.Mutate(new(WiredVariableTarget.User, token), Holders[user], WiredVariableMutation.Set, value, Context.VariableFrame!));
        }

        public WiredVariableAddonBox Filter(int[] owned, string variable, string operand, int userSource = 0, int furniSource = 100)
        {
            Assert.True(WiredBoxRegistry.TryGet("wf_xtra_filter_users_by_var", out var descriptor));
            var box = new WiredVariableAddonBox(Room, new Item { Id = 90 }, descriptor, Module, _ => new Dictionary<int, string>());
            var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
            {
                OwnedIntParams = [.. owned],
                VariableIds = [Id(variable), Id(operand)],
                UserSourceTypes = [userSource],
                FurniSourceTypes = [furniSource]
            };
            WiredNativeTestSupport.Install(box, native);

            return box;
        }

        private static string Id(string token) => token.Length == 0 ? "n" : "user:" + token[7..];

        public int[] Kept => Context.SelectorUserOrder.ToArray();
    }

    private sealed class Operations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }

    private static Rig Scored()
    {
        var rig = new Rig();
        rig.Give(Value, 0, 10);
        rig.Give(Value, 1, 20, 1);
        rig.Give(Value, 2, 30, 1);

        return rig;
    }

    [Theory]
    [InlineData(0, 2, new[] { 3, 2 })]
    [InlineData(1, 2, new[] { 1, 2 })]
    [InlineData(0, 1, new[] { 3 })]
    [InlineData(1, 3, new[] { 1, 2, 3 })]
    [InlineData(0, 1000, new[] { 3, 2, 1 })]
    public void TheHighestOrLowestValuesStayInOrderOfRank(int sort, int count, int[] expected)
    {
        var rig = Scored();
        Assert.True(rig.Filter([count, sort, 0, 0], Value, "").Apply(rig.Context));
        Assert.Equal(expected, rig.Kept);
        Assert.Equal(expected.Order(), rig.Context.Selected.UserIds.Order());
        Assert.Equal(expected.Order(), rig.Context.SelectorPool.UserIds.Order());
        Assert.Empty(rig.Module.DrainChanges().Where(change => change.Kind != WiredVariableChangeKind.Created));
    }

    [Theory]
    [InlineData(2, 2, new[] { 1, 2 })]
    [InlineData(3, 2, new[] { 3, 2 })]
    [InlineData(4, 2, new[] { 2, 3 })]
    [InlineData(5, 1, new[] { 1 })]
    public void OldestAndLatestCreationAndUpdateRankByTheTimestampsOfTheVariable(int sort, int count, int[] expected)
    {
        var rig = Scored();
        // User 1 was created first and is updated last; the other two keep their creation as their only change.
        rig.Set(Value, 0, 11, 100);
        var box = rig.Filter([count, sort, 0, 0], Value, "");
        Assert.True(box.Apply(rig.Context));

        // Oldest update puts the two untouched holders first, in the order they were last written.
        Assert.Equal(expected, rig.Kept);
    }

    [Fact]
    public void AHolderWithoutTheVariableIsNeverKept()
    {
        var rig = new Rig();
        rig.Give(Value, 0, 5);
        rig.Give(Value, 2, 50, 1);
        Assert.True(rig.Filter([3, 0, 0, 0], Value, "").Apply(rig.Context));
        Assert.Equal([3, 1], rig.Kept);
        Assert.Equal([3, 1], rig.Context.Selected.UserIds.OrderDescending().ToArray());
    }

    [Fact]
    public void EqualValuesKeepTheOrderTheSelectorLeftThemIn()
    {
        var rig = new Rig();
        rig.Give(Value, 0, 7);
        rig.Give(Value, 1, 7, 1);
        rig.Give(Value, 2, 7, 1);
        rig.Context.SelectorUserOrder.Clear();
        rig.Context.SelectorUserOrder.AddRange([3, 1, 2]);
        Assert.True(rig.Filter([2, 0, 0, 0], Value, "").Apply(rig.Context));
        Assert.Equal([3, 1], rig.Kept);
    }

    [Fact]
    public void TheAmountMayComeFromAVariableOfTheTriggeringUser()
    {
        var rig = Scored();
        rig.Give(Reference, 0, 2);
        rig.Context.Triggering.UserIds.Add(1);
        Assert.True(rig.Filter([1, 0, 1, 1], Value, Reference).Apply(rig.Context));
        Assert.Equal([3, 2], rig.Kept);
    }

    [Fact]
    public void AnAmountVariableTheTriggeringUserLacksKeepsTheTopOneAndNeverNegative()
    {
        var rig = Scored();
        rig.Context.Triggering.UserIds.Add(1);
        Assert.True(rig.Filter([1, 0, 1, 1], Value, Reference).Apply(rig.Context));
        Assert.Equal([3], rig.Kept);

        var negative = Scored();
        negative.Give(Reference, 0, -4);
        negative.Context.Triggering.UserIds.Add(1);
        Assert.True(negative.Filter([1, 0, 1, 1], Value, Reference).Apply(negative.Context));
        Assert.Empty(negative.Kept);
        Assert.Empty(negative.Context.Selected.UserIds);
    }

    [Fact]
    public void NoVariablePickedLeavesTheSelectionAsItWas()
    {
        var rig = Scored();
        Assert.True(rig.Filter([1, 0, 0, 0], "", "").Apply(rig.Context));
        Assert.Equal([1, 2, 3], rig.Kept);
        Assert.Equal(3, rig.Context.Selected.UserIds.Count);
    }

    [Fact]
    public void ARankByValueOnAVariableThatHoldsNoValueKeepsNobodyButCreationOrderStillWorks()
    {
        var rig = new Rig();
        rig.Give(Plain, 0, 0);
        rig.Give(Plain, 1, 0, 5);
        rig.Give(Plain, 2, 0, 5);
        Assert.True(rig.Filter([2, 0, 0, 0], Plain, "").Apply(rig.Context));
        Assert.Empty(rig.Kept);

        var byAge = new Rig();
        byAge.Give(Plain, 0, 0);
        byAge.Give(Plain, 1, 0, 5);
        byAge.Give(Plain, 2, 0, 5);
        Assert.True(byAge.Filter([2, 3, 0, 0], Plain, "").Apply(byAge.Context));
        Assert.Equal([3, 2], byAge.Kept);
    }

    [Fact]
    public void OnlyTheUserPoolIsFilteredAndASelectionOfFurniIsLeftAlone()
    {
        var rig = Scored();
        rig.Context.SelectorPool.FurniIds.UnionWith([1u, 2u]);
        rig.Context.SelectorFurniOrder.AddRange([1u, 2u]);
        rig.Context.SelectorKinds = WiredSelectionKind.Furni;
        rig.Context.Selected.FurniIds.UnionWith([1u, 2u]);
        Assert.True(rig.Filter([1, 0, 0, 0], Value, "").Apply(rig.Context));
        // The pool follows the filter, but the stack acts on furni, so its users are not what it now selects.
        Assert.Equal([3], rig.Kept);
        Assert.Equal([3], rig.Context.SelectorPool.UserIds.ToArray());
        Assert.Equal(3, rig.Context.Selected.UserIds.Count);
        Assert.Equal([1u, 2u], rig.Context.SelectorFurniOrder.ToArray());
        Assert.Equal([1u, 2u], rig.Context.Selected.FurniIds.Order().ToArray());
    }

    [Theory]
    [InlineData(1, 0, 0, 0, true)]
    [InlineData(1000, 5, 1, -10, true)]
    [InlineData(0, 0, 0, 0, true)]
    [InlineData(1001, 0, 0, 0, true)]
    [InlineData(-1, 0, 0, 0, true)]
    [InlineData(1, 6, 0, 0, true)]
    [InlineData(1, -1, 0, 0, true)]
    [InlineData(1, 0, 2, 0, true)]
    [InlineData(1, 0, 0, 4, true)]
    // A value outside the form is stored in range (see WiredR102AddonEditorTests), not refused.
    public void TheEditorFormTakesOneToAThousandAndSixSortOrders(int count, int sort, int option, int target, bool accepted)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_filter_users_by_var", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [count, sort, option, target] };
        Assert.Equal(accepted, WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0, 201, true)]
    [InlineData(0, 200, false)]
    [InlineData(0, 11, false)]
    [InlineData(100, 0, true)]
    [InlineData(201, 0, true)]
    [InlineData(0, 0, true)]
    [InlineData(200, 0, false)]
    [InlineData(101, 0, false)]
    public void TheAmountReferenceOffersTheSourcesTheOfficialFrameOffered(int furniSource, int userSource, bool accepted)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_filter_users_by_var", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { FurniSourceTypes = [furniSource], UserSourceTypes = [userSource] };
        Assert.Equal(accepted, WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
    }

    [Fact]
    public void ARunningFilterIsOnlyEverAReadAndNeverWritesAVariable()
    {
        var rig = Scored();
        rig.Module.DrainChanges();
        Assert.True(rig.Filter([2, 1, 0, 0], Value, "").Apply(rig.Context));
        Assert.Empty(rig.Module.DrainChanges());
        Assert.Equal(10, rig.Module.Read(new(WiredVariableTarget.User, Value), rig.Holders[0], rig.Context.VariableFrame!)!.Value);
    }
}
