using System.Collections.Immutable;
using System.Globalization;
using Plus.HabboHotel.Items.Wired.Modern.Actions;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>Native records own editor data; runtime fields are checked immutable projections.</summary>
public static class WiredNativeEditorProjection
{
    private const int MaxSelectorAreaCoordinate = 64;
    // The October Show Message editor: TextAreaParam(40, -1, 8, -1, 400), eight lines of at most 400 characters.
    private const int ShowMessageMaxCharacters = 400;
    private static readonly int[] ShowStyles = [34, 200, 201, 202, 210, 211, 212, 220, 221, 222, 223, 224, 225, 226, 227, 228, 229, 250, 251, 252];

    private static readonly HashSet<string> BuiltIn = ["wf_trg_says_something", "wf_trg_walks_on_furni", "wf_trg_periodically",
        "wf_cnd_user_count_in", "wf_act_teleport_to", "wf_act_show_message", "wf_cnd_furnis_hv_avtrs", "wf_act_move_rotate",
        "wf_act_move_to_dir", "wf_act_control_clock", "wf_act_join_team", "wf_act_give_score", "wf_act_set_altitude",
        "wf_act_send_signal", "wf_act_move_furni_as_group"];

    // The auxiliary editor is asked first for every category (variable/chest triggers, conditions and effects live there);
    // the ordinary per-category helpers handle the rest. A name belongs to exactly one helper.
    public static bool Supports(string name) => BuiltIn.Contains(name) || WiredNativeAuxiliaryEditor.Supports(name)
        || WiredBoxRegistry.TryGet(name, out var descriptor) && descriptor.Category switch
        {
            WiredBoxCategory.Trigger => WiredNativeTriggerEditor.Supports(name),
            WiredBoxCategory.Action => WiredNativeActionEditor.Supports(name),
            WiredBoxCategory.Condition => WiredNativeConditionEditor.Supports(name),
            WiredBoxCategory.Selector => WiredNativeSelectorEditor.Supports(name),
            _ => false
        };

    /// <summary>The registry editor code; code 0 is a valid dialog (says-something, toggle-state).</summary>
    public static int Code(string name) => WiredBoxRegistry.TryGet(name, out var descriptor) ? descriptor.EditorCode
        : throw new InvalidDataException("This native editor projection is unavailable.");

    public static WiredNativeEditorMetadata Metadata(string name) => name switch
    {
        // Says borrows the Sept9 reset defaults; remaining footer fields advertise local support.
        // No Sept16 server metadata oracle is claimed.
        "wf_trg_says_something" => new([], [], [], [], [0, 0, 1], false),
        // Walk/Show footer defaults are local support; Condition borrows Sept9, not Sept16 metadata.
        "wf_trg_walks_on_furni" => new([[100, 200]], [], [100], [], [], false),
        "wf_act_show_message" => new([], [[0, 200, 201]], [], [0], [0, 202, -1], false),
        "wf_cnd_furnis_hv_avtrs" => new([[0, 100, 200, 201]], [], [100], [], [1], false),
        // Complete Sept9 server footers are borrowed; no official Sept16/v86 metadata claim.
        "wf_trg_periodically" => new([], [], [], [], [1], false),
        "wf_cnd_user_count_in" => new([], [], [], [], [1, 50], false),
        "wf_act_teleport_to" => new([[0, 100, 200, 201]], [[0, 200, 201]], [100], [0], [0], false),
        // Rotate owns two fields; the frame adds the hotel's third (FrameDefaults, HotelFrame). Wall picks are allowed as on hhus
        // (AIR WIN63-202610131318-860371703) and skipped by the mover, which only moves floor furni.
        "wf_act_move_rotate" => new([[0, 100, 200, 201]], [], [100], [], [0, 0], true),
        "wf_act_move_to_dir" => new([[0, 100, 200, 201]], [], [100], [], [0, 0, 1], false),
        "wf_act_control_clock" => new([[0, 100, 200, 201]], [], [100], [], [0], false),
        "wf_act_join_team" => new([], [[0, 200, 201]], [], [0], [1, 0], false),
        "wf_act_give_score" => new([], [[0, 200, 201]], [], [0], [5, 0], false),
        "wf_act_set_altitude" => new([[0, 100, 200, 201]], [], [100], [], [0, 0], true),
        "wf_act_send_signal" => new([[0, 100, 101, 200, 201], [0, 100, 101, 200, 201]], [[0, 200, 201]], [100, 200], [200], [0, 0], false),
        // This is the reviewed executable local subset, not an official Group metadata oracle.
        "wf_act_move_furni_as_group" => new([[0, 100, 101, 200, 201], [0, 100, 101, 200, 201]], [[0, 200, 201]], [100, 0], [0], [1, 0, 0], true),
        _ => CategoryMetadata(name)
    };

    /// <summary>
    /// Observed on AIR WIN63-202610131318-860371703: a stack with Bot Reaches User offers source 10 and a stack with Click User offers 11,
    /// inserted after 0 in every user slot of every box in that stack. Other triggers add nothing; furni slots and defaults never change.
    /// </summary>
    public static ImmutableArray<int> TriggerContextSources(IEnumerable<string> stackBoxNames)
    {
        var names = stackBoxNames.ToHashSet(StringComparer.Ordinal);

        return [.. new[] { (Name: "wf_trg_bot_reached_avtr", Source: WiredSources.ReachedUser), (Name: "wf_trg_click_user", Source: WiredSources.ClickedUser) }
            .Where(entry => names.Contains(entry.Name)).Select(entry => entry.Source)];
    }

    public static WiredNativeEditorMetadata WithTriggerContext(WiredNativeEditorMetadata metadata, ImmutableArray<int> contextSources) =>
        contextSources.IsDefaultOrEmpty ? metadata : metadata with
        {
            UsersAllowed = [.. metadata.UsersAllowed.Select(group => group.Contains(WiredSources.Trigger)
                ? group.Take(1).Concat(contextSources.Where(source => !group.Contains(source))).Concat(group.Skip(1)).ToImmutableArray() : group)]
        };

    // Both trigger-context sources, for callers that stand for a stack with either trigger.
    public static readonly ImmutableArray<int> AnyTriggerContext = [WiredSources.ReachedUser, WiredSources.ClickedUser];

