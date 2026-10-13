using System.Collections.Frozen;

namespace Plus.HabboHotel.Items.Wired.Configuration;

/// <summary>How a wired card's behaviour is known. AirSourceOnly cards come from the October AIR client code alone and need a live check.</summary>
public enum WiredEvidenceLevel
{
    Observed,
    AirSourceOnly
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

    public static WiredEvidenceLevel LevelOf(string name) => AirSourceOnly.ContainsKey(name) ? WiredEvidenceLevel.AirSourceOnly : WiredEvidenceLevel.Observed;
}
