using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>Executable scalar boxes decoded from the native editor's compiled runtime shape.</summary>
public sealed class WiredVariableExecutors(WiredVariableModule variables, TimeProvider clock)
{
    public static bool Supports(string name) => name is "wf_act_give_var" or "wf_act_remove_var" or "wf_act_change_var_val"
        or "wf_cnd_has_var" or "wf_cnd_neg_has_var" or "wf_cnd_var_val_match" or "wf_cnd_var_age_match";

    /// <summary>Shape-only validation: save and load must not depend on which definition boxes have been loaded yet.</summary>
    public static bool TryValidate(string name, WiredConfiguration configuration, out string error)
    {
        error = "Invalid scalar variable settings.";

        return TryDecode(name, configuration, out var core, out _) && TryTokens(name, configuration, core, null, out var tokens)
            && ValidateCore(name, configuration, core, tokens, out error, UnchosenDestination(name, configuration));
    }

    /// <summary>
    /// Observed on hhus (AIR WIN63-202610131318-860371703): Give Variable and Change Variable Value save with no destination variable
    /// picked (the picker's "n"/"0"), with every other field checked as usual. Such a box changes nothing until a variable is chosen;
    /// execution still requires a resolved destination.
    /// </summary>
    private static bool UnchosenDestination(string name, WiredConfiguration configuration) =>
        name is "wf_act_give_var" or "wf_act_change_var_val" && configuration.VariableIds.Length > 0
        && WiredVariableAbsent.Is(configuration.VariableIds[0]);

    /// <summary>
    /// Observed on AIR WIN63-202610201032-107964540: Has Variable and Not Has Variable save with no variable, or with a variable of another
    /// domain than their target, and read it as no variable: the first never passes and the second always does.
    /// </summary>
    private static bool ReadsPresence(string name) => name is "wf_cnd_has_var" or "wf_cnd_neg_has_var";

    /// <summary>
    /// The native picker saves opaque catalog ids. Without an authority they only have to be well formed for their target;
    /// with one (execution) each resolves through the room module's current catalog, so a deleted, foreign or retargeted
    /// variable resolves to nothing. An unchosen id stays empty and keeps the box inactive.
    /// </summary>
    private static bool TryTokens(string name, WiredConfiguration configuration, System.Collections.Immutable.ImmutableArray<int> p,
        WiredVariableModule? authority, out string[] tokens)
    {
        tokens = ["", ""];
        var ids = configuration.VariableIds;
        var operand = name is "wf_act_change_var_val" or "wf_cnd_var_val_match";
        var reference = ReferenceSlot(name, p);

        if (ids.Length != (operand || reference is not null ? 2 : 1) || !Enum.IsDefined((WiredVariableTarget)p[0])) {
            return false;
        }

        bool Resolve(string id, int target, out string token)
        {
            token = "";

            if (WiredVariableAbsent.Is(id)) {
                return true;
            }

            if (!WiredVariableDescription.TryParseCatalogId(id, out var parsed, out var parsedToken)) {
                return false;
            }

            if ((int)parsed != target) {
                return ReadsPresence(name) || name == "wf_act_change_var_val";
            }

            token = authority is null ? parsedToken : authority.TryResolveCatalogId(id, parsed, out var reference) ? reference.Token : "";

            return true;
        }

        if (!Resolve(ids[0], p[0], out tokens[0])) {
            return false;
        }

        // A literal-valued box ignores its reference slot.
        if (operand && p[2] == 1) {
            return Enum.IsDefined((WiredVariableTarget)p[4]) && Resolve(ids[1], p[4], out tokens[1]);
        }

        if (reference is { } slot && p[slot.Option] == 1) {
            return Enum.IsDefined((WiredVariableTarget)p[slot.Target]) && Resolve(ids[1], p[slot.Target], out tokens[1]);
        }

        return true;
    }

    /// <summary>
    /// The October Give Variable and Variable Age layouts add "from a variable": the option's index and the reference
    /// variable's target index. Older saves have neither.
    /// </summary>
    private static (int Option, int Target)? ReferenceSlot(string name, System.Collections.Immutable.ImmutableArray<int> p) => name switch
    {
        "wf_act_give_var" when p.Length is GiveExtended or GiveReferenced => (5, 6),
        "wf_cnd_var_age_match" when p.Length is AgeExtended or AgeReferenced => (7, 8),
        _ => null
    };

