using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// wf_var_context, wf_var_echo and wf_var_reference of the g4 remainder batch, run through the variable module the stacks use: the editor
/// frame (observed on hhus, WIN63-202610131318), the definition each saved frame decodes to, and what reads and writes through it do.
/// Behaviour nothing official shows is read from turbo-cloud (WiredVariableContext/Echo/Reference.cs and their tests; facts only).
/// </summary>
public sealed class WiredTurboRemainderG4VariableTests
{
    private const uint Owner = 5;
    private static readonly WiredVariableHolder Player = new(WiredVariableTarget.User, 105, 5);
    private static readonly WiredVariableHolder RoomHolder = new(WiredVariableTarget.Global, 0, 0);
    private static readonly WiredVariableHolder Scope = new(WiredVariableTarget.Context, 0, 0);

    /// <summary>
    /// Owner 5 has the Shop (room 1: shared room variable "coins" 100, shared user variable "hp" 101, a persistent but unshared room
    /// variable 102) and the Hall (room 2: the cards under test). Room 3 belongs to someone else and shares "bank" 300.
    /// </summary>
    private sealed class Directory : IWiredVariableDirectory
    {
        public Dictionary<uint, WiredVariableDefinition> Definitions { get; } = new()
        {
            [100] = new(100, 1, Owner, "coins", WiredVariableTarget.Global, WiredVariableAvailability.Shared, true),
            [101] = new(101, 1, Owner, "hp", WiredVariableTarget.User, WiredVariableAvailability.Shared, true),
            [102] = new(102, 1, Owner, "vault", WiredVariableTarget.Global, WiredVariableAvailability.Persistent, true),
            [300] = new(300, 3, 6, "bank", WiredVariableTarget.Global, WiredVariableAvailability.Shared, true),
            [10] = new(10, 2, Owner, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            [20] = new(20, 2, Owner, "crate", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true),
            [30] = new(30, 2, Owner, "limit", WiredVariableTarget.Global, WiredVariableAvailability.RoomActive, true, 40),
            [40] = new(40, 2, Owner, "memo", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true),
            [41] = new(41, 2, Owner, "flag", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, false),
            // Cards of the Hall, as their saved frames decode.
            [200] = Reference(200, WiredVariableTarget.Global, 100, readOnly: false),
            [201] = Reference(201, WiredVariableTarget.User, 101, readOnly: false),
            [202] = Reference(202, WiredVariableTarget.Global, 100, readOnly: true),
            [203] = Reference(203, WiredVariableTarget.Global, 102, readOnly: false, sourceRoom: 1),
            [204] = Reference(204, WiredVariableTarget.Global, 300, readOnly: false, sourceRoom: 3),
            [210] = Echo(210, WiredVariableTarget.User, 10),
            [211] = Echo(211, WiredVariableTarget.Furni, 20),
            [212] = Echo(212, WiredVariableTarget.Global, 30),
            [213] = Echo(213, WiredVariableTarget.User, 210),
            [214] = Echo(214, WiredVariableTarget.Context, 40),
            [215] = Echo(215, WiredVariableTarget.Global, 202),
            [216] = Echo(216, WiredVariableTarget.User, 217),
            [217] = Echo(217, WiredVariableTarget.User, 216),
            [218] = Echo(218, WiredVariableTarget.User, 999)
        };

        public WiredVariableDefinition? Find(uint id) => Definitions.GetValueOrDefault(id);
        public uint? GetRoomOwner(uint roomId) => roomId switch { 1 or 2 => Owner, 3 => 6u, _ => null };

        public IReadOnlyList<WiredSharedVariable> ListShared(uint ownerId, uint exceptRoomId) => ownerId != Owner ? [] :
            [.. Definitions.Values.Where(x => x.RoomId != exceptRoomId && x.Availability == WiredVariableAvailability.Shared && x.Link is null && x.RoomId is 1 or 2)
                .Select(x => new WiredSharedVariable(x.RoomId, x.RoomId == 1 ? "Shop" : "Hall", x))];

        private static WiredVariableDefinition Reference(uint id, WiredVariableTarget target, uint source, bool readOnly, uint sourceRoom = 1) =>
            new(id, 2, Owner, $"ref{id}", target, WiredVariableAvailability.RoomActive, true,
                Link: new(sourceRoom, new(target, $"custom:{source}"), readOnly));

        private static WiredVariableDefinition Echo(uint id, WiredVariableTarget target, uint source) =>
            new(id, 2, Owner, $"echo{id}", target, WiredVariableAvailability.RoomActive, true, Link: new(2, new(target, $"custom:{source}"), false));
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.FromUnixTimeMilliseconds(1000);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class World
    {
        public Directory Directory { get; } = new();
        public MemoryWiredVariableStore Durable { get; } = new();
        public Clock Time { get; } = new();
        public WiredVariableModule Shop { get; }
        public WiredVariableModule Hall { get; }
        public World()
        {
            Shop = new(1, Directory, Durable, Time);
            Hall = new(2, Directory, Durable, Time);
        }
        public WiredVariableFrame ShopFrame { get; } = new(1, [Player]);
        public WiredVariableFrame HallFrame { get; } = new(2, [Player]);
    }

    private static WiredVariableReference User(uint id) => new(WiredVariableTarget.User, $"custom:{id}");
    private static WiredVariableReference Global(uint id) => new(WiredVariableTarget.Global, $"custom:{id}");
    private static WiredVariableReference Context(uint id) => new(WiredVariableTarget.Context, $"custom:{id}");

    // The editors.

    private static (Room Room, WiredVariableDefinitionBox Box, WiredBoxDescriptor Descriptor) Box(string name)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        room.Id = 2;
        room.OwnerId = (int)Owner;
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        return (room, new(room, new Item { Id = 77, Definition = new() { InteractionName = name } }, descriptor), descriptor);
    }

