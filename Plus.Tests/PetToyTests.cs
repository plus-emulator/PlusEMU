using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Types;
using Xunit;

namespace Plus.Tests;

// Pets play with toys like on Habbo: now and then a pet walks onto a toy by itself and plays with it.
// The pet shows "pla" (a horse kicks with "kck", a frog dips in the pond) and a smile, says a playing line,
// and the toy shows state 1 for three and a half seconds. The pond is a toy only for the pets in its custom params.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData("pet_toy_ball", "", 0, true)]
    [InlineData("toy1", "", 1, true)]
    [InlineData("toy1*3", "", 28, true)]
    [InlineData("toy2", "", 8, true)]
    [InlineData("pet_ufo_toy", "", 17, true)]
    [InlineData("pet_puppy_toy", "", 29, true)]
    [InlineData("pond", "11,9", 11, true)]
    [InlineData("pond", "11,9", 9, true)]
    [InlineData("pond", "11,9", 0, false)]
    [InlineData("yarn", "6,1,0.5,0.5", 1, false)]
    [InlineData("nest_plow_red", "", 0, false)]
    [InlineData("waterbowl*1", "", 0, false)]
    public void ToysAreForEveryPetAndThePondOnlyForItsPets(string name, string customParams, int petType, bool toy)
    {
        Assert.Equal(toy, PetToy.Suits(new ItemDefinition { ItemName = name, CustomParams = customParams }, petType));
    }

    [Theory]
    [InlineData(29, "pet_toy_ball", "", "pla")]
    [InlineData(15, "pet_toy_ball", "", "kck")]
    [InlineData(0, "toy1", "", "pla")]
    [InlineData(28, "pet_ufo_toy", "", "pla")]
    [InlineData(11, "pond", "11,9", "dip")]
    public void APetPlaysWithAToyByItself(int type, string name, string customParams, string posture)
    {
        LegacyRider();
        var pet = CommandPet(type, 17_600, 420);
        var petUser = PetUser(pet);
        var ball = Ball(95, 3, 3, name, customParams);
        var ai = CommandBot(petUser, -1);
        _client.Packets.Clear();

        PlayUntilTheBallMoves(ai, petUser, ball);

        Assert.Equal("1", ball.LegacyDataString);
        Assert.Contains(posture, petUser.Statusses.Keys);
        Assert.Equal("sml", petUser.Statusses.GetValueOrDefault("gst"));
        Assert.Contains(ChatLines().Last(), PetVocals.For(type, PetVocal.Playing));
    }

    [Theory]
    [InlineData(11, "Anyone up for a game of croak-ay?")]
    [InlineData(1, "*playing*")]
    [InlineData(28, "This is pawesome!")]
    [InlineData(8, "Joy is mine!")]
    public void PetsSayTheSharedPlayingLinesAndTheirOwn(int type, string own)
    {
        var lines = PetVocals.For(type, PetVocal.Playing);

        Assert.Contains("I'm having a blast!", lines);
        Assert.Contains("I can't stop playing!", lines);
        Assert.Contains(own, lines);
    }

    [Fact]
    public void TheBallRestsAgainAfterThreeAndAHalfSeconds()
    {
        LegacyRider();
        var pet = CommandPet(29, 17_600, 420);
        var petUser = PetUser(pet);
        var ball = Ball(95, 3, 3);
        var ai = CommandBot(petUser, -1);

        PlayUntilTheBallMoves(ai, petUser, ball);

        for (var tick = 0; tick < 6; tick++) {
            _room.GetRoomItemHandler().OnCycle();
        }

        Assert.Equal("1", ball.LegacyDataString);
        _room.GetRoomItemHandler().OnCycle();
        Assert.Equal("0", ball.LegacyDataString);
    }

    [Fact]
    public void AnotherPlayRestartsTheBallTimer()
    {
        var handler = _room.GetRoomItemHandler();
        var ball = Ball(95, 3, 3);
        handler.PlayPetBall(ball);
        handler.OnCycle();
        handler.PlayPetBall(ball);

        for (var tick = 0; tick < 6; tick++) {
            handler.OnCycle();
        }

        Assert.Equal("1", ball.LegacyDataString);
        handler.OnCycle();
        Assert.Equal("0", ball.LegacyDataString);
    }

    [Fact]
    public void RemovingTheBallCancelsItsReset()
    {
        var handler = _room.GetRoomItemHandler();
        var ball = Ball(95, 3, 3);
        handler.PlayPetBall(ball);
        Assert.True(handler.DeleteFurniture(ball));

        for (var tick = 0; tick < 7; tick++) {
            handler.OnCycle();
        }

        Assert.Equal("1", ball.LegacyDataString);
    }

    [Fact]
    public void IdlePetTicksAllocateNoMemory()
    {
        var user = PetUser(CommandPet(15, 17_600, 420));
        var bytes = 0L;

        for (var sample = 0; sample < 128; sample++) {
            var ai = CommandBot(user, -1);
            ai.OnTimerTick();
            var before = GC.GetAllocatedBytesForCurrentThread();

            for (var tick = 0; tick < 8; tick++) {
                ai.OnTimerTick();
            }

            bytes += GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Assert.Equal(0, bytes);
    }

    private static void PlayUntilTheBallMoves(PetBot ai, RoomUser petUser, Item ball)
    {
        for (var tick = 0; tick < 6000 && ball.LegacyDataString != "1"; tick++) {
            ai.OnTimerTick();

            if (WalkTarget(petUser) == (3, 3) && petUser.Coordinate != new System.Drawing.Point(3, 3)) {
                ArriveAt(ai, petUser, 3, 3);
            }
        }
    }

    private Item Ball(uint id, int x, int y, string name = "pet_toy_ball", string customParams = "")
    {
        var ball = Furni(id, InteractionType.None, WiredBoxType.None);
        ball.Definition.ItemName = name;
        ball.Definition.CustomParams = customParams;
        ball.Definition.FurniLine = "pet_accessories";
        ball.Definition.Walkable = true;
        ball.ExtraData = new LegacyDataFormat { Data = "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, ball, x, y, 0, true, false, false));

        return ball;
    }
}
