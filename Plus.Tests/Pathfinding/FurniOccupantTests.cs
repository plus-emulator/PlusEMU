using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540: a furni that moves or rotates never carries the avatar that sits or lies on it.
// The avatar keeps its tile. It keeps its posture only while the new footprint still covers that tile, and a furni that
// lands on a standing avatar makes it sit or lie at once. The change reaches the room on the next room tick.
public partial class PlacedFurniRoomTests
{
    // A 2x3 bed at (1,1): rotation 0 covers x1-2 and y1-3, rotation 2 covers x1-3 and y1-2.
    private Item OccupantBed(int x, int y, int rotation = 0)
    {
        var bed = Furni(10, InteractionType.Bed, WiredBoxType.None);
        bed.Definition.Length = 3;
        bed.Definition.Width = 2;
        bed.Definition.Height = 1.8;
        bed.Definition.Stackable = false;
        bed.Definition.Walkable = false;
        bed.Definition.Directions = [0, 2];
        bed.Definition.DefaultDirection = 0;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, bed, x, y, rotation, true, false, false));

        return bed;
    }

    // A 2x1 sofa: rotation 4 covers (x,y) and (x+1,y), rotation 2 covers (x,y) and (x,y+1).
    private Item OccupantSofa(int x, int y, int rotation)
    {
        var sofa = Furni(11, InteractionType.None, WiredBoxType.None);
        sofa.Definition.Width = 2;
        sofa.Definition.Length = 1;
        sofa.Definition.Height = 1;
        sofa.Definition.IsSeat = true;
        sofa.Definition.Stackable = false;
        sofa.Definition.Walkable = false;
        sofa.Definition.Directions = [2, 4];
        sofa.Definition.DefaultDirection = 2;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, sofa, x, y, rotation, true, false, false));

        return sofa;
    }

    private Item OccupantChair(int x, int y, int rotation)
    {
        var chair = Furni(12, InteractionType.None, WiredBoxType.None);
        chair.Definition.Height = 1;
        chair.Definition.IsSeat = true;
        chair.Definition.Stackable = false;
        chair.Definition.Walkable = false;
        chair.Definition.Directions = [0, 2, 4, 6];
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, chair, x, y, rotation, true, false, false));

        return chair;
    }

    private RoomUser OccupantActor(int x, int y, bool layered) => layered ? LayeredActor(x, y) : ExecutorActor(x, y);

    private void OccupantMove(Item item, int x, int y, int rotation)
    {
        PlacementService(() => { }).Move(_room, _client, item.Id, x, y, rotation);
        ExecutorTick();
    }

    private int OccupantUpdates() => _client.Packets.Count(packet => packet.Header == ServerPacketHeader.UserUpdateComposer);

    private static void AssertStanding(RoomUser actor, int x, int y, int rotation)
    {
        Assert.Equal((x, y), (actor.X, actor.Y));
        Assert.False(actor.HasStatus("lay"));
        Assert.False(actor.HasStatus("sit"));
        Assert.Equal((rotation, rotation), (actor.RotHead, actor.RotBody));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingABedOffItsSleeperStandsHimUpOnTheSameTile(bool layered)
    {
        var bed = OccupantBed(1, 1);
        var actor = OccupantActor(1, 3, layered);
        Assert.True(actor.HasStatus("lay"));

        // The bed now covers y0-2, so the sleeper's tile (1,3) is free.
        OccupantMove(bed, 1, 0, 0);

        AssertStanding(actor, 1, 3, 0);
        Assert.Equal(1, OccupantUpdates());
        Assert.DoesNotContain("/lay", ExecutorUpdate(actor).Status);
        Assert.False(actor.IsWalking);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingABedThatStillCoversTheSleepersTileSendsNoUserUpdate(bool layered)
    {
        var bed = OccupantBed(1, 1);
        var actor = OccupantActor(2, 2, layered);
        Assert.True(actor.HasStatus("lay"));

        // The bed now covers x2-3 and y1-3. The sleeper's tile (2,2) is still under it.
        OccupantMove(bed, 2, 1, 0);

        Assert.Equal((2, 2), (actor.X, actor.Y));
        Assert.Equal("1.8 null", actor.Statusses["lay"]);
        Assert.Equal(0, OccupantUpdates());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RotatingABedOffTheSleepersLastRowStandsHimUpWithTheOldDirection(bool layered)
    {
        var bed = OccupantBed(0, 0);
        var actor = OccupantActor(2, 3, layered);
        Assert.False(actor.HasStatus("lay"));

        // Landing on a standing avatar: the far row of the bed puts him down at once.
        OccupantMove(bed, 1, 1, 0);
        Assert.True(actor.HasStatus("lay"));

        // Rotation 2 covers x1-3 and y1-2, so (2,3) is no longer under the bed.
        OccupantMove(bed, 1, 1, 2);

        AssertStanding(actor, 2, 3, 0);
        Assert.Equal(1, OccupantUpdates());
        Assert.DoesNotContain("/lay", ExecutorUpdate(actor).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AMovedBedLandingOnAStandingAvatarLaysHimDownOnItsFarRow(bool layered)
    {
        var bed = OccupantBed(0, 0);
        var actor = OccupantActor(2, 3, layered);

        OccupantMove(bed, 1, 1, 0);

        Assert.Equal((2, 3), (actor.X, actor.Y));
        Assert.Equal("1.8 null", actor.Statusses["lay"]);
        Assert.Equal(1, OccupantUpdates());
        Assert.Contains("/lay 1.8 null/", ExecutorUpdate(actor).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ABedRotatedOntoAStandingAvatarLaysHimDownFacingTheBed(bool layered)
    {
        var bed = OccupantBed(1, 1);
        var actor = OccupantActor(3, 1, layered);
        Assert.False(actor.HasStatus("lay"));

        // Rotation 2 covers x1-3 and y1-2, which takes in (3,1).
        OccupantMove(bed, 1, 1, 2);

        Assert.Equal((3, 1), (actor.X, actor.Y));
        Assert.Equal("1.8 null", actor.Statusses["lay"]);
        Assert.Equal((2, 2), (actor.RotHead, actor.RotBody));
        Assert.Contains("/lay 1.8 null/", ExecutorUpdate(actor).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingASofaOffItsSecondTileSitterStandsHimUp(bool layered)
    {
        var sofa = OccupantSofa(1, 1, 4);
        var actor = OccupantActor(2, 1, layered);
        Assert.True(actor.HasStatus("sit"));

        OccupantMove(sofa, 0, 1, 4);

        AssertStanding(actor, 2, 1, 4);
        Assert.Equal(1, OccupantUpdates());
        Assert.DoesNotContain("sit", ExecutorUpdate(actor).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingASofaThatStillCoversTheSitterSendsNoUserUpdate(bool layered)
    {
        var sofa = OccupantSofa(1, 1, 4);
        var actor = OccupantActor(2, 1, layered);

        OccupantMove(sofa, 2, 1, 4);

        Assert.Equal((2, 1), (actor.X, actor.Y));
        Assert.True(actor.HasStatus("sit"));
        Assert.Equal(0, OccupantUpdates());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RotatingASofaTurnsItsSitterWhenHisTileStaysCovered(bool layered)
    {
        var sofa = OccupantSofa(1, 1, 4);
        var actor = OccupantActor(1, 1, layered);
        Assert.Equal(4, actor.RotBody);

        OccupantMove(sofa, 1, 1, 2);

        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.True(actor.HasStatus("sit"));
        Assert.Equal((2, 2), (actor.RotHead, actor.RotBody));
        Assert.Equal(1, OccupantUpdates());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RotatingASofaOffItsSecondTileSitterStandsHimUp(bool layered)
    {
        var sofa = OccupantSofa(1, 1, 4);
        var actor = OccupantActor(2, 1, layered);

        // Rotation 2 covers (1,1) and (1,2), so (2,1) is free.
        OccupantMove(sofa, 1, 1, 2);

        AssertStanding(actor, 2, 1, 4);
        Assert.Equal(1, OccupantUpdates());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RotatingAChairTurnsItsSitter(bool layered)
    {
        var chair = OccupantChair(1, 1, 0);
        var actor = OccupantActor(1, 1, layered);

        OccupantMove(chair, 1, 1, 4);

        Assert.True(actor.HasStatus("sit"));
        Assert.Equal((4, 4), (actor.RotHead, actor.RotBody));
        Assert.Equal(1, OccupantUpdates());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MovingAChairAwayStandsItsSitterUpOnTheSameTile(bool layered)
    {
        var chair = OccupantChair(1, 1, 2);
        var actor = OccupantActor(1, 1, layered);

        OccupantMove(chair, 3, 3, 2);

        AssertStanding(actor, 1, 1, 2);
        Assert.Equal(1, OccupantUpdates());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AChairMovedOntoAStandingAvatarSeatsHimAtOnce(bool layered)
    {
        var chair = OccupantChair(3, 3, 6);
        var actor = OccupantActor(1, 1, layered);

        OccupantMove(chair, 1, 1, 6);

        Assert.Equal((1, 1), (actor.X, actor.Y));
        Assert.True(actor.HasStatus("sit"));
        Assert.Equal((6, 6), (actor.RotHead, actor.RotBody));
        Assert.Equal(1, OccupantUpdates());
    }

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void ABedTurnedToADirectionItLacksTakesItsDefaultDirection(int requested)
    {
        var bed = OccupantBed(1, 1, 2);

        PlacementService(() => { }).Move(_room, _client, bed.Id, 1, 1, requested);

        Assert.Equal(0, bed.Rotation);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void ASofaTurnedToADirectionItLacksTakesItsDefaultDirection(int requested)
    {
        var sofa = OccupantSofa(1, 1, 4);

        PlacementService(() => { }).Move(_room, _client, sofa.Id, 1, 1, requested);

        Assert.Equal(2, sofa.Rotation);
    }
}
