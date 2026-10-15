using System.Collections.Frozen;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>
/// How a wired card's behaviour is known. AirSourceOnly cards come from the October AIR client code alone and need a live check.
/// TurboReferenceOnly cards have a runtime read from the turbo-cloud emulator (facts only) and no official runtime observation.
/// </summary>
public enum WiredEvidenceLevel
{
    Observed,
    AirSourceOnly,
    TurboReferenceOnly
}

public sealed record WiredEvidence(WiredEvidenceLevel Level, string Source, string Reason);

/// <summary>Single source of truth for cards whose behaviour has no runtime observation; every other card is Observed.</summary>
public static class WiredEvidenceRegistry
{
    private const string NoCatalogue = "No catalogue offer on hhus (offerid -1); editor read from the AIR client, server semantics inferred from the editor fields.";
    private const string Conditions = "wired_setup/conditions/";
    private const string Variables = "wired_setup/variables/";
    private const string Actions = "wired_setup/actiontypes/";
    private const string Triggers = "wired_setup/triggerconfs/";
    private const string Addons = "wired_setup/addons/";
    private const string OctoberCards = "Editor read from the AIR client (WIN63-202610131318-860371703); no live observation yet, runtime inferred from the editor.";
    private const string OctoberUnsold = "No furni on hhus; editor read from the AIR client (WIN63-202610131318-860371703), runtime inferred from the editor.";

