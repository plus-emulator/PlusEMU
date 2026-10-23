using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // A colour tile changes state on every double-click, the two plain game tiles, the blob, the buttons and the plates never do.
    // The super blob changes too.
    [Theory]
    [InlineData("wf_colortile", 7, true)]
    [InlineData("wf_tile1", 10, false)]
    [InlineData("wf_blob", 2, false)]
    [InlineData("wf_blob2", 2, true)]
    [InlineData("wf_button", 2, false)]
    [InlineData("wf_pressureplate", 4, false)]
    public void ADoubleClickChangesTheStateOfOnlyTheFurniThatCanBeUsed(string name, int modes, bool changes)
    {
        var f = new Fixture([(41, 8, 10, 0.0, 0, name, null)], live: true);
        f.Room.OwnerId = 1;
        f.Room.OwnerName = "user1";
        f.Room.Type = "private";
        f.Items[41].Definition.Modes = modes;
        f.Items[41].ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        typeof(Item).GetField("_interactors", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.SetValue(f.Items[41], TestItemRuntime.Interactors);
        f.Items[41].BindInteractionClock(TimeProvider.System);
        var owner = f.Walker(1, 7, 10, 7, 11);
        f.Advance(1000);

        f.Owned(() => new FurnitureUseService(null!, TestItemRuntime.Quests).Use(f.Room, owner.GetClient()!, new(41, 0)));
        f.Advance(500);

        Assert.Equal(changes ? "1" : "0", f.Items[41].LegacyDataString);
    }
}
