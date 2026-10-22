using System.Collections.Frozen;
using System.Text.Json;
using System.Text.RegularExpressions;
using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired.Variables;

public static class WiredVariableDefinitions
{
    internal const string InvalidReferenceKey = "wiredfurni.error.variables.reference.valid";
    internal const string ReferenceMustOwnKey = "wiredfurni.error.variables.reference.must_own";
    internal const string ReferenceExistsKey = "wiredfurni.error.variables.reference.already_exists";
    internal const string EchoSelfReferenceKey = "wiredfurni.error.echo.self_reference";
    public static bool Supports(string name) => name is "wf_var_user" or "wf_var_furni" or "wf_var_room"
        or "wf_var_context" or "wf_var_echo" or "wf_var_reference" or "wf_var_quest" or "wf_var_quest_chain"
        or "wf_var_dailytask" or "wf_var_pointer";
    /// <summary>The picker id of a variable shared from another room; the module still authorizes the source it names.</summary>
    internal static string SharedId(uint roomId, WiredVariableTarget target, uint definitionId) =>
        $"shared:{roomId}:{(target == WiredVariableTarget.Global ? "room" : "user")}:{definitionId}";

    internal static bool TryParseShared(string id, out uint roomId, out WiredVariableTarget target, out uint definitionId)
    {
        roomId = definitionId = 0;
        target = default;
        var parts = id.Split(':');

        if (parts.Length != 4 || parts[0] != "shared" || parts[2] is not ("user" or "room")
            || !uint.TryParse(parts[1], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out roomId) || roomId == 0
            || !uint.TryParse(parts[3], System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out definitionId) || definitionId == 0
            || SharedId(roomId, parts[2] == "room" ? WiredVariableTarget.Global : WiredVariableTarget.User, definitionId) != id) {
            return false;
        }

        target = parts[2] == "room" ? WiredVariableTarget.Global : WiredVariableTarget.User;

        return true;
    }

    internal const string NameLengthKey = "wiredfurni.error.variables.name_length";
    internal const string NameSyntaxKey = "wiredfurni.error.variables.name_syntax";
    internal const string NameTakenKey = "wiredfurni.error.variables.name_uniq";
    internal const string MissingVariableKey = "wiredfurni.error.variables.missing_variable";
    internal const string NeedsExecutionContextKey = "wiredfurni.error.variables.requires_execution_context";

    /// <summary>
    /// The key a proposed variable name is refused with, or null. Observed on AIR WIN63-202610201032-107964540: a pointer name outside
    /// 1 to 40 characters, or with a character other than a letter, digit or underscore, is refused with these keys and no parameters.
    /// </summary>
    internal static string? RefuseName(string text) => text.Length is < 1 or > 40 ? NameLengthKey : ValidName(text) ? null : NameSyntaxKey;

    // The boxes whose name a new save cleans and checks (see CleanName and WiredVariableModule.RefuseName).
    private static readonly FrozenSet<string> NamedBoxes = FrozenSet.ToFrozenSet(["wf_var_user", "wf_var_furni", "wf_var_room", "wf_var_context", "wf_var_pointer"]);

    internal static bool IsNamed(string name) => NamedBoxes.Contains(name);

    /// <summary>
    /// The name a new save stores. Observed on AIR WIN63-202610201032-107964540: each space becomes an underscore, the name is lower case
    /// and loses its leading and trailing underscores ("_a" and "a_" store "a", "PtrUP" stores "ptrup"); inner underscores stay. Whether
    /// the 40 character limit counts before or after this is not observed, so a longer name is kept as sent and stays too long. Only
    /// ASCII capitals are lowered, so any other letter stays a syntax error.
    /// </summary>
    internal static string CleanName(string text) => text.Length > 40 ? text
        : string.Concat(text.Select(c => c == ' ' ? '_' : char.IsAsciiLetterUpper(c) ? char.ToLowerInvariant(c) : c)).Trim('_');

    private static bool ValidName(string text) => text.Length is >= 1 and <= 40 && Regex.IsMatch(text, "^[A-Za-z0-9_]+$", RegexOptions.CultureInvariant);

    /// <summary>
    /// An echo saved before its variable was picked (observed on hhus: the initial frame, variable id "0", saved under a name and
    /// reopened unchanged). It is a valid card that mirrors nothing, so it is stored but defines no variable.
    /// </summary>
    public static bool IsUnpickedEcho(string name, WiredConfiguration configuration) => name == "wf_var_echo" && configuration.Version == 1
        && configuration.IntParams.Length == 0 && configuration.VariableIds.Length == 1 && WiredVariableAbsent.Is(configuration.VariableIds[0])
        && ValidName(configuration.Text);

