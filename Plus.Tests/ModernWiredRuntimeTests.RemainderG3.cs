using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// Group g3 of the remainder cards: "Place Temporary Furni" and "Remove Temporary Furni", fired through the
// same engine fixture the other wired action tests use. Both cards only ever touch temporary furni (temporary-band ids, no owner row,
// no inventory), so the checks that matter are that nothing permanent is created, moved or removed and that a refused
// placement leaves no ghost behind.
public partial class ModernWiredRuntimeTests
{
    private const uint G3Lamp = 5;

    internal static (Room Room, Gamemap Map, ConcurrentDictionary<uint, Item> Items) WorldForG3() => World();

    internal static Item MakeItemForG3(uint id, string name) => MakeItem(id, name);

    internal static WiredModernAction ActionBoxForG3(Room room, string name, IItemDataManager definitions) => ActionBox(room, name, definitions: definitions);

    private sealed record G3Stack(TeleportFixture F, WiredModernAction Action, ItemDefinition Lamp);

    private static ItemDefinition G3Definition(uint id, bool stackable = true, ItemType type = ItemType.Floor)
    {
        var definition = MakeItem(id, "g3-lamp-" + id).Definition;
        definition.Id = id;
        definition.Stackable = stackable;
        definition.Type = type;

        return definition;
    }

    /// <summary>The enter-room stack of the fixture, with its action swapped for a configured Place Temporary Furni.</summary>
    private static G3Stack G3PlaceStack(WiredConfiguration proposed, Action<Dictionary<uint, ItemDefinition>>? more = null)
    {
        var f = new TeleportFixture();
        var lamp = G3Definition(G3Lamp);
        var definitions = new Dictionary<uint, ItemDefinition> { [G3Lamp] = lamp };
        more?.Invoke(definitions);
        f.DefinitionManager = new TestWiredDefinitions(() => definitions);
        f.Engine.Remove(f.Action.Item.Id);
        f.Items.TryRemove(f.Action.Item.Id, out _);
        var action = ActionBox(f.Room, "wf_act_place_furni", definitions: f.DefinitionManager);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(action, proposed, out var config, out var error), error);
        action.ApplyConfiguration(config);
        f.Items[action.Item.Id] = action.Item;
        Assert.True(f.Engine.Add(action));

