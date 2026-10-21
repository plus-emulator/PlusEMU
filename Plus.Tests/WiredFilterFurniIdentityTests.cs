using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Groups;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// The measured furniture quantity filter (native 10, literal option, amount, F100, U0, helper variable "0") kept different members of the same
/// two picked furniture while the picks, the world and the filter configuration were unchanged. These tests drive a real native compile, a real
/// selector, the real addon module and policy, and the engine's own limit application, with a scripted random source passed to the module.
/// </summary>
public sealed class WiredFilterFurniIdentityTests
{
    private const uint A = 11;
    private const uint B = 12;
    private const uint C = 13;

    // Support, not a sequence or a distribution: over every scripted draw each firing keeps exactly one picked member, and the retained
    // members together include both picked furniture. Which draw selects which member is deliberately not asserted.
    [Fact]
    public void NativeFilterFurniAmountOneCanRetainEitherPickedMemberUnderTheSameUnchangedPicks()
    {
        var retained = Enumerable.Range(0, 8).Select(draw => Fire(1, new ScriptedRandom(draw))).ToArray();

        Assert.All(retained, members => Assert.Single(members));
        Assert.All(retained, members => Assert.Contains(members.Single(), new[] { A, B }));
        Assert.Equal(new[] { A, B }, retained.SelectMany(members => members).Distinct().Order());
    }

