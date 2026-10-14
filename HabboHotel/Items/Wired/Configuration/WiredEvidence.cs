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
        ["wf_act_move_furni_to"] = new(WiredEvidenceLevel.TurboReferenceOnly, Actions + "class_4162.as", AirEditorCards)
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static WiredEvidenceLevel LevelOf(string name) => AirSourceOnly.ContainsKey(name) ? WiredEvidenceLevel.AirSourceOnly
        : TurboReferenceOnly.ContainsKey(name) ? WiredEvidenceLevel.TurboReferenceOnly : WiredEvidenceLevel.Observed;
}