    private static WiredNativeEditorMetadata CategoryMetadata(string name) => WiredNativeAuxiliaryEditor.Supports(name)
        ? WiredNativeAuxiliaryEditor.Metadata(name)
        : WiredBoxRegistry.TryGet(name, out var descriptor)
            ? descriptor.Category switch
            {
                WiredBoxCategory.Trigger => WiredNativeTriggerEditor.Metadata(name),
                WiredBoxCategory.Action => WiredNativeActionEditor.Metadata(name),
                WiredBoxCategory.Condition => WiredNativeConditionEditor.Metadata(name),
                WiredBoxCategory.Selector => WiredNativeSelectorEditor.Metadata(name),
                _ => throw new InvalidDataException("This native editor projection is unavailable.")
            }
            : throw new InvalidDataException("This native editor projection is unavailable.");

    private static bool TryCompileCategory(WiredBoxDescriptor descriptor, WiredNativeEditorConfiguration native,
        out WiredConfiguration runtime) => WiredNativeAuxiliaryEditor.Supports(descriptor.CanonicalName)
        ? WiredNativeAuxiliaryEditor.TryCompile(descriptor.CanonicalName, native, out runtime)
        : descriptor.Category switch
        {
            WiredBoxCategory.Trigger => WiredNativeTriggerEditor.TryCompile(descriptor.CanonicalName, native, out runtime),
            WiredBoxCategory.Action => WiredNativeActionEditor.TryCompile(descriptor.CanonicalName, native, out runtime),
            WiredBoxCategory.Condition => WiredNativeConditionEditor.TryCompile(descriptor.CanonicalName, native, out runtime),
            WiredBoxCategory.Selector => WiredNativeSelectorEditor.TryCompile(descriptor.CanonicalName, native, out runtime),
            _ => Fail(out runtime)
        };

    private static bool Fail(out WiredConfiguration runtime)
    {
        runtime = new();

        return false;
    }

    /// <summary>
    /// The localization key the editor reports when a furni source is outside the card's allowed group. Observed on hhus for
    /// Remove Furni with source 100 (WiredValidationError, no parameters). User sources are unobserved and keep the generic refusal.
    /// </summary>
    public static string? SourceRejectionKey(WiredBoxDescriptor descriptor, WiredNativeEditorConfiguration native)
    {
        if (!Supports(descriptor.CanonicalName)) {
            return null;
        }

        // Observed on AIR WIN63-202610151745-318700621: a leaderboard whose override variables do not match its override count is refused
        // with this key.
        if (descriptor.CanonicalName == "wf_xtra_var_leaderboard") {
            return "wiredfurni.error.variable_leaderboard.invalid_configuration";
        }

        // Observed on AIR WIN63-202610131318-860371703: Freeze User with effect 5 is refused with this key and no parameters.
        if (descriptor.CanonicalName == "wf_act_freeze" && native.OwnedIntParams is [< 0 or > 4, _]) {
            return "wiredfurni.error.invalid_freeze_effect";
        }

        // Observed on AIR WIN63-202610201032-107964540: Set Furni Highlight and Set Furni Visibility answer each refused value with its
        // own key.
        if (WiredFurniVisualRules.Applies(descriptor.CanonicalName)
            && WiredFurniVisualRules.ValueRefusal(descriptor.CanonicalName, WiredFurniVisualRules.Normalize(descriptor.CanonicalName, native,
                Metadata(descriptor.CanonicalName).OwnedDefaults).OwnedIntParams) is { } refusal) {
            return refusal;
        }

        if (WiredFurniVisualRules.Applies(descriptor.CanonicalName)) {
            if (!WiredFurniVisualRules.DelayAccepted(native.Delay)) {
                return WiredFurniVisualRules.DelayKey;
            }

            if (WiredFurniVisualRules.UserSourceRefusal(native, Metadata(descriptor.CanonicalName)) is { } userSource) {
                return userSource;
            }
        }

        var metadata = Metadata(descriptor.CanonicalName);

        var allowed = metadata.FurniAllowed;

        if (native.FurniSourceTypes.Length == allowed.Length
            && native.FurniSourceTypes.Where((source, index) => !allowed[index].Contains(source)).Any()) {
            return "wiredfurni.error.invalid_furni_source";
        }

        return WiredEditorStoreRules.SourceRefusal(descriptor.CanonicalName, native, metadata);
    }

    /// <summary>
    /// The key a client save is refused with before it is compiled, or null. Observed on AIR WIN63-202610201032-107964540: the capturer
    /// refuses an odd name or a second variable with its own key, and a Says text may not capture one name twice. A fresh capturer opens
    /// with an empty name, which only a save is refused for.
    /// </summary>
    public static string? SaveRefusal(WiredBoxDescriptor descriptor, WiredNativeEditorConfiguration native) =>
        Supports(descriptor.CanonicalName) ? WiredTextAddonRules.Refusal(descriptor.CanonicalName, native) : null;

    /// <summary>
    /// A save the hotel drops without any reply: no error and no save success. Observed on AIR WIN63-202610131318-860371703:
    /// Join Team with team 9, and Give Score To Team with team 9 (observed on AIR WIN63-202610151745-318700621).
    /// </summary>
    public static bool DroppedWithoutReply(WiredBoxDescriptor descriptor, WiredNativeEditorConfiguration native) =>
        descriptor.CanonicalName switch
        {
            "wf_act_join_team" => native.OwnedIntParams is [< 1 or > 4, _],
            "wf_act_give_score_tm" => native.OwnedIntParams is [_, _, < 1 or > 4],
            // Observed on AIR WIN63-202610201032-107964540: an unknown user source, or more than 20 picked furni.
            WiredFurniVisualRules.Highlight or WiredFurniVisualRules.Visibility => WiredFurniVisualRules.DroppedWithoutReply(native),
            _ => WiredEditorStoreRules.DroppedWithoutReply(descriptor.CanonicalName, native)
        };