    // Runtime layouts written for the October editors: the older count plus their new fields.
    private const int GiveExtended = 8;
    private const int ChangeExtended = 10;
    private const int ValueMatchExtended = 11;
    private const int AgeExtended = 10;
    // Give and Age with a reference variable carry the reference's own user and furniture sources (the editor's second selector).
    private const int GiveReferenced = 10;
    private const int AgeReferenced = 12;

    /// <summary>"Same holder": the reference variable is read on each destination holder rather than from its own source.</summary>
    private static bool SameHolder(string name, System.Collections.Immutable.ImmutableArray<int> p) => name switch
    {
        "wf_act_give_var" => p.Length is GiveExtended or GiveReferenced && p[5] == 1 && p[7] == 1,
        "wf_act_change_var_val" => p.Length == ChangeExtended && p[2] == 1 && p[9] == 1,
        "wf_cnd_var_val_match" => p.Length == ValueMatchExtended && p[2] == 1 && p[9] == 1,
        _ => false
    };

    private static bool TryDecode(string name, WiredConfiguration configuration, out System.Collections.Immutable.ImmutableArray<int> core, out long literal)
    {
        core = configuration.IntParams;
        literal = 0;
        var lengths = name switch
        {
            "wf_act_give_var" => new[] { 5, GiveExtended, GiveReferenced },
            "wf_act_remove_var" => [3],
            "wf_act_change_var_val" => [9, ChangeExtended],
            "wf_cnd_has_var" or "wf_cnd_neg_has_var" => [4],
            "wf_cnd_var_val_match" => [10, ValueMatchExtended],
            "wf_cnd_var_age_match" => [8, AgeExtended, AgeReferenced],
            _ => [-1]
        };
        var literalIndex = name switch { "wf_act_give_var" => 2, "wf_act_change_var_val" or "wf_cnd_var_val_match" => 3, _ => -1 };

        foreach (var length in lengths) {
            if (core.Length == length + 2 && literalIndex >= 0 && core[length] == 1) {
                literal = ((long)core[length + 1] << 32) | (uint)core[literalIndex];
                core = core[..length];

                return true;
            }
        }

        if (!lengths.Contains(core.Length)) {
            return false;
        }

        if (literalIndex >= 0) {
            literal = core[literalIndex];
        }

        return true;
    }