    // Controls: cardinality and the outside item.
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void NativeFilterFurniAmountTwoRetainsBothPickedMembersAndNeverTheOutsideItem(int draw)
    {
        var members = Fire(2, new ScriptedRandom(draw));

        Assert.Equal(new[] { A, B }, members.Order());
        Assert.DoesNotContain(C, members);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void NativeFilterFurniAmountOneNeverRetainsTheOutsideItem(int draw) =>
        Assert.DoesNotContain(C, Fire(1, new ScriptedRandom(draw)));

    // The normal module (no injected random) hands the shared random to a literal furniture limit that truncates; nothing is drawn here, so this is
    // deterministic. This is the local policy for the subset choice, not a claim about an official algorithm, distribution or order.
    [Fact]
    public void NormalModuleSuppliesTheSharedRandomToALiteralFurnitureLimit()
    {
        var policy = new WiredAddonPolicy();

        Assert.True(new WiredAddonModule("wf_xtra_filter_furni", Filter(1, 0)).Apply(Inputs(), policy));

        Assert.Equal(1, policy.FurniLimit);
        Assert.Same(Random.Shared, policy.FurniFilterRandom);
    }

    [Fact]
    public void AnInjectedRandomIsTheOneAFurnitureLiteralLimitUses()
    {
        var random = new ScriptedRandom(0);
        var policy = new WiredAddonPolicy();

        Assert.True(new WiredAddonModule("wf_xtra_filter_furni", Filter(1, 0), random).Apply(Inputs(), policy));

        Assert.Same(random, policy.FurniFilterRandom);
    }

    // Variable-mode filters keep the first selector entries whatever the draw would have been.
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public void VariableModeFiltersKeepTheFirstEntries(int draw)
    {
        var random = new ScriptedRandom(draw);
        var policy = new WiredAddonPolicy();
        Assert.True(new WiredAddonModule("wf_xtra_filter_users", new() { IntParams = [1, 1, 3, 0, 0], VariableIds = ["missing"] }, random).Apply(Inputs(), policy));
        Assert.True(new WiredAddonModule("wf_xtra_filter_furni", new() { IntParams = [1, 1, 3, 0, 0], VariableIds = ["missing"] }, random).Apply(Inputs(), policy));
        var selection = new WiredSelectedIds();
        selection.FurniIds.UnionWith(new uint[] { 1, 2, 3 });
        selection.UserIds.UnionWith(new[] { 1, 2, 3 });

        var filtered = policy.FilterSelection(selection, new uint[] { 3, 1, 2 }, new[] { 3, 1, 2 });

        Assert.Null(policy.FurniFilterRandom);
        Assert.Null(policy.UserFilterRandom);
        Assert.Equal(new uint[] { 3 }, filtered.FurniIds);
        Assert.Equal(new[] { 3 }, filtered.UserIds);
        Assert.Equal(0, random.Calls);
    }

    // Observed on AIR WIN63-202610131318-860371703: a literal Filter To X Users keeps a random N on each firing (both users were kept
    // across firings), like the furni filter.
    [Fact]
    public void LiteralUserFilterKeepsARandomSubsetOnEachFiring()
    {
        var kept = new HashSet<int>();

        foreach (var draw in Enumerable.Range(0, 8)) {
            var random = new ScriptedRandom(draw);
            var policy = new WiredAddonPolicy();
            Assert.True(new WiredAddonModule("wf_xtra_filter_users", Filter(1, 0), random).Apply(Inputs(), policy));
            Assert.Same(random, policy.UserFilterRandom);
            var selection = new WiredSelectedIds();
            selection.UserIds.UnionWith(new[] { 1, 2 });

            var filtered = policy.FilterSelection(selection, userOrder: new[] { 1, 2 });

            kept.Add(Assert.Single(filtered.UserIds));
        }

        Assert.Equal(new[] { 1, 2 }, kept.Order());
    }

    // A variable-mode furniture filter clears the random slot; a literal one sets it only when its own amount is the limit.
    [Fact]
    public void OnlyALiteralFurnitureFilterThatSetsTheLimitActivatesTheRandom()
    {
        var random = new ScriptedRandom(0);
        var variable = new WiredConfiguration { IntParams = [3, 1, 3, 0, 0], VariableIds = ["missing"] };
        var literalThenVariable = new WiredAddonPolicy();
        new WiredAddonModule("wf_xtra_filter_furni", Filter(1, 0), random).Apply(Inputs(), literalThenVariable);
        new WiredAddonModule("wf_xtra_filter_furni", variable, random).Apply(Inputs(), literalThenVariable);
        var variableThenSmallerLiteral = new WiredAddonPolicy();
        new WiredAddonModule("wf_xtra_filter_furni", variable, random).Apply(Inputs(), variableThenSmallerLiteral);
        new WiredAddonModule("wf_xtra_filter_furni", Filter(1, 0), random).Apply(Inputs(), variableThenSmallerLiteral);
        var variableThenLargerLiteral = new WiredAddonPolicy();
        new WiredAddonModule("wf_xtra_filter_furni", variable, random).Apply(Inputs(), variableThenLargerLiteral);
        new WiredAddonModule("wf_xtra_filter_furni", Filter(5, 0), random).Apply(Inputs(), variableThenLargerLiteral);

        Assert.Null(literalThenVariable.FurniFilterRandom);
        Assert.Same(random, variableThenSmallerLiteral.FurniFilterRandom);
        Assert.Null(variableThenLargerLiteral.FurniFilterRandom);
        Assert.Equal([1, 1, 3], new[] { literalThenVariable.FurniLimit, variableThenSmallerLiteral.FurniLimit, variableThenLargerLiteral.FurniLimit });
    }

    // Policy-level controls: nothing is drawn unless the limit truncates, the original selection is never mutated, and a limited subset is not redrawn.
    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(5)]
    public void NothingIsDrawnWhenTheLimitIsUnsetZeroOrAtLeastThePoolAndTheOriginalSelectionIsUntouched(int? limit)
    {
        var random = new ScriptedRandom(1);
        var policy = new WiredAddonPolicy { FurniLimit = limit, FurniFilterRandom = random };
        var selection = new WiredSelectedIds();
        selection.FurniIds.UnionWith(new uint[] { 1, 2, 3 });

        var filtered = policy.FilterSelection(selection, new uint[] { 3, 1, 2 });

        Assert.Equal(0, random.Calls);
        Assert.Equal(limit == 0 ? 0 : 3, filtered.FurniIds.Count);
        Assert.Equal(3, selection.FurniIds.Count);
    }

    [Fact]
    public void ATruncatingFurnitureLimitDrawsOnceFromPickedMembersAndALimitedSubsetIsNotRedrawn()
    {
        var random = new ScriptedRandom(1);
        var policy = new WiredAddonPolicy { FurniLimit = 2, FurniFilterRandom = random };
        var selection = new WiredSelectedIds();
        selection.FurniIds.UnionWith(new uint[] { 1, 2, 3, 4 });

        var first = policy.FilterSelection(selection, new uint[] { 4, 3, 2, 1 });
        var drawsAfterFirst = random.Calls;
        var second = policy.FilterSelection(first, new uint[] { 4, 3, 2, 1 });

        Assert.Equal(2, first.FurniIds.Count);
        Assert.Subset(selection.FurniIds, first.FurniIds);
        Assert.Equal(2, drawsAfterFirst);
        Assert.Equal(drawsAfterFirst, random.Calls);
        Assert.Equal(first.FurniIds.Order(), second.FurniIds.Order());
        Assert.Equal(4, selection.FurniIds.Count);
    }

