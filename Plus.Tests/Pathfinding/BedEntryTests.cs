using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Fact]
    public void DoubleBedClickSelectsTheRequestedSide()
    {
        var bed = Furni(10, InteractionType.Bed, WiredBoxType.None);
        bed.Definition.Length = 3;
        bed.Definition.Width = 2;
        bed.Definition.Height = 1.8;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, bed, 1, 1, 0, true, false, false));
        var actor = ExecutorActor(1, 0);

        actor.MoveTo(2, 1);

        for (var tick = 0; tick < 10; tick++) {
            ExecutorTick();
        }

        Assert.Equal((2, 1), (actor.X, actor.Y));
        Assert.Equal("1.8 null", actor.Statusses["lay"]);
        Assert.False(actor.IsWalking);
    }

    [Theory]
    [InlineData(0, 0, 1, 1, 1)]
    [InlineData(0, 3, 1, 2, 1)]
    [InlineData(2, 1, 0, 1, 1)]
    [InlineData(2, 1, 3, 1, 2)]
    [InlineData(4, 0, 1, 1, 1)]
    [InlineData(4, 3, 1, 2, 1)]
    [InlineData(6, 1, 0, 1, 1)]
    [InlineData(6, 1, 3, 1, 2)]
    public void ExecutorDoubleBedEntryLiesOnEitherSide(int rotation, int startX, int startY, int pillowX, int pillowY)
    {
        var bed = Furni(10, InteractionType.Bed, WiredBoxType.None);
        bed.Definition.Length = 3;
        bed.Definition.Width = 2;
        bed.Definition.Height = 1.8;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, bed, 1, 1, rotation, true, false, false));
        var actor = ExecutorActor(startX, startY);
        actor.MoveTo(pillowX, pillowY);
        ExecutorTick();
        ExecutorTick();
        Assert.Equal((pillowX, pillowY, 0d), (actor.X, actor.Y, actor.Z));
        Assert.Equal("1.8 null", actor.Statusses["lay"]);
        Assert.Equal(rotation, actor.RotBody);
        Assert.False(actor.IsWalking);
        Assert.Contains("/lay 1.8 null/", ExecutorUpdate(actor).Status);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(6)]
    public void ExecutorDoubleBedOccupantCanSwitchSidesByClickingTheOtherFoot(int rotation)
    {
        var bed = Furni(10, InteractionType.Bed, WiredBoxType.None);
        bed.Definition.Length = 3;
        bed.Definition.Width = 2;
        bed.Definition.Height = 1.8;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, bed, 1, 1, rotation, true, false, false));
        var actor = ExecutorActor(1, 1);
        Assert.True(actor.HasStatus("lay"));
        var across = rotation is 2 or 6;
        actor.MoveTo(across ? 3 : 2, across ? 2 : 3);
        ExecutorTick();
        ExecutorTick();
        Assert.Equal(across ? (1, 2) : (2, 1), (actor.X, actor.Y));
        Assert.Equal("1.8 null", actor.Statusses["lay"]);
        Assert.False(actor.IsWalking);
        Assert.Contains("/lay 1.8 null/", ExecutorUpdate(actor).Status);
    }
}
