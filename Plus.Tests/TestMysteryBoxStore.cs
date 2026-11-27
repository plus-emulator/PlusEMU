using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.MysteryBoxes;
using Plus.HabboHotel.Users.Inventory.Furniture;

namespace Plus.Tests;

internal sealed class TestMysteryBoxStore : IMysteryBoxStore
{
    public const uint OwnerPrizeId = 900;
    public const uint GuestPrizeId = 901;

    public Dictionary<int, MysteryBoxColors> Users { get; } = [];
    public List<MysteryBoxPrize> PrizeTable { get; } = [];
    public List<MysteryBoxOpening> Opened { get; } = [];

    public MysteryBoxColors Colors(int userId) => Users.GetValueOrDefault(userId, MysteryBoxColors.None);

    public IReadOnlyList<MysteryBoxPrize> Prizes() => PrizeTable;

    public MysteryBoxOpened? Open(MysteryBoxOpening opening)
    {
        if (Colors(opening.GuestId).Key != opening.KeyColor) {
            return null;
        }

        Opened.Add(opening);
        Users[opening.GuestId] = Colors(opening.GuestId) with { Key = "" };

        return new(Prize(OwnerPrizeId, opening.OwnerId, opening.OwnerPrize), Prize(GuestPrizeId, opening.GuestId, opening.GuestPrize));
    }

    private static InventoryItem Prize(uint id, int ownerId, ItemDefinition definition) => new()
    {
        Id = id,
        OwnerId = (uint)ownerId,
        Definition = definition,
        ExtraData = FurniExtraData.Load(definition, "", keepLegacy: true)
    };
}
