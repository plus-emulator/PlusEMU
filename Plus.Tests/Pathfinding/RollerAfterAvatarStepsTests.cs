using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Xunit;

namespace Plus.Tests;

// In a room tick the avatars step first and the rollers run after, in the same tick.
public partial class PlacedFurniRoomTests
{
    private RoomUser StepOrderBot(int x, int y, double z = 0)
    {
        var data = (RoomBot)RuntimeHelpers.GetUninitializedObject(typeof(RoomBot));
        data.AiType = BotAiType.Generic;
        data.Name = "Robbie";
        var bot = new RoomUser(0, RoomId, 5, _room, null, TestChatEmotions.Unused, TestRewardProgress.Unused) { X = x, Y = y, BotData = data };
        var users = (ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager).GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_room.GetRoomUserManager())!;
        users[bot.VirtualId] = bot;
        bot.InternalRoomId = bot.VirtualId;
        bot.SetPos(x, y, z);
        _room.GetGameMap().Navigation!.Admit(bot);
        ExecutorTick();

        return bot;
    }

    private bool StepOrderSlid(RoomUser actor)
    {
        try {
            ExecutorAvatarSlide(actor);

            return true;
        }
        catch (InvalidOperationException) {
            return false;
        }
    }

    // Runs room ticks until the actor slides and returns the zero-based tick, or -1.
    private int StepOrderSlideTick(RoomUser actor, int ticks = 6)
    {
        for (var tick = 0; tick < ticks; tick++) {
            ExecutorTick();

            if (StepOrderSlid(actor)) {
                return tick;
            }
        }

        return -1;
    }

    private RoomUser StepOrderViewerOffTheRoller() => ExecutorActor(3, 3);

    private Item StepOrderSolid(uint id, int x, int y)
    {
        var item = Furni(id, InteractionType.None, WiredBoxType.None);
        item.Definition.Walkable = false;
        item.Definition.Stackable = false;
        item.Definition.Height = 1;
        item.Definition.Width = item.Definition.Length = 1;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, x, y, 0, true, false, false));

        return item;
    }

    [Fact]
    public void ARiderSlidesInTheTickABotStartsToLeaveTheTarget()
    {
        ExecutorRoller(10, 0, 1);
        var rider = ExecutorRollerActor(0, 1, 0.5);
        var bot = StepOrderBot(1, 1);
        EnableExecutorRollers();
        ExecutorTick();
        ExecutorTick();
        Assert.Equal((0, 1), (rider.X, rider.Y));

        bot.MoveTo(3, 1);

        Assert.Equal(0, StepOrderSlideTick(rider));
        Assert.True(bot.HasStatus("mv"));
        Assert.Equal((1, 1), (rider.X, rider.Y));
    }

    [Fact]
    public void CargoSlidesInTheTickABotStartsToLeaveTheTarget()
    {
        ExecutorRoller(10, 0, 1);
        var cargo = ExecutorFloor(12, 0, 1, z: 0.5, height: 0.1);
        StepOrderViewerOffTheRoller();
        var bot = StepOrderBot(1, 1);
        EnableExecutorRollers();
        ExecutorTick();
        ExecutorTick();
        Assert.Equal((0, 1), (cargo.GetX, cargo.GetY));

        bot.MoveTo(3, 1);
        ExecutorTick();

        Assert.True(bot.HasStatus("mv"));
        Assert.Equal((1, 1), (cargo.GetX, cargo.GetY));
    }

    [Fact]
    public void ARiderSlidesInTheTickAUserStartsToLeaveTheTarget()
    {
        ExecutorRoller(10, 0, 1);
        var stepper = ExecutorActor(1, 1);
        var rider = StepOrderBot(0, 1, 0.5);
        EnableExecutorRollers();
        ExecutorTick();
        ExecutorTick();
        Assert.Equal((0, 1), (rider.X, rider.Y));

        stepper.MoveTo(3, 1);

        Assert.Equal(0, StepOrderSlideTick(rider));
        Assert.True(stepper.HasStatus("mv"));
    }

    [Fact]
    public void ABotThatStaysOnTheTargetStillStopsTheSlide()
    {
        ExecutorRoller(10, 0, 1);
        var rider = ExecutorRollerActor(0, 1, 0.5);
        StepOrderBot(1, 1);
        EnableExecutorRollers();

        Assert.Equal(-1, StepOrderSlideTick(rider));
        Assert.Equal((0, 1), (rider.X, rider.Y));
    }

    [Fact]
    public void ARiderStaysWhenABotStepsOntoTheTargetInTheSameTick()
    {
        ExecutorRoller(10, 0, 1);
        var rider = ExecutorRollerActor(0, 1, 0.5);
        var bot = StepOrderBot(2, 1);
        EnableExecutorRollers();
        bot.MoveTo(1, 1);

        ExecutorTick();

        Assert.False(StepOrderSlid(rider));
        Assert.Equal((0, 1), (rider.X, rider.Y));
    }

    [Fact]
    public void ABotThatWalksOntoARollerSlidesInTheTickItArrives()
    {
        ExecutorRoller(10, 1, 1);
        StepOrderViewerOffTheRoller();
        var bot = StepOrderBot(0, 1);
        EnableExecutorRollers();
        bot.MoveTo(1, 1);

        ExecutorTick();
        Assert.False(StepOrderSlid(bot));
        Assert.Equal((0, 1), (bot.X, bot.Y));

        ExecutorTick();
        var slide = ExecutorAvatarSlide(bot);
        Assert.Equal((1, 1, 2, 1), (slide.FromX, slide.FromY, slide.ToX, slide.ToY));
    }

    [Fact]
    public void AUserThatWalksOntoARollerSlidesInTheTickItArrives()
    {
        ExecutorRoller(10, 1, 1);
        var user = ExecutorActor(0, 1);
        EnableExecutorRollers();
        user.MoveTo(1, 1);

        ExecutorTick();
        Assert.False(StepOrderSlid(user));

        ExecutorTick();
        var slide = ExecutorAvatarSlide(user);
        Assert.Equal((1, 1, 2, 1), (slide.FromX, slide.FromY, slide.ToX, slide.ToY));
    }

    [Fact]
    public void TheArrivalUpdateComesBeforeTheSlideInTheSameTick()
    {
        ExecutorRoller(10, 1, 1);
        var user = ExecutorActor(0, 1);
        EnableExecutorRollers();
        user.MoveTo(1, 1);
        ExecutorTick();
        ExecutorTick();

        var headers = _client.Packets.Select(packet => packet.Header).ToList();
        var update = headers.LastIndexOf(ServerPacketHeader.UserUpdateComposer);
        var slide = headers.IndexOf(ServerPacketHeader.SlideObjectBundleComposer);
        Assert.True(slide >= 0 && update >= 0 && update < slide, $"update at {update}, slide at {slide}");
    }

    [Fact]
    public void ARolledUserGetsUpdatesOneAndTwoTicksAfterTheSlide()
    {
        ExecutorRoller(10, 0, 1);
        var user = ExecutorRollerActor(0, 1, 0.5);
        EnableExecutorRollers();
        ExecutorTick();
        Assert.True(StepOrderSlid(user));
        Assert.False(ExecutorHasUpdate(user));

        ExecutorTick();
        Assert.True(ExecutorHasUpdate(user));
        Assert.False(StepOrderSlid(user));

        ExecutorTick();
        Assert.True(ExecutorHasUpdate(user));
    }

    [Fact]
    public void ACancelledStepStillLandsWhenCargoUsedItsTile()
    {
        ExecutorRoller(10, 0, 1);
        var cargo = ExecutorFloor(12, 0, 1, z: 0.5, height: 0.1);
        StepOrderViewerOffTheRoller();
        var bot = StepOrderBot(1, 1);
        EnableExecutorRollers();
        ExecutorTick();
        ExecutorTick();
        bot.MoveTo(3, 1);
        ExecutorTick();
        Assert.Equal((1, 1), (cargo.GetX, cargo.GetY));

        bot.ClearMovement(true);
        ExecutorTick();
        ExecutorTick();

        Assert.Equal((2, 1), (bot.X, bot.Y));
        Assert.Equal((1, 1), (cargo.GetX, cargo.GetY));
        Assert.False(bot.IsWalking);
    }

    [Fact]
    public void ACancelledStepStillLandsWhenARiderUsedItsTile()
    {
        ExecutorRoller(10, 0, 1);
        var rider = ExecutorRollerActor(0, 1, 0.5);
        var bot = StepOrderBot(1, 1);
        EnableExecutorRollers();
        ExecutorTick();
        ExecutorTick();
        bot.MoveTo(3, 1);
        ExecutorTick();
        Assert.Equal((1, 1), (rider.X, rider.Y));

        bot.ClearMovement(true);
        ExecutorTick();
        ExecutorTick();

        Assert.Equal((2, 1), (bot.X, bot.Y));
        Assert.Equal((1, 1), (rider.X, rider.Y));
    }

    [Fact]
    public void ASolidFurniOnTheStepTargetDoesNotKeepTheUnitUnderARider()
    {
        ExecutorRoller(10, 0, 1);
        var rider = ExecutorRollerActor(0, 1, 0.5);
        var bot = StepOrderBot(1, 1);
        EnableExecutorRollers();
        ExecutorTick();
        ExecutorTick();
        bot.MoveTo(2, 1);
        ExecutorTick();
        Assert.Equal((1, 1), (rider.X, rider.Y));

        StepOrderSolid(30, 2, 1);
        ExecutorTick();
        ExecutorTick();

        Assert.NotEqual((rider.X, rider.Y), (bot.X, bot.Y));
    }

    [Fact]
    public void ASolidFurniOnTheStepTargetDoesNotKeepTheUnitUnderCargo()
    {
        ExecutorRoller(10, 0, 1);
        var cargo = ExecutorFloor(12, 0, 1, z: 0.5, height: 0.1);
        StepOrderViewerOffTheRoller();
        var bot = StepOrderBot(1, 1);
        EnableExecutorRollers();
        ExecutorTick();
        ExecutorTick();
        bot.MoveTo(2, 1);
        ExecutorTick();
        Assert.Equal((1, 1), (cargo.GetX, cargo.GetY));

        StepOrderSolid(30, 2, 1);
        ExecutorTick();
        ExecutorTick();

        Assert.NotEqual((cargo.GetX, cargo.GetY), (bot.X, bot.Y));
    }
}
