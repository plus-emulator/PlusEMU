using Plus.HabboHotel.Items.Wired.Settings;

namespace Plus.HabboHotel.Items.Wired.Variables;

public sealed partial class WiredRoomVariables
{
    // The room adapter provides the room's Wired timezone; without one the hotel default (Europe/London, as hhus behaves) applies.
    public Func<TimeZoneInfo> TimeZone { get; set; } = () => WiredRoomSettings.DefaultTimeZone;
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

        // Time Utilities parts, named "<parent>.<mode>.<part>" as observed on hhus (AIR WIN63-202610131318-860371703); the creation-time
        // and last-update modes also list the base row "<parent>.<mode>".
        if (MetadataOn(definition.ItemId, "wf_xtra_var_time_util")?.TimeUtilities is { } time && TimeVisible(time, source)) {
            foreach (var sub in time.Parts) {
                if (SyntheticId(definition.Target, definition.ItemId, sub, true) is { } id && _room.GetRoomItemHandler().GetItem(id) is null) {
                    yield return new(definition with { ItemId = id, Name = definition.Name + "." + time.CatalogKey(sub) }, true, !time.Writable)
                    {
                        IsDerived = true,
                        TextConnector = WiredVariableTimeUtilities.Connector(sub)?.ToDictionary() ?? new Dictionary<int, string>()
                    };
                }
            }
        }
    }

    /// <summary>
    /// hhus gates each mode on the parent's own capability: Value needs a value, creation time needs canReadCreationTime (a global
    /// has none) and last update needs canReadLastUpdateTime.
    /// </summary>
    private static bool TimeVisible(WiredVariableTimeUtilities time, WiredVariableDescription source) => time.Mode switch
    {
        1 => source.CanReadCreationTime,
        2 => source.CanReadLastUpdateTime,
        _ => source.HasValue
    };

    internal static WiredVariableDerivation TimeDerivation(WiredVariableReference source, WiredVariableTimeUtilities time, int sub, Func<TimeZoneInfo> zone) =>
        new(source, value => time.Read(value, sub, zone()) is { } result ? new(result, null, null) : null, time.Mode == 0, time.Mode != 0,
            time.Writable ? (parent, part) => time.Write(parent, sub, part, zone()) : null)
        { RequiresCreationTime = time.Mode == 1 };

    private WiredVariableDerivation? ResolveDerived(WiredVariableReference reference)
    {
        if (!WiredVariableModule.TryDefinitionId(reference.Token, out var id) || reference.Target == WiredVariableTarget.Context
            || !TryDecode(reference.Target, id, out var baseId, out var sub, out var isTime)
            || !_definitions.TryGetValue(baseId, out var definition) || !definition.HasPersistedConfiguration
            || _room.GetRoomItemHandler().GetItem(id) is not null) {
            return null;
        }

        var source = new WiredVariableReference(reference.Target, $"custom:{baseId}");

        if (isTime) {
            var time = MetadataOn(baseId, "wf_xtra_var_time_util")?.TimeUtilities;

            return time is null || !time.HasPart(sub) ? null : TimeDerivation(source, time, sub, TimeZone);
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
            ? new(source, value => level.ReadDerived(value.Value, sub) is { } part ? new(part, null, null) : null,
                Write: sub == 1 ? (_, xp) => level.LevelSystem!.ClampXp(xp) : null)
            { Interceptable = true, HasValue = sub != 6 }
            : null;
    }

    public static uint? SyntheticId(WiredVariableTarget target, uint baseId, int sub, bool time)
    {
        if (target is not (WiredVariableTarget.User or WiredVariableTarget.Furni or WiredVariableTarget.Global)
            || baseId is 0 or >= 6250000 || (time ? sub is < 0 or > 26 : sub is < 0 or > 7)) {
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
    /// <summary>Flashes the definition box of a written variable (the parent's for a sub-variable) and the variable add-ons on its tile.</summary>
    internal void FlashDefinition(WiredVariableReference reference, int kind)
    {
        if (!WiredVariableModule.TryDefinitionId(reference.Token, out var id)) {
            return;
        }

        if (TryDecode(reference.Target, id, out var baseId, out _, out _)) {
            id = baseId;
        }

        FlashDefinition(id, kind);
    }

    internal void FlashDefinition(uint id, int kind)
    {
        if (_room.GetRoomItemHandler() is not { } items || _room.GetWired() is not { } wired
            || items.GetItem(id) is not { IsFloorItem: true } definition || definition.Definition.WiredDescriptor is null) {
            return;
        }

        wired.FlashState(definition, WiredFlashStates.NextDefinition(definition.LegacyDataString, kind));

        foreach (var addon in items.GetFloor.Where(item => item.Id != definition.Id && item.GetX == definition.GetX && item.GetY == definition.GetY
                     && item.Definition.WiredDescriptor?.CanonicalName.StartsWith("wf_xtra_var_", StringComparison.Ordinal) == true)) {
            wired.OnEvent(addon);
        }
    }

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