    /// <summary>
    /// The frame as the hotel sends it. move_rotate ends with a hotel-owned int that is 1 until the first save and 0 after it,
    /// whatever was saved (r92k r92h-hidden-ints: [0,0,1] initially, every reopen ...,0). match_to_sshot sends only its four
    /// option ints: AIR WIN63-202610131318-860371703 wired_setup/actiontypes/class_4247.as:37-67 (code 3) reads and returns exactly
    /// four. A saved match_to_sshot frame carries its snapshot as text, one "id,state,rotation,x,y,altitude" entry per
    /// furni with N for each part the saved options do not restore (r92l r92l-sshot-hidden: [0,0,0,0] gives "id,N,N,N,N,N",
    /// [1,1,1,1] gives "id,0,0,8,2,0").
    /// </summary>
    /// <summary>
    /// A save may echo the frame's trailing hotel-owned int (see <see cref="HotelFrame"/>); the hotel accepts it and keeps only the box's
    /// own fields. Observed on hhus (AIR WIN63-202610131318-860371703): move_rotate saved with [0,0,1] and match_to_sshot with
    /// [0,0,0,0,1] are accepted.
    /// </summary>
    public static WiredNativeEditorConfiguration WithoutHotelInt(string name, WiredNativeEditorConfiguration native)
    {
        if (name is not ("wf_act_move_rotate" or "wf_act_match_to_sshot") || !Supports(name)) {
            return native;
        }

        var owned = Metadata(name).OwnedDefaults.Length;
        var p = native.OwnedIntParams;

        return p.Length == owned + 1 && p[^1] is 0 or 1 ? native with { OwnedIntParams = p[..owned] } : native;
    }

