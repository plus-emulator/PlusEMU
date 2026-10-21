using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Observed on AIR WIN63-202610201032-107964540: a Wired toggle moves the controller to the next group (0, 3, 6, 9, 0) but the rollers
    // keep the step time they had. Only a use changes the step time.
    [Fact]
    public void AWiredToggleOfTheSpeedControllerChangesItsStateButNotTheRollerStepTime()
    {
        var item = PlaceSpeedController("0");

        foreach (var state in new[] { "3", "6", "9", "0" }) {
            item.Interactor.OnWiredTrigger(item);

            Assert.Equal(state, item.LegacyDataString);
            Assert.Equal(500, ConfigurationItemStates.RollerStepMilliseconds(_room));
            Assert.Equal(1, _room.GetRoomItemHandler().RollerStepTicks);
        }
    }

    [Fact]
    public void AUseOfTheSpeedControllerChangesTheRollerStepTimeAfterAWiredToggle()
    {
        var item = PlaceSpeedController("0");
        item.Interactor.OnWiredTrigger(item);

        item.Interactor.OnTrigger(_client, item, 0, true);

        Assert.Equal("6", item.LegacyDataString);
        Assert.Equal(1500, ConfigurationItemStates.RollerStepMilliseconds(_room));
    }
}
