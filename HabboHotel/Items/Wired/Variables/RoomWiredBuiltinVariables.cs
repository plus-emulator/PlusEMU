using Plus.HabboHotel.Subscriptions;
using System.Globalization;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Plus.HabboHotel.Rooms.Games.Teams;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// Reads live room state. Missing engine-owned properties (projectile, sign, entry provenance, visibility)
/// are absent unless the engine supplies them; they are never fabricated as zero. Movement is supplied
/// by the stack engine so its collision, animation and carried-user policy applies to variable writes too.
/// </summary>
public sealed class RoomWiredBuiltinVariables(Room room,
    Func<WiredVariableReference, WiredVariableHolder, WiredVariableFrame, long?>? engineRead = null,
    Func<WiredVariableReference, WiredVariableHolder, int, WiredVariableFrame, bool>? engineWrite = null,
    Action<Item, WiredVariableFrame>? stateChanged = null, IRoomItemMetadataStore? metadataStore = null, IItemTravelStore? travelStore = null,
    Plus.HabboHotel.Quests.IQuestManager? quests = null, Func<uint, WiredQuestBinding?>? questBindings = null) : IWiredBuiltinVariables
{
    /// <summary>Source capabilities, independent of whether a particular holder currently has the variable.</summary>
    public static bool HasNumericValue(WiredVariableReference reference)
    {
        var key = Normalize(reference.Token);

        if (Plus.HabboHotel.Items.Wired.Chests.WiredChestVariables.Supports(reference.Target, key)) {
            return Plus.HabboHotel.Items.Wired.Chests.WiredChestVariables.HasNumericValue(reference.Target, key);
        }

        return reference.Target switch
        {
            WiredVariableTarget.Furni => key is "@altitude" or "@class_id" or "@dimensions.x" or "@dimensions.y" or "@height"
                or "@id" or "@owner_id" or "@position" or "@occupation" or "@position.x" or "@position.y" or "@rotation" or "@state" or "@type" or "@wallitem_offset"
                or "@projectile.animation.furni_collisions" or "@projectile.animation.user_collisions" or "@projectile.animation.tiles_traveled"
                or "~area_hide.root_x" or "~area_hide.root_y" or "~area_hide.width" or "~area_hide.length"
                or "~clock.state" or "~clock.pulse_count" or "~teleport.target_id" or "~background_color.hue" or "~background_color.saturation" or "~background_color.lightness"
                or "~recolorable_furni.color.rgb" or "~recolorable_furni.color.rgb.red" or "~recolorable_furni.color.rgb.green" or "~recolorable_furni.color.rgb.blue"
                or "@projectile.animation.position.x" or "@projectile.animation.position.y" or "@projectile.animation.position.altitude",
            WiredVariableTarget.User => WiredQuestVariables.TryParseKey(key, out _, out _) || key is "@achievement_score" or "@level" or "@altitude" or "@bot_id" or "@dance" or "@direction" or "@effect"
                or "@gender" or "@handitem" or "@index" or "@pet_id" or "@pet_owner_id" or "@favourite_group_id" or "@position" or "@position.x" or "@position.y" or "@room_entry.method"
                or "@room_entry.teleport_id" or "@sign" or "@team.type" or "@team.color" or "@team.score" or "@type" or "@user_id"
                or "~horse.controller_user_id" or "~pet.creation_time" or "~pet.energy" or "~pet.experience"
                or "~pet.experience_required" or "~pet.happiness" or "~pet.level" or "~pet.max_energy"
                or "~pet.max_happiness" or "~pet.max_level" or "~pet.owner_id" or "~pet.scratches"
                // The plant's numeric parts carry a value; its can_/is_ flags do not.
                or "~plant.color" or "~plant.max_wellbeing_seconds" or "~plant.rarity_level" or "~plant.remaining_growing_seconds"
                or "~plant.remaining_wellbeing_seconds" or "~plant.shape",
            WiredVariableTarget.Global => key is "@furni_count" or "@room_id" or "@user_count" or "@wired_timer" or "@group_id"
                or "@teams.red.score" or "@teams.red.size" or "@teams.green.score" or "@teams.green.size"
                or "@teams.blue.score" or "@teams.blue.size" or "@teams.yellow.score" or "@teams.yellow.size"
                or "@current_time" or "@current_time.milliseconds_of_seconds" or "@current_time.seconds_of_minute"
                or "@current_time.minute_of_hour" or "@current_time.hour_of_day" or "@current_time.day_of_week"
                or "@current_time.day_of_month" or "@current_time.day_of_year" or "@current_time.week_of_year"
                or "@current_time.month_of_year" or "@current_time.year",
            WiredVariableTarget.Context => key is "@selector_furni_count" or "@selector_user_count" or "@signal_furni_count" or "@signal_user_count"
                or "@event.signal.antenna_id" or "@event.chat.type" or "@event.chat.style" or "@event.habbicon" or "@event.link.source_room_id"
                or "@event.variable_update.box_id" or "@event.variable_update.change_type" or "@event.variable_update.old_value"
                or "@event.variable_update.new_value" or "@event.variable_update.difference" or "@event.variable_update.change_origin",
            _ => false
        };
    }

    /// <summary>Finite implemented numeric-write domains; holder-specific admission still applies.</summary>
    internal static bool SupportsNumericWrite(WiredVariableReference reference)
    {
        var key = Normalize(reference.Token);

        return reference.Target switch
        {
            WiredVariableTarget.Furni => key is "@position" or "@occupation" or "@position.x" or "@position.y"
                or "@altitude" or "@rotation" or "@wallitem_offset" or "@state" or "~clock.pulse_count"
                or "~area_hide.root_x" or "~area_hide.root_y" or "~area_hide.width" or "~area_hide.length"
                or "~teleport.target_id" or "~background_color.hue" or "~background_color.saturation" or "~background_color.lightness"
                or "~recolorable_furni.color.rgb",
            WiredVariableTarget.User => key is "@handitem" or "@team.score" or "@position" or "@position.x" or "@position.y" or "@direction",
            WiredVariableTarget.Global => key is "@teams.red.score" or "@teams.green.score" or "@teams.blue.score" or "@teams.yellow.score",
            _ => false
        };
    }

    // v2 only: legacy and shadow rooms write gate states directly and never enter the module's admission.
    public bool SequencesGateWrites => GateTransitionService.For(room) != null;

    public bool CanInterceptChanges(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableFrame frame) =>
        reference.Target != WiredVariableTarget.Furni || FindItem(holder, frame)?.IsWallItem != true
        || Normalize(reference.Token) is not ("@position" or "@occupation" or "@position.x" or "@position.y"
            or "@altitude" or "@rotation" or "@wallitem_offset");

    // The gate's per-write FIFO decides: behind a pending write, or a closing from another thread, the whole
    // transaction waits for the owner. Otherwise it runs now with the transform's single, already evaluated result.
    public IDisposable? Admit(WiredVariableReference reference, WiredVariableHolder holder, ref Func<int, int> transform,
        Func<Func<int, int>, Action> replayWith, Func<bool> stillTargeted, out WiredAdmission admission)
    {
        admission = WiredAdmission.Proceed;

        if (holder.Target != WiredVariableTarget.Furni || Normalize(reference.Token) != "@state") {
            return null;
        }

        if (FindItem(holder) is not { } item || !GateTransitionService.IsGate(item) || GateTransitionService.For(room) is not { } gates) {
            return null;
        }

        var original = transform;
        string? Peek(string current) => int.TryParse(current, out var value)
            ? original(value).ToString(CultureInfo.InvariantCulture) : null;
        Action Replay(string? prepared) => replayWith(prepared is null ? original : _ => int.Parse(prepared, CultureInfo.InvariantCulture));
        var scope = gates.AdmitVariableWrite(item, Peek, Replay, stillTargeted, out admission, out var evaluated);

        if (evaluated is not null) {
            transform = _ => int.Parse(evaluated, CultureInfo.InvariantCulture);
        }

        return scope;
    }

    public WiredVariableValue? Read(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableFrame frame)
    {
        if (reference.Target != holder.Target || frame.RoomId != room.Id || !frame.Contains(holder)) {
            return null;
        }

        var key = Normalize(reference.Token);

        if (holder.Target == WiredVariableTarget.Furni && frame.WallInspectionSnapshot is not null && WiredWallBuiltinValues.Supports(key)) {
            var item = FindItem(holder, frame);

            if (item is null || !item.IsWallItem || item.RoomId != room.Id) {
                return null;
            }

            WiredWallBuiltinValues.TryReadInspection(room, item, key, holder, frame, out var inspected);

            return inspected is long capturedNumber ? new(capturedNumber, null, null) : null;
        }

        long? value = holder.Target switch
        {
            WiredVariableTarget.Furni => ReadItem(key, holder, frame),
            WiredVariableTarget.User => WiredQuestVariables.TryParseKey(key, out var questDefinition, out var questPart) ? ReadQuest(questDefinition, questPart, holder) : ReadAvatar(key, holder),
            WiredVariableTarget.Global => key switch
            {
                "@furni_count" => room.GetRoomItemHandler().GetWallAndFloor.Count(),
                "@room_id" => checked((int)room.Id),
                "@user_count" => room.GetRoomUserManager().GetRoomUsers().Count,
                _ => null
            },
            WiredVariableTarget.Context => key switch
            {
                "@selector_furni_count" => frame.Selector.Count(x => x.Target == WiredVariableTarget.Furni),
                "@selector_user_count" => frame.Selector.Count(x => x.Target == WiredVariableTarget.User),
                "@signal_furni_count" => frame.Signal.Count(x => x.Target == WiredVariableTarget.Furni),
                "@signal_user_count" => frame.Signal.Count(x => x.Target == WiredVariableTarget.User),
                _ => null
            },
            _ => null
        };
        value ??= engineRead?.Invoke(reference with { Token = $"internal:{key}" }, holder, frame);

        return value is long number ? new(number, null, null) : null;
    }

    public static bool SupportsPresenceMutation(WiredVariableReference reference) => reference.Target == WiredVariableTarget.Furni
        && (AreaHideIndex(Normalize(reference.Token)) is >= 5 and <= 7 || Normalize(reference.Token) is "@can_move_freely" or "~walk_helper.is_multi_walk")
        || CarriesPresenceValue(reference);

    // Give @can_move_freely to a floor furni makes Has Variable pass and Remove makes it fail; it does not let a Wired move pass an avatar standing on the next tile.
    // ~walk_helper.is_multi_walk is listed creatable too, but a colour tile never takes it, so it needs a walk helper, which this hotel does not have.
    private readonly HashSet<uint> _movesFreely = [];

    /// <summary>
    /// A built-in whose Give and Remove carry a value: @handitem. Give @handitem 2 hands the avatar item 2, Change Variable assign 5 swaps it for 5, and Remove drops it.
    /// </summary>
    public static bool CarriesPresenceValue(WiredVariableReference reference) => reference.Target == WiredVariableTarget.User
        && Normalize(reference.Token) == "@handitem";

    /// <summary>
    /// A built-in that Give and Remove Variable save on but never change: @habbicon. A placeholder reads null after each.
    /// </summary>
    public static bool SavesWithoutEffect(WiredVariableReference reference) => reference.Target == WiredVariableTarget.User
        && Normalize(reference.Token) == "@habbicon";

    public bool MutatePresence(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableMutation mutation, WiredVariableFrame frame)
    {
        if (!SupportsPresenceMutation(reference) || reference.Target != holder.Target || frame.RoomId != room.Id || !frame.Contains(holder)
            || mutation is not (WiredVariableMutation.Give or WiredVariableMutation.Replace or WiredVariableMutation.Remove)) {
            return false;
        }

        var item = FindItem(holder, frame);
        var key = Normalize(reference.Token);

        if (key == "~walk_helper.is_multi_walk") {
            return false;
        }

        if (key == "@can_move_freely") {
            if (item is null || item.IsWallItem) {
                return false;
            }

            lock (_movesFreely) {
                return mutation == WiredVariableMutation.Remove ? _movesFreely.Remove(item.Id) : _movesFreely.Add(item.Id);
            }
        }

        if (item is null || !Plus.HabboHotel.Items.AreaHide.AreaHideState.TryRead(item, out var state)) {
            return false;
        }

        var index = AreaHideIndex(key);
        var value = mutation == WiredVariableMutation.Remove ? 0 : 1;

        return state[index] != value && Plus.HabboHotel.Items.AreaHide.AreaHideState.Set(room, item, index, value);
    }

    public bool Write(WiredVariableReference reference, WiredVariableHolder holder, int value, WiredVariableFrame frame)
    {
        var changed = Write(reference, holder, value, frame, out var completed);

        if (changed) {
            completed?.Invoke();
        }

        return changed;
    }

    public bool Write(WiredVariableReference reference, WiredVariableHolder holder, int value, WiredVariableFrame frame, out Action? completed)
    {
        completed = null;

        if (reference.Target != holder.Target || frame.RoomId != room.Id || !frame.Contains(holder)) {
            return false;
        }

        var key = Normalize(reference.Token);

        if (holder.Target == WiredVariableTarget.User) {
            var avatar = room.GetRoomUserManager().GetRoomUserByVirtualId(holder.EntityId);

            if (avatar is null || !Matches(avatar, holder)) {
                return false;
            }

            if (key == "@handitem" && value is >= 0 and <= 9999) {
                avatar.CarryItem(value);

                return true;
            }

            if (key == "@team.score" && !avatar.IsBot && avatar.Team != Team.None && value >= 0) {
                var game = room.GetGameManager();
                var current = game.Points[(int)avatar.Team];
                var difference = (long)value - current;

                if (difference is < int.MinValue or > int.MaxValue) {
                    return false;
                }

                game.AddPointToTeam(avatar.Team, (int)difference);

                return true;
            }
        }

        if (holder.Target == WiredVariableTarget.Furni && AreaHideIndex(key) is >= 1 and <= 4 and var areaIndex) {
            var item = FindItem(holder, frame);

            return item is not null && Plus.HabboHotel.Items.AreaHide.AreaHideState.Set(room, item, areaIndex, value);
        }

        if (holder.Target == WiredVariableTarget.Furni && key == "~teleport.target_id") {
            var item = FindItem(holder, frame);

            return value > 0 && item is { IsTemporary: false } && InteractionTypes.IsLinkedPair(item.Definition.InteractionType)
                && travelStore?.SetLinkedTeleporter(item.Id, room.Id, (uint)value) == true;
        }

        if (holder.Target == WiredVariableTarget.Furni && key is "~background_color.hue" or "~background_color.saturation" or "~background_color.lightness") {
            var item = FindItem(holder, frame);

            if (item is null || !IsToner(item) || metadataStore is null || value is < 0 or > 255) {
                return false;
            }

            return RoomItemMetadataService.SetTonerFromWired(room, item, new(item.Id,
                key == "~background_color.hue" ? value : room.TonerData.Hue,
                key == "~background_color.saturation" ? value : room.TonerData.Saturation,
                key == "~background_color.lightness" ? value : room.TonerData.Lightness), metadataStore);
        }

        // ~recolorable_furni.color.rgb is listed as writable and its red, green and blue parts only read. A write to a Builders Club class (every recolourable class is one) is
        // refused without an error and leaves the colour, however the furni was placed; the colour still reads.
        if (holder.Target == WiredVariableTarget.Furni && key == "~recolorable_furni.color.rgb") {
            var item = FindItem(holder, frame);

            if (item is null || item.Definition.ItemName.StartsWith("bc_", StringComparison.Ordinal)
                || item.ExtraData is not Plus.HabboHotel.Items.DataFormat.RecolorableDataFormat recolorable || !recolorable.SetColor(value)) {
                return false;
            }

            room.GetRoomItemHandler().UpdateItem(item);
            room.SendObjectData(item, item.LegacyDataString, false);

            return true;
        }

        if (holder.Target == WiredVariableTarget.Furni && key == "@state") {
            var item = FindItem(holder, frame);

            // A variable write leaves the state of a Wired box, wf_antenna1 or wf_antenna2 unchanged; a Wired floor switch still changes.
            if (item is null || item.IsWired || WiredStackEngine.IsSignalAntenna(item)) {
                return false;
            }

            // A value below 0 or from the furni's state count up sets state 0.
            var state = value < 0 || item.Definition.Modes <= value ? 0 : value;

            // A furni placed from the inventory can carry no state text yet; it shows state 0, and any write sets it.
            if (item.Definition.Modes <= 0
                || item.LegacyDataString is { Length: > 0 } text && (!int.TryParse(text, out var previous) || previous == state)) {
                return false;
            }

            var mark = FurnitureStateEvents.Mark();

            if (GateTransitionService.For(item) != null) {
                if (!WriteGateState(item, state)) {
                    return false;
                }
            }
            else {
                item.LegacyDataString = state.ToString(CultureInfo.InvariantCulture);
                item.UpdateState();
            }

            // The completion reports this write only; a write the room's pass already reported stays reported.
            if (stateChanged is not null && FurnitureStateEvents.TakeWriteSince(item, mark)) {
                completed = () => stateChanged(item, frame);
            }

            return true;
        }

        return engineWrite?.Invoke(reference with { Token = $"internal:{key}" }, holder, value, frame) == true;
    }

    // v2 only: a gate's state goes through its transition service.
    private bool WriteGateState(Item item, int value)
    {
        var next = value.ToString(CultureInfo.InvariantCulture);

        // Closing writes from other threads were sequenced whole by Admit; nothing is notified early.
        if (GateTransitionService.IsClosing(item, next) && !RoomOwnerScope.IsOwner(room)) {
            return false;
        }

        return GateTransitionService.WriteNow(item, next, GateCloseReason.Wired) != GateTransition.Refused;
    }

    private Item? FindItem(WiredVariableHolder holder, WiredVariableFrame? frame = null)
    {
        var item = room.GetRoomItemHandler().GetItem(unchecked((uint)holder.EntityId));

        return item is not null && WiredVariableRuntimeFrames.FurniHolder(item) == holder
            && (frame?.RuntimeContext is not { } context || context.FurniIdentity.TryGetValue(item.Id, out var captured) && ReferenceEquals(captured, item)) ? item : null;
    }
    private long? ReadItem(string key, WiredVariableHolder holder, WiredVariableFrame frame)
    {
        var item = FindItem(holder, frame);

        if (item is null || item.IsWallItem && item.RoomId != room.Id) {
            return null;
        }

        // Wall coordinates require the native parser and captured-reference checks in engineRead.
        if (item.IsWallItem && key is "@position" or "@occupation" or "@position.x" or "@position.y" or "@altitude" or "@rotation" or "@wallitem_offset") {
            return null;
        }

        if (key == "@can_move_freely") {
            lock (_movesFreely) {
                return Flag(!item.IsWallItem && _movesFreely.Contains(item.Id));
            }
        }

        if (AreaHideIndex(key) is >= 1 and var areaIndex
            && Plus.HabboHotel.Items.AreaHide.AreaHideState.TryRead(item, out var area)) {
            return areaIndex >= 5 ? Flag(area[areaIndex] > 0) : area[areaIndex];
        }

        return key switch
        {
            // A Builders Club furni's @id is the id the client sees.
            "@id" => item.IsWallItem ? WiredWallBuiltinValues.ReadIdentity(item.Id, item.Definition.SpriteId, key) : unchecked((int)BuildersClubItemIds.ToWire(item.Id)),
            "@owner_id" => checked((int)item.OwnerId),
            "@class_id" => item.IsWallItem ? WiredWallBuiltinValues.ReadIdentity(item.Id, item.Definition.SpriteId, key) : item.Definition.SpriteId,
            "@height" => Hundredths(item.TotalHeight - item.GetZ),
            // No state text yet (a furni placed from the inventory) is state 0, as the client shows it.
            "@state" => int.TryParse(item.LegacyDataString, out var state) ? state : string.IsNullOrEmpty(item.LegacyDataString) ? 0 : null,
            "@position" => (item.GetX << 8) | item.GetY,
            "@occupation" => (item.GetX << 16) | (item.GetY << 8) | FurnitureDirections.RotationIndex(item.Definition, item.Rotation),
            "~teleport.target_id" when InteractionTypes.IsLinkedPair(item.Definition.InteractionType) => travelStore?.FindLinkedTeleporter(item.Id),
            "~background_color.hue" when IsToner(item) => room.TonerData.Hue,
            "~background_color.saturation" when IsToner(item) => room.TonerData.Saturation,
            "~background_color.lightness" when IsToner(item) => room.TonerData.Lightness,
            "~recolorable_furni.color.rgb" when item.ExtraData is Plus.HabboHotel.Items.DataFormat.RecolorableDataFormat color => color.Rgb,
            "~recolorable_furni.color.rgb.red" when item.ExtraData is Plus.HabboHotel.Items.DataFormat.RecolorableDataFormat color => color.Red,
            "~recolorable_furni.color.rgb.green" when item.ExtraData is Plus.HabboHotel.Items.DataFormat.RecolorableDataFormat color => color.Green,
            "~recolorable_furni.color.rgb.blue" when item.ExtraData is Plus.HabboHotel.Items.DataFormat.RecolorableDataFormat color => color.Blue,
            "@position.x" => item.GetX,
            "@position.y" => item.GetY,
            "@altitude" => Hundredths(item.GetZ),
            "@rotation" => FurnitureDirections.RotationIndex(item.Definition, item.Rotation),
            "@dimensions.x" => item.Definition.Width,
            "@dimensions.y" => item.Definition.Length,
            "@is_stackable" => Flag(item.IsFloorItem && item.Definition.Stackable),
            "@can_stand_on" => Flag(item.IsFloorItem && item.Definition.Walkable),
            "@can_sit_on" => Flag(item.IsFloorItem && item.Definition.IsSeat),
            "@can_lay_on" => Flag(item.IsFloorItem && item.Definition.InteractionType == InteractionType.Bed),
            // The connector reads 0 Normal, 1 BC, 2 Temp, and a Builders Club block reads 1.
            "@type" => item.IsTemporary ? 2 : item.IsBuildersClub ? 1 : item.OwnerId > 0 ? 0 : null,
            _ => null
        };
    }
    // A quest definition's own value, only for a definition present in this room and a player (never a bot or an offline holder).
    private long? ReadQuest(uint definitionId, string? part, WiredVariableHolder holder)
    {
        if (quests is null || questBindings?.Invoke(definitionId) is not { } binding) {
            return null;
        }

        var avatar = room.GetRoomUserManager().GetRoomUserByVirtualId(holder.EntityId);

        if (avatar is null || avatar.IsBot || !Matches(avatar, holder)) {
            return null;
        }

        var habbo = avatar.GetClient()?.GetHabbo();

        return part == WiredQuestVariables.CurrentStepPart ? WiredQuestVariables.CurrentStep(quests, binding, habbo)
            : WiredQuestVariables.Read(quests, binding, habbo);
    }

    private long? ReadAvatar(string key, WiredVariableHolder holder)
    {
        var avatar = room.GetRoomUserManager().GetRoomUserByVirtualId(holder.EntityId);

        if (avatar is null || !Matches(avatar, holder)) {
            return null;
        }

        var habbo = avatar.IsBot ? null : avatar.GetClient()?.GetHabbo();

        return key switch
        {
            "@index" => avatar.VirtualId,
            "@type" => avatar.IsPet ? 2 : avatar.IsBot ? 4 : 1,
            "@user_id" => habbo?.Id,
            "@bot_id" => avatar.IsBot && !avatar.IsPet ? avatar.BotData.Id : null,
            "@pet_id" => avatar.IsPet ? avatar.PetData?.PetId : null,
            "@position" => (avatar.X << 8) | avatar.Y,
            "@position.x" => avatar.X,
            "@position.y" => avatar.Y,
            "@altitude" => Hundredths(avatar.Z),
            "@direction" => avatar.RotBody,
            // @dance, @sign and @effect exist only while the avatar dances, holds a sign or wears an effect, and @team.color and @team.score only while it is in a team.
            "@dance" => avatar.DanceId > 0 ? avatar.DanceId : null,
            // An avatar carrying nothing has no @handitem (a placeholder shows "null").
            "@handitem" => avatar.CarryItemId == 0 ? null : avatar.CarryItemId,
            // @sign keeps the sign for about 5 s, long after its status is gone.
            "@sign" => avatar.HeldSign,
            "@effect" => habbo?.Effects?.CurrentEffect is > 0 and var effect ? effect : null,
            "@gender" => habbo is null ? -1 : habbo.Gender.Equals("M", StringComparison.OrdinalIgnoreCase) ? 0 : 1,
            "@achievement_score" => habbo?.HabboStats?.AchievementPoints,
            // @level is the account level the extended profile shows as Level, so it is the hotel's user level, not an achievement level.
            "@level" => habbo is null ? null : Plus.HabboHotel.Items.Wired.Modern.Conditions.WiredUserLevel.Of(avatar),
            "@favourite_group_id" => habbo?.HabboStats?.FavouriteGroupId is > 0 ? habbo.HabboStats.FavouriteGroupId : null,
            "@is_group_admin" => Flag(habbo is not null && room.Group is { } group && (group.CreatorId == habbo.Id || group.IsAdmin(habbo.Id))),
            "@pet_owner_id" => avatar.IsPet ? avatar.PetData?.OwnerId : null,
            _ when key.StartsWith("~pet.", StringComparison.Ordinal) || key.StartsWith("~horse.", StringComparison.Ordinal) => ReadPet(key, avatar),
            "@is_hc" => Flag(habbo is not null && ClubAccess.LevelFor(habbo.Access) > 0),
            "@is_idle" => Flag(avatar.IsAsleep),
            "@is_frozen" => Flag(avatar.Freezed || avatar.Frozen),
            "@is_trading" => Flag(avatar.IsTrading),
            "@is_muted" => Flag(habbo is not null && habbo.TimeMuted > 0),
            "@is_owner" => Flag(habbo is not null && habbo.Id == room.OwnerId),
            "@has_rights" => Flag(habbo is not null && room.CheckRights(habbo.Client)),
            "@team.type" => Plus.HabboHotel.Items.Wired.Modern.Actions.WiredGameState.For(room).ReadTeamType(room, avatar),
            "@team.color" => !avatar.IsBot && avatar.Team != Team.None ? (int)avatar.Team : null,
            "@team.score" => !avatar.IsBot && avatar.Team != Team.None ? room.GetGameManager().Points[(int)avatar.Team] : null,
            _ => null
        };
    }
    private bool IsToner(Item item) => item.Definition.InteractionType == InteractionType.Toner && room.TonerData?.ItemId == item.Id;
    private long? ReadPet(string key, RoomUser avatar)
    {
        if (!avatar.IsPet || avatar.PetData is not { } pet) {
            return null;
        }

        if (key.StartsWith("~horse.", StringComparison.Ordinal)) {
            if (pet.Type != 15) {
                return null;
            }

            return key switch
            {
                "~horse.has_saddle" => Flag(pet.Saddle > 0),
                "~horse.is_riding" => Flag(avatar.RidingHorse),
                "~horse.controller_user_id" => avatar.RidingHorse && room.GetRoomUserManager().GetRoomUserByVirtualId(avatar.HorseId) is { IsBot: false } rider ? rider.HabboId : 0,
                _ => null
            };
        }

        if (pet.Type == 16) {
            return null;
        }

        return key switch
        {
            "~pet.creation_time" => pet.CreatedAt?.ToUnixTimeMilliseconds(),
            "~pet.energy" => pet.Energy,
            "~pet.experience" => pet.Experience,
            "~pet.experience_required" => pet.ExperienceGoal,
            "~pet.happiness" => pet.Nutrition,
            "~pet.level" => pet.Level,
            "~pet.max_energy" => Plus.HabboHotel.Rooms.AI.Pet.MaxEnergy,
            "~pet.max_happiness" => Plus.HabboHotel.Rooms.AI.Pet.MaxNutrition,
            "~pet.max_level" => Plus.HabboHotel.Rooms.AI.Pet.MaxLevel,
            "~pet.owner_id" => pet.OwnerId,
            "~pet.scratches" => pet.Respect,
            _ => null
        };
    }
    private static bool Matches(RoomUser avatar, WiredVariableHolder holder) => avatar.IsBot
        ? !holder.CanPersist && holder.StableId == -(long)avatar.VirtualId - 1
        : holder.CanPersist && holder.StableId == avatar.HabboId;
    // A builtin flag that is set reads -1 (@is_owner, @has_rights, @is_stackable).
    private static int? Flag(bool flag) => flag ? -1 : null;
    private static int Hundredths(double value) => (int)Math.Clamp(Math.Round(value * 100), int.MinValue, int.MaxValue);
    private static int AreaHideIndex(string key) => key switch
    {
        "~area_hide.root_x" => 1,
        "~area_hide.root_y" => 2,
        "~area_hide.width" => 3,
        "~area_hide.length" => 4,
        "~area_hide.is_invisible_furni" => 5,
        "~area_hide.hiding_wallitems" => 6,
        "~area_hide.inverted" => 7,
        _ => -1
    };

    public static string Normalize(string token)
    {
        var key = token.StartsWith("internal:", StringComparison.Ordinal) ? token[9..] : token;

        return key switch
        {
            "@projectile.animation.tiles_travelled" => "@projectile.animation.tiles_traveled",
            "@projectile.animation.is_travelling" => "@projectile.animation.is_traveling",
            "@team_red_score" => "@teams.red.score",
            "@team_red_size" => "@teams.red.size",
            "@team_green_score" => "@teams.green.score",
            "@team_green_size" => "@teams.green.size",
            "@team_blue_score" => "@teams.blue.score",
            "@team_blue_size" => "@teams.blue.size",
            "@team_yellow_score" => "@teams.yellow.score",
            "@team_yellow_size" => "@teams.yellow.size",
            "@current_time.millisecond_of_second" => "@current_time.milliseconds_of_seconds",
            "@antenna_id" => "@event.signal.antenna_id",
            "@position_x" => "@position.x",
            "@position_y" => "@position.y",
            "@effect_id" => "@effect",
            "@handitem_id" => "@handitem",
            "@team_score" => "@team.score",
            "@team_type" => "@team.type",
            "@team_color" => "@team.color",
            _ => key
        };
    }
}
