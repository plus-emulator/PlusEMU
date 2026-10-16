namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// Which furniture wf_act_place_furni may use as a template. Kept in one place so later probes can refine it.
/// <para>Observed (AIR WIN63-202610131318-860371703): r93a saving an owned, tradeable pura_mdl5 as the template was refused with
/// WiredValidationError <c>wiredfurni.error.furni_can_not_be_cloned</c> and no parameters (runtime receipts line 330, editor frame
/// line 282). r80 accepted Builders Club loans, which PlusEMU does not have.</para>
/// <para>Inferred, not yet observed: every owned item that can carry value is refused the same way: tradeable, marketplace,
/// recyclable, rare or limited items, and furniture whose interaction pays out or holds value (credit exchange, gifts, wired
/// chests, badges, purchasable clothing, deals). Other owned furniture and wired clones are accepted.</para>
/// </summary>
public static class WiredPlaceTemplatePolicy
{
    public const string NotCloneableKey = "wiredfurni.error.furni_can_not_be_cloned";

    /// <summary>A definition whose copies could carry value if they ever left the room.</summary>
    public static bool CarriesValue(ItemDefinition definition) => definition.AllowTrade || definition.AllowMarketplaceSell
        || definition.AllowEcotronRecycle || definition.IsRare
        || definition.InteractionType is InteractionType.Exchange or InteractionType.Gift or InteractionType.WiredChestFurni
            or InteractionType.WiredChestCoins or InteractionType.Badge or InteractionType.PurchasableClothing
            or InteractionType.Deal or InteractionType.Roomdeal;

    /// <summary>
    /// Wired boxes are never templates (observed on hhus, AIR WIN63-202610131318-860371703: saving wf_act_toggle_state as the template
    /// was refused with <c>wiredfurni.error.furni_can_not_be_cloned</c>). Gates, one-way gates and rollers can be.
    /// </summary>
    public static bool CanClone(Item item) => !IsWiredBox(item.Definition) && (item.IsTemporary || item.UniqueNumber == 0 && !CarriesValue(item.Definition));

    /// <summary>Any Wired box: triggers, effects, conditions, selectors, add-ons and variable boxes (as <see cref="Item.IsWired"/>).</summary>
    public static bool IsWiredBox(ItemDefinition definition) => definition.WiredDescriptor != null
        || definition.InteractionType is InteractionType.WiredSelector or InteractionType.WiredAddon or InteractionType.WiredVariable
            or InteractionType.WiredEffect or InteractionType.WiredTrigger or InteractionType.WiredCondition;

    /// <summary>The save refusal for templates that cannot be cloned; null when every template still in the room can be.</summary>
    public static string? RefusalKey(Rooms.Room room, IEnumerable<Configuration.WiredFurniSnapshot> templates) => templates
        .Select(snapshot => room.GetRoomItemHandler().GetItem(snapshot.ItemId))
        .Any(item => item != null && !CanClone(item)) ? NotCloneableKey : null;
}
