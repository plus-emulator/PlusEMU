using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// Observed on hhus (AIR WIN63-202610131318-860371703): a walks-on stack's flash and message go out in the same tick as the UserUpdate
// that announces the step onto the furni (Location 6,11 with mv 6,10,0.2); a walks-off stack's in the tick announcing the step off
// (Location 6,10 with mv 5,11). The furni interactions still run with the arrival.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(WalkActorKind.Habbo, false)]
    [InlineData(WalkActorKind.Habbo, true)]
    [InlineData(WalkActorKind.Bot, false)]
    [InlineData(WalkActorKind.Bot, true)]
    public void WalkTriggersFireInTheTickThatAnnouncesTheStep(WalkActorKind kind, bool v2)
    {
        var origin = ExecutorFloor(10, 0, 1);
        var landing = ExecutorFloor(11, 1, 1);
        var fired = new List<(WiredEventKind Kind, RoomUser? Actor, Item? Item)>();
        WalkObserveModern(20, 3, 0, "wf_trg_walks_off_furni", origin, fired);
        WalkObserveModern(22, 2, 3, "wf_trg_walks_on_furni", landing, fired);
        var actor = WalkActor(kind, 0, 1, v2);

        actor.MoveTo(1, 1);
        AnnounceTick();

        Assert.Equal((0, 1), (actor.X, actor.Y));
        Assert.True(actor.HasStatus("mv"));
        Assert.Equal(new[] { (WiredEventKind.WalkOff, origin.Id), (WiredEventKind.WalkOn, landing.Id) },
            fired.Select(x => (x.Kind, x.Item!.Id)).ToArray());

        AnnounceTick();
        AnnounceTick();

        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.Equal(2, fired.Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ArrivalStillRunsTheNonWiredLandingWithoutRepeatingTheWalkEvents(bool v2)
    {
        var landing = ExecutorFloor(11, 1, 1);
        var fired = new List<(WiredEventKind Kind, RoomUser? Actor, Item? Item)>();
        WalkObserveModern(22, 2, 3, "wf_trg_walks_on_furni", landing, fired);
        var actor = WalkActor(WalkActorKind.Habbo, 0, 1, v2);

        actor.MoveTo(1, 1);
        AnnounceTick();

        Assert.Single(fired);
        Assert.Null(actor.LastItem);

        AnnounceTick();
        AnnounceTick();

        Assert.Single(fired);
        Assert.Same(landing, actor.LastItem);
    }

    [Fact]
    public void AnAnnouncedStepStandsTheUserOnTheFurniItStepsTo()
    {
        ExecutorFloor(10, 0, 1);
        var landing = ExecutorFloor(11, 1, 1);
        var actor = WalkActor(WalkActorKind.Habbo, 0, 1, v2: true);

        actor.MoveTo(1, 1);
        AnnounceTick();

        Assert.True(actor.SetStep);
        Assert.False(WiredRoomOperations.IsOnItem(actor, landing));
        Assert.True(WiredRoomOperations.IsStandingOn(actor, landing));

        ExecutorTick();

        Assert.False(actor.SetStep);
        Assert.True(WiredRoomOperations.IsStandingOn(actor, landing));
    }

    // Observed on AIR WIN63-202610151745-318700621: a users-in-area selector picks a user as soon as the step into the area is
    // announced, and stops picking one whose step out of it is announced.
    [Fact]
    public void SelectorsReadTheTileAnAnnouncedStepGoesTo()
    {
        ExecutorFloor(10, 0, 1);
        ExecutorFloor(11, 1, 1);
        var actor = WalkActor(WalkActorKind.Habbo, 0, 1, v2: true);

        actor.MoveTo(1, 1);
        AnnounceTick();

        Assert.True(actor.SetStep);
        Assert.Equal((0, 1), (actor.X, actor.Y));
        Assert.Equal((1, 1), SelectorTile(actor));

        ExecutorTick();

        Assert.False(actor.SetStep);
        Assert.Equal((1, 1), SelectorTile(actor));
    }

    // Observed on AIR WIN63-202610151745-318700621: Bot Give Handitem's bot takes the item, walks next to the user and hands it over
    // 300 ms after that step is announced.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GiveHandItemBotWalksNextToTheUserAndHandsOverAfterTheAnnouncedStep(bool v2)
    {
        var user = Viewer(3, 1);
        var bot = WalkActor(WalkActorKind.Bot, 0, 1, v2);
        var targets = WiredBotTargets.For(_room);

        targets.Give(_room, bot, user, 2, 0);

        Assert.Equal(2, bot.CarryItemId);
        Assert.Equal(0, user.CarryItemId);

        // The room's own pass polls the bot targets on its Wired clock; the hand-over follows the announcement by 300 ms.
        var announced = false;

        for (var tick = 0; tick < 12 && !announced; tick++) {
            ExecutorTick();

            if (WiredRoomOperations.StandingTile(bot) == new System.Drawing.Point(2, 1)) {
                announced = true;
                targets.Poll(_room, targets.HandOverAnnouncedAt(bot) ?? 0);
                var at = Assert.NotNull(targets.HandOverAnnouncedAt(bot));
                targets.Poll(_room, at + WiredBotTargets.HandOverDelayMilliseconds - 1);
                Assert.Equal(0, user.CarryItemId);
                targets.Poll(_room, at + WiredBotTargets.HandOverDelayMilliseconds);
            }
        }

        Assert.True(announced);
        Assert.Equal(2, user.CarryItemId);
        Assert.Equal(0, bot.CarryItemId);
        Assert.False(targets.HasTargets);
    }

    // Observed on AIR WIN63-202610201032-107964540: Bot Give Handitem's bot takes the item and always walks to a tile next to the
    // user and hands it over there, also when the closest tile next to the user cannot be entered.
    [Fact]
    public void GiveHandItemBotWalksToAFreeTileWhenTheClosestOneIsBlocked()
    {
        var block = Furni(70, InteractionType.None, WiredBoxType.None);
        block.Definition.Walkable = false;
        block.Definition.Height = 1;
        block.Definition.Width = block.Definition.Length = 1;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, block, 2, 1, 0, true, false, false));
        var user = Viewer(3, 1);
        var bot = WalkActor(WalkActorKind.Bot, 0, 1, v2: true);
        var targets = WiredBotTargets.For(_room);

        targets.Give(_room, bot, user, 2, 0);

        for (var tick = 0; tick < 40 && user.CarryItemId == 0; tick++) {
            ExecutorTick();

            if (targets.HandOverAnnouncedAt(bot) is { } at) {
                targets.Poll(_room, at + WiredBotTargets.HandOverDelayMilliseconds);
            }
        }

        Assert.Equal(2, user.CarryItemId);
        Assert.Equal(0, bot.CarryItemId);
        Assert.NotEqual((0, 1), (bot.X, bot.Y));
    }

    // Observed on AIR WIN63-202610151745-318700621: when the user leaves while the bot walks the item over, the bot keeps walking to
    // where the user stood and lets the item go there.
    [Fact]
    public void GiveHandItemBotFinishesTheWalkAndDropsTheItemWhenTheUserLeft()
    {
        var user = Viewer(3, 1);
        var bot = WalkActor(WalkActorKind.Bot, 0, 1, v2: true);
        var targets = WiredBotTargets.For(_room);

        targets.Give(_room, bot, user, 2, 0);
        targets.Forget(user);

        Assert.True(targets.HasTargets);
        Assert.Equal(2, bot.CarryItemId);

        var announced = false;

        for (var tick = 0; tick < 12 && !announced; tick++) {
            ExecutorTick();

            if (WiredRoomOperations.StandingTile(bot) == new System.Drawing.Point(2, 1)) {
                announced = true;
                targets.Poll(_room, targets.HandOverAnnouncedAt(bot) ?? 0);
                var at = Assert.NotNull(targets.HandOverAnnouncedAt(bot));
                targets.Poll(_room, at + WiredBotTargets.HandOverDelayMilliseconds - 1);
                Assert.Equal(2, bot.CarryItemId);
                targets.Poll(_room, at + WiredBotTargets.HandOverDelayMilliseconds);
            }
        }

        Assert.True(announced);
        Assert.Equal(0, bot.CarryItemId);
        Assert.Equal(0, user.CarryItemId);
        Assert.False(targets.HasTargets);
    }

    // Observed on AIR WIN63-202610151745-318700621: with the user boxed in by furni the bot takes the item but cannot walk it over, and
    // when the user then leaves it keeps holding it.
    [Fact]
    public void GiveHandItemBotKeepsTheItemWhenItCannotReachTheUser()
    {
        foreach (var (id, x, y) in new (uint, int, int)[] { (50, 2, 2), (51, 2, 3), (52, 3, 2) }) {
            var block = Furni(id, InteractionType.None, WiredBoxType.None);
            block.Definition.Walkable = false;
            block.Definition.Height = 1;
            block.Definition.Width = block.Definition.Length = 1;
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, block, x, y, 0, true, false, false));
        }

        var user = Viewer(3, 3);
        var bot = WalkActor(WalkActorKind.Bot, 0, 0, v2: true);
        var targets = WiredBotTargets.For(_room);

        targets.Give(_room, bot, user, 2, 0);
        targets.Forget(user);

        for (var tick = 0; tick < 8; tick++) {
            ExecutorTick();
            targets.Poll(_room, (tick + 1) * 500);
        }

        Assert.Equal(2, bot.CarryItemId);
        Assert.Equal(0, user.CarryItemId);
        Assert.Equal((0, 0), (bot.X, bot.Y));
    }

    // Observed on AIR WIN63-202610151745-318700621: a Move/Rotate into a tile is blocked by a user whose step onto it was announced, and
    // not by one whose step off it was.
    [Fact]
    public void WiredFurniMovesMeetUsersOnTheTileTheirStepGoesTo()
    {
        var block = ExecutorFloor(60, 3, 3, 0, 1);
        var actor = WalkActor(WalkActorKind.Habbo, 0, 1, v2: true);

        actor.MoveTo(1, 1);
        ExecutorTick();

        Assert.True(actor.SetStep);
        Assert.Equal((0, 1), (actor.X, actor.Y));
        Assert.False(WiredRoomOperations.CanMoveItem(_room, block, 1, 1, 0));
        Assert.True(WiredRoomOperations.CanMoveItem(_room, block, 0, 1, 0));
    }

    // Observed on AIR WIN63-202610151745-318700621: a user frozen while walking finishes the step that was announced and stops there.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FreezeLetsTheAnnouncedStepFinish(bool v2)
    {
        var actor = WalkActor(WalkActorKind.Habbo, 0, 1, v2);

        actor.MoveTo(3, 1);
        AnnounceTick();

        Assert.True(actor.SetStep);
        Assert.Equal((1, 1), (actor.SetX, actor.SetY));

        WiredAvatarState.For(_room).FreezeUser(actor, 0, false);

        for (var tick = 0; tick < 4; tick++) {
            AnnounceTick();
        }

        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.False(actor.SetStep);
        Assert.False(actor.HasStatus("mv"));
    }

    // Observed on AIR WIN63-202610151745-318700621: the freeze stops the user on the step announced when it fired; a step announced
    // later is not taken.
    [Fact]
    public void FreezeBeforeAnyStepIsAnnouncedKeepsTheUserInPlace()
    {
        var actor = WalkActor(WalkActorKind.Habbo, 0, 1, v2: true);

        actor.MoveTo(3, 1);
        WiredAvatarState.For(_room).FreezeUser(actor, 0, false);

        for (var tick = 0; tick < 4; tick++) {
            AnnounceTick();
        }

        Assert.Equal((0, 1), (actor.X, actor.Y));
        Assert.False(actor.SetStep);
    }

    private (int X, int Y) SelectorTile(RoomUser actor)
    {
        var context = new WiredRuntimeContext(_room, new(WiredEventKind.ClickFurni), new(() => [], () => [actor]), new NoOperations());
        var avatar = Assert.Single(WiredSelectorRuntimeInput.Capture(context, new(), TestGroupManager.Empty).World.Users);

        return (avatar.X, avatar.Y);
    }

    private sealed class NoOperations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) =>
            throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }

    [Fact]
    public void FlashBatchListsWrittenFurniThenTheTriggerThenTheOtherBoxes()
    {
        var effect = Furni(1, InteractionType.WiredEffect, WiredBoxType.None);
        var condition = Furni(2, InteractionType.WiredCondition, WiredBoxType.None);
        var trigger = Furni(3, InteractionType.WiredTrigger, WiredBoxType.None);
        var tile = Furni(4, InteractionType.None, WiredBoxType.None);

        Assert.Equal(new uint[] { 4, 3, 1, 2 }, WiredComponent.FlashOrder([effect, condition, trigger, tile]).Select(item => item.Id));
    }

    // A room tick, then the Wired pass that runs the stacks it queued (the 50 ms fast cycle).
    private void AnnounceTick()
    {
        ExecutorTick();
        _room.GetWired().OnFastCycle();
    }
}