    private static bool ValidateCore(string name, WiredConfiguration configuration, System.Collections.Immutable.ImmutableArray<int> p,
        string[] tokens, out string error, bool unchosenDestination = false)
    {
        error = "Invalid scalar variable settings.";

        if (!Supports(name) || configuration.Version != 1 || !Enum.IsDefined((WiredVariableTarget)p[0])) {
            return false;
        }

        // Observed on AIR WIN63-202610201032-107964540: Change Variable Value saves with a variable of another domain than its target
        // (a furni variable on a user target) and then changes nothing, like a box with no variable picked.
        unchosenDestination |= name == "wf_act_change_var_val" && tokens[0].Length == 0;
        // Observed on AIR WIN63-202610201032-107964540: Variable Value Match saves with no variable, or with option 1 and no reference
        // variable, and then never passes. Observed with option 1 and only the reference empty, only the left variable empty, or both
        // empty, and with same holder on and either slot empty (furni target only). Other empty-slot shapes are assumed to save the
        // same; they still never pass.
        var valueMatch = name == "wf_cnd_var_val_match";
        unchosenDestination |= valueMatch && tokens[0].Length == 0;

        if (!unchosenDestination && !(ReadsPresence(name) && tokens[0].Length == 0) && !ValidToken(tokens[0])) {
            return false;
        }

        if (name is "wf_act_give_var" or "wf_act_remove_var") {
            // Observed on hhus (AIR WIN63-202610131318-860371703): Give Variable saves with a global variable (and then gives nothing).
            // Observed on hhus (AIR WIN63-202610151745-318700621): so does Remove Variable, and it then removes nothing.
            var destination = new WiredVariableReference((WiredVariableTarget)p[0], tokens[0]);

            if (!unchosenDestination && !WiredVariableModule.TryDefinitionId(tokens[0], out _)
                && !RoomWiredBuiltinVariables.SupportsPresenceMutation(destination) && !RoomWiredBuiltinVariables.SavesWithoutEffect(destination)) {
                return false;
            }

            if (name == "wf_act_give_var" && p[1] is not (0 or 1)) {
                return false;
            }
        }

        if (name is "wf_act_change_var_val" or "wf_cnd_var_val_match") {
            if (name == "wf_act_change_var_val" ? !WiredVariableArithmetic.IsSupported(p[1]) : p[1] is < 0 or > 5) {
                return false;
            }

            if (p[2] is not (0 or 1) || !Enum.IsDefined((WiredVariableTarget)p[4])) {
                return false;
            }

            if (p[2] == 1 && !ValidToken(tokens[1]) && !(valueMatch && tokens[1].Length == 0)) {
                return false;
            }
        }

        if (name == "wf_cnd_var_age_match" && (p[1] is not (0 or 1) || p[2] is not (0 or 2) || p[4] is < 0 or > 7)) {
            return false;
        }

        if (ReferenceSlot(name, p) is { } slot && (p[slot.Option] is not (0 or 1) || !Enum.IsDefined((WiredVariableTarget)p[slot.Target])
            || p[slot.Option] == 1 && !ValidToken(tokens[1]))) {
            return false;
        }

        // Same holder reads the reference on the destination's own holders, so both must be of one user or furni kind.
        var sameIndex = name switch
        {
            "wf_act_give_var" when p.Length is GiveExtended or GiveReferenced => 7,
            "wf_act_change_var_val" when p.Length == ChangeExtended => 9,
            "wf_cnd_var_val_match" when p.Length == ValueMatchExtended => 9,
            _ => -1
        };
        var referenceTarget = name == "wf_act_give_var" ? 6 : 4;
        var option = name == "wf_act_give_var" ? 5 : 2;

        // Observed on AIR WIN63-202610201032-107964540: Variable Value Match also saves same holder on a global target, and then passes.
        // The other two boxes were not probed on a global target.
        var sameHolderTarget = (WiredVariableTarget)p[0] is WiredVariableTarget.User or WiredVariableTarget.Furni
            || valueMatch && (WiredVariableTarget)p[0] == WiredVariableTarget.Global;

        if (sameIndex >= 0 && (p[sameIndex] is not (0 or 1) || p[sameIndex] == 1 && (p[option] != 1 || p[referenceTarget] != p[0]
            || !sameHolderTarget))) {
            return false;
        }

        // Observed on AIR WIN63-202610201032-107964540: Not Has Variable stores a quantifier of 2.
        if (name.StartsWith("wf_cnd_", StringComparison.Ordinal) && p[^1] is not (0 or 1) && !(name == "wf_cnd_neg_has_var" && p[^1] == 2)) {
            return false;
        }

        var sourceIndices = name switch
        {
            "wf_act_give_var" => p.Length == GiveReferenced ? new[] { 3, 4, 8, 9 } : [3, 4],
            "wf_act_remove_var" => [1, 2],
            "wf_act_change_var_val" or "wf_cnd_var_val_match" => [5, 6, 7, 8],
            "wf_cnd_var_age_match" => p.Length == AgeReferenced ? [5, 6, 9, 10] : [5, 6],
            _ => [1, 2]
        };

        // Observed on AIR WIN63-202610201032-107964540: Has Variable and Not Has Variable store the trigger user source 10 in the user slot.
        if (sourceIndices.Any(i => p[i] is not (0 or 11 or 100 or 101 or 200 or 201) && !(ReadsPresence(name) && i == 1 && p[i] == 10))) {
            return false;
        }

        error = "";

        return true;
    }

    /// <summary>The variable a Give Variable or Change Variable Value box writes, resolved through the room's catalog.</summary>
    public WiredVariableReference? Destination(string name, WiredConfiguration configuration) =>
        name is "wf_act_give_var" or "wf_act_change_var_val" or "wf_act_remove_var" && TryDecode(name, configuration, out var p, out _)
        && TryTokens(name, configuration, p, variables, out var tokens) && tokens[0].Length > 0
            ? new WiredVariableReference((WiredVariableTarget)p[0], tokens[0]) : null;

