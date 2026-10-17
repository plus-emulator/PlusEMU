using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Variables;

public sealed class WiredVariableChangedTrigger : WiredModernBox, IWiredContextualTrigger
{
    public WiredVariableChangedTrigger(Room room, Item item, WiredBoxDescriptor descriptor) : base(room, item, descriptor) =>
        ApplyConfiguration(WiredNativeEditorProjection.DefaultRuntime(item.Id, descriptor) ?? new() { IntParams = [0, 1, 1, 1, 1, 1, 1, -1] });
    public IReadOnlyCollection<WiredEventKind> Events { get; } = [WiredEventKind.Variable];
    public bool HidesChat(WiredRuntimeContext context) => false;
    public override bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        if (WiredNativeEditorProjection.Supports(Descriptor.CanonicalName)
            && (!WiredNativeEditorProjection.IsBound(Item.Id, Descriptor, proposed)
                || !WiredNativeEditorProjection.TryValidateRuntime(Item, Descriptor, proposed, out validated, out error))) {
            validated = proposed;
            error = "Invalid native variable authority.";

            return false;
        }

        // The native compile already stores the normalized form, so a valid draft is kept as the very record it was bound to.
        validated = proposed;

        return TryNormalize(proposed, out _, out error);
    }

    /// <summary>The integer shape only; the picked variable is an opaque catalog id resolved by the module at match time.</summary>
    internal static bool TryNormalize(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        validated = proposed;
        error = "Choose a scalar variable and change types.";
        var p = proposed.IntParams;

        var pickedBoxes = p.Length == 9 && p[8] == 1;

        if (proposed.Version != 1 || p.Length != 8 && !pickedBoxes || !Enum.IsDefined((WiredVariableTarget)p[0])
            || p.Skip(1).Take(6).Any(value => value is not (0 or 1)) || proposed.VariableIds.Length != 1
            || !pickedBoxes && (WiredVariableAbsent.Is(proposed.VariableIds[0])
                || !WiredVariableDescription.TryParseCatalogId(proposed.VariableIds[0], out var target, out _) || (int)target != p[0])) {
            return false;
        }

        if (p[2] == 0) {
            p = p.SetItem(3, 0).SetItem(4, 0).SetItem(5, 0);
        }

        // Room variables and built-ins are never created or deleted by a box. A projectile's is_travelling still disappears when it
        // lands, which hhus reports as a deletion (AIR WIN63-202610151745-318700621).
        if (!pickedBoxes && (p[0] == (int)WiredVariableTarget.Global
            || WiredVariableDescription.TryParseCatalogId(proposed.VariableIds[0], out _, out var pickedToken)
                && pickedToken.StartsWith("internal:", StringComparison.Ordinal)
                && Array.IndexOf(WiredProjectileFlights.Intercepted, RoomWiredBuiltinVariables.Normalize(pickedToken)) < 0)) {
            p = p.SetItem(1, 0).SetItem(6, 0);
        }

        p = p.SetItem(7, p[7] < 0 ? -1 : p[7] & 15);

        if (p[1] == 0 && p[6] == 0 && (p[2] == 0 || p[3] + p[4] + p[5] == 0)) {
            return false;
        }

        validated = proposed with { IntParams = p };
        error = "";

        return true;
    }
    public override bool Execute(WiredRuntimeContext context)
    {
        if (!ReferenceEquals(context.Room, Instance) || context.Event.Kind != WiredEventKind.Variable
            || context.Event.VariableChange is not { } change || change.RoomId != context.Room.Id
            || !TryValidateConfiguration(context.ConfigurationOf(this), out var config, out _)) {
            return false;
        }

        var module = Instance.GetWired().Variables.Module;

        if (config.IntParams.Length == 9) {
            return MatchesPickedBoxes(config, change, module, context.Room.Id);
        }

        if (!module.TryResolveCatalogId(config.VariableIds[0], (WiredVariableTarget)config.IntParams[0], out var reference)) {
            return false;
        }

        // A Time Utilities Value part is interceptable. Observed on hhus (AIR WIN63-202610131318-860371703): every change of the parent
        // is a change of each part, and the part's own values pick increased, decreased or unchanged (+1 ms on the parent fired
        // year/unchanged and milliseconds_of_seconds/increased; +1 on the year part fired year/increased and milliseconds/unchanged).
        if (module.ProjectPartChange(reference, change) is { } part) {
            return Matches(config with { Text = reference.Token }, part, context.Room.Id);
        }

        return Matches(config with { Text = reference.Token }, change, context.Room.Id);
    }
    // Source-derived scalar support. Resolve only live definitions of this room through the module's ownership checks.
    // Echo/reference/derived-box identity is intentionally not guessed without a runtime observation.
    internal static bool MatchesPickedBoxes(WiredConfiguration config, WiredVariableChange change, WiredVariableModule module, uint roomId)
    {
        if (change.RoomId != roomId || module.RoomId != roomId || config.IntParams.Length != 9 || config.IntParams[8] != 1) {
            return false;
        }

        return module.DescribeDefinitions(config.SelectedItems).Any(variable => variable.Definition.Link is null
            && variable.Definition.Target != WiredVariableTarget.Context
            && Matches(config with { IntParams = config.IntParams.SetItem(0, (int)variable.Definition.Target), Text = variable.Definition.Token }, change, roomId));
    }
    public static bool Matches(WiredConfiguration config, WiredVariableChange change) => Matches(config, change, change.RoomId);
    public static bool Matches(WiredConfiguration config, WiredVariableChange change, uint currentRoomId)
    {
        var p = config.IntParams;

        if (p.Length is not (8 or 9) || p[0] != (int)change.Key.Target) {
            return false;
        }

        if (config.Text.StartsWith("internal:", StringComparison.Ordinal)) {
            if (RoomWiredBuiltinVariables.Normalize(config.Text) != change.InternalKey) {
                return false;
            }
        }
        else if (change.InternalKey.Length != 0 || !WiredVariableModule.TryDefinitionId(config.Text, out var id) || id != change.Key.DefinitionId) {
            return false;
        }

        var origin = change.NativeOrigin(currentRoomId);

        if (p[7] is not (-1 or 0) && (p[7] & (1 << origin)) == 0) {
            return false;
        }

        if (change.Kind == WiredVariableChangeKind.Created) {
            return p[1] == 1;
        }

        if (change.Kind == WiredVariableChangeKind.Removed) {
            return p[6] == 1;
        }

        if (p[2] == 0 || change.Before is null || change.After is null) {
            return false;
        }

        return change.After.Value.CompareTo(change.Before.Value) switch { > 0 => p[3] == 1, < 0 => p[4] == 1, _ => p[5] == 1 };
    }
}