    public static readonly FrozenDictionary<string, WiredEvidence> AirSourceOnly = new Dictionary<string, WiredEvidence>(StringComparer.OrdinalIgnoreCase)
    {
        // AS3 wired_setup/conditions/class_4263.as (badge code, no ints)
        ["wf_cnd_wearing_badge"] = new(WiredEvidenceLevel.AirSourceOnly, Conditions + "class_4263.as", NoCatalogue),
        // AS3 wired_setup/conditions/class_4227.as (effect id int)
        ["wf_cnd_wearing_effect"] = new(WiredEvidenceLevel.AirSourceOnly, Conditions + "class_4227.as", NoCatalogue),
        // AS3 wired_setup/conditions/class_4227.as (negative)
        ["wf_cnd_not_wearing_fx"] = new(WiredEvidenceLevel.AirSourceOnly, Conditions + "class_4227.as", NoCatalogue),
        // AS3 wired_setup/conditions/class_4263.as (negative)
        ["wf_cnd_not_wearing_b"] = new(WiredEvidenceLevel.AirSourceOnly, Conditions + "class_4263.as", NoCatalogue),
        // AS3 wired_setup/variables/QuestVariable.as
        ["wf_var_quest"] = new(WiredEvidenceLevel.AirSourceOnly, Variables + "QuestVariable.as", NoCatalogue),
        // AS3 wired_setup/variables/QuestChainVariable.as
        ["wf_var_quest_chain"] = new(WiredEvidenceLevel.AirSourceOnly, Variables + "QuestChainVariable.as", NoCatalogue),
        // The October boxes of the r88 dossier: editor grammar read from WIN63-202610131318-860371703, runtime inferred from it.
        ["wf_act_progress_ach"] = new(WiredEvidenceLevel.AirSourceOnly, Actions + "ProgressAchievement.as", OctoberCards),
        ["wf_act_give_effect"] = new(WiredEvidenceLevel.AirSourceOnly, Actions + "class_3931.as", OctoberUnsold),
        ["wf_act_override_height"] = new(WiredEvidenceLevel.AirSourceOnly, Actions + "class_4092.as", OctoberUnsold),
        ["wf_act_progress_reward_track"] = new(WiredEvidenceLevel.AirSourceOnly, Actions + "ProgressRewardTrack.as", OctoberUnsold),
        ["wf_act_reset_reward_track"] = new(WiredEvidenceLevel.AirSourceOnly, Actions + "class_4288.as", OctoberUnsold),
        // The incoming furni transparency/highlight payloads are read from the client parsers class_2988 and class_3488.
        ["wf_act_set_visibility"] = new(WiredEvidenceLevel.AirSourceOnly, Actions + "class_4285.as", OctoberCards),
        ["wf_act_set_highlight"] = new(WiredEvidenceLevel.AirSourceOnly, Actions + "class_4214.as", OctoberCards),
        // Link transport is server-only in the client: delivery between wf_link endpoints is inferred.
        ["wf_act_send_message"] = new(WiredEvidenceLevel.AirSourceOnly, Actions + "SendLinkMessage.as", OctoberCards),
        ["wf_act_send_dataframe"] = new(WiredEvidenceLevel.AirSourceOnly, Actions + "SendLinkDataframe.as", OctoberCards),
        ["wf_trg_room_initializes"] = new(WiredEvidenceLevel.AirSourceOnly, Triggers + "RoomInitialized.as", OctoberCards),
        ["wf_trg_receive_message"] = new(WiredEvidenceLevel.AirSourceOnly, Triggers + "ReceiveLinkMessage.as", OctoberCards),
        ["wf_trg_receive_dataframe"] = new(WiredEvidenceLevel.AirSourceOnly, Triggers + "ReceiveLinkDataframe.as", OctoberCards),
        ["wf_cnd_user_level"] = new(WiredEvidenceLevel.AirSourceOnly, Conditions + "LevelMatches.as", OctoberUnsold),
        ["wf_xtra_var_share"] = new(WiredEvidenceLevel.AirSourceOnly, Addons + "VariableAddonShareAddon.as", OctoberCards),
        ["wf_xtra_var_import"] = new(WiredEvidenceLevel.AirSourceOnly, Addons + "VariableAddonImportAddon.as", OctoberCards),
        ["wf_xtra_global_placeholder"] = new(WiredEvidenceLevel.AirSourceOnly, Addons + "GlobalPlaceholderAddon.as", OctoberUnsold),
        ["wf_xtra_achievement_enabler"] = new(WiredEvidenceLevel.AirSourceOnly, Addons + "class_4012.as", OctoberCards),
        ["wf_xtra_var_web_api"] = new(WiredEvidenceLevel.AirSourceOnly, Addons + "VariablesWebApiAddon.as", OctoberCards),
        ["wf_xtra_link_speaker"] = new(WiredEvidenceLevel.AirSourceOnly, Addons + "class_4123.as", OctoberCards),
        ["wf_var_dailytask"] = new(WiredEvidenceLevel.AirSourceOnly, Variables + "DailyTaskVariable.as", OctoberCards),
        ["wf_var_pointer"] = new(WiredEvidenceLevel.AirSourceOnly, Variables + "PointerVariable.as", OctoberCards)
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private const string BotCards = "Editor observed on hhus (r62-batch-02); runtime behaviour read from turbo-cloud, never fired on official.";
    private const string GameCards = "Editor observed on hhus (r62-batch-01); the game start and end sources read from turbo-cloud, never fired on official.";
    private const string AirEditorCards = "Editor read from the AIR client only (no catalogue offer on hhus); runtime read from turbo-cloud, never fired on official.";
    private const string RemainderCards = "Editor read from the AIR client (and observed on hhus where the card has an offer); runtime read from turbo-cloud, never fired on official.";

    public static readonly FrozenDictionary<string, WiredEvidence> TurboReferenceOnly = new Dictionary<string, WiredEvidence>(StringComparer.OrdinalIgnoreCase)
    {
        // AS3 actiontypes/class_4125.as; turbo WiredActionBotTeleport.cs
        ["wf_act_bot_teleport"] = new(WiredEvidenceLevel.TurboReferenceOnly, Actions + "class_4125.as", BotCards),
        // AS3 actiontypes/class_4124.as; turbo WiredActionBotMove.cs
        ["wf_act_bot_move"] = new(WiredEvidenceLevel.TurboReferenceOnly, Actions + "class_4124.as", BotCards),
        // AS3 actiontypes/class_4250.as; turbo WiredActionBotTalk.cs
        ["wf_act_bot_talk"] = new(WiredEvidenceLevel.TurboReferenceOnly, Actions + "class_4250.as", BotCards),
        // AS3 actiontypes/class_4184.as; turbo WiredActionBotTalkToAvatar.cs
        ["wf_act_bot_talk_to_avatar"] = new(WiredEvidenceLevel.TurboReferenceOnly, Actions + "class_4184.as", BotCards),
        // AS3 actiontypes/class_4307.as; turbo WiredActionBotFollowAvatar.cs
        ["wf_act_bot_follow_avatar"] = new(WiredEvidenceLevel.TurboReferenceOnly, Actions + "class_4307.as", BotCards),
        // AS3 actiontypes/class_4170.as; turbo WiredActionBotChangeFigure.cs
        ["wf_act_bot_clothes"] = new(WiredEvidenceLevel.TurboReferenceOnly, Actions + "class_4170.as", BotCards),
        // AS3 actiontypes/class_4031.as; turbo WiredActionBotGiveHandItem.cs
        ["wf_act_bot_give_handitem"] = new(WiredEvidenceLevel.TurboReferenceOnly, Actions + "class_4031.as", BotCards),
        // AS3 triggerconfs/class_4024.as; turbo WiredTriggerBotReachesHabbo.cs
        ["wf_trg_bot_reached_avtr"] = new(WiredEvidenceLevel.TurboReferenceOnly, Triggers + "class_4024.as", BotCards),
        // AS3 triggerconfs/class_4251.as; turbo WiredTriggerBotReachesItem.cs
        ["wf_trg_bot_reached_stf"] = new(WiredEvidenceLevel.TurboReferenceOnly, Triggers + "class_4251.as", BotCards),
        // AS3 triggerconfs/TriggerOnce.as (code 3, shared with wf_trg_at_given_time); turbo WiredTriggerAtTimeLong.cs
        ["wf_trg_at_time_long"] = new(WiredEvidenceLevel.TurboReferenceOnly, Triggers + "TriggerOnce.as", AirEditorCards),
        // AS3 actiontypes/class_4162.as; turbo WiredActionMoveFurniTo.cs
        ["wf_act_move_furni_to"] = new(WiredEvidenceLevel.TurboReferenceOnly, Actions + "class_4162.as", AirEditorCards),
        // AS3 triggerconfs/class_4226.as; turbo WiredTriggerTransactionCompleted.cs
        ["wf_trg_transaction_complete"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/triggerconfs/class_4226.as", RemainderCards),
        // AS3 conditions/chests/ChestHasAmount.as; turbo WiredConditionChestHasItems.cs
        ["wf_cnd_chest_has_items"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/conditions/chests/ChestHasAmount.as", RemainderCards),
        // AS3 conditions/chests/class_4163.as; turbo WiredConditionChestHasItemTypes.cs
        ["wf_cnd_chest_has_item_type"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/conditions/chests/class_4163.as", RemainderCards),
        // AS3 addons/chests/class_3960.as; turbo WiredAddonChestItemTypeScanner.cs
        ["wf_xtra_scan_chest_furni_by_type"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/chests/class_3960.as", RemainderCards),
        // AS3 actiontypes/chests/class_4174.as; turbo WiredActionGiveCurrencyFromChest.cs
        ["wf_act_give_currency"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/chests/class_4174.as", RemainderCards),
        // AS3 actiontypes/chests/class_4292.as; turbo WiredActionGiveFurniFromChest.cs
        ["wf_act_give_furni"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/chests/class_4292.as", RemainderCards),
        // AS3 actiontypes/class_4199.as; turbo WiredActionGiveReward.cs
        ["wf_act_give_reward"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/class_4199.as", RemainderCards),
        // AS3 actiontypes/class_4046.as; turbo WiredActionMuteTriggerer.cs
        ["wf_act_mute_triggerer"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/class_4046.as", RemainderCards),
        // AS3 actiontypes/class_3972.as; turbo WiredActionPlaceFurni.cs
        ["wf_act_place_furni"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/class_3972.as", RemainderCards),
        // AS3 actiontypes/class_4089.as; turbo WiredActionRemoveFurni.cs
        ["wf_act_remove_furni"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/class_4089.as", RemainderCards),
        // AS3 addons/VariableTimeUtilAddon.as; turbo WiredAddonVariableTimeUtil.cs
        ["wf_xtra_var_time_util"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/VariableTimeUtilAddon.as", RemainderCards),
        // AS3 variables/ReferenceVariable.as; turbo WiredVariableReference.cs
        ["wf_var_reference"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/variables/ReferenceVariable.as", RemainderCards)
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private const string R90 = "reviews/october-batch-r90-r90-";
    private const string R90Observed = "Observed on hhus (r90, WIN63-202610131318-860371703, ledger v39 limited_observed_slice).";

    /// <summary>Cards promoted to Observed by the r90 live run (ledger v39); the source is that batch's summary.</summary>
    public static readonly FrozenDictionary<string, WiredEvidence> ObservedR90 = new Dictionary<string, WiredEvidence>(StringComparer.OrdinalIgnoreCase)
    {
        ["wf_cnd_date_rng_active"] = new(WiredEvidenceLevel.Observed, R90 + "date-rng-summary.json", R90Observed),
        ["wf_xtra_var_fx_health"] = new(WiredEvidenceLevel.Observed, R90 + "varfx-a-summary.json", R90Observed),
        ["wf_xtra_var_fx_progress"] = new(WiredEvidenceLevel.Observed, R90 + "varfx-a-summary.json", R90Observed),
        ["wf_xtra_var_fx_status"] = new(WiredEvidenceLevel.Observed, R90 + "varfx-a-summary.json", R90Observed),
        ["wf_xtra_var_fx_boss"] = new(WiredEvidenceLevel.Observed, R90 + "varfx-b-summary.json", R90Observed),
        ["wf_xtra_var_fx_level"] = new(WiredEvidenceLevel.Observed, R90 + "varfx-b-summary.json", R90Observed),
        ["wf_xtra_var_fx_number"] = new(WiredEvidenceLevel.Observed, R90 + "varfx-b-summary.json", R90Observed),
        ["wf_var_echo"] = new(WiredEvidenceLevel.Observed, R90 + "var-echo-summary.json", R90Observed),
        ["wf_var_context"] = new(WiredEvidenceLevel.Observed, R90 + "var-context-summary.json", R90Observed),
        ["wf_xtra_text_input_variable"] = new(WiredEvidenceLevel.Observed, R90 + "capturer-summary.json", R90Observed),
        ["wf_trg_game_starts"] = new(WiredEvidenceLevel.Observed, R90 + "game-a-summary.json", R90Observed),
        ["wf_trg_game_ends"] = new(WiredEvidenceLevel.Observed, R90 + "game-a-summary.json", R90Observed),
        ["wf_act_give_score_tm"] = new(WiredEvidenceLevel.Observed, R90 + "game-a-summary.json", R90Observed),
        ["wf_act_give_score"] = new(WiredEvidenceLevel.Observed, R90 + "game-b-summary.json", R90Observed),
        ["wf_trg_score_achieved"] = new(WiredEvidenceLevel.Observed, R90 + "game-a-summary.json", R90Observed),
        ["wf_xtra_filter_users_by_var"] = new(WiredEvidenceLevel.Observed, R90 + "pair-leaderboard-summary.json", R90Observed),
        ["wf_xtra_var_leaderboard"] = new(WiredEvidenceLevel.Observed, R90 + "pair-leaderboard-summary.json", R90Observed)
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>The label furni wf_label (WiredLabel) is not a wired box, so it is kept out of the card tables; the r90 run observed it too.</summary>
    public static readonly WiredEvidence LabelFurniR90 = new(WiredEvidenceLevel.Observed, R90 + "label-summary.json", R90Observed);

    private const string R92 = "reviews/r92-movement-facts.md";
    private const string R92Observed = "Observed on hhus (r92d/r92e/r92f/r92g movement, ordering and variable probes, WIN63-202610131318-860371703); receipts per window in the facts file.";

    /// <summary>Movement cards (and the variable min/max operators) whose runtime the r92 probes decided; the source is the facts file.</summary>
    public static readonly FrozenDictionary<string, WiredEvidence> ObservedR92 = new Dictionary<string, WiredEvidence>(StringComparer.OrdinalIgnoreCase)
    {
        ["wf_act_move_to_dir"] = new(WiredEvidenceLevel.Observed, R92 + "#r92d-r92-mtd-wall", R92Observed),
        ["wf_act_chase"] = new(WiredEvidenceLevel.Observed, R92 + "#r92f-r92-chase", R92Observed),
        ["wf_act_flee"] = new(WiredEvidenceLevel.Observed, R92 + "#r92f-r92-flee", R92Observed),
        ["wf_act_rel_mov"] = new(WiredEvidenceLevel.Observed, R92 + "#r92f-r92-move-addons", R92Observed),
        ["wf_act_user_to_furni"] = new(WiredEvidenceLevel.Observed, R92 + "#r92f-r92-u2f-targets", R92Observed),
        ["wf_act_match_to_sshot"] = new(WiredEvidenceLevel.Observed, R92 + "#r92e-r92-room14-def", R92Observed),
        ["wf_xtra_mov_physics"] = new(WiredEvidenceLevel.Observed, R92 + "#r92e-r92-room14-def", R92Observed),
        ["wf_xtra_mov_carry_users"] = new(WiredEvidenceLevel.Observed, R92 + "#r92e-r92-room14-def", R92Observed),
        ["wf_xtra_anim_time"] = new(WiredEvidenceLevel.Observed, R92 + "#r92f-r92-move-addons", R92Observed),
        ["wf_xtra_mov_no_animation"] = new(WiredEvidenceLevel.Observed, R92 + "#r92f-r92-move-addons", R92Observed),
        ["wf_xtra_mov_curve"] = new(WiredEvidenceLevel.Observed, R92 + "#r92f-r92-move-addons", R92Observed),
        ["wf_act_change_var_val"] = new(WiredEvidenceLevel.Observed, R92 + "#4-still-undecided", R92Observed),
        ["wf_act_move_rotate"] = new(WiredEvidenceLevel.Observed, R92 + "#r92g-r92-move-rotate", R92Observed),
        ["wf_act_furni_to_user"] = new(WiredEvidenceLevel.Observed, R92 + "#r92g-r92-user-moves", R92Observed),
        ["wf_act_move_rotate_user"] = new(WiredEvidenceLevel.Observed, R92 + "#r92g-r92-user-moves", R92Observed),
        ["wf_cnd_valid_moves"] = new(WiredEvidenceLevel.Observed, R92 + "#r92g-r92-valid-moves", R92Observed),
        ["wf_act_call_stacks"] = new(WiredEvidenceLevel.Observed, R92 + "#r92g-r92-signals", R92Observed),
        ["wf_xtra_exec_in_order"] = new(WiredEvidenceLevel.Observed, R92 + "#r92g-r92-exec-order", R92Observed),
        ["wf_xtra_random"] = new(WiredEvidenceLevel.Observed, R92 + "#r92g-r92-exec-order", R92Observed)
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static WiredEvidenceLevel LevelOf(string name) => AirSourceOnly.ContainsKey(name) ? WiredEvidenceLevel.AirSourceOnly
        : TurboReferenceOnly.ContainsKey(name) ? WiredEvidenceLevel.TurboReferenceOnly : WiredEvidenceLevel.Observed;
}