        return new(f, action, lamp);
    }

    private static WiredConfiguration G3Place(WiredTemporaryPlacement? placement = null, params WiredFurniSnapshot[] snapshots) =>
        WiredTemporaryFurnitureActions.Defaults("wf_act_place_furni") with
        {
            TemporaryPlacement = placement ?? new(),
            Snapshots = [.. snapshots]
        };

    private static WiredFurniSnapshot G3Snap(uint item, uint definition, int x, int y, double z, int rotation, string state) => new(item, definition, x, y, z, rotation, state);

    private static Item[] G3Temporaries(Room room) => room.GetRoomItemHandler().GetFloor.Where(item => item.IsTemporary).OrderBy(item => item.GetX).ThenBy(item => item.GetY).ToArray();

    [Fact]
    public void G3PlaceFiresOnTheStackAndAddsTemporaryCopiesNobodyOwnsInTheRoom()
    {
        var stack = G3PlaceStack(G3Place(null, G3Snap(900, G3Lamp, 2, 0, 0, 0, "1")));
        using var f = stack.F;
        var permanent = f.Items.Values.Where(item => !item.IsTemporary).Select(item => item.Id).Order().ToArray();
        f.Fire(land: false);
        var copy = Assert.Single(G3Temporaries(f.Room));
        Assert.Equal((2, 0, "1"), (copy.GetX, copy.GetY, copy.LegacyDataString));
        Assert.Same(stack.Lamp, copy.Definition);
        // The first copy takes the first id of the client's temporary band and belongs to the room's registry alone.
        Assert.Equal(TemporaryItemIds.First, copy.Id);
        Assert.True(f.Room.GetRoomItemHandler().OwnsTemporary(copy));
        Assert.Same(copy, f.Room.GetRoomItemHandler().GetItem(copy.Id));
        // Nothing permanent was created, moved or lost: the placement is not an inventory move.
        Assert.Equal(permanent, f.Items.Values.Where(item => !item.IsTemporary).Select(item => item.Id).Order().ToArray());
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void G3PlaceCopiesTheSnapshotNotTheOriginalSoTheOriginalStaysAndNothingMoves()
    {
        var original = MakeItem(77, "g3-original");
        var stack = G3PlaceStack(G3Place(null, G3Snap(77, G3Lamp, 2, 1, 0, 0, "0")));
        using var f = stack.F;
        original.Definition = stack.Lamp;
        original.SetState(2, 1, 0, Gamemap.GetAffectedTiles(1, 1, 2, 1, 0));
        f.Items[original.Id] = original;
        f.Fire(land: false);
        // The picked furni is still where it was, and the copy sits on the same tile on top of it.
        Assert.Same(original, f.Room.GetRoomItemHandler().GetItem(77));
        Assert.Equal((2, 1), (original.GetX, original.GetY));
        var copy = Assert.Single(G3Temporaries(f.Room));
        Assert.Equal((2, 1), (copy.GetX, copy.GetY));
        Assert.NotEqual(original.Id, copy.Id);
    }

    [Fact]
    public void G3EachFireStacksAnotherCopyUpToAltitude35AndThenPlacesNothingWithoutAnError()
    {
        // Observed on hhus: copies stacked "On top" stopped at z 35.0 with no error and no per-room cap (560 copies).
        var stack = G3PlaceStack(G3Place(null, G3Snap(900, G3Lamp, 2, 0, 0, 0, "0")));
        using var f = stack.F;
        stack.Lamp.Height = 1;
        var handler = f.Room.GetRoomItemHandler();

        for (var i = 0; i < 250; i++) {
            Assert.NotNull(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 0, 2, 0, 0));
        }

        var fires = 0;

        while (fires < 100 && stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User]))) {
            fires++;
        }

        var stacked = G3Temporaries(f.Room).Where(item => item.GetX == 2 && item.GetY == 0).ToArray();
        Assert.Equal(36, fires);
        Assert.Equal(36, stacked.Length);
        Assert.Equal(35d, stacked.Max(item => item.GetZ));
        // The next fire adds nothing, reports nothing and takes nothing away.
        Assert.False(stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User])));
        Assert.Equal(286, G3Temporaries(f.Room).Length);
        Assert.Empty(f.Errors);
    }

    [Theory]
    [InlineData(2, 2, false)]
    [InlineData(-3, 0, false)]
    [InlineData(1, -1, true)]
    public void G3OffsetsShiftTheCopyAndAnOffsetOffTheMapPlacesNothing(int dx, int dy, bool placed)
    {
        var stack = G3PlaceStack(G3Place(new(OffsetX: dx, OffsetY: dy), G3Snap(900, G3Lamp, 1, 1, 0, 0, "0")));
        using var f = stack.F;
        f.Target.Definition.Stackable = true;
        var fired = stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User]));
        var copies = G3Temporaries(f.Room);

        if (placed) {
            Assert.True(fired);
            Assert.Equal((1 + dx, 1 + dy), (Assert.Single(copies).GetX, Assert.Single(copies).GetY));
        }
        else {
            Assert.False(fired);
            Assert.Empty(copies);
        }

        Assert.Empty(f.Errors);
    }

    [Fact]
    public void G3AltitudeModesTopSourceCustomAndOffsetAreClampedIntoTheRoom()
    {
        var stack = G3PlaceStack(G3Place(new(Altitude: WiredPlaceAltitudeType.SourceAltitude, OffsetAltitudeHundredths: 50), G3Snap(900, G3Lamp, 2, 2, 3, 0, "0")));
        using var f = stack.F;
        Assert.True(stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User])));
        var copy = Assert.Single(G3Temporaries(f.Room));
        Assert.Equal(3.5, copy.GetZ);
        Assert.True(f.Room.GetRoomItemHandler().RemoveTemporaryFloorItem(copy));

        var top = G3PlaceStack(G3Place(null, G3Snap(900, G3Lamp, 2, 2, 7, 0, "0")));
        using var g = top.F;
        Assert.True(top.Action.Execute(Context(g.Room, new(WiredEventKind.Use), g.Items.Values.ToArray(), [g.User])));
        // On top of the target location ignores the snapshot's own altitude and rests on the floor.
        Assert.Equal(0, Assert.Single(G3Temporaries(g.Room)).GetZ);

        var capped = G3PlaceStack(G3Place(new(Altitude: WiredPlaceAltitudeType.SourceAltitude, OffsetAltitudeHundredths: 8000), G3Snap(900, G3Lamp, 2, 2, 79, 0, "0")));
        using var h = capped.F;
        capped.Action.Execute(Context(h.Room, new(WiredEventKind.Use), h.Items.Values.ToArray(), [h.User]));
        Assert.All(G3Temporaries(h.Room), item => Assert.InRange(item.GetZ, 0, 80));
    }

    [Fact]
    public void G3CustomLocationUsesAUserTargetAndKeepsTheGroupLayoutAroundTheFirstFurni()
    {
        var stack = G3PlaceStack(G3Place(new(TargetIsUser: true, Location: WiredPlaceLocationType.CustomLocation, OffsetX: 1),
            G3Snap(900, G3Lamp, 0, 2, 0, 0, "0"), G3Snap(901, G3Lamp, 1, 2, 0, 2, "0")));
        using var f = stack.F;
        // The triggering user stands on (0, 0): the first furni lands on it (shifted by the offset) and the second keeps its place beside it.
        f.Fire(land: false);
        var copies = G3Temporaries(f.Room);
        Assert.Equal(new[] { (1, 0), (2, 0) }, copies.Select(item => (item.GetX, item.GetY)).ToArray());
        Assert.Equal(new[] { 0, 2 }, copies.Select(item => item.Rotation).ToArray());
    }

    [Fact]
    public void G3CustomLocationWithoutAUserTargetPlacesNothing()
    {
        var stack = G3PlaceStack(G3Place(new(TargetIsUser: true, Location: WiredPlaceLocationType.CustomLocation), G3Snap(900, G3Lamp, 0, 2, 0, 0, "0")));
        using var f = stack.F;
        // No event user and no selected user: the custom target does not exist, so there is nothing to place on.
        Assert.False(stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [])));
        Assert.Empty(G3Temporaries(f.Room));
    }

    [Fact]
    public void G3CopiesTakeTheNextIdOfTheTemporaryBand()
    {
        // Observed on hhus: copies 2147401728, 1729, 1730, 1731 with removals in between, and 2147401728 again after an unload. AIR
        // class_1846.isTempId is 2147401728..2147418111.
        var stack = G3PlaceStack(G3Place(null, G3Snap(900, G3Lamp, 2, 0, 0, 0, "0")));
        using var f = stack.F;
        var handler = f.Room.GetRoomItemHandler();
        var first = Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 2, 0, 0));
        var second = Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 2, 1, 0));
        var third = Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 2, 2, 0));
        Assert.Equal([2147401728u, 2147401729u, 2147401730u], new[] { first.Id, second.Id, third.Id });
        Assert.All(new[] { first, second, third }, item => Assert.True(TemporaryItemIds.Contains(item.Id)));
        Assert.True(handler.RemoveTemporaryFloorItem(second));
        Assert.True(handler.RemoveTemporaryFloorItemsByWired([first]));
        Assert.Equal(2147401731u, Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 2, 1, 0)).Id);
        Assert.Equal(2147401732u, Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 2, 0, 0)).Id);
        Assert.False(TemporaryItemIds.Contains(TemporaryItemIds.First - 1));
        Assert.False(TemporaryItemIds.Contains(TemporaryItemIds.Last + 1));
    }

    [Fact]
    public void G3AReloadDropsEveryCopyAndStartsTheBandAgain()
    {
        // Observed on hhus: after a 16-minute unload no copy was listed and the next copy was 2147401728 again.
        var (room, _, _) = World();
        var handler = room.GetRoomItemHandler();
        var lamp = G3Definition(G3Lamp);
        Assert.Equal(TemporaryItemIds.First, Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(lamp, 1, 0, 0, 0)).Id);
        Assert.Equal(TemporaryItemIds.First + 1, Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(lamp, 1, 1, 0, 0)).Id);
        handler.LoadFurniture([]);
        Assert.Empty(G3Temporaries(room));
        Assert.Equal(TemporaryItemIds.First, Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(lamp, 1, 0, 0, 0)).Id);
    }

    [Fact]
    public void G3ARefusedTileLeavesNoGhostAndTheNextIdIsStillTheFirst()
    {
        var stack = G3PlaceStack(G3Place(null, G3Snap(900, G3Lamp, 1, 1, 0, 0, "0"), G3Snap(901, G3Lamp, 2, 2, 0, 0, "0")));
        using var f = stack.F;
        var handler = f.Room.GetRoomItemHandler();
        // (1, 1) holds a furni nothing can stack on, so only the second snapshot is placed.
        Assert.False(f.Target.Definition.Stackable);
        f.Room.GetGameMap().AddToMap(f.Target);
        Assert.True(stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User])));
        var copy = Assert.Single(G3Temporaries(f.Room));
        Assert.Equal((2, 2), (copy.GetX, copy.GetY));
        Assert.Equal(TemporaryItemIds.First, copy.Id);
        Assert.Null(handler.GetFloor.FirstOrDefault(item => item.IsTemporary && item.GetX == 1 && item.GetY == 1));
        Assert.Single(f.Room.GetGameMap().GetCoordinatedItems(new(1, 1)));
    }

    [Theory]
    [InlineData(InteractionType.Gate)]
    [InlineData(InteractionType.OneWayGate)]
    [InlineData(InteractionType.Roller)]
    public void G3NoCopyIsPlacedOntoAGateOrRollerEvenWhenItStacks(InteractionType type)
    {
        // Observed on hhus: one-way gate, gate and roller templates save, but a copy on top of them is a placement failure; with an
        // offset to a free tile the copy is placed.
        var stack = G3PlaceStack(G3Place(null, G3Snap(900, G3Lamp, 1, 1, 0, 0, "0"), G3Snap(901, G3Lamp, 2, 2, 0, 0, "0")));
        using var f = stack.F;
        f.Target.Definition.Stackable = true;
        f.Target.Definition.InteractionType = type;
        f.Room.GetGameMap().AddToMap(f.Target);
        Assert.True(stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User])));
        var copy = Assert.Single(G3Temporaries(f.Room));
        Assert.Equal((2, 2), (copy.GetX, copy.GetY));
        Assert.True(f.Room.GetRoomItemHandler().BlocksTemporaryCopy(stack.Lamp, 1, 1, 0));
        Assert.False(f.Room.GetRoomItemHandler().BlocksTemporaryCopy(stack.Lamp, 2, 1, 0));
    }

    [Fact]
    public void G3NothingIsPlacedForAGoneWallOrNonsenseSnapshot()
    {
        var stack = G3PlaceStack(G3Place(null,
            G3Snap(900, 999, 2, 2, 0, 0, "0"),
            G3Snap(901, 6, 2, 1, 0, 0, "0"),
            G3Snap(902, G3Lamp, 2, 0, 0, 9, "0"),
            G3Snap(903, G3Lamp, 2, 0, 0, -1, "0")),
            definitions => definitions[6] = G3Definition(6, type: ItemType.Wall));
        using var f = stack.F;
        Assert.False(stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User])));
        Assert.Empty(G3Temporaries(f.Room));
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void G3AnEmptyBoxPlacesNothing()
    {
        var stack = G3PlaceStack(G3Place());
        using var f = stack.F;
        f.Fire(land: false);
        Assert.Empty(G3Temporaries(f.Room));
        Assert.Empty(f.Errors);
    }

    private sealed class G3PlaceCatalog : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 5;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            10 => new(10, 1, 5, "spawn", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true),
            11 => new(11, 1, 5, "operand", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true),
            _ => null
        };
    }

    private static (G3Stack Stack, WiredVariableModule Module, WiredVariableFrame Frame) G3SpawnStack(WiredTemporaryPlacement placement, ImmutableArray<string> ids)
    {
        var stack = G3PlaceStack(G3Place(placement, G3Snap(900, G3Lamp, 2, 1, 0, 0, "0")) with { VariableIds = ids });
        var wired = stack.F.Room.GetWired();
        var module = new WiredVariableModule(1, new G3PlaceCatalog(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(wired.Variables, module);
        var frame = new WiredVariableFrame(1, []);
        Assert.True(module.Mutate(new(WiredVariableTarget.Context, "custom:11"), new(WiredVariableTarget.Context, 0, 0), WiredVariableMutation.Give, 42, frame));

        return (stack, module, frame);
    }

    private static long? G3SpawnedValue(Room room, WiredVariableModule module, Item copy)
    {
        var holder = WiredVariableRuntimeFrames.FurniHolder(copy);

        return module.Read(new(WiredVariableTarget.Furni, "custom:10"), holder, new(1, [holder]))?.Value;
    }

    [Theory]
    [InlineData(false, 7, "ctx:11", 7)]
    [InlineData(true, 7, "ctx:11", 42)]
    [InlineData(true, 7, "ctx:12", 0)]
    [InlineData(true, 7, "n", 0)]
    public void G3SpawnWithVariableGivesEveryCopyTheLiteralOrTheReadValueAndZeroWhenTheOperandCannotBeRead(bool valueIsVariable, int literal, string operand, long expected)
    {
        var (stack, module, frame) = G3SpawnStack(new(SpawnWithVariable: true, ValueIsVariable: valueIsVariable, Value: literal, ValueTarget: (int)WiredVariableTarget.Context),
            ["furni:10", operand]);
        using var f = stack.F;
        var context = Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User]);
        context.VariableFrame = frame;
        Assert.True(WiredTemporaryFurnitureActions.Execute("wf_act_place_furni", stack.Action.Item, context, stack.Action.Configuration, f.DefinitionManager!));
        var copy = Assert.Single(G3Temporaries(f.Room));
        Assert.Equal(expected, G3SpawnedValue(f.Room, module, copy));
    }

    [Fact]
    public void G3AFurniVariableOnlyLivesOnTheCopyAndGoesWhenTheCopyIsRemoved()
    {
        var (stack, module, frame) = G3SpawnStack(new(SpawnWithVariable: true, Value: 3), ["furni:10", "n"]);
        using var f = stack.F;
        var context = Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User]);
        context.VariableFrame = frame;
        Assert.True(WiredTemporaryFurnitureActions.Execute("wf_act_place_furni", stack.Action.Item, context, stack.Action.Configuration, f.DefinitionManager!));
        var copy = Assert.Single(G3Temporaries(f.Room));
        Assert.Equal(3, G3SpawnedValue(f.Room, module, copy));
        // A variable id that is not a furni variable is never given to a copy.
        Assert.True(f.Room.GetRoomItemHandler().RemoveTemporaryFloorItem(copy));
        Assert.Empty(G3Temporaries(f.Room));
    }

    private static G3Stack G3RemoveStack(int source)
    {
        var f = new TeleportFixture();
        f.Engine.Remove(f.Action.Item.Id);
        f.Items.TryRemove(f.Action.Item.Id, out _);
        var action = ActionBox(f.Room, "wf_act_remove_furni");
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(action, new() { IntParams = [0, source] }, out var config, out var error), error);
        action.ApplyConfiguration(config);
        f.Items[action.Item.Id] = action.Item;
        Assert.True(f.Engine.Add(action));

        return new(f, action, G3Definition(G3Lamp));
    }

    [Fact]
    public void G3RemoveTakesTheTriggeringTemporaryFurniOutOfTheRoomAndNothingElse()
    {
        var stack = G3RemoveStack(0);
        using var f = stack.F;
        var handler = f.Room.GetRoomItemHandler();
        var copy = Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 2, 2, 0));
        var permanent = f.Target;
        Assert.True(f.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.Enter) { Actor = f.User, EventItem = copy }));
        f.Engine.OnFastCycle();
        Assert.Null(handler.GetItem(copy.Id));
        Assert.Empty(f.Room.GetGameMap().GetCoordinatedItems(new(2, 2)));
        Assert.False(handler.OwnsTemporary(copy));
        Assert.Same(permanent, handler.GetItem(permanent.Id));
        // The same furni cannot be removed twice.
        Assert.False(stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use) { EventItem = copy }, f.Items.Values.ToArray(), [f.User])));
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void G3RemoveLeavesPermanentFurniEvenWhenTheyAreTheTriggeringOrSelectedOnes()
    {
        var stack = G3RemoveStack(0);
        using var f = stack.F;
        var handler = f.Room.GetRoomItemHandler();
        var before = handler.GetFloor.Select(item => item.Id).Order().ToArray();
        var context = Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User]);
        context.Triggering.FurniIds.Add(f.Target.Id);
        Assert.False(stack.Action.Execute(context));
        Assert.Equal(before, handler.GetFloor.Select(item => item.Id).Order().ToArray());
        Assert.Same(f.Target, handler.GetItem(f.Target.Id));
        Assert.Equal((1, 1), (f.Target.GetX, f.Target.GetY));
    }

    [Fact]
    public void G3RemoveBySelectorPoolRemovesEveryTemporaryCopyInThePool()
    {
        var stack = G3RemoveStack(200);
        using var f = stack.F;
        var handler = f.Room.GetRoomItemHandler();
        var a = Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 2, 0, 0));
        var b = Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 2, 1, 0));
        var kept = Assert.IsType<Item>(handler.PlaceTemporaryFloorItem(stack.Lamp, 1, 2, 2, 0));
        var context = Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User]);
        context.SelectorPool.FurniIds.UnionWith([a.Id, b.Id, f.Target.Id]);
        context.Selected.FurniIds.UnionWith([a.Id, b.Id, f.Target.Id]);
        context.SelectorKinds = WiredSelectionKind.Furni;
        Assert.True(stack.Action.Execute(context));
        Assert.Null(handler.GetItem(a.Id));
        Assert.Null(handler.GetItem(b.Id));
        Assert.Same(kept, handler.GetItem(kept.Id));
        Assert.Same(f.Target, handler.GetItem(f.Target.Id));
        Assert.Single(G3Temporaries(f.Room));
    }

    [Fact]
    public void G3RemoveRefusesAForgedTemporaryThatTheRoomRegistryDoesNotOwn()
    {
        var stack = G3RemoveStack(0);
        using var f = stack.F;
        var forged = new Item { Id = unchecked((uint)-9), IsTemporary = true, ExtraData = new LegacyDataFormat { Data = "0" }, Definition = G3Definition(G3Lamp) };
        forged.SetState(2, 2, 0, Gamemap.GetAffectedTiles(1, 1, 2, 2, 0));
        f.Items[forged.Id] = forged;
        var context = Context(f.Room, new(WiredEventKind.Use) { EventItem = forged }, f.Items.Values.ToArray(), [f.User]);
        context.Triggering.FurniIds.Add(forged.Id);
        Assert.False(stack.Action.Execute(context));
        Assert.Same(forged, f.Room.GetRoomItemHandler().GetItem(forged.Id));
    }

    [Fact]
    public void G3PlaceThenRemoveRoundTripLeavesTheRoomAsItWasAndReusesNoIdWhileTheRoomLives()
    {
        var stack = G3PlaceStack(G3Place(null, G3Snap(900, G3Lamp, 2, 0, 0, 0, "0")));
        using var f = stack.F;
        var handler = f.Room.GetRoomItemHandler();
        var baseline = handler.GetFloor.Select(item => item.Id).Order().ToArray();
        f.Fire(land: false);
        var first = Assert.Single(G3Temporaries(f.Room));
        Assert.True(handler.RemoveTemporaryFloorItem(first));
        Assert.Equal(baseline, handler.GetFloor.Select(item => item.Id).Order().ToArray());
        Assert.True(stack.Action.Execute(Context(f.Room, new(WiredEventKind.Use), f.Items.Values.ToArray(), [f.User])));
        var second = Assert.Single(G3Temporaries(f.Room));
        // Observed on hhus: a removed copy's id is not handed to the next copy.
        Assert.Equal(first.Id + 1, second.Id);
        Assert.Null(handler.GetItem(first.Id));
        Assert.False(handler.OwnsTemporary(first));
    }
}
