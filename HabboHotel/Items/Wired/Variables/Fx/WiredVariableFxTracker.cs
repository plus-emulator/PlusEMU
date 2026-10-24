using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;

namespace Plus.HabboHotel.Items.Wired.Variables.Fx;

/// <summary>
/// Per-viewer deltas. Pass only holders whose room objects the viewer has received. Call after variable,
/// team, config, placement or builtin changes. Remove the viewer on leave; never share this state between rooms.
/// </summary>
public sealed class WiredVariableFxTracker(WiredVariableModule variables, int maxStatuses = 1000)
{
    private readonly Dictionary<long, Viewer> _viewers = [];
    private readonly Dictionary<long, (WiredVariableFxBatch Batch, Viewer State)> _pending = [];
    public WiredVariableReadSnapshot CaptureReads(WiredVariableFrame frame, IReadOnlyList<WiredVariableFxBinding> bindings) =>
        variables.CaptureReads(bindings.SelectMany(x => new[] { x.Variable, x.OverrideMin, x.OverrideMax, x.Audience })
            .OfType<WiredVariableReference>(), frame);
    public void RemoveViewer(long stableUserId)
    {
        _viewers.Remove(stableUserId);
        _pending.Remove(stableUserId);
    }
    /// <summary>Call on room-unit/furniture removal, even if the same holder returns before the next flush.</summary>
    public void DetachHolder(WiredVariableHolder holder)
    {
        if (holder.Target is not (WiredVariableTarget.User or WiredVariableTarget.Furni)) {
            return;
        }

        foreach (var viewer in _viewers.Values) {
            foreach (var key in viewer.Statuses.Keys.Where(x => x.EntityId == holder.EntityId && x.UserEntity == (holder.Target == WiredVariableTarget.User)).ToArray()) {
                viewer.Statuses.Remove(key);
                viewer.Holders.Remove(key);
            }

            viewer.Ready.RemoveWhere(x => x.Target == holder.Target && x.EntityId == holder.EntityId);
        }

        // A previously composed batch may still contain the detached object. It must not become acknowledged state.
        _pending.Clear();
    }
    /// <summary>Call after all packets were composed/enqueued without failure; this does not acknowledge network delivery. Reset the viewer on disconnect/send failure.</summary>
    public bool Acknowledge(long stableUserId, WiredVariableFxBatch batch)
    {
        if (!_pending.TryGetValue(stableUserId, out var pending) || !ReferenceEquals(pending.Batch, batch)) {
            return false;
        }

        _viewers[stableUserId] = pending.State;
        _pending.Remove(stableUserId);

        return true;
    }

    public WiredVariableFxBatch Update(WiredVariableHolder viewer, WiredVariableFrame frame,
        IReadOnlyList<WiredVariableFxBinding> bindings, IReadOnlyList<WiredVariableHolder> readyHolders,
        Func<WiredVariableHolder, int> team, WiredVariableReadSnapshot? reads = null, IReadOnlySet<WiredVariableKey>? rewritten = null)
    {
        if (reads is null) {
            using var snapshot = CaptureReads(frame, bindings);

            return Update(viewer, frame, bindings, readyHolders, team, snapshot, rewritten);
        }

        if (viewer.Target != WiredVariableTarget.User || !viewer.CanPersist || !frame.Contains(viewer)) {
            throw new ArgumentException("FX viewer must be a present player.", nameof(viewer));
        }

        if (!_viewers.TryGetValue(viewer.StableId, out var state)) {
            state = new();
        }

        // A box binds once per variable on its tile; its config is sent once.
        var configs = bindings.DistinctBy(x => x.Config.Id).ToDictionary(x => x.Config.Id);
        var changedConfigs = configs.Values.Where(x => !state.Configs.TryGetValue(x.Config.Id, out var old) || old != Signature(x.Config))
            .Select(x => x.Config).ToArray();
        var removedConfigs = state.Configs.Keys.Where(x => !configs.ContainsKey(x)).ToArray();
        var resent = changedConfigs.Select(x => x.Id).ToHashSet();
        var wanted = new Dictionary<WiredVariableFxKey, WiredVariableFxStatus>();
        var identities = new Dictionary<WiredVariableFxKey, WiredVariableHolder>();
        var resend = new HashSet<WiredVariableFxKey>();

        foreach (var binding in bindings) {
            foreach (var holder in readyHolders) {
                if (wanted.Count >= maxStatuses) {
                    break;
                }

                if (holder.Target != binding.Variable.Target || !frame.Contains(holder) || !CanSee(binding, viewer, holder, frame, team, reads)) {
                    continue;
                }

                var value = reads.Read(binding.Variable, holder, frame);

                if (value is null) {
                    continue;
                }

                var min = ReadOverride(binding.OverrideMin, holder, frame, reads);
                var max = ReadOverride(binding.OverrideMax, holder, frame, reads);

                if (min is not null || max is not null) {
                    min ??= binding.Config.Min;
                    max ??= binding.Config.Max;

                    if (max <= min) {
                        max = min + 1;
                    }
                }

                var extra = ImmutableSortedDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);

                if (binding.Config.ColorId == 1002) {
                    var color = WiredVariableFxSettings.TeamColorOf(team(holder));

                    if (color is not null) {
                        extra["delegated_color"] = color;
                    }
                }

                // A levelling box on a variable without a level-up system sends empty extras.
                if ((binding.Config.Category == 2 || binding.Config.ColorId == 1001) && binding.Level?.Invoke(value.Value) is { } level) {
                    extra["current_level"] = level.Level.ToString(CultureInfo.InvariantCulture);
                    extra["max_level"] = level.MaxLevel.ToString(CultureInfo.InvariantCulture);
                    extra["is_maxed"] = level.IsMaxed ? "true" : "false";
                    min = level.Start;
                    max = level.Next;
                }

                var wireVariable = WiredVariableModule.TryDefinitionId(binding.Variable.Token, out var definitionId)
                    ? (binding.Config.UserFx ? "user:" : "furni:") + definitionId : binding.Variable.Token;
                var key = new WiredVariableFxKey(binding.Config.Id, wireVariable, binding.Config.UserFx, holder.EntityId);
                identities[key] = holder;
                // A variable given at runtime to a user or furni the viewer already has is sent with initialize false; a first sync or a saved box sends all its
                // statuses again, each with initialize true. A holder the viewer did not have before still starts initialized.
                var initialize = !state.Synced || resent.Contains(binding.Config.Id) || !state.Ready.Contains(holder)
                    || state.Holders.TryGetValue(key, out var previousHolder) && previousHolder != holder;

                // A Give with override reports the value again even when it did not change.
                if (rewritten is not null && WiredVariableModule.TryDefinitionId(binding.Variable.Token, out var written) && rewritten.Contains(new(written, holder.Target, holder.StorageId))) {
                    resend.Add(key);
                }

                wanted[key] = new(key, initialize, value.Value, min, max, extra.ToImmutable());
            }
        }