    private static bool Saves(string card, int[] ints, string text, string[] variables, out string error, out WiredVariableDefinition? definition)
    {
        var (_, box, descriptor) = Box(card);
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. ints], Text = text, VariableIds = [.. variables] };
        definition = null;
        error = "";

        return WiredNativeEditorProjection.TryCompile(77, descriptor, native, out var configuration)
            && box.TryValidateConfiguration(configuration, out _, out error)
            && (WiredVariableDefinitions.TryDecode(card, 77, 2, Owner, configuration, out definition, out _) || WiredVariableDefinitions.IsUnpickedEcho(card, configuration));
    }

    [Fact]
    public void TheThreeCardsOpenOnTheObservedFrames()
    {
        // wf_var_context: native 3, one int (value checkbox, off), no variable slot.
        var context = WiredNativeEditorProjection.DefaultNative(Box("wf_var_context").Descriptor);
        Assert.Equal([0], context.OwnedIntParams.ToArray());
        Assert.Equal((0, 3), (context.VariableIds.Length, context.NativeCode));
        // wf_var_echo: native 7, no ints, one variable slot, unpicked ("0" on the wire).
        var echo = WiredNativeEditorProjection.DefaultNative(Box("wf_var_echo").Descriptor);
        Assert.Equal((0, 1, 7), (echo.OwnedIntParams.Length, echo.VariableIds.Length, echo.NativeCode));
        Assert.True(WiredVariableAbsent.Is(echo.VariableIds[0]));
        // wf_var_reference: native 4, one int ("read only", on), one variable slot, unpicked.
        var reference = WiredNativeEditorProjection.DefaultNative(Box("wf_var_reference").Descriptor);
        Assert.Equal([1], reference.OwnedIntParams.ToArray());
        Assert.Equal((1, 4), (reference.VariableIds.Length, reference.NativeCode));
        Assert.All(new[] { context, echo, reference }, native => Assert.Equal("", native.Text));
    }

    // wf_var_context

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(-1, false)]
    public void AContextVariableSavesWithOrWithoutAValue(int withValue, bool saved)
    {
        Assert.Equal(saved, Saves("wf_var_context", [withValue], "score_ctx", [], out _, out var definition));

        if (saved) {
            Assert.Equal((WiredVariableTarget.Context, withValue == 1, "score_ctx"), (definition!.Target, definition.HasValue, definition.Name));
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("has space")]
    [InlineData("dash-ed")]
    [InlineData("abcdefghijklmnopqrstuvwxyzabcdefghijklmno")]
    public void ANameThatIsNotOneToFortyLettersDigitsOrUnderscoresIsRefusedOnEveryCard(string name)
    {
        Assert.False(Saves("wf_var_context", [1], name, [], out _, out _));
        Assert.False(Saves("wf_var_echo", [], name, ["user:10"], out _, out _));
        Assert.False(Saves("wf_var_reference", [1], name, ["shared:1:room:100"], out _, out _));
        Assert.True(Saves("wf_var_context", [1], new string('a', 40), [], out _, out _));
    }

    [Fact]
    public void AContextVariableLivesInOneExecutionAndIsGoneInTheNext()
    {
        var world = new World();
        var firing = new WiredVariableFrame(2, [Player]);
        var next = new WiredVariableFrame(2, [Player]);

        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Give, 7, firing));
        Assert.Equal(7, world.Hall.Read(Context(40), Scope, firing)!.Value);
        Assert.Null(world.Hall.Read(Context(40), Scope, next));
        Assert.True(world.Durable.GetHolders(40).Count == 0);
    }

    [Fact]
    public void AContextVariableWithAValueIsGivenReplacedChangedAndRemoved()
    {
        var world = new World();
        var frame = world.HallFrame;

        Assert.False(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Set, 1, frame));
        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Give, 7, frame));
        // Giving again does nothing; "override existing variable" is Replace.
        Assert.False(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Give, 8, frame));
        Assert.Equal(7, world.Hall.Read(Context(40), Scope, frame)!.Value);
        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Replace, 9, frame));
        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Set, 10, frame));
        Assert.True(world.Hall.Change(Context(40), Scope, WiredVariableMutation.Set, value => value * 3, frame));
        Assert.Equal(30, world.Hall.Read(Context(40), Scope, frame)!.Value);
        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Remove, 0, frame));
        Assert.Null(world.Hall.Read(Context(40), Scope, frame));
        Assert.False(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Remove, 0, frame));
    }

    [Fact]
    public void AContextVariableWithoutAValueExistsButCannotBeChanged()
    {
        var world = new World();
        var frame = world.HallFrame;

        Assert.True(world.Hall.Mutate(Context(41), Scope, WiredVariableMutation.Give, 55, frame));
        Assert.NotNull(world.Hall.Read(Context(41), Scope, frame));
        Assert.False(world.Hall.Mutate(Context(41), Scope, WiredVariableMutation.Set, 3, frame));
        Assert.True(world.Hall.Mutate(Context(41), Scope, WiredVariableMutation.Remove, 0, frame));
        Assert.Null(world.Hall.Read(Context(41), Scope, frame));
    }

    [Fact]
    public void AContextVariableRemembersWhenItWasCreatedAndChanged()
    {
        var world = new World();
        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Give, 1, world.HallFrame));
        world.Time.Now = DateTimeOffset.FromUnixTimeMilliseconds(5000);
        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Set, 2, world.HallFrame));

        var value = world.Hall.Read(Context(40), Scope, world.HallFrame)!;

        Assert.Equal((1000, 5000), (value.CreatedAt!.Value.ToUnixTimeMilliseconds(), value.UpdatedAt!.Value.ToUnixTimeMilliseconds()));
    }

    [Fact]
    public void AContextVariableChangePublishesTheChangeOnceToTheHallsTriggers()
    {
        var world = new World();
        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Give, 1, world.HallFrame));
        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Set, 2, world.HallFrame));
        Assert.True(world.Hall.Mutate(Context(40), Scope, WiredVariableMutation.Remove, 0, world.HallFrame));

        var changes = world.Hall.DrainChanges();

        Assert.Equal([WiredVariableChangeKind.Created, WiredVariableChangeKind.Updated, WiredVariableChangeKind.Removed], changes.Select(change => change.Kind));
        Assert.All(changes, change => Assert.Equal((2u, 0), (change.RoomId, change.NativeOrigin(2))));
        Assert.Empty(world.Hall.DrainChanges());
    }

    // wf_var_echo

    [Theory]
    [InlineData("user:10", WiredVariableTarget.User)]
    [InlineData("furni:20", WiredVariableTarget.Furni)]
    [InlineData("room:30", WiredVariableTarget.Global)]
    [InlineData("ctx:40", WiredVariableTarget.Context)]
    public void AnEchoTakesTheTargetOfTheVariableItMirrors(string picked, WiredVariableTarget target)
    {
        Assert.True(Saves("wf_var_echo", [], "mirror", [picked], out var error, out var definition), error);
        Assert.Equal((target, "mirror"), (definition!.Target, definition.Name));
        Assert.Equal(new WiredVariableReference(target, "custom:" + picked[(picked.IndexOf(':') + 1)..]), definition.Link!.Source);
        Assert.False(definition.Link.ReadOnly);
        Assert.Equal(2u, definition.Link.SourceRoomId);
    }

    [Fact]
    public void AnEchoMayBeSavedBeforeItsVariableIsPickedAndThenMirrorsNothing()
    {
        // Observed on hhus: the initial editor frame (variable id "0") saved under a name and reopened unchanged.
        Assert.True(Saves("wf_var_echo", [], "r70_probe_echo", ["0"], out var error, out var definition), error);
        Assert.Null(definition);
        Assert.True(Saves("wf_var_echo", [], "r70_probe_echo", [WiredVariableAbsent.Id], out error, out _), error);
        // A name is still needed, and a picked id the room cannot resolve is not "unpicked".
        Assert.False(Saves("wf_var_echo", [], "", ["0"], out _, out _));
        Assert.False(Saves("wf_var_echo", [], "r70_probe_echo", ["user:0"], out _, out _));
        Assert.False(Saves("wf_var_echo", [], "r70_probe_echo", ["nonsense"], out _, out _));
    }

    [Fact]
    public void AnEchoReadsAndWritesTheVariableItMirrorsAndAnnouncesTheChangesUnderTheOriginalName()
    {
        var world = new World();
        var frame = world.HallFrame;

        Assert.True(world.Hall.Mutate(User(210), Player, WiredVariableMutation.Give, 25, frame));
        Assert.Equal(25, world.Hall.Read(User(10), Player, frame)!.Value);
        Assert.Equal(25, world.Hall.Read(User(210), Player, frame)!.Value);
        Assert.True(world.Hall.Mutate(User(210), Player, WiredVariableMutation.Set, 30, frame));
        Assert.Equal(30, world.Hall.Read(User(10), Player, frame)!.Value);
        Assert.True(world.Hall.Mutate(User(10), Player, WiredVariableMutation.Set, 31, frame));
        Assert.Equal(31, world.Hall.Read(User(210), Player, frame)!.Value);
        Assert.True(world.Hall.Mutate(User(210), Player, WiredVariableMutation.Remove, 0, frame));
        Assert.Null(world.Hall.Read(User(10), Player, frame));
        Assert.All(world.Hall.DrainChanges(), change => Assert.Equal(10u, change.Key.DefinitionId));
    }

    [Fact]
    public void AnEchoHoldsNothingOfItsOwn()
    {
        var world = new World();

        Assert.True(world.Hall.Mutate(User(210), Player, WiredVariableMutation.Give, 25, world.HallFrame));

        Assert.Empty(world.Durable.GetHolders(210));
        Assert.Empty(world.Hall.GetStoredHolders(210).Where(entry => entry.Key.DefinitionId == 210 && entry.Key.HolderId != Player.StableId));
        Assert.Single(world.Hall.GetStoredHolders(10));
    }

    [Fact]
    public void AnEchoOfARoomOrContextVariableFollowsItsKind()
    {
        var world = new World();

        Assert.True(world.Hall.Mutate(Global(212), RoomHolder, WiredVariableMutation.Set, 99, world.HallFrame));
        Assert.Equal(99, world.Hall.Read(Global(30), RoomHolder, world.HallFrame)!.Value);
        // A room variable is never given or taken, through an echo either.
        Assert.False(world.Hall.Mutate(Global(212), RoomHolder, WiredVariableMutation.Give, 1, world.HallFrame));
        Assert.False(world.Hall.Mutate(Global(212), RoomHolder, WiredVariableMutation.Remove, 0, world.HallFrame));

        Assert.True(world.Hall.Mutate(Context(214), Scope, WiredVariableMutation.Give, 3, world.HallFrame));
        Assert.Equal(3, world.Hall.Read(Context(40), Scope, world.HallFrame)!.Value);
    }

    [Fact]
    public void AnEchoOfAnEchoStillReachesTheOriginalAndALoopOrAMissingSourceReachesNothing()
    {
        var world = new World();

        Assert.True(world.Hall.Mutate(User(213), Player, WiredVariableMutation.Give, 4, world.HallFrame));
        Assert.Equal(4, world.Hall.Read(User(10), Player, world.HallFrame)!.Value);
        Assert.Null(world.Hall.Read(User(216), Player, world.HallFrame));
        Assert.False(world.Hall.Mutate(User(216), Player, WiredVariableMutation.Give, 1, world.HallFrame));
        Assert.Null(world.Hall.Read(User(218), Player, world.HallFrame));
        Assert.False(world.Hall.Mutate(User(218), Player, WiredVariableMutation.Give, 1, world.HallFrame));
        Assert.Empty(world.Hall.DrainChanges().Where(change => change.Key.DefinitionId is 216 or 217 or 218));
    }

    [Fact]
    public void AnEchoOfAReadOnlyReferenceCannotWriteEither()
    {
        var world = new World();
        Assert.True(world.Shop.Mutate(Global(100), RoomHolder, WiredVariableMutation.Set, 5, world.ShopFrame));

        Assert.Equal(5, world.Hall.Read(Global(215), RoomHolder, world.HallFrame)!.Value);
        Assert.False(world.Hall.Mutate(Global(215), RoomHolder, WiredVariableMutation.Set, 99, world.HallFrame));
        Assert.Equal(5, world.Shop.Read(Global(100), RoomHolder, world.ShopFrame)!.Value);
    }

    [Fact]
    public void AnEchoWithoutASourceIsInvisibleToTheCatalog()
    {
        var world = new World();
        var ids = new uint[] { 10, 210, 216, 218 };

        var described = world.Hall.DescribeDefinitions(ids).Select(description => description.Definition.ItemId).ToArray();

        Assert.Contains(10u, described);
        Assert.Contains(210u, described);
        Assert.DoesNotContain(218u, described);
        Assert.DoesNotContain(216u, described);
    }

    // wf_var_reference

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AReferenceSavesReadOnlyOrNot(int readOnly)
    {
        Assert.True(Saves("wf_var_reference", [readOnly], "far_coins", ["shared:1:room:100"], out var error, out var definition), error);
        Assert.Equal((WiredVariableTarget.Global, readOnly == 1, 1u), (definition!.Target, definition.Link!.ReadOnly, definition.Link.SourceRoomId));
        Assert.Equal(new WiredVariableReference(WiredVariableTarget.Global, "custom:100"), definition.Link.Source);
        Assert.False(Saves("wf_var_reference", [2], "far_coins", ["shared:1:room:100"], out _, out _));
        Assert.False(Saves("wf_var_reference", [], "far_coins", ["shared:1:room:100"], out _, out _));
    }

    [Fact]
    public void AReferenceNeedsAVariableOfAnotherRoomAndSaysSoWithTheObservedKey()
    {
        // The unpicked frame ("0") was refused on hhus with this key.
        Assert.False(Saves("wf_var_reference", [1], "r70_probe_reference", ["0"], out var error, out _));
        Assert.Equal("wiredfurni.error.variables.reference.valid", error);
        // The Hall cannot refer to its own variables, and a malformed id is no reference.
        Assert.False(Saves("wf_var_reference", [1], "loop", ["shared:2:room:10"], out error, out _));
        Assert.Equal("wiredfurni.error.variables.reference.valid", error);
        Assert.False(Saves("wf_var_reference", [1], "x", ["shared:1:user:0"], out _, out _));
        Assert.False(Saves("wf_var_reference", [1], "x", ["room:100"], out _, out _));
        Assert.False(Saves("wf_var_reference", [1], "x", ["shared:1:furni:100"], out _, out _));
    }

    [Fact]
    public void TheEditorListsTheSharedVariablesOfTheOwnersOtherRoomsOnly()
    {
        var world = new World();

        var shared = world.Hall.ListShared();

        Assert.Equal(["coins", "hp"], shared.Select(entry => entry.Definition.Name).Order());
        Assert.All(shared, entry => Assert.Equal((1u, "Shop"), (entry.RoomId, entry.RoomName)));
        Assert.DoesNotContain(shared, entry => entry.Definition.ItemId is 102 or 300);
        Assert.Empty(new WiredVariableModule(3, world.Directory, world.Durable, world.Time).ListShared());
    }

    [Fact]
    public void AReferenceReadsAndWritesARoomVariableWhereItLives()
    {
        var world = new World();
        Assert.True(world.Shop.Mutate(Global(100), RoomHolder, WiredVariableMutation.Set, 5, world.ShopFrame));

        Assert.Equal(5, world.Hall.Read(Global(200), RoomHolder, world.HallFrame)!.Value);
        Assert.True(world.Hall.Mutate(Global(200), RoomHolder, WiredVariableMutation.Set, 12, world.HallFrame));
        Assert.Equal(12, world.Shop.Read(Global(100), RoomHolder, world.ShopFrame)!.Value);
        // A change there is seen here, too.
        Assert.True(world.Shop.Mutate(Global(100), RoomHolder, WiredVariableMutation.Set, 40, world.ShopFrame));
        Assert.Equal(40, world.Hall.Read(Global(200), RoomHolder, world.HallFrame)!.Value);
        // A room variable holds its starting value before anything changes it, there as here.
        Assert.Equal(0, new World().Hall.Read(Global(200), RoomHolder, new(2, [Player]))!.Value);
    }

    [Fact]
    public void AReferenceToAUserVariableIsTheSamePlayersValueInBothRooms()
    {
        var world = new World();
        Assert.True(world.Shop.Mutate(User(101), Player, WiredVariableMutation.Give, 80, world.ShopFrame));

        Assert.Equal(80, world.Hall.Read(User(201), Player, world.HallFrame)!.Value);
        Assert.True(world.Hall.Mutate(User(201), Player, WiredVariableMutation.Set, 70, world.HallFrame));
        Assert.Equal(70, world.Shop.Read(User(101), Player, world.ShopFrame)!.Value);
        Assert.True(world.Hall.Mutate(User(201), Player, WiredVariableMutation.Remove, 0, world.HallFrame));
        Assert.Null(world.Shop.Read(User(101), Player, world.ShopFrame));
        // Giving it from here creates it there, for the player's stable id, whichever room index they have.
        var elsewhere = new WiredVariableHolder(WiredVariableTarget.User, 105, 41);
        Assert.True(world.Hall.Mutate(User(201), elsewhere, WiredVariableMutation.Give, 9, new(2, [elsewhere])));
        Assert.Equal(9, world.Shop.Read(User(101), Player, world.ShopFrame)!.Value);
        // A bot holds no persistent value.
        var bot = new WiredVariableHolder(WiredVariableTarget.User, -3, 3, false);
        Assert.False(world.Hall.Mutate(User(201), bot, WiredVariableMutation.Give, 9, new(2, [bot])));
    }

    [Fact]
    public void AReadOnlyReferenceChangesNothing()
    {
        var world = new World();
        Assert.True(world.Shop.Mutate(Global(100), RoomHolder, WiredVariableMutation.Set, 5, world.ShopFrame));

        Assert.Equal(5, world.Hall.Read(Global(202), RoomHolder, world.HallFrame)!.Value);
        Assert.False(world.Hall.Mutate(Global(202), RoomHolder, WiredVariableMutation.Set, 99, world.HallFrame));
        Assert.False(world.Hall.Change(Global(202), RoomHolder, WiredVariableMutation.Set, value => value + 1, world.HallFrame));
        Assert.Equal(5, world.Shop.Read(Global(100), RoomHolder, world.ShopFrame)!.Value);
        Assert.Empty(world.Hall.DrainChanges());
    }

    [Fact]
    public void AReferenceToAVariableThatIsNotSharedOrNotTheOwnersGivesNothing()
    {
        var world = new World();
        Assert.True(world.Shop.Mutate(Global(102), RoomHolder, WiredVariableMutation.Set, 5, world.ShopFrame));

        // "Permanent" but not "shared": the Shop keeps it to itself.
        Assert.Null(world.Hall.Read(Global(203), RoomHolder, world.HallFrame));
        Assert.False(world.Hall.Mutate(Global(203), RoomHolder, WiredVariableMutation.Set, 1, world.HallFrame));
        // Another owner's shared variable.
        Assert.Null(world.Hall.Read(Global(204), RoomHolder, world.HallFrame));
        Assert.False(world.Hall.Mutate(Global(204), RoomHolder, WiredVariableMutation.Set, 1, world.HallFrame));
        Assert.Equal(5, world.Shop.Read(Global(102), RoomHolder, world.ShopFrame)!.Value);
    }

    [Fact]
    public void AReferenceStopsWorkingWhenItsSourceIsPickedUpOrTheRoomChangesOwner()
    {
        var world = new World();
        Assert.True(world.Shop.Mutate(Global(100), RoomHolder, WiredVariableMutation.Set, 5, world.ShopFrame));
        Assert.Equal(5, world.Hall.Read(Global(200), RoomHolder, world.HallFrame)!.Value);

        world.Directory.Definitions.Remove(100);
        Assert.Null(world.Hall.Read(Global(200), RoomHolder, world.HallFrame));
        Assert.False(world.Hall.Mutate(Global(200), RoomHolder, WiredVariableMutation.Set, 1, world.HallFrame));
    }

    [Fact]
    public void AChangeThroughAReferenceIsAnnouncedUnderTheSourcesName()
    {
        var world = new World();
        Assert.True(world.Shop.InitializeGlobal(100));
        Assert.True(world.Hall.Mutate(Global(200), RoomHolder, WiredVariableMutation.Set, 12, world.HallFrame));

        var change = Assert.Single(world.Hall.DrainChanges());

        Assert.Equal((100u, 1u, WiredVariableChangeKind.Updated), (change.Key.DefinitionId, change.RoomId, change.Kind));
    }

    [Fact]
    public void AnArithmeticChangeThroughAReferenceReadsAndWritesTheSameLockedValue()
    {
        var world = new World();
        Assert.True(world.Shop.Mutate(Global(100), RoomHolder, WiredVariableMutation.Set, 5, world.ShopFrame));

        Assert.True(world.Hall.Change(Global(200), RoomHolder, WiredVariableMutation.Set, value => value + 10, world.HallFrame));
        Assert.True(world.Shop.Change(Global(100), RoomHolder, WiredVariableMutation.Set, value => value * 2, world.ShopFrame));

        Assert.Equal(30, world.Hall.Read(Global(200), RoomHolder, world.HallFrame)!.Value);
    }
}
