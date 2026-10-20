using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Conditions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Items.Wired.Modern.Addons;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.Instance;
using Plus.HabboHotel.Rooms.Games.Teams;
using Xunit;
using Xunit.Abstractions;

namespace Plus.Tests;

public sealed partial class WiredActionPortParityTests(ITestOutputHelper output)
{
    [Fact]
    public void NearestPlayerUsesManhattanRangeAndOrdering()
    {
        var (room, _, _) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var item = Floor(1, 0, 0);
        var diagonal = Avatar(room, 1, 2, 2);
        var cardinal = Avatar(room, 2, 3, 0);
        Assert.Null(WiredDirectionalActions.Nearest(item, [diagonal]));
        Assert.Same(cardinal, WiredDirectionalActions.Nearest(item, [diagonal, cardinal]));
    }

    [Fact]
    public void ChaseStepsTowardADiagonalPlayerInsteadOfPublishingCollision()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 1, 1);
        Place(map, items, mover);
        var user = Avatar(room, 1, 2, 2);
        var events = new List<WiredRuntimeEvent>();
        var action = Box(room, "wf_act_chase", [100], [mover.Id], events.Add);
        Assert.True(action.Execute(Context(room, [mover], [user])));
        // Observed on AIR WIN63-202610151745-318700621: a user on a diagonal is not followed; the chase takes a random step instead.
        Assert.Equal(1, Math.Abs(mover.GetX - 1) + Math.Abs(mover.GetY - 1));
        Assert.Empty(events);
    }

    [Fact]
    public void FurniToUserStillBlocksWhenAnotherHumanSharesTheTargetTile()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = StackableMover(map, items);
        var owner = Avatar(room, 1, 4, 4);
        Register(room, map, owner);
        Register(room, map, Avatar(room, 2, 4, 4));
        Assert.Equal(2, map.GetRoomUsers(new(4, 4)).Count);
        var action = Box(room, "wf_act_furni_to_user", [100, 0], [mover.Id]);
        Assert.False(action.Execute(Context(room, [mover], [owner], owner)));
        Assert.Equal((1, 1), (mover.GetX, mover.GetY));
    }

    [Fact]
    public void FurniToUserKeepsPhysicsThroughUsersForOtherOccupants()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = StackableMover(map, items);
        var owner = Avatar(room, 1, 4, 4);
        var other = Avatar(room, 2, 4, 4);
        Register(room, map, owner);
        Register(room, map, other);
        var action = Box(room, "wf_act_furni_to_user", [100, 0], [mover.Id]);
        var context = Context(room, [mover], [owner], owner);
        context.Policy.Addons.Physics = new(false, new HashSet<uint>(), new HashSet<int> { other.VirtualId }, new HashSet<uint>());
        Assert.True(action.Execute(context));
        Assert.Equal((4, 4), (mover.GetX, mover.GetY));
        Assert.Equal(new HashSet<int> { other.VirtualId }, context.Policy.Addons.Physics.ThroughUsers);
    }

    // Observed on AIR WIN63-202610151745-318700621: with two users selected, the furni lands on whichever user was picked.
    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    public void FurniToUserLandsOnEitherSelectedUser(int seed)
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = StackableMover(map, items);
        var first = Avatar(room, 1, 4, 4);
        var second = Avatar(room, 2, 2, 4);
        Register(room, map, first);
        Register(room, map, second);
        var action = Box(room, "wf_act_furni_to_user", [100, WiredSources.Selector], [mover.Id], random: new Random(seed));
        var landings = new HashSet<(int X, int Y)>();

        for (var firing = 0; firing < 20; firing++) {
            var context = Context(room, [mover], [first, second], first);
            context.SelectorPool.UserIds.UnionWith([first.VirtualId, second.VirtualId]);
            action.Execute(context);
            landings.Add((mover.GetX, mover.GetY));
        }

        Assert.Equal(new[] { (2, 4), (4, 4) }, landings.Order());
    }

    [Fact]
    public void OrdinaryMovementStillBlocksATileHoldingAUser()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = StackableMover(map, items);
        var owner = Avatar(room, 1, 4, 4);
        Register(room, map, owner);
        var action = Box(room, "wf_act_rel_mov", [1, 3, 1, 3, 100], [mover.Id]);
        Assert.False(action.Execute(Context(room, [mover], [owner], owner)));
        Assert.Equal((1, 1), (mover.GetX, mover.GetY));
    }

    private static Item StackableMover(Gamemap map, ConcurrentDictionary<uint, Item> items)
    {
        var mover = Floor(1, 1, 1);
        mover.Definition.Height = 1;
        mover.Definition.Walkable = true;
        mover.Definition.Stackable = true;
        Place(map, items, mover);

        return mover;
    }

    [Fact]
    public void FurniToUserOnlyFailsOntoAnOccupiedTargetNotForTheCardItself()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 1, 1);
        mover.Definition.Height = 1;
        mover.Definition.Walkable = true;
        mover.Definition.Stackable = true;
        Place(map, items, mover);
        var owner = Avatar(room, 1, 4, 4);
        Assert.Empty(map.GetRoomUsers(new(4, 4)));
        var action = Box(room, "wf_act_furni_to_user", [100, 0], [mover.Id]);
        Assert.True(action.Execute(Context(room, [mover], [owner], owner)));
        Assert.Equal((4, 4), (mover.GetX, mover.GetY));
    }

    [Theory]
    [InlineData(4, 4)]
    [InlineData(5, 3)]
    public void FurniToUserMovesTheSelectedItemOntoTheStandingHumanAndLiftsThem(int ownerX, int ownerY)
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 1, 1);
        mover.Definition.Height = 1;
        mover.Definition.Walkable = true;
        mover.Definition.Stackable = true;
        Place(map, items, mover);
        var owner = Avatar(room, 1, ownerX, ownerY);
        Register(room, map, owner);
        Assert.Contains(owner, map.GetRoomUsers(new(ownerX, ownerY)));
        var action = Box(room, "wf_act_furni_to_user", [100, 0], [mover.Id]);
        var moved = action.Execute(Context(room, [mover], [owner], owner));
        Assert.True(moved);
        Assert.Equal((ownerX, ownerY), (mover.GetX, mover.GetY));
        Assert.Equal(1.0, owner.Z);
    }

    [Theory]
    [InlineData("wf_act_chase", 3, 2, 2, 2, true)]
    [InlineData("wf_act_flee", 3, 2, 2, 2, true)]
    [InlineData("wf_act_chase", 4, 2, 3, 2, false)]
    [InlineData("wf_act_flee", 4, 2, 1, 2, false)]
    public void AdjacentAvatarPublishesCollisionForChaseAndFleeAndDistanceTwoStillMoves(string name, int userX, int userY,
        int expectedX, int expectedY, bool collision)
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 2, 2);
        Place(map, items, mover);
        var user = Avatar(room, 1, userX, userY);
        var events = new List<WiredRuntimeEvent>();
        var action = Box(room, name, [100], [mover.Id], events.Add);
        Assert.True(action.Execute(Context(room, [mover], [user])));
        Assert.Equal((expectedX, expectedY), (mover.GetX, mover.GetY));

        if (collision) {
            var published = Assert.Single(events);
            Assert.Equal(WiredEventKind.Collision, published.Kind);
            Assert.Same(user, published.Actor);
            Assert.Same(mover, published.EventItem);
        }
        else {
            Assert.Empty(events);
        }
    }

    [Theory]
    [InlineData("wf_act_chase", true)]
    [InlineData("wf_act_chase", false)]
    [InlineData("wf_act_flee", true)]
    [InlineData("wf_act_flee", false)]
    [InlineData("wf_act_move_to_dir", true)]
    [InlineData("wf_act_move_to_dir", false)]
    public void DirectionalActionsAttemptEveryStackedItemIndependently(string name, bool topFirst)
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var bottom = Floor(2, 2, 2);
        bottom.Definition.Stackable = true;
        bottom.Definition.Height = 0.5;
        var top = Floor(1, 2, 2);
        top.SetState(2, 2, 0.5);
        Place(map, items, bottom);
        Place(map, items, top);
        var user = Avatar(room, 1, name == "wf_act_flee" ? 0 : 4, 2);
        var action = Box(room, name, name == "wf_act_move_to_dir" ? [2, 0, 100, 0] : [100], [1, 2]);
        Assert.True(action.Execute(Context(room, topFirst ? [top, bottom] : [bottom, top], [user])));
        // Flee sidesteps when its away tile is taken (observed r92f r92-flee wall-w1/w2), so a blocked bottom moves aside.
        Assert.Equal(topFirst ? (name == "wf_act_flee" ? (2, 3, 0.0) : (2, 2, 0.0)) : (3, 2, 0.0), (bottom.GetX, bottom.GetY, bottom.GetZ));
        Assert.Equal((3, 2, topFirst ? 0.0 : 0.5), (top.GetX, top.GetY, top.GetZ));
    }

    [Theory]
    [InlineData(1, false, false)]
    [InlineData(0, false, false)]
    [InlineData(1, true, true)]
    [InlineData(0, true, true)]
    public void MoveToDirectionDispatchesCollisionAndLetsPhysicsThroughUsersPass(int blockUsers, bool throughUser, bool moves)
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var item = Floor(1, 2, 2);
        Place(map, items, item);
        var user = Avatar(room, 1, 3, 2);
        map.AddUserToMap(user, new(3, 2));
        var collisions = new List<WiredRuntimeEvent>();
        var action = Box(room, "wf_act_move_to_dir", [2, 0, 100, blockUsers], [1], collisions.Add);
        var context = Context(room, [item], [user]);
        context.Policy.Addons.Physics = new(false, new HashSet<uint>(), throughUser ? new HashSet<int> { user.VirtualId } : new HashSet<int>(), new HashSet<uint>());
        action.Execute(context);
        var collision = Assert.Single(collisions.Where(published => published.Kind == WiredEventKind.Collision));
        Assert.Same(user, collision.Actor);
        Assert.Same(item, collision.EventItem);
        Assert.Equal(moves ? (3, 2) : (2, 2), (item.GetX, item.GetY));
    }

    [Fact]
    public void MoveToDirectionStaysOnItsHeadingWhileAUserBlocksAndMovesOnceTheyLeave()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var item = Floor(1, 2, 2);
        Place(map, items, item);
        var user = Avatar(room, 1, 3, 2);
        map.AddUserToMap(user, new(3, 2));
        var action = Box(room, "wf_act_move_to_dir", [2, 2, 100, 1], [1], _ => { });
        Assert.False(action.Execute(Context(room, [item], [user])));
        Assert.Equal((2, 2), (item.GetX, item.GetY));
        Assert.False(action.Execute(Context(room, [item], [user])));
        Assert.Equal((2, 2), (item.GetX, item.GetY));
        map.RemoveUserFromMap(user, new(3, 2));
        Assert.True(action.Execute(Context(room, [item], [user])));
        Assert.Equal((3, 2), (item.GetX, item.GetY));
    }

    [Fact]
    public void MoveToDirectionWithoutBlockingUsersTurnsAndRetriesInTheSameExecution()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var item = Floor(1, 2, 2);
        Place(map, items, item);
        var user = Avatar(room, 1, 3, 2);
        map.AddUserToMap(user, new(3, 2));
        var action = Box(room, "wf_act_move_to_dir", [2, 2, 100, 0], [1], _ => { });
        Assert.True(action.Execute(Context(room, [item], [user])));
        Assert.Equal((2, 3), (item.GetX, item.GetY));
    }

    // Observed on hhus (AIR WIN63-202610131318-860371703): each save of the box restarts the furni on the configured direction.
    [Fact]
    public void MoveToDirectionSaveRestartsTheRememberedHeading()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var item = Floor(1, 2, 2);
        Place(map, items, item);
        var user = Avatar(room, 1, 3, 2);
        map.AddUserToMap(user, new(3, 2));
        var action = Box(room, "wf_act_move_to_dir", [2, 2, 100, 0], [1], _ => { });
        Assert.True(action.Execute(Context(room, [item], [user])));
        Assert.Equal((2, 3), (item.GetX, item.GetY));
        map.RemoveUserFromMap(user, new(3, 2));
        Assert.True(action.Execute(Context(room, [item], [])));
        Assert.Equal((2, 4), (item.GetX, item.GetY));
        ModernWiredRuntimeTests.LoadStoredRuntime(action, "wf_act_move_to_dir",
            new WiredConfiguration { IntParams = [2, 2, 100, 0], SelectedItems = [1] });
        Assert.True(action.Execute(Context(room, [item], [])));
        Assert.Equal((3, 4), (item.GetX, item.GetY));
    }

    // Official AIR r92d r92-mtd-blocked: east stacks at z=1; NE passes between the two neighboring blocks at z=0.
    [Theory]
    [InlineData(2, 3, 2, 1.0)]
    [InlineData(1, 3, 1, 0.0)]
    public void MoveToDirectionMatchesObservedStackingAndDiagonalPassage(int heading, int x, int y, double z)
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 2, 2);
        var east = Floor(2, 3, 2);
        var north = Floor(3, 2, 1);

        foreach (var item in new[] { mover, east, north }) {
            item.Definition.Stackable = true;
            item.Definition.Height = 1;
            Place(map, items, item);
        }

        var action = Box(room, "wf_act_move_to_dir", [heading, 0, 100, 1], [mover.Id]);
        Assert.True(action.Execute(Context(room, [mover], [])));
        Assert.Equal((x, y, z), (mover.GetX, mover.GetY, mover.GetZ));
        Assert.Equal((3, 2, 0.0), (east.GetX, east.GetY, east.GetZ));
        Assert.Equal((2, 1, 0.0), (north.GetX, north.GetY, north.GetZ));
    }

    [Theory]
    [InlineData(0, 3, 3, true)]
    [InlineData(0, 2, 0, true)]
    [InlineData(0, 0, 1, false)]
    [InlineData(1, 0, 1, true)]
    [InlineData(2, 2, 0, false)]
    public void SlideKeepsWalkingWhenDiagonalStepDistanceDecreases(int mode, int x, int y, bool resumes)
    {
        var (room, map, _) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var user = Avatar(room, 1, 0, 0);
        var users = (ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager).GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(room.GetRoomUserManager())!;
        users[user.VirtualId] = user;
        map.Navigation.Admit(user);
        user.MoveTo(5, 0);
        map.Navigation.ApplyDirty();
        map.Navigation.CycleUsers();
        Assert.True(user.IsWalking, "The fixture must have an active walk before the wired slide.");
        Assert.True(new WiredRoomMovement((_, _, _) => { }).MoveAvatar(Context(room, [], [user]), user, x, y, false, mode));
        map.Navigation.CycleUsers();
        Assert.Equal(resumes, user.IsWalking);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    public void SnapshotQuantifierIncludesMissingPickedFurniture(int quantifier, bool matches)
    {
        var first = Floor(1, 1, 1);
        var config = new WiredConfiguration
        {
            IntParams = [1, 0, 0, 0, 100, quantifier],
            SelectedItems = [1, 2],
            Snapshots = [new(1, 0, 1, 1, 0, 0, "0"), new(2, 0, 2, 2, 0, 0, "0")]
        };
        Assert.Equal(matches, WiredItemConditions.MatchesSnapshot(config, [first]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void GroupTargetEditorTranslatesTheLayoutOntoItsChosenFurnitureOrUser(int targetIsUser)
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var first = Floor(1, 0, 1);
        var second = Floor(2, 1, 1);
        var target = Floor(3, 3, 3);
        target.Definition.Stackable = true;
        Place(map, items, first);
        Place(map, items, second);
        Place(map, items, target);
        var user = Avatar(room, 1, 3, 3);
        var action = Box(room, "wf_act_move_furni_as_group", [targetIsUser, 1, -1, 100, 100, 0], [1, 2], secondary: [3]);
        Assert.True(action.Execute(Context(room, [first, second, target], [user])));
        Assert.Equal((4, 2), (first.GetX, first.GetY));
        Assert.Equal((5, 2), (second.GetX, second.GetY));
        Assert.Equal((3, 3), (target.GetX, target.GetY));
    }

    [Fact]
    public void GroupRejectsTheWholeTranslationWhenOneMemberWouldLeaveTheRoom()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var first = Floor(1, 0, 1);
        var second = Floor(2, 1, 1);
        var target = Floor(3, 5, 3);
        Place(map, items, first);
        Place(map, items, second);
        Place(map, items, target);
        var action = Box(room, "wf_act_move_furni_as_group", [0, 0, 0, 100, 100, 0], [1, 2], secondary: [3]);
        Assert.False(action.Execute(Context(room, [first, second, target], [])));
        Assert.Equal((0, 1), (first.GetX, first.GetY));
        Assert.Equal((1, 1), (second.GetX, second.GetY));
    }

    [Fact]
    public void MoveToEditorResolvesMoversSeparatelyFromItsPickedTarget()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 0, 1);
        var target = Floor(3, 3, 3);
        Place(map, items, mover);
        Place(map, items, target);
        var action = Box(room, "wf_act_move_furni_to", [2, 2, 100, 100], [1], text: "3");
        Assert.True(action.Execute(Context(room, [mover, target], [])));
        Assert.Equal((5, 3), (mover.GetX, mover.GetY));
        Assert.Equal((3, 3), (target.GetX, target.GetY));
    }

    [Theory]
    [InlineData(0, 1, 3, 2)]
    [InlineData(2, 1, 4, 3)]
    [InlineData(4, 2, 3, 5)]
    [InlineData(6, 2, 1, 3)]
    public void MoveToSpacesTheMoverFromTheTargetByTheDirectionAndTileCount(int direction, int tiles, int expectedX, int expectedY)
    {
        // wf_act_move_furni_to: radio N/E/S/W (0/2/4/6) and "Space placed furnis N tiles apart" (1..5), measured from the target furni.
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 0, 0);
        var target = Floor(3, 3, 3);
        Place(map, items, mover);
        Place(map, items, target);
        var action = Box(room, "wf_act_move_furni_to", [direction, tiles, 100, 100], [1], text: "3");
        Assert.True(action.Execute(Context(room, [mover, target], [])));
        Assert.Equal((expectedX, expectedY), (mover.GetX, mover.GetY));
        Assert.Equal((3, 3), (target.GetX, target.GetY));
    }

    [Fact]
    public void MoveToUsesOnlyTheFirstTargetAndDoesNothingWithoutOne()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 0, 0);
        var first = Floor(3, 3, 3);
        var second = Floor(4, 5, 5);
        Place(map, items, mover);
        Place(map, items, first);
        Place(map, items, second);
        var action = Box(room, "wf_act_move_furni_to", [2, 1, 100, 100], [1], text: "3;4");
        Assert.True(action.Execute(Context(room, [mover, first, second], [])));
        Assert.Equal((4, 3), (mover.GetX, mover.GetY));
        var none = Box(room, "wf_act_move_furni_to", [2, 1, 100, 100], [1], text: "99");
        Assert.False(none.Execute(Context(room, [mover, first, second], [])));
        Assert.Equal((4, 3), (mover.GetX, mover.GetY));
    }

    [Fact]
    public void MoveToLeavesTheTargetWhereItIsWhenItIsAlsoAMover()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var mover = Floor(1, 0, 0);
        var target = Floor(3, 3, 3);
        Place(map, items, mover);
        Place(map, items, target);
        var action = Box(room, "wf_act_move_furni_to", [2, 1, 100, 100], [1, 3], text: "3");
        Assert.True(action.Execute(Context(room, [mover, target], [])));
        Assert.Equal((4, 3), (mover.GetX, mover.GetY));
        Assert.Equal((3, 3), (target.GetX, target.GetY));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(3, 1)]
    [InlineData(2, 0)]
    [InlineData(2, 6)]
    public void MoveToRefusesDirectionsAndTileCountsTheEditorDoesNotOffer(int direction, int tiles)
    {
        var (room, _, _) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var action = CreateBox(room, "wf_act_move_furni_to");
        Assert.False(WiredNativeTestSupport.TryValidateRuntime(action, new() { IntParams = [direction, tiles, 100, 100], SelectedItems = [1], Text = "3" }, out _, out _));
    }

    [Theory]
    [InlineData(true, false, null, 1)]
    [InlineData(false, false, null, 1)]
    [InlineData(true, true, 1, 1)]
    [InlineData(false, true, 1, 1)]
    [InlineData(true, true, null, 2)]
    [InlineData(false, true, null, 2)]
    [InlineData(true, true, 0, 0)]
    [InlineData(false, true, -1, 0)]
    public void VariableSortAndQuantityAddonsFilterTheActualSelectorPoolInStackOrder(bool sortFirst, bool variableQuantity, int? variableCount, int expectedCount)
    {
        var (room, _, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        room.Id = 1;
        room.OwnerId = 5;
        var movers = Enumerable.Range(1, 3).Select(id => Floor((uint)id, id, 1)).ToArray();

        foreach (var item in movers) {
            item.OwnerId = 5;
            items[item.Id] = item;
        }

        var holders = movers.Select(WiredVariableRuntimeFrames.FurniHolder).ToArray();
        var module = new WiredVariableModule(1, new FurnitureDirectory(), new MemoryWiredVariableStore(), new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var frame = new WiredVariableFrame(1, holders);

        for (var i = 0; i < holders.Length; i++) {
            module.Mutate(new(WiredVariableTarget.Furni, "custom:10"), holders[i], WiredVariableMutation.Give, new[] { 10, 30, 20 }[i], frame);
        }

        if (variableQuantity && variableCount is { } amount) {
            // First selected holder lacks the operand; the next readable holder supplies it.
            Assert.True(module.Mutate(new(WiredVariableTarget.Furni, "custom:11"), holders[1], WiredVariableMutation.Give, amount, frame));
            Assert.True(module.Mutate(new(WiredVariableTarget.Furni, "custom:11"), holders[2], WiredVariableMutation.Give, 2, frame));
        }

        var world = new WiredSelectorWorld(6, 6, movers.Select(item => new WiredSelectorFurniture(item.Id, 0, "test", "0", item.GetX, item.GetY, 0, 0, [(item.GetX, item.GetY)])).ToArray(), []);
        var errors = new List<Exception>();
        var engine = new WiredStackEngine(() => 0, _ => true, _ => true, _ => { }, errors.Add);
        engine.BindRuntime(room, new(() => items.Values, () => []), new UnusedOperations());
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_game_starts", out var triggerDescriptor));
        var trigger = new WiredModernTrigger(room, Floor(50, 5, 5), triggerDescriptor);
        WiredNativeTestSupport.InstallRuntime(trigger, WiredTriggerConfiguration.Defaults("wf_trg_game_starts"));
        engine.Add(trigger);
        Assert.True(WiredBoxRegistry.TryGet("wf_slc_furni_picks", out var selectorDescriptor));
        var selectorItem = InRoom(room, Floor(51, 5, 5));
        var selector = new WiredSelectorBox(room, selectorItem, selectorDescriptor, new(), TestGroupManager.Empty, readWorld: _ => world);
        WiredNativeTestSupport.Install(selector, WiredNativeEditorProjection.DefaultNative(selectorDescriptor) with
        {
            PrimaryItems = [new(1, false), new(2, false), new(3, false)]
        });
        engine.Add(selector);
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_filter_furni_by_var", out var sortDescriptor));
        var sort = new WiredVariableAddonBox(room, Floor(sortFirst ? 52u : 53u, 5, 5), sortDescriptor, module, _ => new Dictionary<int, string>());
        WiredNativeTestSupport.Install(sort, WiredNativeTestSupport.FromLegacyVariableAddon(sortDescriptor,
            new() { IntParams = [0, 0, 2, 1, 0, 0], Text = "custom:10\t" }));
        engine.Add(sort);
        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_filter_furni", out var quantityDescriptor));
        var quantity = new WiredAddonBox(room, Floor(sortFirst ? 53u : 52u, 5, 5), quantityDescriptor, new(), TestGroupManager.Empty,
            variables: context => WiredSelectorVariableBridge.Create(context, module), readWorld: _ => world);
        var quantityNative = WiredNativeEditorProjection.DefaultNative(quantityDescriptor);
        WiredNativeTestSupport.Install(quantity, variableQuantity
            // Observed on AIR WIN63-202610131318-860371703: the filter's holder slot offers 0/100/201, so the holders are picked here.
            ? quantityNative with
            {
                OwnedIntParams = [2, 1, WiredNativeAuxiliaryEditor.AirTarget(1)],
                FurniSourceTypes = [100],
                PrimaryItems = [new(1, false), new(2, false), new(3, false)],
                VariableIds = ["furni:11"]
            }
            : quantityNative with { OwnedIntParams = [1, 0, 0] });
        engine.Add(quantity);
        Assert.True(WiredBoxRegistry.TryGet("wf_act_change_var_val", out var actionDescriptor));
        var action = new WiredVariableConfiguredBox(room, Floor(54, 5, 5), actionDescriptor, new(module, TimeProvider.System));
        WiredNativeTestSupport.Install(action, WiredNativeEditorProjection.DefaultNative(actionDescriptor) with
        {
            OwnedIntParams = [WiredNativeAuxiliaryEditor.AirTarget(1), 0, 0, 0, 7, WiredNativeAuxiliaryEditor.AirTarget(1)],
            UserSourceTypes = [0, 0],
            FurniSourceTypes = [200, 0],
            VariableIds = ["furni:10", "n"]
        });
        engine.Add(action);
        Assert.True(engine.DispatchSynchronously(new(WiredEventKind.GameStart)));
        engine.OnFastCycle();
        Assert.Empty(errors);
        var expected = expectedCount == 0 ? new long[] { 10, 30, 20 } : expectedCount == 2
            ? sortFirst ? new long[] { 10, 7, 7 } : [7, 7, 20]
            : sortFirst ? new long[] { 10, 7, 20 } : [7, 30, 20];
        var actual = holders.Select(holder => module.Read(new(holder.Target, "custom:10"), holder, frame)!.Value).ToArray();

        if (!variableQuantity) {
            // A literal amount keeps a random subset of the picks: exactly one holder was changed to 7 and every other holder keeps its own original value.
            long[] original = [10, 30, 20];
            Assert.Equal(1, actual.Count(value => value == 7));
            Assert.All(actual.Select((value, index) => (value, index)).Where(pair => pair.value != 7), pair => Assert.Equal(original[pair.index], pair.value));

            return;
        }

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false, "0", "1", "2")]
    [InlineData(true, "3", "4", "5")]
    public void SelectorsFlashBetweenTheirRestingStatePlusOneAndPlusTwo(bool filter, string resting, string first, string second)
    {
        // Room 79874981 (WIN63-202610121709): area selectors rest at 0 and alternate 1/2; filter-mode selectors rest at 3 and alternate 4/5.
        var (room, _, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var flashed = new List<Item>();
        var world = new WiredSelectorWorld(6, 6, [], []);
        var engine = new WiredStackEngine(() => 0, _ => true, _ => true, flashed.Add, error => throw error);
        engine.BindRuntime(room, new(() => items.Values, () => []), new UnusedOperations());
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_game_starts", out var triggerDescriptor));
        var trigger = new WiredModernTrigger(room, Floor(50, 5, 5), triggerDescriptor);
        WiredNativeTestSupport.InstallRuntime(trigger, WiredTriggerConfiguration.Defaults("wf_trg_game_starts"));
        engine.Add(trigger);
        Assert.True(WiredBoxRegistry.TryGet("wf_slc_furni_area", out var selectorDescriptor));
        var selectorItem = InRoom(room, Floor(51, 5, 5));
        var selector = new WiredSelectorBox(room, selectorItem, selectorDescriptor, new(), TestGroupManager.Empty, readWorld: _ => world);
        WiredNativeTestSupport.Install(selector, WiredNativeEditorProjection.DefaultNative(selectorDescriptor) with
        {
            OwnedIntParams = [0, 0, 2, 2],
            Filter = filter
        });
        engine.Add(selector);

        Assert.True(engine.DispatchSynchronously(new(WiredEventKind.GameStart)));
        engine.OnFastCycle();

        Assert.Contains(selectorItem, flashed);
        Assert.Equal(first, WiredFlashStates.Next(resting, selector));
        Assert.Equal(second, WiredFlashStates.Next(first, selector));
        Assert.Equal(first, WiredFlashStates.Next(second, selector));
        Assert.Equal("1", WiredFlashStates.Next("0", trigger));
        Assert.Equal("0", WiredFlashStates.Next("1", trigger));
    }

    [Fact]
    public void AltitudeAdditionCanPassFortyWithinThePlatformTopLimit()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var item = Floor(1, 1, 1);
        item.SetState(1, 1, 30);
        Place(map, items, item);
        var action = Box(room, "wf_act_set_altitude", [0, 100], [1], text: "20");
        Assert.True(action.Execute(Context(room, [item], [])));
        Assert.Equal(50, item.GetZ);
    }

    [Theory]
    [InlineData(1, "50", 50)]
    [InlineData(1, "80", 79)]
    [InlineData(0.65, "80", 79.35)]
    [InlineData(1, "80", 79, "1,0.75,0.5,0.25,0.1", "0")]
    [InlineData(1, "80", 79.5, "1,0.75,0.5,0.25,0.1", "2")]
    [InlineData(1, "80", 80, "1,0.75,0.5,0.25,0", "4")]
    [InlineData(4, "80", 76, "4,3,2,1,0", "0")]
    [InlineData(4, "80", 77, "4,3,2,1,0", "1")]
    [InlineData(4, "80", 78, "4,3,2,1,0", "2")]
    [InlineData(4, "80", 79, "4,3,2,1,0", "3")]
    [InlineData(4, "80", 80, "4,3,2,1,0", "4")]
    public void AltitudeSetHonorsTheEightyTileTopLimitAndDefinitionHeight(double height, string requested, double expected,
        string adjustableHeights = "", string state = "0")
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var item = Floor(1, 1, 1);
        item.Definition.Height = height;
        item.Definition.AdjustableHeights = adjustableHeights.Length == 0 ? new List<double>()
            : adjustableHeights.Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToList();
        item.ExtraData = new LegacyDataFormat { Data = state };
        Place(map, items, item);
        var action = Box(room, "wf_act_set_altitude", [2, 100], [1], text: requested);
        Assert.True(action.Execute(Context(room, [item], [])));
        Assert.Equal(expected, item.GetZ);
    }

    [Fact]
    public void ZeroHeightStateAtTheFloorStacksTheNextItemAtTheFloorAndNavSeesNoHeight()
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var support = Floor(1, 2, 2);
        support.Definition.Stackable = true;
        support.Definition.Height = 4;
        support.Definition.AdjustableHeights = [4, 3, 2, 1, 0];
        support.ExtraData = new LegacyDataFormat { Data = "4" };
        Place(map, items, support);
        Assert.Equal((0, 0), (support.CurrentHeight, support.TotalHeight));
        var nav = NavItemRecord.Capture(support, 1, 6, 6);
        Assert.Equal((0, 0), (nav.Height, nav.Top));
        var next = Floor(2, 4, 4);
        next.Definition.Stackable = true;
        next.SetState(4, 4, 3);
        Place(map, items, next);
        Assert.True(WiredRoomOperations.MoveItem(room, next, 2, 2, 0, null, animate: false));
        Assert.Equal(0, next.GetZ);
    }

    [Fact]
    public void AdjustableHeightFallsBackToTheDefinitionWhenTheStateOrListCannotSelectOne()
    {
        var item = Floor(1, 2, 2);
        item.Definition.Height = 2;
        item.Definition.AdjustableHeights = [1, 0.5];
        item.ExtraData = new LegacyDataFormat { Data = "5" };
        Assert.Equal(2, item.CurrentHeight);
        item.ExtraData = new LegacyDataFormat { Data = "x" };
        Assert.Equal(2, item.CurrentHeight);
        item.ExtraData = new LegacyDataFormat { Data = "-1" };
        Assert.Equal(2, item.CurrentHeight);
        item.ExtraData = new LegacyDataFormat { Data = "1" };
        Assert.Equal(0.5, item.CurrentHeight);
        item.Definition.AdjustableHeights = [1];
        Assert.Equal(2, item.CurrentHeight);
        item.Definition.AdjustableHeights = [1, -0.5];
        Assert.Equal(2, item.CurrentHeight);
    }

    [Theory]
    [InlineData("wf_act_give_score")]
    [InlineData("wf_act_give_score_tm")]
    public void ScoreQuotaEditorCountsPerPlayerAndResetsPerGame(string name)
    {
        var (room, _, _) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var user = Avatar(room, 1, 1, 1);
        user.Team = Team.Red;
        room.GetSoccer().StartGame();
        room.GetGameManager().AddPointToTeam(Team.Red, 20);
        var action = Box(room, name, name == "wf_act_give_score" ? [5, 1, 0, 2] : [5, 1, 1, 0, 2], []);
        var context = Context(room, [], [user], user);
        Assert.True(action.Execute(context));
        Assert.True(action.Execute(context));
        Assert.False(action.Execute(context));
        Assert.Equal(10, room.GetGameManager().Points[1]);
        WiredGameState.For(room).ResetQuotas();
        Assert.True(action.Execute(context));
        Assert.Equal(5, room.GetGameManager().Points[1]);
    }

    [Theory]
    [InlineData("furni:10", "user:11", true)]
    [InlineData("furni:10", "furni:11", false)]
    [InlineData("user:10", "user:11", false)]
    [InlineData("n", "n", true)]
    public void NativePlacementUsesCatalogIdsOfTheDeclaredVariableTargets(string destination, string source, bool expected)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_place_furni", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [0, 0, 0, 0, 0, 0, 1, 1, 0, 1],
            VariableIds = [destination, source]
        };
        Assert.Equal(expected, WiredNativeEditorProjection.TryCompile(7, descriptor, native, out var runtime));

        if (expected) {
            Assert.Equal(new[] { destination, source }, runtime.VariableIds.ToArray());
            Assert.NotNull(runtime.TemporaryPlacement);
            Assert.Empty(runtime.IntParams);
        }
    }

    [Theory]
    [InlineData(false, 0, 0, 23, 23, 1)]
    [InlineData(true, 0, 0, 26, 26, 2)]
    [InlineData(false, 0, 1, 13, 26, 2)]
    [InlineData(false, 1, 0, 20, 23, 2)]
    [InlineData(false, 1, 1, 10, 23, 1)]
    public void RealScalarActionsBatchWithinAStackUnlessOrderedOrSeparatedByADelay(bool ordered, int addDelay, int multiplyDelay, int firstValue, int expected, int notifications)
    {
        var (room, _, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        room.Id = 1;
        room.OwnerId = 5;
        var target = Floor(1, 0, 1);
        target.OwnerId = 5;
        items[target.Id] = target;
        var holder = WiredVariableRuntimeFrames.FurniHolder(target);
        var reference = new WiredVariableReference(WiredVariableTarget.Furni, "custom:10");
        var module = new WiredVariableModule(1, new FurnitureDirectory(), new MemoryWiredVariableStore(), new FixedTimeProvider(DateTimeOffset.UnixEpoch));
        var frame = new WiredVariableFrame(1, [holder]);
        module.Mutate(reference, holder, WiredVariableMutation.Give, 10, frame);
        module.DrainChanges();
        long now = 0;
        var errors = new List<Exception>();
        var engine = new WiredStackEngine(() => now, _ => true, _ => true, _ => { }, errors.Add);
        engine.BindRuntime(room, new(() => items.Values, () => []), new UnusedOperations());
        var triggerItem = Floor(50, 5, 5);
        Assert.True(WiredBoxRegistry.TryGet("wf_trg_game_starts", out var triggerDescriptor));
        var trigger = new WiredModernTrigger(room, triggerItem, triggerDescriptor);
        WiredNativeTestSupport.InstallRuntime(trigger, WiredTriggerConfiguration.Defaults("wf_trg_game_starts"));
        engine.Add(trigger);
        Add(51, 1, 3, addDelay);
        Add(52, 3, 2, multiplyDelay);

        if (ordered) {
            Assert.True(WiredBoxRegistry.TryGet("wf_xtra_exec_in_order", out var descriptor));
            var addon = new WiredAddonBox(room, Floor(53, 5, 5), descriptor, new(), TestGroupManager.Empty,
                readWorld: _ => new WiredSelectorWorld(6, 6, [], []));
            WiredNativeTestSupport.Install(addon, WiredNativeEditorProjection.DefaultNative(descriptor));
            engine.Add(addon);
        }

        var accepted = engine.DispatchSynchronously(new(WiredEventKind.GameStart));
        Assert.Empty(errors);
        Assert.True(accepted);
        Assert.Equal(10, module.Read(reference, holder, frame)!.Value);
        engine.OnFastCycle();
        Assert.Equal(firstValue, module.Read(reference, holder, frame)!.Value);
        now = 500;
        engine.OnFastCycle();
        now = 1000;
        engine.OnFastCycle();
        Assert.Equal(expected, module.Read(reference, holder, frame)!.Value);
        Assert.Equal(notifications, module.DrainChanges().Count);
        Assert.Empty(errors);

        void Add(uint id, int operation, int operand, int actionDelay)
        {
            var item = InRoom(room, Floor(id, 5, 5));
            Assert.True(WiredBoxRegistry.TryGet("wf_act_change_var_val", out var descriptor));
            var box = new WiredVariableConfiguredBox(room, item, descriptor, new(module, TimeProvider.System));
            var air = WiredNativeAuxiliaryEditor.AirTarget(1);
            WiredNativeTestSupport.Install(box, WiredNativeEditorProjection.DefaultNative(descriptor) with
            {
                OwnedIntParams = [air, operation, 0, 0, operand, air],
                UserSourceTypes = [0, 0],
                FurniSourceTypes = [100, 100],
                VariableIds = ["furni:10", "n"],
                PrimaryItems = [new(1, false)],
                Delay = actionDelay
            });
            engine.Add(box);
        }
    }

    [Theory]
    [InlineData("bb_counter", InteractionType.Banzaicounter)]
    [InlineData("fball_counter", InteractionType.Counter)]
    [InlineData("es_counter", InteractionType.Freezetimer)]
    public void NativeTimerClicksAndWiredControlsDriveActualRoomGames(string name, InteractionType type)
    {
        var (room, map, items) = World();
        using var ownerScope = RoomOwnerScope.Enter(room);
        var wired = new WiredComponent(room, TestLogging.Logger, TimeProvider.System, TestRoomSettings.Empty, TestWiredRoomSettingsFactory.Instance,
            TestWiredConfigurationStore.Instance, TestWiredDatabase.Instance, TestWiredRewardService.Instance, TestBotManagementStore.Instance,
            TestWiredClients.Empty, TestGroupManager.Empty, TestWiredDefinitions.Unused, TestWiredCommands.Unused, TestWiredAccess.Unused, TestItemRuntime.Travel);
        typeof(Room).GetField("_wiredComponent", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, wired);
        var timer = Floor(1, 0, 1);
        timer.Definition.ItemName = name;
        timer.Definition.InteractionName = "game_timer";
        timer.Definition.InteractionType = type;
        timer.LegacyDataString = "8";
        timer.Attach(room, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
        Place(map, items, timer);
        wired.AttachRoomItem(timer);
        var clocks = (WiredCounterController)typeof(WiredComponent).GetField("_counters", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(wired)!;
        var interactor = TestItemRuntime.Interactors.Create(timer, TimeProvider.System);
        interactor.OnTrigger(null, timer, 0, false);
        Assert.False(Active());
        interactor.OnTrigger(null, timer, 0, true);
        Assert.True(Active());
        Assert.True(clocks.IsRunning(timer));
        var pause = Box(room, "wf_act_control_clock", [3, 100], [1], clocks: clocks);
        Assert.True(pause.Execute(Context(room, [timer], [])));
        Assert.False(Active());
        Assert.Equal(8000, clocks.ReadMilliseconds(timer));
        interactor.OnTrigger(null, timer, 0, true);
        Assert.True(Active());
        interactor.OnTrigger(null, timer, 2, true);
        Assert.False(Active());
        Assert.Equal(30000, clocks.ReadMilliseconds(timer));

        bool Active() => type switch
        {
            InteractionType.Banzaicounter => room.GetBanzai().IsBanzaiActive,
            InteractionType.Freezetimer => room.GetFreeze().GameIsStarted,
            _ => room.GetSoccer().GameIsStarted
        };
    }


    // Native pick validation reads the box item's live room.
    private static Item InRoom(Room room, Item item)
    {
        typeof(Item).GetField("_room", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(item, room);

        return item;
    }

    private static WiredModernAction Box(Room room, string name, int[] parameters, uint[] selected, Action<WiredRuntimeEvent>? publish = null, string text = "", WiredCounterController? clocks = null, uint[]? secondary = null, Random? random = null)
    {
        var action = CreateBox(room, name, publish, clocks: clocks, random: random);
        var proposed = new WiredConfiguration { IntParams = [.. parameters], SelectedItems = [.. selected], SecondarySelectedItems = [.. secondary ?? []], Text = text };

        if (name is "wf_act_move_to_dir" or "wf_act_set_altitude" or "wf_act_move_furni_as_group"
            or "wf_act_control_clock" or "wf_act_give_score") {
            ModernWiredRuntimeTests.LoadStoredRuntime(action, name, proposed);
        }
        else {
            Assert.True(WiredNativeTestSupport.TryValidateRuntime(action, proposed, out var config, out var error), error);
            action.ApplyConfiguration(config);
        }

        return action;
    }

    private static WiredModernAction CreateBox(Room room, string name, Action<WiredRuntimeEvent>? publish = null,
        WiredCounterController? clocks = null, IItemDataManager? definitions = null, Random? random = null)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        var item = Floor(50, 5, 5);
        typeof(Item).GetField("_room", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(item, room);
        item.RoomId = room.Id;

        return new(room, item, descriptor, clocks ?? new(), publish ?? (_ => { }),
            (_, _, _) => { }, new(), TestLogging.Logger, TimeProvider.System, TestWiredRewardService.Instance,
            TestBotManagementStore.Instance, TestWiredClients.Empty, definitions ?? TestWiredDefinitions.Unused, TestItemRuntime.Travel,
            movementRandom: random);
    }

    private sealed class ItemDefinitions(ItemDefinition definition) : IItemDataManager
    {
        public void Init() { }
        public ItemDefinition? GetItemByName(string name) => Items.Values.FirstOrDefault(item => item.ItemName == name);
        public Dictionary<int, uint> Gifts { get; } = [];
        public Dictionary<uint, ItemDefinition> Items { get; } = new() { [definition.Id] = definition };
    }

    private static void Register(Room room, Gamemap map, RoomUser user)
    {
        var users = (ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager).GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(room.GetRoomUserManager())!;
        users[user.VirtualId] = user;
        using var owner = RoomOwnerScope.Enter(room);
        map.Navigation.Admit(user);
    }

    private static RoomUser Avatar(Room room, int id, int x, int y)
    {
        var (client, _) = HabbiconTestSupport.Client(new Habbo { Id = id, Username = $"user{id}", CurrentRoom = room });
        var user = new RoomUser(id, 0, id, room, client, TestChatEmotions.Unused, TestRewardProgress.Unused);
        user.InitializePosition(x, y, 0);

        return user;
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

    private static void Place(Gamemap map, ConcurrentDictionary<uint, Item> items, Item item)
    {
        items[item.Id] = item;
        map.AddToMap(item);
        item.EnableNavigationSynchronization();
        map.Navigation.Inputs.Attach(item);
        map.Navigation.ApplyDirty();
    }

    private static (Room Room, Gamemap Map, ConcurrentDictionary<uint, Item> Items) World(string? heightmap = null)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        typeof(Room).GetField("_interactionClock", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, TimeProvider.System);
        var map = new Gamemap(room, new RoomModel("wired-parity", 0, 0, 0, 0, heightmap ?? string.Join('\r', Enumerable.Repeat("000000", 6)), 0, 0, true),
            TestLogging.Navigation, TestRoomSettings.Empty, TestGroupManager.Empty, TestNavigationDatabase.Instance, TestNavigationRewards.Instance);
        var handler = new RoomItemHandling(room, TestRoomItemStore.Instance, TestRoomItemMetadataStore.Instance,
            TestGameClientManager.Empty, TestLanguageManager.RoomItems, TestItemRuntime.Interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards);
        typeof(Room).GetField("_gamemap", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, map);
        typeof(Room).GetField("_roomItemHandling", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, handler);
        typeof(Room).GetField("_roomUserManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room,
            new RoomUserManager(room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(),
                TestChatEmotions.Unused, TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel));
        TestRoomUserSnapshots.Install(room);
        map.GenerateMaps();

        return (room, map, (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler)!);
    }

    private static WiredRuntimeContext Context(Room room, Item[] items, RoomUser[] users, RoomUser? eventActor = null)
    {
        foreach (var user in users) {
            if (room.GetRoomUserManager().GetRoomUserByVirtualId(user.VirtualId) == null) {
                Register(room, room.GetGameMap(), user);
            }
        }

        var context = new WiredRuntimeContext(room, new(WiredEventKind.Use) { Actor = eventActor }, new(() => items, () => users), new UnusedOperations());

        if (users.FirstOrDefault() is { } actor) {
            context.Triggering.UserIds.Add(actor.VirtualId);
        }

        context.Policy.Addons.DisableAnimation = true;

        return context;
    }

    private sealed class UnusedOperations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }

    private sealed class FurnitureDirectory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint id) => id is 10 or 11 ? new(id, 1, 5, "value", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true) : null;
        public uint? GetRoomOwner(uint roomId) => roomId == 1 ? 5u : null;
    }
}
