using Plus.HabboHotel.Items;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// A badge display sends its state, badge code, owner name and date as a string array, just like Habbo.
// The database keeps the badge code, owner name and date separated by tabs.
public sealed class BadgeDisplayDataTests
{
    private const string Stored = "ACH_RecycledItems1\tmorfeu\t25-11-2026";

    private static readonly ItemDefinition Display = new()
    {
        Type = ItemType.Floor,
        ItemName = "badge_display",
        PublicName = "badge_display",
        InteractionName = "badge_display",
        InteractionType = InteractionType.BadgeDisplay,
        Width = 1,
        Length = 1,
        VendingIds = [],
        AdjustableHeights = []
    };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ABadgeDisplaySendsStateBadgeOwnerAndDate(bool keepLegacy)
    {
        var data = FurnitureDataSnapshot.Capture(FurniExtraData.Load(Display, Stored, keepLegacy));

        var strings = Assert.IsType<FurnitureDataSnapshot.Strings>(data);
        Assert.Equal(["0", "ACH_RecycledItems1", "morfeu", "25-11-2026"], strings.Values.ToArray());
    }

    [Fact]
    public void ABadgeDisplayIsSavedAsItWasStored()
    {
        Assert.Equal(Stored, FurniExtraData.Load(Display, Stored, keepLegacy: true).Serialize());
    }
}
