namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>Builders Club templates admitted by Place Temporary Furni.</summary>
public static class WiredPlaceTemplatePolicy
{
    public const string NotCloneableKey = "wiredfurni.error.furni_can_not_be_cloned";

    // Observed on AIR WIN63-202610151745-318700621: Builders Club templates save; an owned Pura item is refused.
    // Trade flags describe the catalogue definition, not whether this room instance came from Builders Club.
    public static bool CanClone(Item item) => item.IsBuildersClub && !item.IsTemporary && item.UniqueNumber == 0
        && !IsWiredBox(item.Definition)
        && item.Definition.InteractionType is not (InteractionType.Exchange or InteractionType.Gift or InteractionType.WiredChestFurni
            or InteractionType.WiredChestCoins or InteractionType.Badge or InteractionType.PurchasableClothing
            or InteractionType.Deal or InteractionType.Roomdeal);

    /// <summary>Any Wired box: triggers, effects, conditions, selectors, add-ons and variable boxes (as <see cref="Item.IsWired"/>).</summary>
    public static bool IsWiredBox(ItemDefinition definition) => definition.WiredDescriptor != null
        || definition.InteractionType is InteractionType.WiredSelector or InteractionType.WiredAddon or InteractionType.WiredVariable
            or InteractionType.WiredEffect or InteractionType.WiredTrigger or InteractionType.WiredCondition;

    /// <summary>The save refusal for templates that cannot be cloned; null when every template still in the room can be.</summary>
    public static string? RefusalKey(Rooms.Room room, IEnumerable<Configuration.WiredFurniSnapshot> templates) => templates
        .Select(snapshot => room.GetRoomItemHandler().GetItem(snapshot.ItemId))
        .Any(item => item != null && !CanClone(item)) ? NotCloneableKey : null;
}
