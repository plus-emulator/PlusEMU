using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Selectors;

namespace Plus.HabboHotel.Items.Wired.Modern.Addons;

public sealed record WiredAddonVariableRequest(int Target, string Token, int UserSource, int FurniSource,
    WiredConfiguration Configuration);
public sealed record WiredAddonInputs(WiredSelectorWorld World, WiredSelectorInputs Selection, long NowMs,
    Func<WiredAddonVariableRequest, long?>? ReadVariable = null,
    Func<int, WiredConfiguration, IEnumerable<uint>>? ResolveFurni = null,
    Func<int, IEnumerable<int>>? ResolveUsers = null);

/// <summary>One instance per placed addon; Reset is required on configure, move and pickup.</summary>
public sealed class WiredAddonModule
{
    public static IReadOnlyList<string> Names { get; } =
    [
        "wf_xtra_anim_time", "wf_xtra_mov_no_animation", "wf_xtra_mov_carry_users", "wf_xtra_or_eval",
        "wf_xtra_exec_in_order", "wf_xtra_execution_limit", "wf_xtra_text_output_furni_name",
        "wf_xtra_filter_furni", "wf_xtra_mov_curve", "wf_xtra_mov_physics", "wf_xtra_rotate_to_dir",
        "wf_xtra_random", "wf_xtra_unseen", "wf_xtra_filter_users", "wf_xtra_text_output_username"
    ];
    private readonly Random _random;
    private IWiredActionPicker? _picker;
    private long _windowStartMs;
    private int _executionsInWindow;
    public string Name { get; }
    public WiredConfiguration Configuration { get; private set; }

    public WiredAddonModule(string name, WiredConfiguration configuration, Random? random = null)
    {
        Name = name;
        _random = random ?? Random.Shared;
        Configuration = WiredAddonConfiguration.Normalize(name, configuration);
        CreatePicker();
    }

    public void Configure(WiredConfiguration configuration)
    {
        Configuration = WiredAddonConfiguration.Normalize(Name, configuration);
        Reset();
        CreatePicker();
    }

    public void Reset()
    {
        _windowStartMs = 0;
        _executionsInWindow = 0;
        _picker?.Reset();
    }

