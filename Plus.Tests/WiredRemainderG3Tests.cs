using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.CompilerServices;
using Plus.Database;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Group g3 of the remainder cards: the editors of the five cards against the October AS3 and the official frames,
// Time Utilities through the room's variable catalog, and Filter Users by Variable through the stack's selector pool.
public sealed partial class WiredRemainderG3Tests
{
    private static string Groups(ImmutableArray<ImmutableArray<int>> groups) => string.Join(';', groups.Select(group => string.Join(',', group)));

    // Official initial frames: r80 (place, remove), r77 batch 05 (filter, capturer, time utilities; r83 for the last).
    [Theory]
    [InlineData("wf_act_place_furni", "0,100,101,110,200,201;0,100,101,200,201;0,100,101,200,201", "0,200,201;0,200,201", "110,0,200", "0,200", true, "0,0,0,0,0,0,0,0,0,-20")]
    [InlineData("wf_act_remove_furni", "0,200,201", "", "200", "", false, "")]
    [InlineData("wf_xtra_filter_users_by_var", "0,100,201", "0,201", "100", "0", false, "10,0,0,1")]
    [InlineData("wf_xtra_text_input_variable", "", "", "", "", false, "0")]
    [InlineData("wf_xtra_var_time_util", "", "", "", "", false, "0,1")]
    public void TheEditorOpensTheWayTheOfficialFrameDid(string name, string furniAllowed, string usersAllowed, string furniDefaults,
        string userDefaults, bool wall, string owned)
    {
        var metadata = WiredNativeEditorProjection.Metadata(name);
        Assert.Equal(furniAllowed, Groups(metadata.FurniAllowed));
        Assert.Equal(usersAllowed, Groups(metadata.UsersAllowed));
        Assert.Equal(furniDefaults, string.Join(',', metadata.FurniDefaults));
        Assert.Equal(userDefaults, string.Join(',', metadata.UserDefaults));
        Assert.Equal(wall, metadata.AllowWall);
        Assert.Equal(owned, string.Join(',', metadata.OwnedDefaults));
    }

