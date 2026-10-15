namespace Plus.HabboHotel.Items.Wired.Variables;

public sealed partial class WiredRoomVariables
{
    // The room adapter can provide its configured wired timezone; UTC is the upstream default.
    public Func<TimeZoneInfo> TimeZone { get; set; } = () => TimeZoneInfo.Utc;
    private WiredVariableMetadataBox? DerivedMetadataOn(uint definitionId) => MetadataOn(definitionId, "wf_xtra_var_lvlup_system");

    /// <summary>The quest or chain a published quest definition of this room reads, or none.</summary>
    private WiredQuestBinding? QuestBinding(uint definitionId) =>
        _definitions.TryGetValue(definitionId, out var box) && box.HasPersistedConfiguration && IsAttached(box.Item)
        && box.Descriptor.CanonicalName is "wf_var_quest" or "wf_var_quest_chain"
            ? WiredQuestVariables.Binding(box.Descriptor.CanonicalName, box.Configuration.Text) : null;

    private IEnumerable<WiredVariableDescription> DerivedDescriptions(WiredVariableDescription source)
    {
        var definition = source.Definition;

        if (definition.Target == WiredVariableTarget.Context) {
            yield break;
        }

        if (source.HasValue && DerivedMetadataOn(definition.ItemId) is { } level) {
            foreach (var sub in Enumerable.Range(0, level.DerivedKeys.Length).Where(level.HasDerived)) {
                if (SyntheticId(definition.Target, definition.ItemId, sub, false) is { } id && _room.GetRoomItemHandler().GetItem(id) is null) {
                    yield return new(definition with { ItemId = id, Name = definition.Name + "." + level.DerivedKeys[sub] }, true, true) { IsDerived = true };
                }
            }
        }

        if (source.HasValue && QuestBinding(definition.ItemId) is { } quest) {
            var keys = WiredQuestVariables.Keys(quest.Kind);

            foreach (var sub in Enumerable.Range(0, keys.Length)) {
                if (SyntheticId(definition.Target, definition.ItemId, sub, false) is { } id && _room.GetRoomItemHandler().GetItem(id) is null) {
                    yield return new(definition with { ItemId = id, Name = definition.Name + "." + keys[sub] }, true, true) { IsDerived = true };
                }
            }
        }

        // Time Utilities add no sub-variables to the room catalogue: observed on hhus in both modes (r84 creation time, r90 value), the
        // catalogue lists only the parent. How the hotel exposes the parts, if at all, is not known.
    }

    private WiredVariableDerivation? ResolveDerived(WiredVariableReference reference)
    {
        if (!WiredVariableModule.TryDefinitionId(reference.Token, out var id) || reference.Target == WiredVariableTarget.Context
            || !TryDecode(reference.Target, id, out var baseId, out var sub, out var isTime)
            || !_definitions.TryGetValue(baseId, out var definition) || !definition.HasPersistedConfiguration
            || _room.GetRoomItemHandler().GetItem(id) is not null) {
            return null;
        }

        var source = new WiredVariableReference(reference.Target, $"custom:{baseId}");

        // Time Utilities parts are not catalogue variables (see DerivedDescriptions), so their ids resolve to nothing.
        if (isTime) {
            return null;
        }

        if (QuestBinding(baseId) is { } quest && _quests is not null && sub < WiredQuestVariables.Keys(quest.Kind).Length) {
            // A chain's current step reads the player's own step progress through its builtin part.
            if (quest.Kind == WiredQuestKind.Chain && sub == 0) {
                return new(new(WiredVariableTarget.User, WiredQuestVariables.Token(baseId, WiredQuestVariables.CurrentStepPart)), value => value);
            }

            return new(source, value => WiredQuestVariables.Derive(_quests, quest, value.Value, sub) is { } derived ? new(derived, null, null) : null);
        }

        var level = DerivedMetadataOn(baseId);

        return level is not null && level.HasDerived(sub)
            ? new(source, value => new(level.ReadDerived(value.Value, sub), null, null)) : null;
    }

    public static uint? SyntheticId(WiredVariableTarget target, uint baseId, int sub, bool time)
    {
        if (target is not (WiredVariableTarget.User or WiredVariableTarget.Furni or WiredVariableTarget.Global)
            || baseId is 0 or >= 6250000 || (time ? sub is < 1 or > 26 : sub is < 0 or > 7)) {
            return null;
        }

        return Offset(target, time) + baseId * (time ? 32u : 16u) + (uint)(time ? sub : sub + 1);
    }
    private static uint Offset(WiredVariableTarget target, bool time) => (target, time) switch
    {
        (WiredVariableTarget.Furni, false) => 800000000,
        (WiredVariableTarget.Global, false) => 900000000,
        (WiredVariableTarget.Furni, true) => 1700000000,
        (WiredVariableTarget.Global, true) => 1900000000,
        (_, true) => 1500000000,
        _ => 700000000
    };
    private static bool TryDecode(WiredVariableTarget target, uint id, out uint baseId, out int sub, out bool time)
    {
        time = id >= 1500000000;
        baseId = 0;
        sub = 0;
        var offset = Offset(target, time);

        if (id < offset) {
            return false;
        }

        var local = id - offset;
        var stride = time ? 32u : 16u;
        baseId = local / stride;
        sub = (int)(local % stride) - (time ? 0 : 1);

        return SyntheticId(target, baseId, sub, time) == id;
    }
}