    public bool Execute(string name, WiredConfiguration configuration, WiredVariableFrame frame)
    {
        string[] tokens = ["", ""];

        if (!TryDecode(name, configuration, out var p, out var literal) || !TryTokens(name, configuration, p, variables, out tokens)
            || !ValidateCore(name, configuration, p, tokens, out _)) {
            return false;
        }

        var reference = new WiredVariableReference((WiredVariableTarget)p[0], tokens[0]);
        var sourceStart = name switch
        {
            "wf_act_give_var" => 3,
            "wf_act_remove_var" => 1,
            "wf_act_change_var_val" or "wf_cnd_var_val_match" or "wf_cnd_var_age_match" => 5,
            _ => 1
        };
        var targets = Select(frame, reference.Target, p[sourceStart], p[sourceStart + 1], Picks(configuration, p[sourceStart + 1])).ToArray();

        var operandReference = ReferenceSlot(name, p) is { } slot && p[slot.Option] == 1
            ? new WiredVariableReference((WiredVariableTarget)p[slot.Target], tokens[1]) : (WiredVariableReference?)null;
        var sameHolder = SameHolder(name, p);
        var valueSource = name switch
        {
            "wf_act_give_var" => operandReference,
            "wf_act_change_var_val" when p[2] == 1 && !WiredVariableArithmetic.IsUnary(p[1]) => new WiredVariableReference((WiredVariableTarget)p[4], tokens[1]),
            _ => null
        };

        // Observed on AIR WIN63-202610201032-107964540: Give Variable and Change Variable never read a pointer as their value, so the
        // destination keeps its value.
        if (valueSource is not null && variables.IsPointer(valueSource)) {
            return false;
        }

        // A global variable always exists: Give Variable on one changes nothing (observed on hhus: = 0 still passed after giving 7), and
        // Remove Variable leaves its value (observed on hhus: 3 before the remove, 6 after another +3).
        if (name is "wf_act_give_var" or "wf_act_remove_var" && reference.Target == WiredVariableTarget.Global) {
            return false;
        }

        if (name is "wf_act_give_var" or "wf_act_remove_var") {
            var mutation = name == "wf_act_remove_var" ? WiredVariableMutation.Remove : p[1] == 1 ? WiredVariableMutation.Replace : WiredVariableMutation.Give;
            var changed = false;
            var initial = literal;

            // Given from a variable: its value on each holder itself, or the first value its reference holders have.
            if (operandReference is { } given && !sameHolder) {
                var (referenceUsers, referenceFurni) = p.Length == GiveReferenced ? (p[8], p[9]) : (p[3], p[4]);

                if (FirstValue(configuration, given, referenceUsers, referenceFurni, frame) is not { } first) {
                    return false;
                }

                initial = first;
            }

            foreach (var target in targets) {
                var value = initial;

                if (operandReference is { } own && sameHolder) {
                    if (variables.Read(own, target, frame) is not { } held) {
                        continue;
                    }

                    value = held.Value;
                }

                changed |= variables.Mutate(reference, target, mutation, name == "wf_act_remove_var" ? 0 : value, frame);
            }

            return changed;
        }

        if (name is "wf_cnd_has_var" or "wf_cnd_neg_has_var") {
            var outcomes = targets.Select(target => variables.Read(reference, target, frame) is not null).ToArray();

            if (outcomes.Length == 0) {
                return false;
            }

            var matches = Quantify(outcomes, p[^1]);

            return name == "wf_cnd_neg_has_var" ? !matches : matches;
        }

        if (name == "wf_cnd_var_age_match") {
            // Volt duration units: milliseconds, seconds, minutes, hours, days, weeks, months, years.
            long[] units = [1, 1000, 60000, 3600000, 86400000, 604800000, 2592000000, 31536000000];
            long duration = p[3];

            // The October editor can read the duration, in the chosen unit, from a variable.
            if (operandReference is { } timeout) {
                var (durationUsers, durationFurni) = p.Length == AgeReferenced ? (p[9], p[10]) : (p[5], p[6]);

                if (FirstValue(configuration, timeout, durationUsers, durationFurni, frame) is not { } read) {
                    return false;
                }

                duration = read;
            }

            var durationTicks = (decimal)duration * units[p[4]] * TimeSpan.TicksPerMillisecond;
            var now = clock.GetUtcNow();

            return Quantify(targets.Select(target =>
            {
                var current = variables.Read(reference, target, frame);

                if (current is null) {
                    return false;
                }

                // A global has no readable creation time (its catalogue row lists canReadCreationTime false). Observed on AIR
                // WIN63-202610151745-318700621 (wf_cnd_var_age_match.pass_fail): "created less than 60 s ago" fails for a global placed
                // seconds earlier.
                var timestamp = p[1] == 1 ? current.UpdatedAt : reference.Target == WiredVariableTarget.Global ? null : current.CreatedAt;

                if (timestamp is null) {
                    return false;
                }

                var ageTicks = now <= timestamp.Value ? 0 : (now - timestamp.Value).Ticks;

                return p[2] == 0 ? ageTicks < durationTicks : ageTicks > durationTicks;
            }), p[^1]);
        }

        var operands = new List<(WiredVariableHolder Holder, long Value)>();
        var unary = name == "wf_act_change_var_val" && WiredVariableArithmetic.IsUnary(p[1]);

        // Same holder: each destination holder supplies its own operand from the reference variable.
        if (sameHolder && !unary) {
            var own = new WiredVariableReference((WiredVariableTarget)p[4], tokens[1]);
            using var ownReads = variables.CaptureReads([own], frame);
            var paired = targets.Select(target => (Target: target, Operand: ownReads.Read(own, target, frame)))
                .Where(pair => pair.Operand is not null).Select(pair => (pair.Target, pair.Operand!.Value)).ToArray();

            if (name == "wf_cnd_var_val_match") {
                // A holder without the reference variable does not match.
                return Quantify(targets.Select(target => ownReads.Read(own, target, frame) is { } operandValue
                    && variables.Read(reference, target, frame) is { } value && WiredVariablePredicates.Compare(p[1], value.Value, operandValue.Value)), p[^1]);
            }

            return ChangeEach(reference, paired, p[1], frame);
        }

        if (p[2] == 1 && !unary) {
            var operandVariable = new WiredVariableReference((WiredVariableTarget)p[4], tokens[1]);
            var picked = Picks(configuration, p[8]);
            using var reads = variables.CaptureReads([operandVariable], frame);

            foreach (var source in Select(frame, operandVariable.Target, p[7], p[8], picked)) {
                if (reads.Read(operandVariable, source, frame) is { } value) {
                    operands.Add((source, value.Value));
                }
            }

            if (operands.Count == 0) {
                return false;
            }
        }

        var operand = operands.Count == 0 ? literal : operands[0].Value;

        if (name == "wf_cnd_var_val_match") {
            return Quantify(targets.Select(target => variables.Read(reference, target, frame) is { } value
                && WiredVariablePredicates.Compare(p[1], value.Value, operand)), p[^1]);
        }

        return ChangeEach(reference, targets.Select(target => (target, operand)).ToArray(), p[1], frame);
    }