    // The engine applies the limit more than once; the stack must draw once and keep that subset.
    [Fact]
    public void NativeFilterFurniAmountOneDrawsOnceAcrossTheEnginesRepeatedLimitApplications()
    {
        var random = new ScriptedRandom(0);

        Assert.Single(Fire(1, random));

        Assert.Equal(1, random.Calls);
    }

    [Fact]
    public void NativeFilterFurniAmountTwoDrawsNothingWhenItDoesNotTruncate()
    {
        var random = new ScriptedRandom(0);

        Assert.Equal(new[] { A, B }, Fire(2, random).Order());
        Assert.Equal(0, random.Calls);
    }

    private static WiredConfiguration Filter(int amount, int mode) => new() { IntParams = [amount, mode, 0, 0, 100] };

    private static WiredAddonInputs Inputs() => new(WiredSelectorTests.World(), WiredSelectorTests.Inputs(), 0, _ => null);

    // Observed on AIR WIN63-202610201032-107964540: an amount of 0 is stored as 1.
    [Fact]
    public void NativeFilterFurniAmountZeroIsStoredAsOne()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_filter_furni", out var descriptor));

        Assert.True(WiredNativeEditorProjection.TryCompile(52, descriptor, FilterNative(descriptor, 0), out var runtime));
        Assert.Equal(1, runtime.Origin!.Native.OwnedIntParams[0]);
    }

    private static WiredNativeEditorConfiguration FilterNative(WiredBoxDescriptor descriptor, int amount) =>
        WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [amount, 0, 1],
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            VariableIds = ["0"]
        };

    // One firing over a fresh room: selector picks A and B (the measured transport order B, A) with an unpicked C in the same injected world,
    // then the filter, then a recorder that snapshots the surviving furniture pool.
    private static uint[] Fire(int amount, Random? random)
    {
        var (room, _, items) = World();
        room.Id = 1;
        room.OwnerId = 5;
        var furniture = new[] { (A, 1), (B, 2), (C, 3) }.Select(entry => Floor(entry.Item1, entry.Item2, 1)).ToArray();

        foreach (var item in furniture) {
            item.OwnerId = 5;
            items[item.Id] = item;
        }

        var world = new WiredSelectorWorld(6, 6,
            furniture.Select(item => new WiredSelectorFurniture(item.Id, 0, "test", "0", item.GetX, item.GetY, 0, 0, [(item.GetX, item.GetY)])).ToArray(), []);
        var errors = new List<Exception>();
        var engine = new WiredStackEngine(() => 0, _ => true, _ => true, _ => { }, errors.Add);
        engine.BindRuntime(room, new(() => items.Values, () => []), new UnusedOperations());
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_game_starts", out var triggerDescriptor));
        var trigger = new WiredModernTrigger(room, Floor(50, 5, 5), triggerDescriptor);
        WiredNativeTestSupport.InstallRuntime(trigger, WiredTriggerConfiguration.Defaults("wf_trg_game_starts"));
        engine.Add(trigger);
        Assert.True(WiredBoxRegistry.TryGet("wf_slc_furni_picks", out var selectorDescriptor));
        var selector = new WiredSelectorBox(room, InRoom(room, Floor(51, 5, 5)), selectorDescriptor, new(), TestGroupManager.Empty, readWorld: _ => world);
        WiredNativeTestSupport.Install(selector, WiredNativeEditorProjection.DefaultNative(selectorDescriptor) with
        {
            PrimaryItems = [new(B, false), new(A, false)]
        });
        engine.Add(selector);
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_filter_furni", out var filterDescriptor));
        var filter = new ModuleFilterAddon(room, InRoom(room, Floor(52, 5, 5)), filterDescriptor, TestGroupManager.Empty, world, random);
        WiredNativeTestSupport.Install(filter, FilterNative(filterDescriptor, amount));
        // The native-bound compile: the literal option carries the amount and leaves the variable operand and sources at their literal values.
        Assert.NotNull(filter.Configuration.Origin?.Native);
        Assert.Equal([amount, 0, 0, 0, 100], filter.Configuration.IntParams.ToArray());
        Assert.Equal(["0"], filter.Configuration.VariableIds.ToArray());
        engine.Add(filter);
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_unseen", out var recorderDescriptor));
        var recorder = new PoolRecorder(room, InRoom(room, Floor(53, 5, 5)), recorderDescriptor);
        engine.Add(recorder);

        Assert.True(engine.DispatchSynchronously(new(WiredEventKind.GameStart)));
        engine.OnFastCycle();
        Assert.Empty(errors);

        return Assert.Single(recorder.Snapshots);
    }

    // Test-only adapter: it only delegates to the real module (which receives the random) with the same runtime input the production addon box captures.
    private sealed class ModuleFilterAddon : WiredConfiguredBehaviorBox, IWiredContextualAddon
    {
        private readonly WiredAddonModule _module;
        private readonly WiredSelectorRoomState _state = new();
        private readonly IGroupManager _groups;
        private readonly WiredSelectorWorld _world;

        public ModuleFilterAddon(Room room, Item item, WiredBoxDescriptor descriptor, IGroupManager groups, WiredSelectorWorld world, Random? random)
            : base(room, item, descriptor, c => WiredAddonConfiguration.Normalize(descriptor.CanonicalName, c))
        {
            _module = new(descriptor.CanonicalName, Configuration, random);
            _groups = groups;
            _world = world;
        }

        public bool AfterConditions => false;

        public bool Apply(WiredRuntimeContext context)
        {
            var input = WiredSelectorRuntimeInput.Capture(context, _state, _groups, readWorld: _ => _world);

            return _module.Apply(input.ForAddons(context.NowMilliseconds), context.Policy.Addons, context.ConfigurationOf(this));
        }

        public void Reset() => _module.Reset();
        protected override void ConfigurationChanged() => _module.Configure(Configuration);
    }

    // Test-only observer, applied after the conditions and the filter: the furniture pool the following actions would resolve.
    private sealed class PoolRecorder(Room room, Item item, WiredBoxDescriptor descriptor)
        : WiredConfiguredBehaviorBox(room, item, descriptor, c => c), IWiredContextualAddon
    {
        public List<uint[]> Snapshots { get; } = [];
        public bool AfterConditions => true;

        public bool Apply(WiredRuntimeContext context)
        {
            Snapshots.Add([.. context.SelectorPool.FurniIds.Order()]);

            return true;
        }

        public void Reset() { }
    }

    // A deterministic stand-in for the random source: every request is checked to be a valid bound and answered with the scripted draw clamped into it.
    private sealed class ScriptedRandom(int draw) : Random
    {
        public int Calls { get; private set; }
        public override int Next()
        {
            Calls++;

            return draw;
        }
        public override int Next(int maxValue)
        {
            Calls++;
            Assert.True(maxValue > 0, $"Next({maxValue}) asks for an empty range");

            return Math.Min(draw, maxValue - 1);
        }
        public override int Next(int minValue, int maxValue)
        {
            Calls++;
            Assert.True(minValue < maxValue, $"Next({minValue}, {maxValue}) asks for an empty range");

            return Math.Min(minValue + draw, maxValue - 1);
        }
    }

    private static Item InRoom(Room room, Item item)
    {
        typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, room);

        return item;
    }

    private static Item Floor(uint id, int x, int y)
    {
        var item = new Item
        {
            Id = id,
            ExtraData = new LegacyDataFormat { Data = "0" },
            Definition = new()
            {
                Type = ItemType.Floor,
                ItemName = "test",
                InteractionName = "test",
                Width = 1,
                Length = 1,
                Modes = 2,
                AdjustableHeights = [],
                VendingIds = [],
                PublicName = "test"
            }
        };
        item.SetState(x, y, 0);

        return item;
    }

    private static (Room Room, Gamemap Map, ConcurrentDictionary<uint, Item> Items) World()
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        typeof(Room).GetField("_interactionClock", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, TimeProvider.System);
        var map = new Gamemap(room, new RoomModel("wired-filter", 0, 0, 0, 0, string.Join('\r', Enumerable.Repeat("000000", 6)), 0, 0, true),
            TestLogging.Navigation, TestRoomSettings.Empty, TestGroupManager.Empty, TestNavigationDatabase.Instance, TestNavigationRewards.Instance);
        var handler = new RoomItemHandling(room, TestRoomItemStore.Instance, TestRoomItemMetadataStore.Instance,
            TestGameClientManager.Empty, TestLanguageManager.RoomItems, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
        typeof(Room).GetField("_gamemap", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, map);
        typeof(Room).GetField("_roomItemHandling", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, handler);
        typeof(Room).GetField("_roomUserManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room,
            new RoomUserManager(room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(),
                TestChatEmotions.Unused, TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel));
        TestRoomUserSnapshots.Install(room);
        typeof(Gamemap).GetProperty("GameMap")!.SetValue(map, new byte[6, 6]);
        typeof(Gamemap).GetProperty("EffectMap")!.SetValue(map, new byte[6, 6]);

        return (room, map, (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!);
    }

    private sealed class UnusedOperations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }
}
