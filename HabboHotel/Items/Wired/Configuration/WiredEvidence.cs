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
        ["wf_var_quest_chain"] = new(WiredEvidenceLevel.AirSourceOnly, Variables + "QuestChainVariable.as", NoCatalogue)
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private const string BotCards = "Editor observed on hhus (r62-batch-02); runtime behaviour read from turbo-cloud, never fired on official.";
    private const string GameCards = "Editor observed on hhus (r62-batch-01); the game start and end sources read from turbo-cloud, never fired on official.";
    private const string AirEditorCards = "Editor read from the AIR client only (no catalogue offer on hhus); runtime read from turbo-cloud, never fired on official.";
    private const string RemainderCards = "Editor read from the AIR client (and observed on hhus where the card has an offer); runtime read from turbo-cloud, never fired on official.";
    private const string Actions = "wired_setup/actiontypes/";
    private const string Triggers = "wired_setup/triggerconfs/";

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
        // AS3 triggerconfs/class_4195.as; turbo WiredTriggerGameStarts.cs
        ["wf_trg_game_starts"] = new(WiredEvidenceLevel.TurboReferenceOnly, Triggers + "class_4195.as", GameCards),
        // AS3 triggerconfs/class_4066.as; turbo WiredTriggerGameEnds.cs
        ["wf_trg_game_ends"] = new(WiredEvidenceLevel.TurboReferenceOnly, Triggers + "class_4066.as", GameCards),
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
        // AS3 actiontypes/GiveScore.as; turbo WiredActionGiveScore.cs
        ["wf_act_give_score"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/GiveScore.as", RemainderCards),
        // AS3 actiontypes/class_4040.as; turbo WiredActionGiveScoreToTeam.cs
        ["wf_act_give_score_tm"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/class_4040.as", RemainderCards),
        // AS3 triggerconfs/ScoreAchieved.as; turbo WiredTriggerScoreAchieved.cs
        ["wf_trg_score_achieved"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/triggerconfs/ScoreAchieved.as", RemainderCards),
        // AS3 actiontypes/class_3972.as; turbo WiredActionPlaceFurni.cs
        ["wf_act_place_furni"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/class_3972.as", RemainderCards),
        // AS3 actiontypes/class_4089.as; turbo WiredActionRemoveFurni.cs
        ["wf_act_remove_furni"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/actiontypes/class_4089.as", RemainderCards),
        // AS3 addons/class_4236.as; turbo WiredAddonFilterUsersByVariable.cs
        ["wf_xtra_filter_users_by_var"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/class_4236.as", RemainderCards),
        // AS3 addons/VariableCapturerAddon.as; turbo WiredAddonVariableCapturer.cs
        ["wf_xtra_text_input_variable"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/VariableCapturerAddon.as", RemainderCards),
        // AS3 addons/VariableTimeUtilAddon.as; turbo WiredAddonVariableTimeUtil.cs
        ["wf_xtra_var_time_util"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/VariableTimeUtilAddon.as", RemainderCards),
        // AS3 variables/ContextVariable.as; turbo WiredVariableContext.cs
        ["wf_var_context"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/variables/ContextVariable.as", RemainderCards),
        // AS3 variables/EchoVariable.as; turbo WiredVariableEcho.cs
        ["wf_var_echo"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/variables/EchoVariable.as", RemainderCards),
        // AS3 variables/ReferenceVariable.as; turbo WiredVariableReference.cs
        ["wf_var_reference"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/variables/ReferenceVariable.as", RemainderCards),
        // AS3 addons/variablefx/VariableFxHealthPointsAddon.as; turbo WiredAddonVariableFxHealthPoints.cs
        ["wf_xtra_var_fx_health"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/variablefx/VariableFxHealthPointsAddon.as", RemainderCards),
        // AS3 addons/variablefx/VariableFxProgressBarAddon.as; turbo WiredAddonVariableFxProgressBar.cs
        ["wf_xtra_var_fx_progress"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/variablefx/VariableFxProgressBarAddon.as", RemainderCards),
        // AS3 addons/variablefx/VariableFxLevellingProgressAddon.as; turbo WiredAddonVariableFxLevellingProgress.cs
        ["wf_xtra_var_fx_level"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/variablefx/VariableFxLevellingProgressAddon.as", RemainderCards),
        // AS3 addons/variablefx/VariableFxStatusBarAddon.as; turbo WiredAddonVariableFxStatusBar.cs
        ["wf_xtra_var_fx_status"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/variablefx/VariableFxStatusBarAddon.as", RemainderCards),
        // AS3 addons/variablefx/VariableFxBossBarAddon.as; turbo WiredAddonVariableFxBossBar.cs
        ["wf_xtra_var_fx_boss"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/variablefx/VariableFxBossBarAddon.as", RemainderCards),
        // AS3 addons/variablefx/VariableFxNumberDisplayAddon.as; turbo WiredAddonVariableFxNumberDisplay.cs
        ["wf_xtra_var_fx_number"] = new(WiredEvidenceLevel.TurboReferenceOnly, "wired_setup/addons/variablefx/VariableFxNumberDisplayAddon.as", RemainderCards)
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static WiredEvidenceLevel LevelOf(string name) => AirSourceOnly.ContainsKey(name) ? WiredEvidenceLevel.AirSourceOnly
        : TurboReferenceOnly.ContainsKey(name) ? WiredEvidenceLevel.TurboReferenceOnly : WiredEvidenceLevel.Observed;
}