    [Fact]
    public void PlaceFurniOpensOnItsSnapshotAndSavesWithEveryOfferedSourceButNoOther()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_place_furni", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);
        Assert.Equal([110, 0, 200], native.FurniSourceTypes.ToArray());
        Assert.Equal([0, 200], native.UserSourceTypes.ToArray());
        Assert.Equal(["n", "n"], native.VariableIds.Select(id => WiredVariableAbsent.Is(id) ? "n" : id).ToArray());
        Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native, out var runtime));
        Assert.Equal(110, runtime.FurniSources["templates"]);
        Assert.Equal(0, runtime.FurniSources["target"]);
        Assert.Equal(200, runtime.FurniSources["value"]);
        Assert.Equal(0, runtime.UserSources["target"]);
        Assert.Equal(200, runtime.UserSources["value"]);

        foreach (var source in new[] { 0, 100, 101, 110, 200, 201 }) {
            Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { FurniSourceTypes = [source, 0, 200] }, out _), source.ToString());
        }

        // 110 is the template slot's alone; the user slots offer only the triggering user, the selector and the signal.
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { FurniSourceTypes = [110, 110, 200] }, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { FurniSourceTypes = [110, 0, 110] }, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { FurniSourceTypes = [111, 0, 200] }, out _));
        Assert.Equal("wiredfurni.error.invalid_furni_source", WiredNativeEditorProjection.SourceRejectionKey(descriptor, native with { FurniSourceTypes = [110, 110, 200] }));

        foreach (var user in new[] { 10, 11, 100, 101 }) {
            Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { UserSourceTypes = [user, 200] }, out _), user.ToString());
        }
    }

    [Fact]
    public void PlaceFurniRefusesOutOfRangeOffsetsAndTheEditorFormsItCannotWrite()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_place_furni", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);

        bool Compiles(params int[] owned) => WiredNativeEditorProjection.TryCompile(10, descriptor, native with { OwnedIntParams = [.. owned] }, out _);

        Assert.True(Compiles(1, 1, 2, 64, -64, 8000, 0, 0, 0, 0));
        Assert.True(Compiles(0, 0, 0, 0, 0, -8000, 1, 1, int.MinValue, -20));
        Assert.False(Compiles(0, 0, 0, 65, 0, 0, 0, 0, 0, -20));
        Assert.False(Compiles(0, 0, 0, 0, -65, 0, 0, 0, 0, -20));
        Assert.False(Compiles(0, 0, 0, 0, 0, 8001, 0, 0, 0, -20));
        Assert.False(Compiles(0, 2, 0, 0, 0, 0, 0, 0, 0, -20));
        Assert.False(Compiles(0, 0, 3, 0, 0, 0, 0, 0, 0, -20));
        Assert.False(Compiles(2, 0, 0, 0, 0, 0, 0, 0, 0, -20));
        Assert.False(Compiles(0, 0, 0, 0, 0, 0, 0, 0));
    }

    [Fact]
    public void RemoveFurniHasNoParametersAndOnlyTheTriggeringSelectorAndSignalSources()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_remove_furni", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor);

        foreach (var source in new[] { 0, 200, 201 }) {
            Assert.True(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { FurniSourceTypes = [source] }, out var runtime), source.ToString());
            Assert.Equal(source, runtime.FurniSources["items"]);
        }

        foreach (var source in new[] { 100, 101, 110 }) {
            Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { FurniSourceTypes = [source] }, out _), source.ToString());
        }

        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { OwnedIntParams = [0] }, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { Text = "x" }, out _));
    }

    [Theory]
    [InlineData(100, "primary")]
    [InlineData(110, "primary")]
    [InlineData(101, "secondary")]
    [InlineData(0, "none")]
    [InlineData(200, "none")]
    [InlineData(201, "none")]
    public void PlaceFurniSavesTheSnapshotOfTheListTheTemplateSourceNames(int source, string kept)
    {
        var (room, _, items) = ModernWiredRuntimeTests.WorldForG3();
        var definition = ModernWiredRuntimeTests.MakeItemForG3(5, "lamp").Definition;
        definition.Id = 5;
        definition.Stackable = true;
        var primary = ModernWiredRuntimeTests.MakeItemForG3(10, "lamp");
        primary.Definition = definition;
        primary.SetState(1, 1, 0);
        var secondary = ModernWiredRuntimeTests.MakeItemForG3(11, "lamp");
        secondary.Definition = definition;
        secondary.SetState(2, 2, 0);
        items[primary.Id] = primary;
        items[secondary.Id] = secondary;
        Assert.True(WiredBoxRegistry.TryGet("wf_act_place_furni", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            PrimaryItems = [new(10, false)],
            SecondaryItems = [new(11, false)],
            FurniSourceTypes = [source, 0, 200]
        };
        var snapshots = (ImmutableArray<WiredFurniSnapshot>)typeof(WiredConfigurationService)
            .GetMethod("CaptureSnapshots", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, [room, "wf_act_place_furni", native])!;
        var expected = kept switch { "primary" => new uint[] { 10 }, "secondary" => [11u], _ => [] };
        Assert.Equal(expected, snapshots.Select(snapshot => snapshot.ItemId).ToArray());
    }

    [Fact]
    public void PlaceFurniCopiesTheSavedSnapshotWhenTheTemplateSourceIsTheSnapshotItself()
    {
        var (room, _, items) = ModernWiredRuntimeTests.WorldForG3();
        var definition = ModernWiredRuntimeTests.MakeItemForG3(5, "lamp").Definition;
        definition.Id = 5;
        definition.Stackable = true;
        var manager = new TestWiredDefinitions(() => new() { [5] = definition });
        var action = ModernWiredRuntimeTests.ActionBoxForG3(room, "wf_act_place_furni", manager);
        Assert.True(WiredBoxRegistry.TryGet("wf_act_place_furni", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            SavedState = new() { Snapshots = [new(900, 5, 2, 1, 0, 0, "1")] }
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(action.Item.Id, descriptor, native, out var compiled));
        Assert.True(WiredConfigurationSave.TrySave(action, compiled, TestWiredConfigurationStore.Instance, out var error), error);
        Assert.True(action.Execute(new WiredRuntimeContext(room, new(WiredEventKind.Use), new(() => items.Values.ToArray(), () => []), new G3Operations())));
        var copy = Assert.Single(room.GetRoomItemHandler().GetFloor.Where(item => item.IsTemporary));
        Assert.Equal((2, 1, "1"), (copy.GetX, copy.GetY, copy.LegacyDataString));
    }

    private sealed class G3Operations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }

    // ---------------------------------------------------------------- Time Utilities

    private sealed class G3Clock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class G3Directory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => roomId == 1 ? 5u : null;
        public WiredVariableDefinition? Find(uint id) => id == 20
            ? new(20, 1, 5, "stamp", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true) : null;
    }

    private sealed class G3TimeRig
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private readonly ConcurrentDictionary<uint, Item> _floor;
        public G3Clock Clock { get; } = new(DateTimeOffset.FromUnixTimeMilliseconds(1_000_000));
        public WiredRoomVariables Variables { get; }
        public WiredVariableModule Module { get; }
        public WiredVariableHolder Holder { get; } = new(WiredVariableTarget.User, 7, 1);
        public WiredVariableFrame Frame { get; }
        public WiredVariableMetadataBox TimeUtil { get; }

        public G3TimeRig(int mask, int mode)
        {
            var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
            room.Id = 1;
            room.OwnerId = 5;
            var handler = new RoomItemHandling(room, TestRoomItemStore.Instance, TestRoomItemMetadataStore.Instance, TestGameClientManager.Empty, TestLanguageManager.RoomItems, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
            typeof(Room).GetField("_roomItemHandling", Private)!.SetValue(room, handler);
            _floor = (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_floorItems", Private)!.GetValue(handler)!;
            var database = DispatchProxy.Create<IDatabase, ModernWiredRuntimeTests.RecordingProxy>();
            ((ModernWiredRuntimeTests.RecordingProxy)(object)database).InvokeMethod = (method, _) => throw new InvalidOperationException("Unexpected database access: " + method.Name);
            Variables = new(room, database, Clock);
            var resolve = typeof(WiredRoomVariables).GetMethod("ResolveDerived", Private)!.CreateDelegate<Func<WiredVariableReference, WiredVariableDerivation?>>(Variables);
            Module = new(1, new G3Directory(), new MemoryWiredVariableStore(), Clock, null, resolve);
            typeof(WiredRoomVariables).GetField("<Module>k__BackingField", Private)!.SetValue(Variables, Module);
            Frame = new(1, [Holder]);
            var baseItem = new Item { Id = 20, OwnerId = 5, RoomId = 1, Definition = new() { Id = 20, InteractionName = "wf_var_user" } };
            _floor[baseItem.Id] = baseItem;
            var definition = Assert.IsType<WiredVariableDefinitionBox>(Variables.CreateBox(baseItem));
            WiredNativeTestSupport.InstallLegacyVariableDraft(definition, new() { IntParams = [1, 0], Text = "stamp" });
            Variables.ConfigurationLoaded(definition);
            var item = new Item { Id = 21, Definition = new() { InteractionName = "wf_xtra_var_time_util" } };
            _floor[item.Id] = item;
            TimeUtil = Assert.IsType<WiredVariableMetadataBox>(Variables.CreateBox(item));
            Variables.ConfigurationLoaded(TimeUtil);
            Configure(mask, mode);
        }

        public void Configure(int mask, int mode) => WiredNativeTestSupport.InstallLegacyVariableDraft(TimeUtil, new() { IntParams = [mask, mode] });

        public void Give(long value, Action? later = null)
        {
            Assert.True(Module.Mutate(new(WiredVariableTarget.User, "custom:20"), Holder, WiredVariableMutation.Give, value, Frame));
            later?.Invoke();
        }

        public void Set(long value) => Assert.True(Module.Mutate(new(WiredVariableTarget.User, "custom:20"), Holder, WiredVariableMutation.Set, value, Frame));

        // Each ticked part is its own catalogue variable "<parent>.<mode>.<part>" (observed on hhus, AIR WIN63-202610131318-860371703).
        public WiredVariableReference Part(int sub) =>
            new(WiredVariableTarget.User, $"custom:{WiredRoomVariables.SyntheticId(WiredVariableTarget.User, 20, sub, true)!.Value}");
        public long? Read(int sub) => Module.Read(Part(sub), Holder, Frame)?.Value;
        public long? Parent() => Module.Read(new(WiredVariableTarget.User, "custom:20"), Holder, Frame)?.Value;

        public string[] Names() => Variables.Catalog().Variables.Select(variable => variable.Definition.Name).Order().ToArray();
    }

    private const long Tuesday = 1_700_000_000; // 2023-11-14 22:13:20 UTC (GMT in London), a Tuesday in ISO week 46.
    private const long TuesdayMs = Tuesday * 1000;

    [Fact]
    public void TimeUtilitiesReadEveryCalendarFieldAndEveryElapsedUnitOfTheValueAsEpochMilliseconds()
    {
        // A Value-mode parent holds epoch milliseconds, the unit of @current_time (observed on hhus, AIR WIN63-202610131318-860371703).
        var rig = new G3TimeRig(WiredVariableTimeUtilities.ValidMask, 0);
        rig.Give(TuesdayMs);
        var expected = new Dictionary<int, long>
        {
            [1] = 0,
            [2] = 20,
            [3] = 13,
            [4] = 22,
            [5] = 2,
            [6] = 14,
            [7] = 318,
            [8] = 46,
            [9] = 11,
            [10] = 2023,
            [20] = TuesdayMs,
            [21] = Tuesday,
            [22] = Tuesday / 60,
            [23] = Tuesday / 3600,
            [24] = Tuesday / 86400,
            [25] = Tuesday / 604800,
            [26] = (2023 - 1970) * 12 + 10
        };

        foreach (var (sub, value) in expected) {
            Assert.Equal(value, rig.Read(sub));
        }

        Assert.Equal(17, rig.Names().Count(name => name.StartsWith("stamp.")));
    }

    [Fact]
    public void TheEditorsNamesNameTheSubVariablesAndOnlyTheTickedOnesExist()
    {
        // AIR VariableTimeUtilAddon.as:48-59 (WIN63-202610131318-860371703) SubVariableParam names.
        // Observed on hhus: "<parent>.value.<part>", with the month part sent as "month_of_year " (trailing space).
        var rig = new G3TimeRig(WiredVariableTimeUtilities.ValidMask, 0);
        rig.Give(TuesdayMs);
        Assert.Equal(new[]
        {
            "stamp", "stamp.value.day", "stamp.value.day_of_month", "stamp.value.day_of_week", "stamp.value.day_of_year", "stamp.value.hour",
            "stamp.value.hour_of_day", "stamp.value.millisecond", "stamp.value.milliseconds_of_seconds", "stamp.value.minute",
            "stamp.value.minute_of_hour", "stamp.value.month", "stamp.value.month_of_year ", "stamp.value.second", "stamp.value.seconds_of_minute",
            "stamp.value.week", "stamp.value.week_of_year", "stamp.value.year"
        }.Order().ToArray(), rig.Names());
        rig.Configure((1 << 10) | (1 << 21), 0);
        Assert.Equal(["stamp", "stamp.value.second", "stamp.value.year"], rig.Names());
        rig.Configure((1 << 10) | (1 << 21), 1);
        Assert.Equal(["stamp", "stamp.creation_time", "stamp.creation_time.second", "stamp.creation_time.year"], rig.Names());
        rig.Configure((1 << 10) | (1 << 21), 2);
        Assert.Equal(["stamp", "stamp.last_update_time", "stamp.last_update_time.second", "stamp.last_update_time.year"], rig.Names());
        rig.Configure((1 << 10) | (1 << 21), 0);
        Assert.Equal(2023, rig.Read(10));
        Assert.Equal(Tuesday, rig.Read(21));
        Assert.Null(rig.Read(0));
        Assert.Null(rig.Read(9));
        Assert.Null(rig.Read(22));
    }

    [Theory]
    [InlineData(2, 0, 15)]
    [InlineData(-5, 17, 14)]
    public void CalendarFieldsFollowTheRoomTimezoneWhileElapsedUnitsStayUtc(int hours, int hour, int day)
    {
        var rig = new G3TimeRig(WiredVariableTimeUtilities.ValidMask, 0);
        rig.Variables.TimeZone = () => TimeZoneInfo.CreateCustomTimeZone("g3", TimeSpan.FromHours(hours), "g3", "g3");
        rig.Give(TuesdayMs);
        Assert.Equal((hour, 2023, 11, day), (rig.Read(4), rig.Read(10), rig.Read(9), rig.Read(6)));
        Assert.Equal(Tuesday, rig.Read(21));
        Assert.Equal(Tuesday / 86400, rig.Read(24));
    }

    [Fact]
    public void CreationAndLastUpdateModesReadTheMillisecondsOfTheVariablesOwnTimestamps()
    {
        var rig = new G3TimeRig(WiredVariableTimeUtilities.ValidMask, 1);
        rig.Clock.Now = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_123);
        rig.Give(5);
        rig.Clock.Now = DateTimeOffset.FromUnixTimeMilliseconds(1_700_003_661_456);
        rig.Set(6);
        Assert.Equal(123, rig.Read(1));
        Assert.Equal(1_700_000_000_123, rig.Read(20));
        Assert.Equal(Tuesday, rig.Read(21));
        Assert.Equal(22, rig.Read(4));
        // The base row "<parent>.creation_time" is the raw timestamp.
        Assert.Equal(1_700_000_000_123, rig.Read(0));
        rig.Configure(WiredVariableTimeUtilities.ValidMask, 2);
        Assert.Equal(1_700_003_661_456, rig.Read(0));
        Assert.Equal(456, rig.Read(1));
        Assert.Equal(1_700_003_661_456, rig.Read(20));
        Assert.Equal(Tuesday + 3661, rig.Read(21));
        Assert.Equal(23, rig.Read(4));
        Assert.Equal(14, rig.Read(3));
        Assert.Equal(21, rig.Read(2));
        // The value mode is a different reading of the same variable: the stored 6 is millisecond six of 1970.
        rig.Configure(WiredVariableTimeUtilities.ValidMask, 0);
        Assert.Equal(6, rig.Read(20));
        Assert.Equal(0, rig.Read(21));
        Assert.Equal(1970, rig.Read(10));
    }

    [Fact]
    public void TwoElapsedUnitsSubtractToTheTimeBetweenTwoInstants()
    {
        var rig = new G3TimeRig(1 << 23, 0);
        rig.Give(TuesdayMs);
        var first = rig.Read(23)!.Value;
        rig.Set(TuesdayMs + (7200 + 59) * 1000);
        Assert.Equal(2, rig.Read(23)!.Value - first);
    }

    [Theory]
    [InlineData(-1L, 1970)]
    [InlineData(253402300799999L, 9999)]
    public void AValueAtTheEdgeOfTheCalendarReadsItsYear(long value, int year)
    {
        var rig = new G3TimeRig(WiredVariableTimeUtilities.ValidMask, 0);
        rig.Give(value);
        Assert.Equal(year, rig.Read(10));
    }

    [Theory]
    [InlineData(long.MinValue)]
    [InlineData(253402300800000L)]
    [InlineData(long.MaxValue)]
    public void AValueOutsideTheCalendarYearsHasNoCalendarOrElapsedReading(long value)
    {
        var rig = new G3TimeRig(WiredVariableTimeUtilities.ValidMask, 0);
        rig.Give(value);

        foreach (var sub in new[] { 1, 4, 10, 20, 21, 26 }) {
            Assert.Null(rig.Read(sub));
        }
    }

    [Fact]
    public void WritingAValuePartMovesTheParentByThatPartAndKeepsEveryOtherField()
    {
        // Observed on hhus (AIR WIN63-202610131318-860371703): year 2026 → 2025 moved 2026-10-16 to 2025-10-16, then day_of_week 1
        // moved it to the Monday of that week.
        var rig = new G3TimeRig((1 << 5) | (1 << 10) | (1 << 4) | (1 << 21), 0);
        var friday = new DateTimeOffset(2026, 10, 16, 21, 26, 0, 123, TimeSpan.Zero).ToUnixTimeMilliseconds();
        rig.Give(friday);
        Assert.Equal((5, 2026, 22), (rig.Read(5), rig.Read(10), rig.Read(4)));
        Assert.True(rig.Module.Mutate(rig.Part(10), rig.Holder, WiredVariableMutation.Set, 2025, rig.Frame));
        Assert.Equal(new DateTimeOffset(2025, 10, 16, 21, 26, 0, 123, TimeSpan.Zero).ToUnixTimeMilliseconds(), rig.Parent());
        Assert.Equal((4, 2025), (rig.Read(5), rig.Read(10)));
        Assert.True(rig.Module.Mutate(rig.Part(5), rig.Holder, WiredVariableMutation.Set, 1, rig.Frame));
        Assert.Equal(new DateTimeOffset(2025, 10, 13, 21, 26, 0, 123, TimeSpan.Zero).ToUnixTimeMilliseconds(), rig.Parent());
        // Calendar parts are set on the room's wall clock: hour_of_day 9 in London summer time is 08:xx UTC.
        Assert.True(rig.Module.Mutate(rig.Part(4), rig.Holder, WiredVariableMutation.Set, 9, rig.Frame));
        Assert.Equal(new DateTimeOffset(2025, 10, 13, 8, 26, 0, 123, TimeSpan.Zero).ToUnixTimeMilliseconds(), rig.Parent());
        // Elapsed parts move the parent by whole units and keep the remainder.
        Assert.True(rig.Module.Mutate(rig.Part(21), rig.Holder, WiredVariableMutation.Set, 0, rig.Frame));
        Assert.Equal(123, rig.Parent());
        // A part is never given or removed on its own, and the parent is written as a whole, so a refused write keeps it.
        Assert.False(rig.Module.Mutate(rig.Part(10), rig.Holder, WiredVariableMutation.Give, 2000, rig.Frame));
        Assert.False(rig.Module.Mutate(rig.Part(10), rig.Holder, WiredVariableMutation.Set, 10000, rig.Frame));
        Assert.Equal(123, rig.Parent());
    }

    [Fact]
    public void AValuePartChangeIsInterceptedAsThePartsOwnChange()
    {
        var rig = new G3TimeRig(1 << 10, 0);
        rig.Give(TuesdayMs);
        rig.Module.DrainChanges();
        Assert.True(rig.Module.Mutate(rig.Part(10), rig.Holder, WiredVariableMutation.Set, 2024, rig.Frame));
        var change = Assert.Single(rig.Module.DrainChanges());
        Assert.Equal(20u, change.Key.DefinitionId);
        var part = rig.Module.ProjectPartChange(rig.Part(10), change)!;
        Assert.Equal((2023L, 2024L), (part.Before!.Value, part.After!.Value));
        Assert.Equal(WiredRoomVariables.SyntheticId(WiredVariableTarget.User, 20, 10, true), part.Key.DefinitionId);
        var config = new WiredConfiguration { IntParams = [(int)WiredVariableTarget.User, 0, 1, 1, 0, 0, 0, -1], Text = rig.Part(10).Token };
        Assert.True(WiredVariableChangedTrigger.Matches(config, part));
        Assert.False(WiredVariableChangedTrigger.Matches(config with { IntParams = [(int)WiredVariableTarget.User, 0, 1, 0, 1, 0, 0, -1] }, part));
    }

    [Fact]
    public void AParentChangeIsAnUnchangedChangeOfAPartThatKeptItsValue()
    {
        // Observed on hhus (AIR WIN63-202610131318-860371703): +1 ms on the parent fired the year part's Variable Changed set to unchanged.
        var rig = new G3TimeRig(1 << 10, 0);
        rig.Give(TuesdayMs);
        rig.Module.DrainChanges();
        rig.Set(TuesdayMs + 1);
        var part = rig.Module.ProjectPartChange(rig.Part(10), Assert.Single(rig.Module.DrainChanges()))!;
        var config = new WiredConfiguration { IntParams = [(int)WiredVariableTarget.User, 0, 1, 0, 0, 1, 0, -1], Text = rig.Part(10).Token };
        Assert.True(WiredVariableChangedTrigger.Matches(config, part));
        Assert.False(WiredVariableChangedTrigger.Matches(config with { IntParams = [(int)WiredVariableTarget.User, 0, 1, 1, 0, 0, 0, -1] }, part));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void TimestampPartsAndTheirBaseRowAreReadOnly(int mode)
    {
        var rig = new G3TimeRig(1 << 10, mode);
        rig.Give(TuesdayMs);
        var year = rig.Read(10);
        Assert.NotNull(year);
        Assert.False(rig.Module.Mutate(rig.Part(10), rig.Holder, WiredVariableMutation.Set, 1999, rig.Frame));
        Assert.False(rig.Module.Mutate(rig.Part(0), rig.Holder, WiredVariableMutation.Set, 1, rig.Frame));
        Assert.Equal(year, rig.Read(10));
        Assert.Equal(TuesdayMs, rig.Parent());
        Assert.Null(rig.Module.ProjectPartChange(rig.Part(10), new(1, new(20, WiredVariableTarget.User, rig.Holder.StorageId),
            WiredVariableChangeKind.Updated, new(1, null, null), new(2, null, null), 0, 0)));
    }

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(0x7FE | 0x7F00000, 2, true)]
    [InlineData(1 << 1, 1, true)]
    [InlineData(1 << 0, 0, false)]
    [InlineData(1 << 11, 0, false)]
    [InlineData(1 << 19, 0, false)]
    [InlineData(1 << 27, 0, false)]
    [InlineData(-1, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(0, -1, false)]
    public void ASubVariableOrClockTheEditorDoesNotOfferIsRefusedNotTrimmed(int mask, int mode, bool accepted)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_var_time_util", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [mask, mode] };
        Assert.Equal(accepted, WiredNativeEditorProjection.TryCompile(10, descriptor, native, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { OwnedIntParams = [mask] }, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(10, descriptor, native with { Text = "x" }, out _));
    }
}