    public static WiredNativeEditorConfiguration HotelFrame(WiredBoxDescriptor descriptor, WiredNativeEditorConfiguration native)
    {
        var name = descriptor.CanonicalName;

        if (name is "wf_cnd_match_snapshot" or "wf_cnd_not_match_snap" or "wf_trg_stuff_state" && Supports(name)) {
            var p = native.OwnedIntParams;
            bool Ticked(int index) => index < p.Length && p[index] == 1;

            // Observed on AIR WIN63-202610201032-107964540: Stuff State lists the state when its one option is 1 and nothing else.
            return native.SavedState.Snapshots.IsDefaultOrEmpty ? native : native with
            {
                Text = SnapshotText(native.SavedState.Snapshots, name == "wf_trg_stuff_state"
                    ? new(Ticked(0), false, false, false) : new(Ticked(0), Ticked(1), Ticked(2), Ticked(3)))
            };
        }

        if (name is not ("wf_act_move_rotate" or "wf_act_match_to_sshot") || !Supports(name)) {
            return native;
        }

        var defaults = Metadata(name).OwnedDefaults;
        var fresh = native.PrimaryItems.IsEmpty && native.SavedState.Snapshots.IsEmpty && native.OwnedIntParams.SequenceEqual(defaults);
        var frame = name == "wf_act_match_to_sshot"
            ? native
            : native with { OwnedIntParams = [.. native.OwnedIntParams, fresh ? 1 : 0] };

        if (name == "wf_act_match_to_sshot" && !fresh && native.OwnedIntParams.Length == 4) {
            var p = native.OwnedIntParams;
            string Part(bool kept, string value) => kept ? value : "N";
            frame = frame with
            {
                Text = string.Join(';', native.SavedState.Snapshots.Where(entry => entry.Wall == null).OrderByDescending(entry => entry.ItemId)
                    .Select(entry => string.Join(',', entry.ItemId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Part(p[0] == 1, entry.State.Length == 0 ? "0" : entry.State),
                        Part(p[1] == 1, entry.Rotation.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Part(p[2] == 1, entry.X.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Part(p[2] == 1, entry.Y.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                        Part(p[3] == 1, entry.Z.ToString(System.Globalization.CultureInfo.InvariantCulture)))))
            };
        }

        return frame;
    }

    /// <summary>
    /// The snapshot conditions' saved text. Observed on hhus (AIR WIN63-202610131318-860371703): a wall torch at `:w=5,0 l=8,40 r`
    /// saved with state matching only is stored as `-2147418112,0,5,0,8,247,r`: the negated wall id, the state, the wall tile, the
    /// offset along the wall, the altitude in hundredths and the side, with no part masked by the unticked options. Floor entries are
    /// id,state,rotation,x,y,altitude in hundredths, highest id first: a colour tile at state 3 on (8,11), a block turned to 2 on (9,11)
    /// and a block turned to 4 stacked on it saved `2147418155,0,4,9,11,100;2147418149,3,0,8,11,0;2147418148,0,2,9,11,0`. The AIR editor
    /// never reads the text (class_4293.as:49-71 reads and writes only the four option ints).
    /// </summary>
    internal static string SnapshotText(IEnumerable<WiredFurniSnapshot> snapshots) => SnapshotText(snapshots, new(true, true, true, true));

    /// <summary>The fields of a snapshot entry an option keeps. Observed on AIR WIN63-202610201032-107964540: a floor entry writes N for each field its option leaves unticked.</summary>
    internal readonly record struct SnapshotFields(bool State, bool Rotation, bool Position, bool Altitude);

    internal static string SnapshotText(IEnumerable<WiredFurniSnapshot> snapshots, SnapshotFields kept)
    {
        var invariant = System.Globalization.CultureInfo.InvariantCulture;
        string Part(bool keep, string value) => keep ? value : "N";

        return string.Join(';', snapshots.Where(entry => entry.Wall == null).OrderByDescending(entry => entry.ItemId)
            .Select(entry => string.Join(',', entry.ItemId.ToString(invariant), Part(kept.State, entry.State.Length == 0 ? "0" : entry.State),
                Part(kept.Rotation, entry.Rotation.ToString(invariant)), Part(kept.Position, entry.X.ToString(invariant)),
                Part(kept.Position, entry.Y.ToString(invariant)),
                Part(kept.Altitude, ((long)Math.Round(entry.Z * 100, MidpointRounding.AwayFromZero)).ToString(invariant))))
            .Concat(snapshots.Where(entry => entry.Wall != null).OrderBy(entry => entry.ItemId).Select(entry =>
            {
                var wall = entry.Wall!;
                var altitude = wall.NativeAltitude ?? wall.CapturedAltitudeHundredths;

                return string.Join(',', (-(long)entry.ItemId).ToString(invariant), entry.State.Length == 0 ? "0" : entry.State,
                    wall.TileX.ToString(invariant), wall.TileY.ToString(invariant), wall.LocalX.ToString(invariant),
                    altitude?.ToString(invariant) ?? wall.PixelY.ToString(invariant), wall.Left ? "l" : "r");
            })));
    }

    /// <summary>
    /// The default int list the frame advertises. move_rotate and match_to_sshot end it with the hotel-owned int at its unsaved value 1,
    /// as observed on hhus (AIR WIN63-202610131318-860371703): [0,0,1] and [0,0,0,0,1]. The editors never read it (class_4131.as:29-38
    /// reads two ints, class_4247.as:45-67 four), so resetting to these defaults saves the same fields as before.
    /// </summary>
    public static ImmutableArray<int> FrameDefaults(string name, WiredNativeEditorMetadata metadata) =>
        name is "wf_act_move_rotate" or "wf_act_match_to_sshot" ? [.. metadata.OwnedDefaults, 1] : metadata.OwnedDefaults;

    public const string SourceCountKey = "wiredfurni.error.invalid_furni_source_count";
    public const string UserSourceCountKey = "wiredfurni.error.invalid_user_source_count";

    /// <summary>
    /// Furni source slots as the hotel stores them. Observed (AIR WIN63-202610131318-860371703): r92g r92-signals saved send_signal
    /// with one furni source [100] and the hotel stored [100, 200], the second slot keeping its default; r92f r92-valid-moves saved
    /// a furni source on a box without furni slots and got this count key (frame 20502).
    /// </summary>
    public static bool TryFillSourceSlots(WiredBoxDescriptor descriptor, WiredNativeEditorConfiguration native,
        out WiredNativeEditorConfiguration filled, out string? error)
    {
        filled = native;
        error = null;

        if (!Supports(descriptor.CanonicalName) || native.FurniSourceTypes.IsDefault) {
            return true;
        }

        var metadata = Metadata(descriptor.CanonicalName);

        // Observed on AIR WIN63-202610201032-107964540: Set Furni Highlight, Set Furni Visibility and Bot Reaches Furni keep the default
        // for a user source slot that is left out, and refuse an extra slot with its own key.
        if ((WiredFurniVisualRules.Applies(descriptor.CanonicalName) || descriptor.CanonicalName == "wf_trg_bot_reached_stf")
            && native.UserSourceTypes.Length != metadata.UsersAllowed.Length) {
            if (native.UserSourceTypes.Length > metadata.UsersAllowed.Length) {
                error = WiredFurniVisualRules.UserSourceCountKey;

                return false;
            }

            filled = native with { UserSourceTypes = [.. native.UserSourceTypes, .. metadata.UserDefaults.Skip(native.UserSourceTypes.Length)] };
            native = filled;
        }

        // Observed on AIR WIN63-202610201032-107964540: these boxes read an empty user source list as the frame default.
        if (WiredSaveLeniency.FillsEmptyUserSources(descriptor.CanonicalName) && native.UserSourceTypes.IsEmpty && metadata.UsersAllowed.Length > 0) {
            filled = native with { UserSourceTypes = metadata.UserDefaults };
            native = filled;
        }

        // Observed on AIR WIN63-202610201032-107964540: the projectile keeps the frame default for a user source slot that is left out.
        if (descriptor.CanonicalName == "wf_xtra_rotate_to_dir" && !native.UserSourceTypes.IsDefault
            && native.UserSourceTypes.Length < metadata.UsersAllowed.Length) {
            filled = native with { UserSourceTypes = [.. native.UserSourceTypes, .. metadata.UserDefaults.Skip(native.UserSourceTypes.Length)] };
            native = filled;
        }

        var slots = metadata.FurniAllowed.Length;
        var given = native.FurniSourceTypes.Length;

        // Observed on AIR WIN63-202610201032-107964540: a user source on a box without a user selector is refused with this key.
        if (metadata.UsersAllowed.Length == 0 && !native.UserSourceTypes.IsDefaultOrEmpty) {
            error = UserSourceCountKey;

            return false;
        }

        if (metadata.LegacySlots > 0 && given == metadata.LegacySlots || given == slots) {
            return true;
        }

        if (given > slots) {
            error = SourceCountKey;

            return false;
        }

        filled = native with { FurniSourceTypes = [.. native.FurniSourceTypes, .. metadata.FurniDefaults.Skip(given).Take(slots - given)] };

        return filled.FurniSourceTypes.Length == slots;
    }

    /// <summary>Characters of a Says Something keyword the hotel keeps.</summary>
    public const int SaysKeywordLength = 200;

    public static bool TryCompile(uint itemId, WiredBoxDescriptor descriptor, WiredNativeEditorConfiguration native,
        out WiredConfiguration runtime, ImmutableArray<int> triggerContext = default, bool storedRow = false)
    {
        runtime = new();
        var name = descriptor.CanonicalName;
        native = WithoutHotelInt(name, native);

        // Observed on AIR WIN63-202610201032-107964540: these boxes store a save in a normal form instead of refusing most odd values.
        if (Supports(name)) {
            native = WiredEditorStoreRules.PadVariables(name, native, Metadata(name));
        }

        if (WiredEditorStoreRules.Applies(name) && Supports(name)) {
            native = WiredEditorStoreRules.Normalize(name, native, Metadata(name).OwnedDefaults);

            if (!storedRow && (WiredEditorStoreRules.ChestSlotRefused(name, native)
                || WiredEditorStoreRules.PlaceholderRefusal(name, native, Metadata(name)) is not null)) {
                return false;
            }

            // A new save keeps its text in the stored form; a stored row is read as it is.
            if (!storedRow) {
                native = WiredEditorStoreRules.NormalizeNewSave(name, native);
            }
        }

        // Observed on AIR WIN63-202610201032-107964540: Set Furni Highlight and Set Furni Visibility store their ints in a normal form.
        if (WiredFurniVisualRules.Applies(name) && Supports(name)) {
            native = WiredFurniVisualRules.Normalize(name, native, Metadata(name).OwnedDefaults);

            // Only a new save is held to the values and the delay the editor offers. A stored row keeps loading with the wider bounds
            // it was saved under.
            if (!storedRow && SourceRejectionKey(descriptor, native) is not null) {
                return false;
            }
        }

        // Observed on AIR WIN63-202610201032-107964540: these boxes store a save in a normal form instead of refusing odd values.
        if (WiredConditionStoreRules.Applies(name) && Supports(name)) {
            native = WiredConditionStoreRules.Normalize(name, native, Metadata(name).OwnedDefaults);
        }

        // Observed on AIR WIN63-202610201032-107964540: a text and ints a box does not read are accepted and dropped. Only a new save is
        // fitted; a stored row loads in the form it was saved in.
        if (!storedRow && WiredSaveLeniency.Applies(name) && Supports(name)) {
            native = WiredSaveLeniency.Normalize(name, native);
        }

        // Observed on AIR WIN63-202610201032-107964540: Room Initialized has no fields; ints and a text in a save are accepted and dropped.
        if (name == "wf_trg_room_initializes" && Supports(name)) {
            native = native with { OwnedIntParams = [], Text = "" };
        }

        // Observed on AIR WIN63-202610201032-107964540: these triggers store a save in a normal form instead of refusing odd values.
        if (WiredTriggerStoreRules.Applies(name) && Supports(name)) {
            native = WiredTriggerStoreRules.Normalize(name, native, Metadata(name).OwnedDefaults);
        }

        // Observed on AIR WIN63-202610201032-107964540: these selectors store a save in a normal form instead of refusing odd values.
        if (!storedRow && WiredSelectorStoreRules.Applies(name) && Supports(name)) {
            native = WiredSelectorStoreRules.Normalize(name, native, Metadata(name).OwnedDefaults);
        }

        // The board keeps its text padded and with unused slots emptied, and reads it back that way (see WiredLeaderboardConfig.Normalize).
        // Observed on AIR WIN63-202610151745-318700621: the area selectors store each of x, y, width and height clamped to 0..64
        // (-5 reads back 0, 500 reads back 64) instead of refusing the save.
        if (name is "wf_slc_furni_area" or "wf_slc_users_area" && native.OwnedIntParams.Length == 4) {
            native = native with { OwnedIntParams = [.. native.OwnedIntParams.Select(value => Math.Clamp(value, 0, MaxSelectorAreaCoordinate))] };
        }

        // Observed on AIR WIN63-202610151745-318700621: Give Score and Give Score To Team save a per-game limit above 10 and read it
        // back as 10 ([5, 99] and [5, 11] read back [5, 10]; [5, 99, 1] reads back [5, 10, 1]). The Give Score editor never sends more
        // than 10: its limit slider runs 1 to 11, and 11 means unlimited and is sent as 0 (GiveScore.as in AIR WIN63-202610131318-860371703).
        if (name is "wf_act_give_score" or "wf_act_give_score_tm" && native.OwnedIntParams.Length >= 2 && native.OwnedIntParams[1] > 10) {
            native = native with { OwnedIntParams = native.OwnedIntParams.SetItem(1, 10) };
        }

        // Observed on AIR WIN63-202610151745-318700621: a variable FX box reads back its stored form, not what the editor sent (a boss bar
        // saved with colour, width and renderer -1 and a stale range target reads back 5/4/100 and its own source).
        if (Variables.Fx.WiredVariableFxSettings.Category(name) >= 0) {
            native = native with { OwnedIntParams = Variables.Fx.WiredVariableFxSettings.Normalize(name, native.OwnedIntParams) };
        }

        // Observed on AIR WIN63-202610151745-318700621: Users By Name stores its names lowercased ("Robbie" reads back "robbie").
        if (name == "wf_slc_users_byname") {
            native = native with { Text = native.Text.ToLowerInvariant() };
        }

        if (name == "wf_xtra_var_leaderboard") {
            native = native with { Text = Leaderboard.WiredLeaderboardConfig.Normalize(native.Text, native.OwnedIntParams) };
        }

        // The snapshot boxes' text is the hotel's listing of the saved snapshot (see HotelFrame); an editor saves it back unchanged and the
        // snapshot is captured again from the room. Observed on AIR WIN63-202610151745-318700621 (r97_sshot_resave_plain): re-saving an
        // opened Match Snapshot succeeds.
        if (name is "wf_act_match_to_sshot" or "wf_cnd_match_snapshot" or "wf_cnd_not_match_snap") {
            native = native with { Text = "" };
        }

        // "bot TAB message" is split the hotel's way: a lone field is the message (see WiredBotActions.TalkText).
        if (name is "wf_act_bot_talk" or "wf_act_bot_talk_to_avatar") {
            native = native with { Text = WiredBotActions.TalkText(native.Text) };
        }

        // Observed on AIR WIN63-202610151745-318700621 (r97_tc_store): the connector is stored as its valid "number=text" lines only, one per
        // number with its last text, in ascending number order ("9=a 3=b -2=neg 20=c" reads back "-2=neg 3=b 9=a 20=c").
        if (name == "wf_xtra_var_text_connector") {
            native = native with { Text = Variables.WiredVariableMetadataBox.NormalizeConnector(native.Text) };
        }

        // Observed on AIR WIN63-202610201032-107964540: the capturer stores a save in a normal form. A stored row keeps the form it was
        // saved in.
        if (storedRow) {
            native = WiredTextAddonRules.StoredRow(name, native);
        }
        else if (WiredTextAddonRules.Applies(name) && Supports(name)) {
            native = WiredTextAddonRules.StoredForm(name, native, Metadata(name).OwnedDefaults);
        }

        native = WiredNativeAuxiliaryEditor.StoredForm(name, native);

        if (itemId == 0 || !Supports(name) || native.Version != 2 || native.Category != descriptor.Category
            || native.NativeCode != Code(name) || !WithinBounds(native)) {
            return false;
        }

        var metadata = WithTriggerContext(Metadata(name), triggerContext);

        var legacy = metadata.LegacySlots > 0 && native.FurniSourceTypes.Length == metadata.LegacySlots
            && native.UserSourceTypes.Length == metadata.LegacySlots;

        if (!legacy && (native.FurniSourceTypes.Length != metadata.FurniAllowed.Length
            || native.UserSourceTypes.Length != metadata.UsersAllowed.Length)
            // A stored row passed the source lists that applied when it was saved; the lists follow the official editors and may narrow later.
            || !storedRow && native.FurniSourceTypes.Where((source, index) => !metadata.FurniAllowed[index].Contains(source)).Any()
            || !storedRow && native.UserSourceTypes.Where((source, index) => !metadata.UsersAllowed[index].Contains(source)).Any()
            // Likewise a stored row keeps the wall picks it was saved with when a card later stops allowing them.
            || !storedRow && !metadata.AllowWall && native.PrimaryItems.Concat(native.SecondaryItems).Any(item => item.Wall)
            || BuiltIn.Contains(name) && native.VariableIds.Length != 0) {
            return false;
        }

        if (!BuiltIn.Contains(name)) {
            if (!TryCompileCategory(descriptor, native, out var derivedCategory) || !WiredLegacyProtocol.IsWithinLimits(derivedCategory)) {
                return false;
            }

            runtime = derivedCategory.Bind(new(itemId, name, derivedCategory, native));

            return true;
        }

        var p = native.OwnedIntParams;
        var furni = ImmutableDictionary.CreateBuilder<string, int>();
        var users = ImmutableDictionary.CreateBuilder<string, int>();
        ImmutableArray<int> parameters;
        var text = native.Text;
        int? quota = null;

        switch (name) {
            case "wf_trg_says_something":
                if (p.Length != 3 || p[0] is < 0 or > 1 || p[1] is < 0 or > 2 || p[2] is < 0 or > 1
                    || native.Text.Length > 1000) {
                    return false;
                }

                // Observed on hhus (AIR WIN63-202610131318-860371703): a longer keyword saves, but only its first 200 characters are kept.
                if (text.Length > SaysKeywordLength) {
                    text = text[..SaysKeywordLength];
                    native = native with { Text = text };
                }

                parameters = [p[1], p[2], p[0]];
                break;
            case "wf_trg_periodically":
                if (p.Length != 1 || p[0] is < 1 or > 120 || native.Text != "") {
                    return false;
                }

                parameters = [p[0]];
                break;
            case "wf_cnd_user_count_in":
                if (p.Length != 2 || p.Any(value => value is < 0 or > 125) || p[0] > p[1]
                    || native.Text != "" || native.Quantifier != 0) {
                    return false;
                }

                parameters = [p[0], p[1], 0];
                users["users"] = 0;
                break;
            case "wf_act_teleport_to":
                if (p.Length != 1 || p[0] is < 0 or > 1 || native.Text != "") {
                    return false;
                }

                parameters = [p[0], native.FurniSourceTypes[0], native.UserSourceTypes[0]];
                furni["targets"] = native.FurniSourceTypes[0];
                users["users"] = native.UserSourceTypes[0];
                break;
            case "wf_trg_walks_on_furni":
                if (p.Length != 0 || native.Text != "") {
                    return false;
                }

                parameters = [native.FurniSourceTypes[0]];
                furni["items"] = native.FurniSourceTypes[0];
                break;
            case "wf_act_show_message":
                if (p.Length != 3 || p[0] is < 0 or > 1 || !ShowStyles.Contains(p[1]) || p[2] is < -1 or > 2
                    || native.Text.Length > ShowMessageMaxCharacters || native.Text.Replace("\r\n", "\n").Replace('\r', '\n').Count(c => c == '\n') >= 8) {
                    return false;
                }

                parameters = [native.UserSourceTypes[0], p[0], p[1], p[2]];
                users["users"] = native.UserSourceTypes[0];
                break;
            case "wf_cnd_furnis_hv_avtrs":
                // Observed on hhus (AIR WIN63-202610131318-860371703): quantifier 1 saves and reads back, though the editor shows none.
                if (p.Length != 1 || p[0] is < 0 or > 1 || native.Quantifier is not (0 or 1) || native.Text != "") {
                    return false;
                }

                parameters = [p[0], native.FurniSourceTypes[0]];
                furni["items"] = native.FurniSourceTypes[0];
                break;
            case "wf_act_move_rotate":
                if (p.Length != 2 || p[0] is < 0 or > 11 || p[1] is < 0 or > 3) {
                    return false;
                }

                if (!WiredMovementConfiguration.TryValidate(name,
                    new() { IntParams = [p[0], p[1], native.FurniSourceTypes[0]] }, out var movement, out _)) {
                    return false;
                }

                parameters = movement.IntParams;
                furni["movers"] = native.FurniSourceTypes[0];
                break;
            case "wf_act_move_to_dir":
                if (p.Length != 3 || p[0] is < 0 or > 7 || p[1] is < 0 or > 6 || p[2] is < 0 or > 1) {
                    return false;
                }

                parameters = [p[0], p[1], native.FurniSourceTypes[0], p[2]];
                furni["items"] = native.FurniSourceTypes[0];
                break;
            case "wf_act_control_clock":
                if (p.Length != 1 || p[0] is < 0 or > 4) {
                    return false;
                }

                parameters = [p[0], native.FurniSourceTypes[0]];
                furni["items"] = native.FurniSourceTypes[0];
                break;
            case "wf_act_join_team":
                if (p.Length != 2 || p[0] is < 1 or > 4 || p[1] is < 0 or > 2) {
                    return false;
                }

                parameters = [p[1], p[0], native.UserSourceTypes[0], 0];
                users["users"] = native.UserSourceTypes[0];
                break;
            case "wf_act_give_score":
                // The points slider runs 1 to 1000 and the radio flips the sign (GiveScore.as), so zero points cannot come from the editor.
                if (p.Length != 2 || p[0] is < -1000 or > 1000 or 0 || p[1] is < 0 or > 10) {
                    return false;
                }

                parameters = [Math.Abs(p[0]), p[0] < 0 ? 1 : 0, native.UserSourceTypes[0], p[1]];
                users["users"] = native.UserSourceTypes[0];
                quota = p[1] == 0 ? null : p[1];
                break;
            case "wf_act_set_altitude":
                if (p.Length != 2 || p[0] is < 0 or > 8000 || p[1] is < 0 or > 2) {
                    return false;
                }

                parameters = [p[1], native.FurniSourceTypes[0]];
                text = (p[0] / 100m).ToString(CultureInfo.InvariantCulture);
                furni["movers"] = native.FurniSourceTypes[0];
                break;
            case "wf_act_send_signal":
                if (p.Length != 2 || p[0] is < 0 or > 1 || p[1] is < 0 or > 1) {
                    return false;
                }

                parameters = [0, native.FurniSourceTypes[1], native.UserSourceTypes[0], p[0], p[1], 0];
                furni["items"] = native.FurniSourceTypes[0];
                furni["forwarded"] = native.FurniSourceTypes[1];
                users["users"] = native.UserSourceTypes[0];
                text = string.Join(';', native.SecondaryItems.Select(item => item.ItemId));
                break;
            case "wf_act_move_furni_as_group":
                if (p.Length != 3 || p[0] is < 0 or > 1 || p[1] is < -64 or > 64 || p[2] is < -64 or > 64) {
                    return false;
                }

                parameters = [p[0], p[1], p[2], native.FurniSourceTypes[0], native.FurniSourceTypes[1], native.UserSourceTypes[0]];
                furni["movers"] = native.FurniSourceTypes[0];
                furni["targets"] = native.FurniSourceTypes[1];
                users["users"] = native.UserSourceTypes[0];
                text = string.Join(';', native.SecondaryItems.Select(item => item.ItemId));
                break;
            default:
                return false;
        }

        var derived = new WiredConfiguration
        {
            IntParams = parameters,
            Text = text,
            SelectedItems = native.PrimaryItems.Select(item => item.ItemId).ToImmutableArray(),
            SecondarySelectedItems = native.SecondaryItems.Select(item => item.ItemId).ToImmutableArray(),
            VariableIds = native.VariableIds,
            FurniSources = furni.ToImmutable(),
            UserSources = users.ToImmutable(),
            Delay = native.Delay ?? 0,
            ScoreQuotaPerGame = quota,
            Snapshots = native.SavedState.Snapshots
        };

        if (!WiredLegacyProtocol.IsWithinLimits(derived)) {
            return false;
        }

        runtime = derived.Bind(new(itemId, name, derived, native));

        return true;
    }

    public static bool WithinBounds(WiredNativeEditorConfiguration native) => native.Version == 2
        && !native.OwnedIntParams.IsDefault && native.OwnedIntParams.Length <= WiredConfigurationLimits.IntParams
        && native.Text != null && native.Text.Length <= WiredConfigurationLimits.TextCharacters
        && System.Text.Encoding.UTF8.GetByteCount(native.Text) <= ushort.MaxValue
        && !native.PrimaryItems.IsDefault && !native.SecondaryItems.IsDefault
        && native.PrimaryItems.Length <= WiredConfigurationLimits.SelectedItems && native.SecondaryItems.Length <= WiredConfigurationLimits.SelectedItems
        && native.PrimaryItems.Concat(native.SecondaryItems).All(item => item != null && (item.ItemId is > 0 and <= int.MaxValue
            || item.Wall && TemporaryItemIds.IsWallIdentity(item.ItemId)))
        && native.PrimaryItems.Distinct().Count() == native.PrimaryItems.Length
        && native.SecondaryItems.Distinct().Count() == native.SecondaryItems.Length
        && !native.FurniSourceTypes.IsDefault && native.FurniSourceTypes.Length <= WiredConfigurationLimits.IntParams
        && !native.UserSourceTypes.IsDefault && native.UserSourceTypes.Length <= WiredConfigurationLimits.IntParams
        && !native.VariableIds.IsDefault && native.VariableIds.Length <= WiredConfigurationLimits.IntParams
        && native.VariableIds.All(token => token != null && token.Length <= 1024)
        && native.SavedState != null && !native.SavedState.Snapshots.IsDefault
        && (native.Category == WiredBoxCategory.Action ? native.Delay is int delay && WiredConfigurationLimits.IsDelay(delay) : native.Delay == null)
        && (native.Category == WiredBoxCategory.Condition ? native.Quantifier is 0 or 1 or 2 : native.Quantifier == null)
        && (native.Category == WiredBoxCategory.Selector ? native.Filter != null && native.Inverse != null : native.Filter == null && native.Inverse == null);

    internal static bool Matches(WiredConfiguration actual, WiredConfiguration expected) => actual.Version == expected.Version
        && actual.IntParams.SequenceEqual(expected.IntParams) && actual.Text == expected.Text
        && actual.SelectedItems.SequenceEqual(expected.SelectedItems) && actual.SecondarySelectedItems.SequenceEqual(expected.SecondarySelectedItems)
        && actual.VariableIds.SequenceEqual(expected.VariableIds) && actual.Snapshots.SequenceEqual(expected.Snapshots)
        && actual.Delay == expected.Delay && actual.SelectionCode == expected.SelectionCode
        && actual.ScoreQuotaPerGame == expected.ScoreQuotaPerGame && actual.TemporaryPlacement == expected.TemporaryPlacement
        && actual.FurniSources.Count == expected.FurniSources.Count && actual.FurniSources.All(pair => expected.FurniSources.TryGetValue(pair.Key, out var value) && value == pair.Value)
        && actual.UserSources.Count == expected.UserSources.Count && actual.UserSources.All(pair => expected.UserSources.TryGetValue(pair.Key, out var value) && value == pair.Value);

    public static bool IsBound(uint itemId, WiredBoxDescriptor descriptor, WiredConfiguration runtime) =>
        runtime.Origin is { Native: not null } origin && origin.ItemId == itemId && origin.Name == descriptor.CanonicalName
        && Matches(runtime, origin.Derived)
        && TryCompile(itemId, descriptor, origin.Native, out var derived, storedRow: true) && Matches(runtime, derived);

    public static bool TryValidateRuntime(Item item, WiredBoxDescriptor descriptor, WiredConfiguration proposed,
        out WiredConfiguration validated, out string error)
    {
        validated = proposed;
        error = "Invalid native editor authority.";

        if (!IsBound(item.Id, descriptor, proposed)) {
            return false;
        }

        error = "";

        return true;
    }

    private const string ClickTileInteraction = "room_invisible_click_tile";

    internal static bool TryValidatePicks(Item item, WiredBoxDescriptor descriptor, WiredConfiguration proposed, out string error)
    {
        error = "Invalid native editor picks.";
        var native = proposed.Origin!.Native;
        var room = item.GetRoom();
        // Save-time picks must be live. Loading and execution resolve the remaining eligible items instead.
        var references = native.PrimaryItems.Concat(native.SecondaryItems).ToArray();

        // Picks are only meaningful in the live room that holds them; a pickless box has nothing to check there.
        if (references.Length == 0) {
            error = "";

            return true;
        }

        if (room == null || references.Any(reference =>
            room.GetRoomItemHandler().GetItem(reference.ItemId) is not { IsTemporary: false } picked || picked.IsWallItem != reference.Wall)) {
            return false;
        }

        // Observed on hhus (WIN63-202610131318): saving a click-tile trigger that picks a furni which is not an invisible click tile
        // is refused with wiredfurni.error.require_click_tiles and no parameters.
        // Observed on AIR WIN63-202610201032-107964540: the picks are only checked when they are the source; with the selector as source
        // a plain furni pick saves.
        if (descriptor.CanonicalName == "wf_trg_click_tile" && (native.FurniSourceTypes.IsDefaultOrEmpty || native.FurniSourceTypes[0] == 100)
            && references.Any(reference =>
            room.GetRoomItemHandler().GetItem(reference.ItemId)?.Definition.InteractionName != ClickTileInteraction)) {
            error = "wiredfurni.error.require_click_tiles";

            return false;
        }

        // Teleport to Room only forwards through WIRED Room Linkers; the client text for this key (WIN63-202610131318) is
        // "Can only select wired room linkers!". Observed on AIR WIN63-202610201032-107964540: the picks are only checked when the picked
        // furni are the source; a plain furni picked with the triggering furni as source saves.
        if (descriptor.CanonicalName == "wf_act_teleport_to_room" && (native.FurniSourceTypes.IsDefaultOrEmpty || native.FurniSourceTypes[0] == 100)
            && native.PrimaryItems.Any(reference =>
            room.GetRoomItemHandler().GetItem(reference.ItemId)?.Definition.InteractionType != InteractionType.WiredRoomLinker)) {
            error = "wiredfurni.error.require_room_linker";

            return false;
        }

        // The AIR client (WIN63-202610131318) never filters furni picks itself (UserDefinedRoomEventsCtrl.stuffSelected only checks
        // the wall flag and the furni limit); these keys exist only as server refusals shown on save.
        var picksSelected = native.FurniSourceTypes.IsDefaultOrEmpty || native.FurniSourceTypes[0] == 100;

        // Counter boxes drive Wired counters and game timers; client text: "Can only select counter furni."
        if (descriptor.CanonicalName is "wf_act_control_clock" or "wf_act_adjust_clock" or "wf_trg_clock_counter" or "wf_cnd_counter_time_matches"
            && picksSelected && native.PrimaryItems.Any(reference =>
                room.GetRoomItemHandler().GetItem(reference.ItemId) is not { } picked || !WiredCounterController.Recognizes(picked))) {
            error = "wiredfurni.error.require_counter_furni";

            return false;
        }

        // OR evaluation scopes picked conditions and add-ons; client text: "Can only select WIRED Conditions and Add-Ons!"
        if (descriptor.CanonicalName == "wf_xtra_or_eval" && picksSelected && native.PrimaryItems.Any(reference =>
            room.GetRoomItemHandler().GetItem(reference.ItemId)?.Definition.WiredDescriptor is not { Category: WiredBoxCategory.Condition or WiredBoxCategory.Addon })) {
            error = "wiredfurni.error.condition_evaluation_furni";

            return false;
        }

        bool AllAntennas(IEnumerable<WiredNativeItemReference> picks) =>
            picks.All(reference => WiredStackEngine.IsSignalAntenna(room.GetRoomItemHandler().GetItem(reference.ItemId)!));

        if (descriptor.CanonicalName is "wf_act_send_signal" or "wf_act_neg_send_signal" && native.FurniSourceTypes[0] is 100 or 101
            && !AllAntennas(native.FurniSourceTypes[0] == 100 ? native.PrimaryItems : native.SecondaryItems)) {
            error = "wiredfurni.error.require_antenna_furni";

            return false;
        }

        if (descriptor.CanonicalName == "wf_trg_recv_signal" && proposed.FurniSources.GetValueOrDefault("items") == WiredSources.Selected
            && !AllAntennas(native.PrimaryItems)) {
            error = "wiredfurni.error.require_antenna_furni";

            return false;
        }

        error = "";

        return true;
    }

    /// <summary>The editor record every card starts from: category metadata defaults, valid by construction.</summary>
    public static WiredNativeEditorConfiguration DefaultNative(WiredBoxDescriptor descriptor, int? calendarYear = null)
    {
        var metadata = Metadata(descriptor.CanonicalName);
        var owned = metadata.OwnedDefaults;

        // The date condition's default window is the room clock's current year; the caller reads that clock once.
        if (calendarYear is { } year && descriptor.CanonicalName == "wf_cnd_match_date" && owned.Length >= 2) {
            owned = owned.SetItem(owned.Length - 2, year).SetItem(owned.Length - 1, year);
        }

        return new()
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = owned,
            FurniSourceTypes = metadata.FurniDefaults,
            UserSourceTypes = metadata.UserDefaults,
            VariableIds = metadata.VariableDefaults,
            Delay = descriptor.Category == WiredBoxCategory.Action ? 0 : null,
            Quantifier = descriptor.Category == WiredBoxCategory.Condition ? 0 : null,
            Filter = descriptor.Category == WiredBoxCategory.Selector ? false : null,
            Inverse = descriptor.Category == WiredBoxCategory.Selector ? false : null
        };
    }

    /// <summary>The compiled default runtime for a fresh card, or null when the name has no native mapping yet.</summary>
    public static WiredConfiguration? DefaultRuntime(uint itemId, WiredBoxDescriptor descriptor, int? calendarYear = null) =>
        Supports(descriptor.CanonicalName) && TryCompile(itemId, descriptor, DefaultNative(descriptor, calendarYear), out var runtime) ? runtime : null;

    /// <summary>The editor record behind a bound runtime; an unbound runtime has no editor representation.</summary>
    public static bool TryProject(Item item, WiredBoxDescriptor descriptor, WiredConfiguration runtime,
        out WiredNativeEditorConfiguration native)
    {
        native = new();

        if (!Supports(descriptor.CanonicalName) || !IsBound(item.Id, descriptor, runtime)) {
            return false;
        }

        native = runtime.Origin!.Native;

        return true;
    }

    public static int SourceForRole(WiredConfiguration runtime, string role) => runtime.FurniSources.GetValueOrDefault(role);

    public static bool UsesSecondary(WiredConfiguration runtime, string role, bool legacySecondary) => runtime.Origin?.Native != null
        ? SourceForRole(runtime, role) == 101 : legacySecondary;
}
