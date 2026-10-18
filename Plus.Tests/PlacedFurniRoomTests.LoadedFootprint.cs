using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A loaded double bed covers its whole footprint, so both lanes lie down without the bed being moved first.
    [Fact]
    public void AFurniLoadedFromTheDatabaseCoversItsWholeFootprint()
    {
        var bed = Furni(304, InteractionType.Bed, WiredBoxType.None);
        bed.RoomId = RoomId;
        bed.Definition.Width = 2;
        bed.Definition.Length = 3;
        bed.GetX = 2;
        bed.GetY = 2;
        _room.GetRoomItemHandler().LoadFurniture([bed]);

        Assert.Contains(new System.Drawing.Point(3, 4), bed.GetCoords);
        Assert.Equal(6, bed.GetCoords.Distinct().Count());
    }
}