    private bool ChangeEach(WiredVariableReference reference, (WiredVariableHolder Target, long Operand)[] targets, int operation,
        WiredVariableFrame frame)
    {
        var any = false;

        foreach (var (target, operand) in targets) {
            if (frame.VariableChanges is { } batch) {
                // Presence is judged when the batch flushes, so a Change may run ahead of the Give that creates it.
                batch.Add(variables, reference, target, operation, operand, frame);
                any |= variables.Read(reference, target, frame) is not null;
            }
            else {
                any |= variables.Change(reference, target, WiredVariableMutation.Set, current => WiredVariableArithmetic.Apply(operation, current, operand), frame, notifyUnchanged: true);
            }
        }

        return any;
    }

    /// <summary>The first value a reference variable has among the holders its source picks.</summary>
    private long? FirstValue(WiredConfiguration configuration, WiredVariableReference reference, int userSource, int furniSource,
        WiredVariableFrame frame)
    {
        using var reads = variables.CaptureReads([reference], frame);

        foreach (var source in Select(frame, reference.Target, userSource, furniSource, Picks(configuration, furniSource))) {
            if (reads.Read(reference, source, frame) is { } value) {
                return value.Value;
            }
        }

        return null;
    }

    internal static IEnumerable<uint> Picks(WiredConfiguration configuration, int source) =>
        source == 101 ? configuration.SecondarySelectedItems : configuration.SelectedItems;

    public static IEnumerable<WiredVariableHolder> Select(WiredVariableFrame frame, WiredVariableTarget target,
        int userSource, int furniSource, IEnumerable<uint> picked)
    {
        if (target is WiredVariableTarget.Context or WiredVariableTarget.Global) {
            return [new(target, 0, 0)];
        }

        var source = target == WiredVariableTarget.User ? userSource : furniSource;

        if (frame.ResolveSource is { } resolve) {
            return resolve(target, source, picked).Where(frame.Contains).Distinct();
        }

        var selected = picked.ToHashSet();
        var candidates = source switch
        {
            0 or 11 => frame.Trigger,
            100 or 101 => frame.Holders.Where(x => x.Target == WiredVariableTarget.Furni && selected.Contains(unchecked((uint)x.EntityId))),
            200 => frame.Selector,
            201 => frame.Signal,
            _ => []
        };

        return candidates.Where(x => x.Target == target && frame.Contains(x)).Distinct();
    }
    private static bool Quantify(IEnumerable<bool> values, int quantifier)
    {
        var outcomes = values.ToArray();

        return outcomes.Length > 0 && (quantifier == 1 ? outcomes.Any(x => x) : outcomes.All(x => x));
    }
    private static bool ValidToken(string token) => WiredVariableModule.TryDefinitionId(token, out _)
        || (token.StartsWith("internal:@", StringComparison.Ordinal) || token.StartsWith("internal:~", StringComparison.Ordinal)) && token.Length > 10;
}