    public bool Apply(WiredAddonInputs input, WiredAddonPolicy policy, WiredConfiguration? snapshot = null)
    {
        var c = snapshot is null ? Configuration : WiredAddonConfiguration.Normalize(Name, snapshot);
        int P(int index) => WiredSelectorSources.Param(c, index);
        HashSet<uint> Furni(int source) => (input.ResolveFurni?.Invoke(source, c)
            ?? WiredSelectorSources.Furni(source, c, input.Selection, input.World)).ToHashSet();
        HashSet<int> Users(int source) => (input.ResolveUsers?.Invoke(source)
            ?? WiredSelectorSources.Users(source, input.Selection, input.World)).ToHashSet();

        switch (Name) {
            case "wf_xtra_anim_time":
                policy.AnimationTimeMs = P(0);
                break;
            case "wf_xtra_mov_no_animation":
                policy.DisableAnimation = true;
                break;
            case "wf_xtra_mov_carry_users":
                policy.Carry = new(P(0) == 1, Users(P(1)));
                break;
            case "wf_xtra_mov_physics":
                policy.Physics = new(P(0) == 1, P(1) == 1 ? Furni(P(4)) : [],
                    P(2) == 1 ? Users(P(6)) : [], P(3) == 1 ? Furni(P(5)) : []);
                break;
            case "wf_xtra_or_eval":
                // Condition scope is not a furniture target, so furniture limits must not subset it.
                policy.Conditions = new((WiredConditionEvaluation)P(0), P(1), P(2),
                    WiredSelectorSources.Furni(P(1), c, input.Selection, input.World).ToHashSet());
                break;
            case "wf_xtra_exec_in_order":
                policy.ExecuteInOrder = true;
                break;
            case "wf_xtra_execution_limit":
                return Acquire(input.NowMs, P(0), P(1));
            case "wf_xtra_random":
            case "wf_xtra_unseen":
                policy.ActionPicker = _picker;
                break;
            case "wf_xtra_filter_furni":
            case "wf_xtra_filter_users": {
                    long count = P(0);

                    if (P(1) == 1) {
                        var token = c.VariableIds.FirstOrDefault() ?? "";
                        count = ReadVariable(input, P(2), token, P(3), P(4), c) ?? count;
                    }

                    var keep = (int)Math.Clamp(count, 0, 10000);

                    if (Name == "wf_xtra_filter_furni") {
                        var literalIsTheLimit = P(1) == 0 && keep < (policy.FurniLimit ?? int.MaxValue);
                        policy.FurniLimit = Math.Min(policy.FurniLimit ?? int.MaxValue, keep);

                        // A literal furniture amount that sets the limit keeps a random subset of its picks; a variable-mode filter keeps the first entries.
                        if (P(1) == 1) {
                            policy.FurniFilterRandom = null;
                        }
                        else if (literalIsTheLimit) {
                            policy.FurniFilterRandom = _random;
                        }
                    }
                    else {
                        // Observed on AIR WIN63-202610131318-860371703: a literal Filter To X Users keeps a random N on each firing, like furni.
                        var literalIsTheLimit = P(1) == 0 && keep < (policy.UserLimit ?? int.MaxValue);
                        policy.UserLimit = Math.Min(policy.UserLimit ?? int.MaxValue, keep);

                        if (P(1) == 1) {
                            policy.UserFilterRandom = null;
                        }
                        else if (literalIsTheLimit) {
                            policy.UserFilterRandom = _random;
                        }
                    }

                    break;
                }
            case "wf_xtra_text_output_furni_name":
            case "wf_xtra_text_output_username": {
                    var parts = c.Text.Split('\t', 2);

                    if (parts[0].Length == 0) {
                        break;
                    }

                    var token = "$(" + parts[0] + ")";
                    policy.TextFormatters.Add((current, text) =>
                    {
                        IEnumerable<string> names;

                        if (Name == "wf_xtra_text_output_furni_name") {
                            var byId = current.World.Furni.ToDictionary(x => x.Id);
                            var ids = current.ResolveFurni?.Invoke(P(1), c)
                                ?? WiredSelectorSources.Furni(P(1), c, current.Selection, current.World);

                            if (P(1) == 100) {
                                // Resolver eligibility enforces caps/identity; saved order determines names and single-first.
                                var eligible = ids.ToHashSet();
                                ids = c.SelectedItems.Distinct().Where(eligible.Contains);
                            }

                            names = ids.Where(byId.ContainsKey).Select(id => byId[id].Name);
                        }
                        else {
                            var byId = current.World.Users.ToDictionary(x => x.Id);
                            names = (current.ResolveUsers?.Invoke(P(1))
                                ?? WiredSelectorSources.Users(P(1), current.Selection, current.World))
                                .Where(byId.ContainsKey).Select(id => byId[id].Name);
                        }

                        var value = P(0) == 2 ? string.Join(parts[1], names) : names.FirstOrDefault() ?? "";

                        return text.Replace(token, value, StringComparison.Ordinal);
                    });
                    break;
                }
            case "wf_xtra_mov_curve": {
                    long? strength = P(3) == 1 ? ReadVariable(input, P(4), c.VariableIds.FirstOrDefault() ?? "", P(5), P(6), c) : P(2);

                    if (strength is not null) {
                        policy.Curve = new(P(0), P(1), (int)Math.Clamp(strength.Value, -1000, 1000));
                    }

                    break;
                }
            case "wf_xtra_rotate_to_dir": {
                    var distance = (WiredProjectileDistance)P(14);
                    long? tiles = distance != WiredProjectileDistance.Normal && P(15) == 1
                        ? ReadVariable(input, P(17), ProjectileVariable(c, 1), P(21), P(22), c) : P(16);

                    if (tiles is null) {
                        distance = WiredProjectileDistance.Normal;
                    }

                    long time = P(4);

                    if (P(2) == 1 && P(3) == 1) {
                        time = ReadVariable(input, P(5), ProjectileVariable(c, 0), P(19), P(20), c) ?? time;
                    }

                    policy.Projectile = new(P(24) == WiredProjectilePolicy.StackMovedSource ? [] : Furni(P(24)), P(0) == 1 ? P(1) : null, P(10),
                    P(18) == 0 ? null : P(18), distance, (int)Math.Clamp(tiles ?? 0, -64, 64))
                    {
                        AppliesToAllMovedFurniture = P(24) == WiredProjectilePolicy.StackMovedSource,
                        VariableMask = P(11),
                        TimePerTileMs = P(2) == 1 ? (int)Math.Clamp(time, 1, 100000) : null,
                        SpeedIncrease = P(9),
                        MeasureX = P(6) == 1,
                        MeasureY = P(7) == 1,
                        MeasureAltitude = P(8) == 1,
                        ShooterSource = P(0) == 1 && P(12) == 1 ? P(23) : null,
                        BunnyHop = P(13) == 1
                    };
                    break;
                }
            default:
                throw new InvalidOperationException("Unsupported addon passed normalization");
        }

        return true;
    }

    // An unpicked projectile variable slot is "0" (the hotel's editor frames) or "n" (older saves); both read as no variable.
    private static string ProjectileVariable(WiredConfiguration c, int slot) =>
        c.VariableIds.ElementAtOrDefault(slot) is { } id && !Plus.HabboHotel.Items.Wired.Variables.WiredVariableAbsent.Is(id) ? id : "";

    private static long? ReadVariable(WiredAddonInputs input, int target, string token, int users, int furni,
        WiredConfiguration c)
    {
        if (input.ReadVariable is null) {
            throw new InvalidOperationException("Variable operand reader is required");
        }

        return input.ReadVariable(new(target, token, users, furni, c));
    }

    // The window counts whole wired pulses (500 ms), not milliseconds since the first execution. Observed on AIR
    // WIN63-202610151745-318700621 (wf_xtra_execution_limit.burst): with 2 executions per 4 pulses, six says one second apart all ran.
    private bool Acquire(long now, int max, int window)
    {
        const int pulse = 500;
        var start = _windowStartMs / pulse;
        var current = now / pulse;

        if (now < _windowStartMs || current - start >= Math.Max(1, window / pulse)) {
            _windowStartMs = now;
            _executionsInWindow = 0;
        }

        if (_executionsInWindow >= max) {
            return false;
        }

        _executionsInWindow++;

        return true;
    }

    private void CreatePicker() => _picker = Name switch
    {
        "wf_xtra_random" => new WiredRandomActionPicker(Configuration.IntParams[0], Configuration.IntParams[1], _random),
        "wf_xtra_unseen" => new WiredUnseenActionPicker(),
        _ => null
    };
}
