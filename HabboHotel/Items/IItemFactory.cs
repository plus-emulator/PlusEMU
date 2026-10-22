using System.Data;
using Plus.HabboHotel.Users;

namespace Plus.HabboHotel.Items;

public interface IItemFactory
{
    Item CreateSingleItemNullable(ItemDefinition definition, Habbo habbo, string extraData, string displayFlags, int groupId = 0, uint limitedNumber = 0, uint limitedStack = 0);
    Item CreateSingleItem(ItemDefinition definition, Habbo habbo, string extraData, string displayFlags, uint itemId, uint limitedNumber = 0, uint limitedStack = 0);
    Item CreateGiftItem(ItemDefinition definition, Habbo habbo, string extraData, string displayFlags, int itemId, uint limitedNumber = 0, uint limitedStack = 0);
    List<Item> CreateMultipleItems(ItemDefinition definition, Habbo habbo, string extraData, int amount, int groupId = 0);
    List<Item> CreateMultipleItems(ItemDefinition definition, int ownerId, string extraData, int amount, int groupId = 0);
    List<Item> CreateTeleporterItems(ItemDefinition definition, Habbo habbo, int groupId = 0);
    void CreateMoodlightData(Item item);
    void CreateTonerData(Item item);

    // The overloads below write inside the caller's transaction, so the items commit or roll back with it.
    Item CreateSingleItemNullable(IDbConnection connection, IDbTransaction transaction, ItemDefinition definition, Habbo habbo, string extraData, int groupId = 0,
        uint limitedNumber = 0, uint limitedStack = 0);
    List<Item> CreateMultipleItems(IDbConnection connection, IDbTransaction transaction, ItemDefinition definition, int ownerId, string extraData, int amount, int groupId = 0);
    List<Item> CreateTeleporterItems(IDbConnection connection, IDbTransaction transaction, ItemDefinition definition, Habbo habbo, int groupId = 0);
    void CreateMoodlightData(IDbConnection connection, IDbTransaction transaction, Item item);
    void CreateTonerData(IDbConnection connection, IDbTransaction transaction, Item item);
}