        var removedStatuses = state.Statuses.Keys.Where(key => !wanted.ContainsKey(key)).ToArray();
        var statuses = wanted.Where(x => x.Value.Initialize || resend.Contains(x.Key) || !state.Statuses.TryGetValue(x.Key, out var old) || old != Signature(x.Value with { Initialize = false }))
            .Select(x => x.Value).ToArray();
        var batch = new WiredVariableFxBatch(!state.Synced || changedConfigs.Length > 0, changedConfigs, removedConfigs, statuses, removedStatuses);
        _pending[viewer.StableId] = (batch, new Viewer
        {
            Configs = configs.ToDictionary(x => x.Key, x => Signature(x.Value.Config)),
            Statuses = wanted.ToDictionary(x => x.Key, x => Signature(x.Value with { Initialize = false })),
            Holders = identities,
            Ready = [.. readyHolders],
            Synced = true
        });

        return batch;
    }

    private static long? ReadOverride(WiredVariableReference? reference, WiredVariableHolder holder, WiredVariableFrame frame, WiredVariableReadSnapshot reads)
    {
        if (reference is null) {
            return null;
        }

        var target = reference.Target == WiredVariableTarget.Global ? new(WiredVariableTarget.Global, 0, 0) : holder;

        return reads.Read(reference, target, frame)?.Value;
    }
    private static bool CanSee(WiredVariableFxBinding binding, WiredVariableHolder viewer, WiredVariableHolder holder,
        WiredVariableFrame frame, Func<WiredVariableHolder, int> team, WiredVariableReadSnapshot reads)
    {
        var visibility = !binding.Config.UserFx && binding.Visibility is 0 or 1 ? 2 : binding.Visibility;

        return visibility switch
        {
            0 => viewer.StableId == holder.StableId,
            1 => viewer.StableId == holder.StableId || team(holder) != 0 && team(holder) == team(viewer),
            2 => true,
            3 => binding.Audience is { } audience && reads.Read(audience, viewer, frame) is not null,
            4 => binding.Audience is { } audience && reads.Read(audience, viewer, frame)?.Value == binding.AudienceValue,
            _ => false
        };
    }
    private static string Signature<T>(T value) => JsonSerializer.Serialize(value);
    private sealed class Viewer
    {
        public bool Synced { get; set; }
        public Dictionary<int, string> Configs { get; set; } = [];
        public Dictionary<WiredVariableFxKey, string> Statuses { get; set; } = [];
        public Dictionary<WiredVariableFxKey, WiredVariableHolder> Holders { get; set; } = [];
        public HashSet<WiredVariableHolder> Ready { get; set; } = [];
    }
}