    public static bool TryDecode(string name, uint itemId, uint roomId, uint ownerId, WiredConfiguration configuration,
        out WiredVariableDefinition? definition, out string error)
    {
        definition = null;
        error = "Invalid variable configuration.";

        if (itemId == 0 || roomId == 0 || ownerId == 0 || configuration.Version != 1) {
            return false;
        }

        var text = configuration.Text;
        var ints = configuration.IntParams;
        WiredVariableTarget target;
        WiredVariableAvailability availability;
        bool hasValue;
        var initial = 0L;
        WiredVariableLink? link = null;
        string? pointerAccessor = null;

        switch (name) {
            case "wf_var_user":
            case "wf_var_furni":
                if (ints.Length != 2 || ints[0] is not (0 or 1)) {
                    return false;
                }

                target = name == "wf_var_user" ? WiredVariableTarget.User : WiredVariableTarget.Furni;

                if (ints[1] != (target == WiredVariableTarget.User ? 0 : 1) && ints[1] != 10
                    && !(target == WiredVariableTarget.User && ints[1] == 11)) {
                    return false;
                }

                availability = (WiredVariableAvailability)ints[1];
                hasValue = ints[0] == 1;
                break;
            case "wf_var_room":
                // The AIR form edits availability only; the starting value is always zero and live values survive saves.
                if (ints.Length != 2 || ints[0] is not (1 or 10 or 11) || ints[1] != 0) {
                    return false;
                }

                target = WiredVariableTarget.Global;
                availability = (WiredVariableAvailability)ints[0];
                hasValue = true;
                break;
            case "wf_var_context":
                if (ints.Length != 1 || ints[0] is not (0 or 1)) {
                    return false;
                }

                target = WiredVariableTarget.Context;
                availability = WiredVariableAvailability.RoomActive;
                hasValue = ints[0] == 1;
                break;
            case "wf_var_echo":
                // An echo names one variable of this room; the picked catalog id carries its target and definition.
                if (configuration.VariableIds.Length != 1
                    || !WiredVariableDescription.TryParseCatalogId(configuration.VariableIds[0], out target, out var echoed)) {
                    return false;
                }

                link = new(roomId, new(target, echoed), false);
                availability = WiredVariableAvailability.RoomActive;
                hasValue = true; // Actual source flags are enforced by resolution, never these labels.
                break;
            case "wf_var_reference":
                // A reference names one shared variable of another room: "shared:{room}:{user|room}:{definition}".
                if (configuration.VariableIds.Length != 1 || ints.Length != 1 || ints[0] is not (0 or 1)) {
                    return false;
                }

                // Observed on hhus (WIN63-202610131318): an unchosen reference ("0") is refused with this key and no parameters.
                if (!TryParseShared(configuration.VariableIds[0], out var sourceRoom, out target, out var sourceItem) || sourceRoom == roomId) {
                    error = InvalidReferenceKey;

                    return false;
                }

                link = new(sourceRoom, new(target, $"custom:{sourceItem}"), ints[0] == 1);
                availability = WiredVariableAvailability.RoomActive;
                hasValue = true;
                break;
            case "wf_var_quest":
            case "wf_var_quest_chain":
                // A read-only user variable over the real quest tables; its own builtin token reads the player's quest state.
                if (ints.Length != 0 || configuration.VariableIds.Length != 0 || WiredQuestVariables.Binding(name, text.Contains('\t') ? text : "") is null) {
                    return false;
                }

                text = text.Split('\t')[0];
                target = WiredVariableTarget.User;
                availability = WiredVariableAvailability.RoomActive;
                hasValue = true;
                link = new(roomId, new(WiredVariableTarget.User, WiredQuestVariables.Token(itemId)), true);
                break;
            case "wf_var_dailytask":
                // AirSourceOnly (DailyTaskVariable.as): name TAB task text. PlusEMU has no daily tasks, so this is a user variable that
                // Wired effects set; the task text is kept with the box.
                if (ints.Length != 0 || configuration.VariableIds.Length != 0) {
                    return false;
                }

                text = text.Split('\t')[0];
                target = WiredVariableTarget.User;
                availability = WiredVariableAvailability.UserActive;
                hasValue = true;
                break;
            case "wf_var_pointer":
                // AirSourceOnly (PointerVariable.as): the variable lives on its source type (0 furni, 1 user, -10 global, -20 context);
                // its accessor is a context variable with a value. A box without one still decodes, it just resolves nothing; a new save
                // refuses it (WiredVariableModule.RefuseAccessor).
                if (ints.Length != 1 || configuration.VariableIds.Length != 1 || ints[0] is not (0 or 1 or -10 or -20)) {
                    return false;
                }

                // The name rules are checked when a pointer is saved (WiredVariableDefinitionBox.RefuseSave), so an older stored pointer still loads.
                target = ints[0] switch { 0 => WiredVariableTarget.Furni, 1 => WiredVariableTarget.User, -10 => WiredVariableTarget.Global, _ => WiredVariableTarget.Context };
                availability = target == WiredVariableTarget.User ? WiredVariableAvailability.UserActive : WiredVariableAvailability.RoomActive;
                hasValue = true;
                pointerAccessor = WiredVariableDescription.TryParseCatalogId(configuration.VariableIds[0], out _, out var accessorToken) ? accessorToken : "";
                break;
            default:
                error = "This variable definition has no executable implementation.";

                return false;
        }

        if (!ValidName(text) && name != "wf_var_pointer") {
            return false;
        }

        definition = new(itemId, roomId, ownerId, text, target, availability, hasValue, initial, link, pointerAccessor);
        error = "";

        return true;
    }
}
