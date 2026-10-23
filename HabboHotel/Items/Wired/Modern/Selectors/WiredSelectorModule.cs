using System.Globalization;
using System.Runtime.CompilerServices;
using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired.Modern.Selectors;

/// <summary>Selection behavior uses Volt's per-box fields; inversion/filtering is composed exactly once.</summary>
public static class WiredSelectorModule
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "wf_slc_furni_bytype", "wf_slc_furni_picks", "wf_slc_users_bytype", "wf_slc_users_team",
        "wf_slc_furni_onfurni", "wf_slc_furni_signal", "wf_slc_furni_neighborhood", "wf_slc_furni_area",
        "wf_slc_users_onfurni", "wf_slc_users_byaction", "wf_slc_users_signal", "wf_slc_users_byname",
        "wf_slc_users_neighborhood", "wf_slc_users_area", "wf_slc_users_handitem", "wf_slc_users_group",
        "wf_slc_furni_altitude", "wf_slc_furni_with_var", "wf_slc_users_with_var", "wf_slc_remote"
    ];

    public static WiredSelectorResult SelectRaw(string name, WiredConfiguration configuration,
        WiredSelectorWorld world, WiredSelectorInputs input, Random? remoteRandom = null) =>
        SelectRaw(name, configuration, world, input, [], remoteRandom ?? Random.Shared);

    private static WiredSelectorResult SelectRaw(string name, WiredConfiguration c,
        WiredSelectorWorld world, WiredSelectorInputs input, HashSet<uint> visiting, Random remoteRandom)
    {
        if (!Names.Contains(name, StringComparer.Ordinal)) {
            throw new ArgumentException("Not a supported selector", nameof(name));
        }

        c = WiredSelectorConfiguration.Normalize(name, c);

        // Furni In Area and Furni On Furni select the Wired boxes in their scope like any other floor furni (the box stack itself, a variable definition box).
        var available = world.Furni.Where(x => x.IsFloor).ToList();
        var selected = new WiredSelectedIds();
        var target = name == "wf_slc_remote" ? WiredSelectorTarget.Both
            : name.StartsWith("wf_slc_users_", StringComparison.Ordinal) ? WiredSelectorTarget.User : WiredSelectorTarget.Furni;
        var (_, invertIndex) = SwitchIndexes(name);
        var filter = FiltersExistingNormalized(name, c);
        var invert = c.Origin?.Native.Inverse ?? WiredSelectorSources.Param(c, invertIndex) == 1;
        int P(int index, int fallback = 0) => WiredSelectorSources.Param(c, index, fallback);
        IEnumerable<WiredSelectorFurniture> Furni(int source) =>
            world.Furni.Where(x => WiredSelectorSources.Furni(source, c, input, world).Contains(x.Id));

        switch (name) {
            case "wf_slc_furni_picks":
                // Picked Wired boxes stay in the selection: Call Stacks with the furni-picks selector picking another stack's trigger box runs that stack.
                selected.FurniIds.UnionWith(world.Furni.Where(x => c.SelectedItems.Contains(x.Id)).Select(x => x.Id));
                break;
            case "wf_slc_furni_signal":
                // A Wired box sent with a signal arrives in Furni From Signal.
                selected.FurniIds.UnionWith(world.Furni.Where(x => input.Signal.FurniIds.Contains(x.Id)).Select(x => x.Id));
                break;
            case "wf_slc_users_signal":
                selected.UserIds.UnionWith(world.Users.Where(x => input.Signal.UserIds.Contains(x.Id)).Select(x => x.Id));
                break;
            case "wf_slc_furni_bytype": {
                    var source = c.FurniSources.GetValueOrDefault("items", P(0) switch { 0 => 100, 1 => 201, 2 => 0, _ => throw new ArgumentException("Unknown type source") });
                    var matchState = P(1) != 0;
                    var kinds = Furni(source).Select(x => (x.DefinitionId, State: matchState ? x.State : "")).ToHashSet();

                    foreach (var item in world.Furni) {
                        if (kinds.Contains((item.DefinitionId, matchState ? item.State : ""))) {
                            selected.FurniIds.Add(item.Id);
                        }
                    }

                    break;
                }
            // The type is one kind, not a bit mask (3 selects nobody).
            case "wf_slc_users_bytype":
                selected.UserIds.UnionWith(world.Users.Where(x => (int)x.Kind == P(0, 1)).Select(x => x.Id));
                break;
            case "wf_slc_users_team":
                selected.UserIds.UnionWith(world.Users.Where(x => x.Kind == WiredSelectorEntityKind.Player
                    && x.Team != 0 && (P(0) == 0 || x.Team == P(0))).Select(x => x.Id));
                break;
            case "wf_slc_users_byname": {
                    // Only a tab separates names (the editors turn line breaks into tabs), and a name keeps its spaces, so " Robbie" or "Robbie " matches nobody.
                    var names = c.Text.Split('\t', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    selected.UserIds.UnionWith(world.Users.Where(x => names.Contains(x.Name)).Select(x => x.Id));
                    break;
                }
            case "wf_slc_users_handitem":
                // Hand item 0 selects the Habbos holding nothing but not a bot standing next to them; the selector only looks at Habbos.
                selected.UserIds.UnionWith(world.Users.Where(x => x.Kind == WiredSelectorEntityKind.Player && x.HandItem == P(0)).Select(x => x.Id));
                break;
            case "wf_slc_users_group": {
                    var groupId = P(0) == 0 ? world.RoomGroupId : P(1);

                    if (groupId > 0) {
                        // Every member of the group is selected, whether or not it is their favourite group.
                        selected.UserIds.UnionWith(world.Users.Where(x => x.Kind == WiredSelectorEntityKind.Player
                            && (world.IsGroupMember?.Invoke(x.Id, groupId) ?? x.GroupIds?.Contains(groupId) == true)).Select(x => x.Id));
                    }

                    break;
                }
            case "wf_slc_users_byaction":
                selected.UserIds.UnionWith(world.Users.Where(x => MatchesAction(x, c, input)).Select(x => x.Id));
                break;
            case "wf_slc_furni_area":
            case "wf_slc_users_area": {
                    // Compare coordinates rather than iterate an unchecked client-sized rectangle.
                    bool InArea(int x, int y) => x >= P(0) && y >= P(1) && x < (long)P(0) + P(2)
                        && y < (long)P(1) + P(3) && x >= 0 && y >= 0 && x < world.Width && y < world.Height;

                    if (target == WiredSelectorTarget.Furni) {
                        selected.FurniIds.UnionWith(available.Where(x => x.Tiles.Any(t => InArea(t.X, t.Y))).Select(x => x.Id));
                    }
                    else {
                        selected.UserIds.UnionWith(world.Users.Where(x => InArea(x.X, x.Y)).Select(x => x.Id));
                    }

                    break;
                }
            case "wf_slc_furni_altitude": {
                    if (!decimal.TryParse(string.IsNullOrWhiteSpace(c.Text) ? "0" : c.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var altitude)) {
                        throw new ArgumentException("Altitude must be an invariant number");
                    }

                    // A tile stacked to 0.6 equals 60, and the stored comparisons 3, 4 and 5 are <=, != and >=. A height compares as a decimal, so the double sum 0.2 + 0.2 + 0.2 is 0.6.
                    selected.FurniIds.UnionWith(available.Where(x => (decimal)x.Z is var z && (P(0, 1) switch
                    {
                        0 => z < altitude,
                        1 => z == altitude,
                        2 => z > altitude,
                        3 => z <= altitude,
                        4 => z != altitude,
                        5 => z >= altitude,
                        _ => false
                    })).Select(x => x.Id));
                    break;
                }
            case "wf_slc_furni_onfurni":
            case "wf_slc_users_onfurni": {
                    var sourceIndex = target == WiredSelectorTarget.Furni ? 1 : 0;
                    var source = P(sourceIndex);

                    foreach (var item in Furni(source).Where(x => x.IsFloor)) {
                        if (target == WiredSelectorTarget.User) {
                            selected.UserIds.UnionWith(world.Users.Where(x => item.Tiles.Contains((x.X, x.Y))).Select(x => x.Id));
                            continue;
                        }

                        foreach (var other in available.Where(x => (P(0) is 2 or 3 || x.Id != item.Id) && x.Tiles.Contains((item.X, item.Y)))) {
                            if (P(0) switch
                            {
                                0 => other.Z > item.Z,
                                1 => other.Z < item.Z,
                                2 => other.Z == item.Z,
                                3 => true,
                                _ => false
                            }) {
                                selected.FurniIds.Add(other.Id);
                            }
                        }
                    }

                    break;
                }
            case "wf_slc_furni_neighborhood":
            case "wf_slc_users_neighborhood": {
                    var tiles = Neighborhood(c, world, input);

                    if (target == WiredSelectorTarget.Furni) {
                        selected.FurniIds.UnionWith(available.Where(x => x.Tiles.Any(tiles.Contains)).Select(x => x.Id));
                    }
                    else {
                        selected.UserIds.UnionWith(world.Users.Where(x => tiles.Contains((x.X, x.Y))).Select(x => x.Id));
                    }

                    break;
                }
            case "wf_slc_furni_with_var":
                if (input.FurniVariablePredicate is null && input.ScopedFurniVariablePredicate is null) {
                    throw new InvalidOperationException("Furniture variable predicate is required");
                }

                selected.FurniIds.UnionWith(available.Where(x => input.ScopedFurniVariablePredicate?.Invoke(name, c, x.Id, input) ?? input.FurniVariablePredicate!(name, c, x.Id)).Select(x => x.Id));
                break;
            case "wf_slc_users_with_var":
                if (input.UserVariablePredicate is null && input.ScopedUserVariablePredicate is null) {
                    throw new InvalidOperationException("Avatar variable predicate is required");
                }

                selected.UserIds.UnionWith(world.Users.Where(x => input.ScopedUserVariablePredicate?.Invoke(name, c, x.Id, input) ?? input.UserVariablePredicate!(name, c, x.Id)).Select(x => x.Id));
                break;
            case "wf_slc_remote": {
                    var stacks = RemoteStacks(c, world, input).ToList();

                    if (c.IntParams.Length >= 4 && P(3) > 0 && P(3) < stacks.Count) {
                        // Sample stacks once; all selectors on each chosen tile participate.
                        for (var i = stacks.Count - 1; i > 0; i--) {
                            var other = remoteRandom.Next(i + 1);
                            (stacks[i], stacks[other]) = (stacks[other], stacks[i]);
                        }

                        stacks = stacks.Take(P(3)).ToList();
                    }

                    // Union or Intersection combines the picked stacks for furni and users alike. A furni-only stack intersected with a users-only stack
                    // selects neither kind, and a picked stack without a selector is an empty operand.
                    var firstStack = true;

                    foreach (var stack in stacks) {
                        // Deeply nested stacks still select, so only a stack already on the path is skipped. The stack check only stops a chain too deep for the thread.
                        if (stack.Count > 0 && (!RuntimeHelpers.TryEnsureSufficientExecutionStack() || !visiting.Add(stack[0]))) {
                            continue;
                        }

                        // Referenced stacks have their own selector sequence, including filter/invert.
                        var stackPool = new WiredSelectedIds();
                        var furniModified = false;
                        var usersModified = false;

                        try {
                            foreach (var id in FiltersLast(stack, id => FiltersExisting(world.RemoteSelectors![id].Name, world.RemoteSelectors[id].Configuration))) {
                                var remote = world.RemoteSelectors![id];
                                var remoteInput = input with
                                {
                                    SelectorPool = stackPool,
                                    FurniModified = furniModified,
                                    UsersModified = usersModified
                                };
                                var result = SelectRaw(remote.Name, remote.Configuration, world, remoteInput, visiting, remoteRandom);
                                stackPool = Compose(result, world, remoteInput);
                                furniModified |= result.Target is WiredSelectorTarget.Furni or WiredSelectorTarget.Both;
                                usersModified |= result.Target is WiredSelectorTarget.User or WiredSelectorTarget.Both;
                            }
                        }
                        finally {
                            if (stack.Count > 0) {
                                visiting.Remove(stack[0]);
                            }
                        }

                        if (firstStack || P(2) == 0) {
                            selected.FurniIds.UnionWith(stackPool.FurniIds);
                            selected.UserIds.UnionWith(stackPool.UserIds);
                        }
                        else {
                            selected.FurniIds.IntersectWith(stackPool.FurniIds);
                            selected.UserIds.IntersectWith(stackPool.UserIds);
                        }

                        firstStack = false;
                    }

                    break;
                }
        }

        return new(selected, target, filter, invert);
    }

    internal static IReadOnlyList<IReadOnlyList<uint>> RemoteStacks(WiredConfiguration c, WiredSelectorWorld world, WiredSelectorInputs? input = null)
    {
        var stacks = new List<IReadOnlyList<uint>>();
        var seen = new HashSet<(int X, int Y, uint UnprojectedId)>();
        var source = WiredSelectorSources.Param(c, 4, WiredSources.Selected);
        var pickedIds = input is null ? c.SelectedItems : WiredSelectorSources.Furni(source, c, input, world);

        foreach (var pickedId in pickedIds) {
            var picked = world.Furni.FirstOrDefault(item => item.Id == pickedId && item.IsFloor);

            if (picked is null) {
                // Custom projections can supply a selector without a furniture model.
                if (world.RemoteSelectors?.ContainsKey(pickedId) == true && seen.Add((0, 0, pickedId))) {
                    stacks.Add(new[] { pickedId });
                }

                continue;
            }

            if (!seen.Add((picked.X, picked.Y, 0))) {
                continue;
            }

            var selectors = world.Furni.Where(item => item.IsFloor && item.X == picked.X && item.Y == picked.Y
                && world.RemoteSelectors?.ContainsKey(item.Id) == true)
                .OrderBy(item => item.Z).ThenBy(item => item.Id).Select(item => item.Id).ToArray();

            // A picked stack without a selector still counts, as an empty one.
            stacks.Add(selectors);
        }

        return stacks;
    }

    public static WiredSelectedIds Compose(WiredSelectorResult result, WiredSelectorWorld world, WiredSelectorInputs input)
    {
        var output = input.SelectorPool.Copy();

        if (result.Target is WiredSelectorTarget.Furni or WiredSelectorTarget.Both) {
            // Inversion includes wall furniture and the executing Wired stack.
            var kept = result.Invert ? world.Furni
                .Select(x => x.Id).Except(result.Selection.FurniIds) : result.Selection.FurniIds;

            if (result.FiltersExisting) {
                var basis = input.FurniModified ? input.SelectorPool.FurniIds : [];
                output.FurniIds.Clear();
                output.FurniIds.UnionWith(basis.Intersect(kept));
            }
            else {
                output.FurniIds.UnionWith(kept);
            }
        }

        if (result.Target is WiredSelectorTarget.User or WiredSelectorTarget.Both) {
            var kept = result.Invert ? world.Users.Select(x => x.Id).Except(result.Selection.UserIds) : result.Selection.UserIds;

            if (result.FiltersExisting) {
                var basis = input.UsersModified ? input.SelectorPool.UserIds : [];
                output.UserIds.Clear();
                output.UserIds.UnionWith(basis.Intersect(kept));
            }
            else {
                output.UserIds.UnionWith(kept);
            }
        }

        return output;
    }

    private static HashSet<(int X, int Y)> Neighborhood(WiredConfiguration c, WiredSelectorWorld world, WiredSelectorInputs input)
    {
        int P(int i) => WiredSelectorSources.Param(c, i);

        if (c.IntParams.Length != 17) {
            throw new ArgumentException("Neighborhood requires its native 441-tile bitmap");
        }

        IEnumerable<(int X, int Y)> positions = P(0) == 1
            ? world.Users.Where(x => WiredSelectorSources.Users(c.UserSources.GetValueOrDefault("anchor", 0), input, world).Contains(x.Id)).Select(x => (x.X, x.Y))
            : world.Furni.Where(x => x.IsFloor && WiredSelectorSources.Furni(c.FurniSources.GetValueOrDefault("anchor", 100), c, input, world).Contains(x.Id)).Select(x => (x.X, x.Y));
        var offsets = NeighborhoodOffsets(c.IntParams).ToArray();
        var tiles = new HashSet<(int X, int Y)>();

        foreach (var (x, y) in positions) {
            foreach (var (dx, dy) in offsets) {
                var tx = (long)x + dx - P(1);
                var ty = (long)y + dy - P(2);

                if (tx >= 0 && ty >= 0 && tx < world.Width && ty < world.Height) {
                    tiles.Add(((int)tx, (int)ty));
                }
            }
        }

        return tiles;
    }

    internal static IEnumerable<(int X, int Y)> NeighborhoodOffsets(System.Collections.Immutable.ImmutableArray<int> fields)
    {
        var x = 0;
        var y = 0;
        var dx = 1;
        var dy = 0;
        var index = 0;
        var length = 1;

        while (index < 441) {
            for (var turn = 0; turn < 2; turn++) {
                for (var step = 0; step < length && index < 441; step++, index++) {
                    if (((uint)fields[3 + index / 32] & (1u << (index % 32))) != 0) {
                        yield return (x, y);
                    }

                    x += dx;
                    y += dy;
                }

                (dx, dy) = (dy, -dx);
            }

            length++;
        }
    }

    private static bool MatchesAction(WiredSelectorAvatar user, WiredConfiguration c, WiredSelectorInputs input)
    {
        int P(int i, int fallback = 0) => WiredSelectorSources.Param(c, i, fallback);
        var wanted = P(0, 1);

        if (wanted == 4) {
            return !user.Idle;
        }

        if (wanted == 7) {
            return !user.Sitting && !user.Lying;
        }

        bool Match(int action, int value) => action == wanted && (wanted switch
        {
            9 => P(1) == 0 || value == P(2),
            10 => P(3) == 0 || value == P(4, 1),
            _ => true
        });

        if (input.ActionUserId == user.Id && input.Action is int action && Match(action, input.ActionParameter)) {
            return true;
        }

        if (wanted switch
        {
            5 => user.Idle,
            6 => user.Sitting,
            8 => user.Lying,
            9 => user.Sign is int sign && Match(9, sign),
            10 => user.Dance > 0 && Match(10, user.Dance),
            _ => false
        }) {
            return true;
        }

        return user.LastAction is int previous && input.NowMs >= user.LastActionAtMs
            && input.NowMs - user.LastActionAtMs <= WiredSelectorRoomState.RememberedMilliseconds(previous)
            && Match(previous, user.LastActionParameter);
    }

    public static bool FiltersExisting(string name, WiredConfiguration c) =>
        Names.Contains(name, StringComparer.Ordinal) && FiltersExistingNormalized(name, WiredSelectorConfiguration.Normalize(name, c));

    private static bool FiltersExistingNormalized(string name, WiredConfiguration c) =>
        c.Origin?.Native.Filter ?? WiredSelectorSources.Param(c, SwitchIndexes(name).Filter) == 1;

    // The filters of a stack narrow what its other selectors picked wherever they stand, so they run after them, keeping the stack order within each group.
    public static IEnumerable<T> FiltersLast<T>(IEnumerable<T> selectors, Func<T, bool> filters)
    {
        var all = selectors.ToArray();

        return all.Where(selector => !filters(selector)).Concat(all.Where(filters));
    }

    internal static (int Filter, int Invert) SwitchIndexes(string name) => name switch
    {
        "wf_slc_furni_area" or "wf_slc_users_area" => (4, 5),
        "wf_slc_furni_bytype" or "wf_slc_furni_onfurni" or "wf_slc_users_group" => (2, 3),
        "wf_slc_users_byaction" => (5, 6),
        "wf_slc_furni_picks" or "wf_slc_furni_signal" or "wf_slc_users_signal" or "wf_slc_users_byname" or "wf_slc_remote" => (0, 1),
        "wf_slc_furni_with_var" or "wf_slc_users_with_var" => (7, 8),
        _ => (1, 2)
    };
}
