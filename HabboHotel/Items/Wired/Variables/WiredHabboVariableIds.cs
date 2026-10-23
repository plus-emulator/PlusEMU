namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// Habbo's fixed ids for the variables every room has: "-N" for a built-in (@) variable, "~N" for a smart (~) one.
/// The client sorts internal variables by Number(variableId). An id the client saved this way names the same built-in token as ours.
/// </summary>
public static class WiredHabboVariableIds
{
    private static readonly IReadOnlyDictionary<string, (WiredVariableTarget Target, string Name)> Ids =
        new Dictionary<string, (WiredVariableTarget, string)>(StringComparer.Ordinal)
        {
            ["-100"] = (WiredVariableTarget.Furni, "@id"),
            ["-101"] = (WiredVariableTarget.Furni, "@class_id"),
            ["-102"] = (WiredVariableTarget.Furni, "@height"),
            ["-110"] = (WiredVariableTarget.Furni, "@state"),
            ["-120"] = (WiredVariableTarget.Furni, "@position.x"),
            ["-121"] = (WiredVariableTarget.Furni, "@position.y"),
            ["-122"] = (WiredVariableTarget.Furni, "@rotation"),
            ["-123"] = (WiredVariableTarget.Furni, "@altitude"),
            ["-124"] = (WiredVariableTarget.Furni, "@is_invisible"),
            ["-125"] = (WiredVariableTarget.Furni, "@position"),
            ["-126"] = (WiredVariableTarget.Furni, "@occupation"),
            ["-129"] = (WiredVariableTarget.Furni, "@type"),
            ["-130"] = (WiredVariableTarget.Furni, "@is_stackable"),
            ["-131"] = (WiredVariableTarget.Furni, "@can_stand_on"),
            ["-132"] = (WiredVariableTarget.Furni, "@can_sit_on"),
            ["-133"] = (WiredVariableTarget.Furni, "@can_lay_on"),
            ["-140"] = (WiredVariableTarget.Furni, "@dimensions.x"),
            ["-141"] = (WiredVariableTarget.Furni, "@dimensions.y"),
            ["-180"] = (WiredVariableTarget.Furni, "@owner_id"),
            ["-190"] = (WiredVariableTarget.Furni, "@wallitem_offset"),
            ["-200"] = (WiredVariableTarget.User, "@index"),
            ["-205"] = (WiredVariableTarget.User, "@type"),
            ["-206"] = (WiredVariableTarget.User, "@gender"),
            ["-210"] = (WiredVariableTarget.User, "@level"),
            ["-211"] = (WiredVariableTarget.User, "@achievement_score"),
            ["-220"] = (WiredVariableTarget.User, "@is_hc"),
            ["-221"] = (WiredVariableTarget.User, "@has_rights"),
            ["-222"] = (WiredVariableTarget.User, "@is_group_admin"),
            ["-223"] = (WiredVariableTarget.User, "@is_owner"),
            ["-230"] = (WiredVariableTarget.User, "@position.x"),
            ["-231"] = (WiredVariableTarget.User, "@position.y"),
            ["-232"] = (WiredVariableTarget.User, "@direction"),
            ["-233"] = (WiredVariableTarget.User, "@altitude"),
            ["-234"] = (WiredVariableTarget.User, "@position"),
            ["-237"] = (WiredVariableTarget.User, "@team.score"),
            ["-238"] = (WiredVariableTarget.User, "@team.color"),
            ["-239"] = (WiredVariableTarget.User, "@team.type"),
            ["-240"] = (WiredVariableTarget.User, "@handitem"),
            ["-241"] = (WiredVariableTarget.User, "@effect"),
            ["-242"] = (WiredVariableTarget.User, "@is_frozen"),
            ["-243"] = (WiredVariableTarget.User, "@is_muted"),
            ["-244"] = (WiredVariableTarget.User, "@is_trading"),
            ["-245"] = (WiredVariableTarget.User, "@habbicon"),
            ["-250"] = (WiredVariableTarget.User, "@favourite_group_id"),
            ["-260"] = (WiredVariableTarget.User, "@dance"),
            ["-261"] = (WiredVariableTarget.User, "@sign"),
            ["-262"] = (WiredVariableTarget.User, "@is_idle"),
            ["-266"] = (WiredVariableTarget.User, "@room_entry.method"),
            ["-267"] = (WiredVariableTarget.User, "@room_entry.teleport_id"),
            ["-270"] = (WiredVariableTarget.User, "@user_id"),
            ["-271"] = (WiredVariableTarget.User, "@pet_id"),
            ["-272"] = (WiredVariableTarget.User, "@bot_id"),
            ["-280"] = (WiredVariableTarget.User, "@pet_owner_id"),
            ["-281"] = (WiredVariableTarget.User, "@transaction.in_trade"),
            ["-282"] = (WiredVariableTarget.User, "@transaction.start_time"),
            ["-283"] = (WiredVariableTarget.User, "@transaction.contract_id"),
            ["-284"] = (WiredVariableTarget.User, "@transaction.state"),
            ["-285"] = (WiredVariableTarget.User, "@transaction.can_accept"),
            ["-286"] = (WiredVariableTarget.User, "@transaction.current_multiplier"),
            ["-300"] = (WiredVariableTarget.Global, "@furni_count"),
            ["-301"] = (WiredVariableTarget.Global, "@user_count"),
            ["-310"] = (WiredVariableTarget.Global, "@wired_timer"),
            ["-320"] = (WiredVariableTarget.Global, "@teams.red.score"),
            ["-321"] = (WiredVariableTarget.Global, "@teams.green.score"),
            ["-322"] = (WiredVariableTarget.Global, "@teams.blue.score"),
            ["-323"] = (WiredVariableTarget.Global, "@teams.yellow.score"),
            ["-330"] = (WiredVariableTarget.Global, "@teams.red.size"),
            ["-331"] = (WiredVariableTarget.Global, "@teams.green.size"),
            ["-332"] = (WiredVariableTarget.Global, "@teams.blue.size"),
            ["-333"] = (WiredVariableTarget.Global, "@teams.yellow.size"),
            ["-380"] = (WiredVariableTarget.Global, "@room_id"),
            ["-381"] = (WiredVariableTarget.Global, "@group_id"),
            ["-400"] = (WiredVariableTarget.Context, "@selector_furni_count"),
            ["-401"] = (WiredVariableTarget.Context, "@selector_user_count"),
            ["-410"] = (WiredVariableTarget.Context, "@signal_furni_count"),
            ["-411"] = (WiredVariableTarget.Context, "@signal_user_count"),
            ["-420"] = (WiredVariableTarget.Context, "@event.signal.antenna_id"),
            ["-450"] = (WiredVariableTarget.Context, "@event.chat.type"),
            ["-451"] = (WiredVariableTarget.Context, "@event.chat.style"),
            ["-452"] = (WiredVariableTarget.Context, "@event.habbicon"),
            ["-453"] = (WiredVariableTarget.Context, "@event.link.source_room_id"),
            ["-459"] = (WiredVariableTarget.Context, "@event.variable_update.box_id"),
            ["-460"] = (WiredVariableTarget.Context, "@event.variable_update.change_type"),
            ["-461"] = (WiredVariableTarget.Context, "@event.variable_update.old_value"),
            ["-462"] = (WiredVariableTarget.Context, "@event.variable_update.new_value"),
            ["-463"] = (WiredVariableTarget.Context, "@event.variable_update.difference"),
            ["-464"] = (WiredVariableTarget.Context, "@event.variable_update.change_origin"),
            ["-470"] = (WiredVariableTarget.Context, "@event.transaction_complete.multiplier"),
            ["-471"] = (WiredVariableTarget.Context, "@event.transaction_complete.deposit.furni_count"),
            ["-472"] = (WiredVariableTarget.Context, "@event.transaction_complete.deposit.coins_count"),
            ["-473"] = (WiredVariableTarget.Context, "@event.transaction_complete.withdrawal.furni_count"),
            ["-474"] = (WiredVariableTarget.Context, "@event.transaction_complete.withdrawal.coins_count"),
            ["-478"] = (WiredVariableTarget.Context, "@event.transaction_failed.reason"),
            ["-1000"] = (WiredVariableTarget.Global, "@current_time"),
            ["-1001"] = (WiredVariableTarget.Global, "@current_time.milliseconds_of_seconds"),
            ["-1002"] = (WiredVariableTarget.Global, "@current_time.seconds_of_minute"),
            ["-1003"] = (WiredVariableTarget.Global, "@current_time.minute_of_hour"),
            ["-1004"] = (WiredVariableTarget.Global, "@current_time.hour_of_day"),
            ["-1005"] = (WiredVariableTarget.Global, "@current_time.day_of_week"),
            ["-1006"] = (WiredVariableTarget.Global, "@current_time.day_of_month"),
            ["-1007"] = (WiredVariableTarget.Global, "@current_time.day_of_year"),
            ["-1008"] = (WiredVariableTarget.Global, "@current_time.week_of_year"),
            ["-1009"] = (WiredVariableTarget.Global, "@current_time.month_of_year"),
            ["-1010"] = (WiredVariableTarget.Global, "@current_time.year"),
            ["-2000"] = (WiredVariableTarget.Furni, "@can_move_freely"),
            ["-2100"] = (WiredVariableTarget.Furni, "@projectile.animation.tiles_travelled"),
            ["-2101"] = (WiredVariableTarget.Furni, "@projectile.animation.user_collisions"),
            ["-2102"] = (WiredVariableTarget.Furni, "@projectile.animation.furni_collisions"),
            ["-2103"] = (WiredVariableTarget.Furni, "@projectile.animation.position.x"),
            ["-2104"] = (WiredVariableTarget.Furni, "@projectile.animation.position.y"),
            ["-2105"] = (WiredVariableTarget.Furni, "@projectile.animation.position.altitude"),
            ["-2106"] = (WiredVariableTarget.Furni, "@projectile.animation.is_travelling"),
            ["~115"] = (WiredVariableTarget.Furni, "~area_hide.hiding_wallitems"),
            ["~116"] = (WiredVariableTarget.Furni, "~area_hide.inverted"),
            ["~114"] = (WiredVariableTarget.Furni, "~area_hide.is_invisible_furni"),
            ["~113"] = (WiredVariableTarget.Furni, "~area_hide.length"),
            ["~110"] = (WiredVariableTarget.Furni, "~area_hide.root_x"),
            ["~111"] = (WiredVariableTarget.Furni, "~area_hide.root_y"),
            ["~112"] = (WiredVariableTarget.Furni, "~area_hide.width"),
            ["~100"] = (WiredVariableTarget.Furni, "~background_color.hue"),
            ["~102"] = (WiredVariableTarget.Furni, "~background_color.lightness"),
            ["~101"] = (WiredVariableTarget.Furni, "~background_color.saturation"),
            ["~130"] = (WiredVariableTarget.Furni, "~chest.available_amount"),
            ["~133"] = (WiredVariableTarget.Furni, "~chest.capacity"),
            ["~132"] = (WiredVariableTarget.Furni, "~chest.is_auto_lock"),
            ["~135"] = (WiredVariableTarget.Furni, "~chest.is_donatable"),
            ["~134"] = (WiredVariableTarget.Furni, "~chest.is_open"),
            ["~131"] = (WiredVariableTarget.Furni, "~chest.locked"),
            ["~122"] = (WiredVariableTarget.Furni, "~clock.is_game_aware"),
            ["~121"] = (WiredVariableTarget.Furni, "~clock.pulse_count"),
            ["~120"] = (WiredVariableTarget.Furni, "~clock.state"),
            ["~502"] = (WiredVariableTarget.User, "~horse.controller_user_id"),
            ["~500"] = (WiredVariableTarget.User, "~horse.has_saddle"),
            ["~501"] = (WiredVariableTarget.User, "~horse.is_riding"),
            ["~431"] = (WiredVariableTarget.User, "~pet.creation_time"),
            ["~402"] = (WiredVariableTarget.User, "~pet.energy"),
            ["~401"] = (WiredVariableTarget.User, "~pet.experience"),
            ["~411"] = (WiredVariableTarget.User, "~pet.experience_required"),
            ["~403"] = (WiredVariableTarget.User, "~pet.happiness"),
            ["~400"] = (WiredVariableTarget.User, "~pet.level"),
            ["~412"] = (WiredVariableTarget.User, "~pet.max_energy"),
            ["~413"] = (WiredVariableTarget.User, "~pet.max_happiness"),
            ["~410"] = (WiredVariableTarget.User, "~pet.max_level"),
            ["~430"] = (WiredVariableTarget.User, "~pet.owner_id"),
            ["~420"] = (WiredVariableTarget.User, "~pet.scratches"),
            ["~610"] = (WiredVariableTarget.User, "~plant.can_breed"),
            ["~611"] = (WiredVariableTarget.User, "~plant.can_harvest"),
            ["~612"] = (WiredVariableTarget.User, "~plant.can_revive"),
            ["~602"] = (WiredVariableTarget.User, "~plant.color"),
            ["~613"] = (WiredVariableTarget.User, "~plant.is_dead"),
            ["~614"] = (WiredVariableTarget.User, "~plant.is_growing"),
            ["~617"] = (WiredVariableTarget.User, "~plant.max_wellbeing_seconds"),
            ["~600"] = (WiredVariableTarget.User, "~plant.rarity_level"),
            ["~616"] = (WiredVariableTarget.User, "~plant.remaining_growing_seconds"),
            ["~615"] = (WiredVariableTarget.User, "~plant.remaining_wellbeing_seconds"),
            ["~601"] = (WiredVariableTarget.User, "~plant.shape"),
            ["~146"] = (WiredVariableTarget.Furni, "~recolorable_furni.color.rgb"),
            ["~142"] = (WiredVariableTarget.Furni, "~recolorable_furni.color.rgb.blue"),
            ["~141"] = (WiredVariableTarget.Furni, "~recolorable_furni.color.rgb.green"),
            ["~140"] = (WiredVariableTarget.Furni, "~recolorable_furni.color.rgb.red"),
            ["~103"] = (WiredVariableTarget.Furni, "~teleport.target_id"),
            ["~104"] = (WiredVariableTarget.Furni, "~walk_helper.is_multi_walk"),
        };

    public static int Count => Ids.Count;

    /// <summary>Every fixed id with the built-in token ("internal:@index") and target it names, in table order.</summary>
    public static IEnumerable<(string Id, WiredVariableTarget Target, string Token)> All =>
        Ids.Select(pair => (pair.Key, pair.Value.Target, "internal:" + pair.Value.Name));

    /// <summary>The built-in token ("internal:@position.x") and target a Habbo id names.</summary>
    public static bool TryResolve(string id, out WiredVariableTarget target, out string token)
    {
        if (Ids.TryGetValue(id, out var known)) {
            target = known.Target;
            token = "internal:" + known.Name;

            return true;
        }

        target = default;
        token = "";

        return false;
    }

    /// <summary>Habbo's id for one of our built-in tokens, if it lists that variable.</summary>
    public static string? IdOf(WiredVariableTarget target, string token)
    {
        var name = token.StartsWith("internal:", StringComparison.Ordinal) ? token[9..] : token;

        return Ids.FirstOrDefault(pair => pair.Value.Target == target && pair.Value.Name == name).Key;
    }
}
