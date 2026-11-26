using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.Utilities.DependencyInjection;

namespace Plus.HabboHotel.Items.MysteryBoxes;

/// <summary>The colour of a user's own box and of the key they hold. An empty colour means none.</summary>
public sealed record MysteryBoxColors(string Box, string Key)
{
    public static readonly MysteryBoxColors None = new("", "");
}

public sealed record MysteryBoxPrize(ItemDefinition Definition, int Weight);

/// <summary>Opens a box: the box gets its opened state, the guest's key is used up and both users get their prize.</summary>
public sealed record MysteryBoxOpening(uint BoxItemId, string OpenedState, int OwnerId, int GuestId, string KeyColor,
    ItemDefinition OwnerPrize, ItemDefinition GuestPrize);

public sealed record MysteryBoxOpened(InventoryItem OwnerPrize, InventoryItem GuestPrize);

public interface IMysteryBoxStore
{
    MysteryBoxColors Colors(int userId);

    IReadOnlyList<MysteryBoxPrize> Prizes();

    /// <summary>Returns null when the guest no longer holds that key.</summary>
    MysteryBoxOpened? Open(MysteryBoxOpening opening);
}

[Singleton]
public interface IMysteryBoxService
{
    /// <summary>Uses the room item when it is a mystery box. Returns false when the item is not a mystery box.</summary>
    bool TryUse(Room room, GameClient session, uint itemId);

    void CancelWait(Room room, GameClient session, int boxOwnerId);

    /// <summary>Sends the user's box and key colours when they have either.</summary>
    void SendKeys(GameClient session);
}

public sealed class MysteryBoxService(IMysteryBoxStore store) : IMysteryBoxService
{
    public bool TryUse(Room room, GameClient session, uint itemId) => throw new NotImplementedException();

    public void CancelWait(Room room, GameClient session, int boxOwnerId) => throw new NotImplementedException();

    public void SendKeys(GameClient session) => throw new NotImplementedException();
}
