using System.Globalization;
using System.Collections.Immutable;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>
/// A room's variables. All callers (effects, conditions, inspector and FX) cross this seam.
/// Durable writes complete before changes enter the queue. The directory remains the authority on ownership/placement.
/// </summary>
public sealed class WiredVariableModule(uint roomId, IWiredVariableDirectory directory, IWiredVariableStore durable,
    TimeProvider clock, IWiredBuiltinVariables? builtins = null, Func<WiredVariableReference, WiredVariableDerivation?>? derive = null)
{
    private readonly MemoryWiredVariableStore _active = new();
    private readonly Queue<WiredVariableChange> _changes = new();
    private readonly object _gate = new();
    // A reference whose source was picked up stays listed as it last was until its room unloads. This module lives as long as the loaded room. In Plus, reads and writes through it still fail to resolve and do nothing.
    private readonly Dictionary<uint, ListedReference> _listedReferences = [];
    private long _valueChanges;
    private long _valueDecreases;

    /// <summary>Counts the writes that changed a stored value or its presence; unchanged accepted writes do not count.</summary>
    internal long ValueChanges => Interlocked.Read(ref _valueChanges);
    internal long ValueDecreases => Interlocked.Read(ref _valueDecreases);
    public uint RoomId => roomId;

    // The database callback commits before any active value or change event becomes visible.
    internal void PersistDefinitionConfiguration(uint definitionId, Func<WiredVariableValue?, WiredVariableDefinitionCommit> persist)
    {
        lock (_gate) {
            var key = new WiredVariableKey(definitionId, WiredVariableTarget.Global, 0);
            var committed = persist(_active.Read(key));

            if (committed.ValueWrite is not { } write) {
                return;
            }

            if (!committed.Definition.IsDurable) {
                _active.Mutate(key, _ => write.After);
            }

            if (write.Changed) {
                _changes.Enqueue(new(committed.Definition.RoomId, key,
                write.Before is null ? WiredVariableChangeKind.Created : WiredVariableChangeKind.Updated, write.Before, write.After, 0, 1)
                { Origin = 2 });
            }
        }
    }

    /// <summary>Capture once per FX/menu flush, share across viewers, then dispose. Never retain across room changes.</summary>
    public WiredVariableReadSnapshot CaptureReads(IEnumerable<WiredVariableReference> references, WiredVariableFrame frame)
    {
        lock (_gate) {
            if (frame.RoomId != roomId) {
                throw new ArgumentException("The frame belongs to another room.", nameof(frame));
            }

            var authority = new ReadDirectory(directory);
            var resolved = references.Distinct().ToDictionary(x => x, x => Resolve(x, false, authority));
            var holders = frame.Holders.Concat([new(WiredVariableTarget.Global, 0, 0), new(WiredVariableTarget.Context, 0, 0)]).Distinct().ToArray();
            var requests = new List<(WiredVariableReference Reference, WiredVariableHolder Holder, Resolved Resolved)>();

            foreach (var (reference, resolution) in resolved) {
                if (resolution is not null) {
                    foreach (var holder in holders) {
                        if (ValidateHolder(reference, holder, frame) && (resolution.Definition?.IsDurable != true || holder.CanPersist)) {
                            requests.Add((reference, holder, resolution));
                        }
                    }
                }
            }

            var values = new Dictionary<WiredVariableKey, WiredVariableValue>();

            foreach (var group in requests.Where(x => x.Resolved.Definition is not null).GroupBy(x => Store(x.Resolved.Definition!, frame))) {
                foreach (var value in group.Key.ReadMany(group.Select(x => Key(x.Resolved.Definition!, x.Holder)).Distinct().ToArray())) {
                    values[value.Key] = value.Value;
                }
            }

            var captured = new Dictionary<(WiredVariableReference, WiredVariableHolder), WiredVariableValue>();

            foreach (var (reference, holder, resolution) in requests) {
                var definition = resolution.Definition;
                var value = resolution.Builtin is { } builtin ? builtins?.Read(builtin, holder, frame)
                    : definition!.IsPointer ? ReadPointer(definition, holder, frame)
                    : Held(definition!, values.GetValueOrDefault(Key(definition!, holder)))
                        ?? (definition!.Target == WiredVariableTarget.Global ? new(definition.InitialValue, null, null) : null);

                if (value is not null && resolution.Convert is { } convert) {
                    value = convert(value);
                }

                if (value is not null) {
                    captured[(reference, holder)] = value;
                }
            }

            return new(roomId, captured);
        }
    }

    public WiredVariableValue? Read(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableFrame frame)
    {
        lock (_gate) {
            if (!ValidateHolder(reference, holder, frame)) {
                return null;
            }

            var resolved = Resolve(reference, false);

            if (resolved is null) {
                return null;
            }

            WiredVariableValue? value;

            if (resolved.Builtin is { } builtin) {
                value = builtins?.Read(builtin, holder, frame);
            }
            else {
                var definition = resolved.Definition!;

                if (definition.IsDurable && !holder.CanPersist) {
                    return null;
                }

                if (definition.IsPointer) {
                    return ReadPointer(definition, holder, frame);
                }

                var stored = Held(definition, Store(definition, frame).Read(Key(definition, holder)));
                value = stored ?? (definition.Target == WiredVariableTarget.Global ? new(definition.InitialValue, null, null) : null);
            }

            return value is not null && resolved.Convert is { } convert ? convert(value) : value;
        }
    }

    public bool Mutate(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableMutation mutation,
        long value, WiredVariableFrame frame, int origin = 0) => Change(reference, holder, mutation, _ => value, frame, origin);

    /// <summary>Arithmetic reads and writes the same locked value, including through references in another room.</summary>
    // Test seam: runs between target resolution and admission, with the attempt number.
    internal Action<int>? ResolutionHook { get; set; }

    private const int AdmissionAttempts = 3;

    public bool Change(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableMutation mutation,
        Func<long, long> transform, WiredVariableFrame frame, int origin = 0, bool notifyUnchanged = false)
    {
        // v2 gate writes are admitted to the per-gate sequencer; everything else runs the original path.
        if (builtins?.SequencesGateWrites == true) {
            return ChangeAdmitted(reference, holder, mutation, transform, frame, origin, notifyUnchanged, admittedTarget: null);
        }

        Action? completed;
        bool changed;

        lock (_gate) {
            changed = ChangeLocked(reference, holder, mutation, transform, frame, origin, notifyUnchanged, out completed);
        }

        if (changed) {
            completed?.Invoke();
        }

        return changed;
    }

    // Aliases are resolved before admission so the gate lane sees the real target, without holding the module lock
    // across it. The admitted target is then compared with the fresh authorized resolution under the lock: a fresh
    // write retries admission a few times, a replay (which carries its admitted target) refuses on any mismatch.
    private bool ChangeAdmitted(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableMutation mutation,
        Func<long, long> transform, WiredVariableFrame frame, int origin, bool notifyUnchanged, WiredVariableReference? admittedTarget)
    {
        for (var attempt = 1; attempt <= (admittedTarget is null ? AdmissionAttempts : 1); attempt++) {
            var target = admittedTarget ?? WriteTarget(reference);
            ResolutionHook?.Invoke(attempt);
            var effective = transform;
            var admission = WiredAdmission.Proceed;
            IDisposable? scope = null;

            if (builtins != null) {
                // Native gates admit bounded room state; custom variable arithmetic remains signed 64-bit.
                Func<int, int> bounded = current =>
                {
                    var next = transform(current);

                    return next is >= int.MinValue and <= int.MaxValue ? (int)next : current;
                };
                var supplied = bounded;
                scope = builtins.Admit(target, holder, ref bounded,
                    replayed => () => ChangeAdmitted(reference, holder, mutation,
                        current => current is >= int.MinValue and <= int.MaxValue ? replayed((int)current) : current,
                        frame, origin, notifyUnchanged, target),
                    () => admittedTarget is not null || WriteTarget(reference) == target, out admission);

                if (!ReferenceEquals(supplied, bounded)) {
                    var admitted = bounded;
                    effective = current => current is >= int.MinValue and <= int.MaxValue ? admitted((int)current) : current;
                }
            }

            // The admission, if any, is held until the completion callback has run.
            using (scope) {
                if (admission == WiredAdmission.Deferred) {
                    return true;
                }

                if (admission == WiredAdmission.Stale) {
                    continue;
                }

                // A transform already evaluated against an old target is never reused or re-run.
                var evaluated = !ReferenceEquals(effective, transform);
                var changed = ChangeIfTargetHolds(reference, holder, mutation, effective, frame, origin, notifyUnchanged, target,
                    retryable: admittedTarget is null && !evaluated);

                if (changed is { } result) {
                    return result;
                }
            }
        }

        return false;
    }

    // Null when the target moved and a fresh write may retry admission.
    private bool? ChangeIfTargetHolds(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableMutation mutation,
        Func<long, long> transform, WiredVariableFrame frame, int origin, bool notifyUnchanged, WiredVariableReference target, bool retryable)
    {
        Action? completed;
        bool changed;

        lock (_gate) {
            if (WriteTargetLocked(reference) != target) {
                return retryable ? null : false;
            }

            changed = ChangeLocked(reference, holder, mutation, transform, frame, origin, notifyUnchanged, out completed);
        }

        if (changed) {
            completed?.Invoke();
        }

        return changed;
    }

    private bool ChangeLocked(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableMutation mutation,
        Func<long, long> transform, WiredVariableFrame frame, int origin, bool notifyUnchanged, out Action? completed)
    {
        completed = null;

        if (frame.Depth >= 32 || !ValidateHolder(reference, holder, frame)) {
            return false;
        }

        var resolved = Resolve(reference, true);

        if (resolved is null) {
            return false;
        }

        if (resolved.Builtin is { } builtin) {
            if (mutation != WiredVariableMutation.Set && RoomWiredBuiltinVariables.CarriesPresenceValue(builtin)) {
                var held = builtins?.Read(builtin, holder, frame);

                // Give keeps a present value; Replace sets it; Remove takes it away.
                if (mutation == WiredVariableMutation.Give && held is not null || mutation == WiredVariableMutation.Remove && held is null) {
                    return false;
                }

                var given = mutation == WiredVariableMutation.Remove ? 0 : transform(held?.Value ?? 0);

                return given is >= int.MinValue and <= int.MaxValue && builtins!.Write(builtin, holder, (int)given, frame, out completed);
            }

            if (mutation != WiredVariableMutation.Set) {
                // Presence flags are not numeric operands; their dedicated source owns creation/removal.
                // The supported area flags cannot be intercepted according to the native variable metadata.
                return builtins?.MutatePresence(builtin, holder, mutation, frame) == true;
            }

            var current = builtins?.Read(builtin, holder, frame);

            if (current is null) {
                return false;
            }

            var next = transform(current.Value);

            if (next is < int.MinValue or > int.MaxValue || next == current.Value || !builtins!.Write(builtin, holder, (int)next, frame, out completed)) {
                return false;
            }

            var after = builtins.Read(builtin, holder, frame);

            if (after is not null && after.Value != current.Value && builtins.CanInterceptChanges(builtin, holder, frame)) {
                _changes.Enqueue(new(roomId, new(0, holder.Target, holder.StorageId), WiredVariableChangeKind.Updated,
                    current, after, holder.EntityId, frame.Depth + 1)
                { Origin = origin, InternalKey = RoomWiredBuiltinVariables.Normalize(builtin.Token) });
            }

            return true;
        }

        var definition = resolved.Definition!;

        if (resolved.Compose is { } compose) {
            // Writing a Time Utilities Value part rewrites the parent's value.
            if (mutation != WiredVariableMutation.Set) {
                return false;
            }

            var convert = resolved.Convert!;
            var part = transform;
            transform = parent => convert(new(parent, null, null)) is { } current && compose(parent, part(current.Value)) is { } next ? next : parent;
        }

        if (definition.IsDurable && (!holder.CanPersist || holder.Target == WiredVariableTarget.User && holder.StableId <= 0)) {
            return false;
        }

        if (mutation == WiredVariableMutation.Set && !definition.HasValue) {
            return false;
        }

        if (definition.Target == WiredVariableTarget.Global && mutation != WiredVariableMutation.Set) {
            return false;
        }

        var key = Key(definition, holder);
        var store = Store(definition, frame);

        if (definition.Target == WiredVariableTarget.Context) {
            if (mutation == WiredVariableMutation.Give && frame.Context.Read(key) != null) {
                return false;
            }

            if (mutation is WiredVariableMutation.Set or WiredVariableMutation.Remove) {
                store = frame.Context.Owner(key) ?? frame.Context;
            }
        }

        var acceptedUnchangedWrite = false;
        var write = store.Mutate(key, previous =>
        {
            var current = previous ?? (definition.Target == WiredVariableTarget.Global ? new(definition.InitialValue, null, null) : null);

            if (mutation == WiredVariableMutation.Give && current is not null) {
                return previous;
            }

            if (mutation is WiredVariableMutation.Set or WiredVariableMutation.Remove && current is null) {
                return previous;
            }

            if (mutation == WiredVariableMutation.Remove) {
                return null;
            }

            // A variable without a value is held as -1 (it reads -1, and after the box is switched to having a value, +1 gives 0).
            var next = definition.HasValue ? transform(current?.Value ?? 0) : -1;

            if (mutation == WiredVariableMutation.Set && !notifyUnchanged && previous is not null && previous.Value == next) {
                return previous;
            }

            // A Give with override re-creates the value even when it is unchanged.
            acceptedUnchangedWrite = (notifyUnchanged && mutation == WiredVariableMutation.Set) || mutation == WiredVariableMutation.Replace;
            var now = clock.GetUtcNow();

            return new(next, previous is null || mutation == WiredVariableMutation.Replace ? now : previous.CreatedAt, now);
        }, definition.IsDurable ? resolved.Authorization : null);

        if (!write.Changed && !acceptedUnchangedWrite) {
            return false;
        }

        if (write.Before?.Value != write.After?.Value || (write.Before is null) != (write.After is null)) {
            Interlocked.Increment(ref _valueChanges);

            if (write.Before is { } before && write.After is { } after && after.Value < before.Value) {
                Interlocked.Increment(ref _valueDecreases);
            }
        }

        // Every successful Give, including an override of an existing value, fires Variable Changed as created and never as changed.
        _changes.Enqueue(new(definition.RoomId, key, write.After is null ? WiredVariableChangeKind.Removed :
            write.Before is null || mutation == WiredVariableMutation.Replace ? WiredVariableChangeKind.Created : WiredVariableChangeKind.Updated,
            write.Before, write.After, holder.EntityId, frame.Depth + 1)
        { Origin = origin, Shared = definition.Availability == WiredVariableAvailability.Shared, Valueless = !definition.HasValue });

        return true;
    }

    /// <summary>Publish a complete speech match into one firing only after every destination is authorized.</summary>
    public bool CaptureContextValues(IReadOnlyDictionary<uint, int> values, WiredVariableFrame frame) =>
        CaptureContextValues(values.ToDictionary(pair => pair.Key, pair => (long)pair.Value), frame);

    public bool CaptureContextValues(IReadOnlyDictionary<uint, long> values, WiredVariableFrame frame)
    {
        lock (_gate) {
            if (frame.RoomId != roomId || frame.Depth >= 32 || values.Count > 8) {
                return false;
            }

            var destinations = new List<(WiredVariableDefinition Definition, long Value)>();

            foreach (var (id, value) in values) {
                var resolved = Resolve(new(WiredVariableTarget.Context, $"custom:{id}"), true);

                if (resolved?.Definition is not { Target: WiredVariableTarget.Context, HasValue: true } definition) {
                    return false;
                }

                destinations.Add((definition, value));
            }

            DateTimeOffset? capturedAt = null;
            DateTimeOffset CaptureNow() => capturedAt ??= clock.GetUtcNow();

            foreach (var (definition, value) in destinations) {
                var key = new WiredVariableKey(definition.ItemId, WiredVariableTarget.Context, 0);
                var write = frame.Context.Mutate(key, before => before?.Value == value ? before
                    : new(value, before is null ? CaptureNow() : before.CreatedAt, CaptureNow()));

                if (write.Changed) {
                    _changes.Enqueue(new(roomId, key, write.Before is null ? WiredVariableChangeKind.Created : WiredVariableChangeKind.Updated,
                    write.Before, write.After, 0, frame.Depth + 1));
                }
            }

            return true;
        }
    }

    /// <summary>Seed a room variable once after loading its saved definition; never overwrite a durable current value.</summary>
    public bool InitializeGlobal(uint definitionId)
    {
        lock (_gate) {
            var resolved = Resolve(new(WiredVariableTarget.Global, $"custom:{definitionId}"), true);

            if (resolved?.Definition is not { Target: WiredVariableTarget.Global, Link: null } definition || definition.ItemId != definitionId) {
                return false;
            }

            var frame = new WiredVariableFrame(roomId, []);
            DateTimeOffset? capturedAt = null;
            var result = Store(definition, frame).Mutate(new(definitionId, WiredVariableTarget.Global, 0),
                before => before ?? new(definition.InitialValue, capturedAt ??= clock.GetUtcNow(), capturedAt.Value),
                definition.IsDurable ? resolved.Authorization : null);

            return result.After is not null;
        }
    }

    /// <summary>Editor assignment reports acceptance, including an unchanged value; rejected authorization returns false.</summary>
    public bool SaveGlobalValue(uint definitionId, long value)
    {
        lock (_gate) {
            var resolved = Resolve(new(WiredVariableTarget.Global, $"custom:{definitionId}"), true);

            if (resolved?.Definition is not { Target: WiredVariableTarget.Global, Link: null } definition || definition.ItemId != definitionId) {
                return false;
            }

            var key = new WiredVariableKey(definitionId, WiredVariableTarget.Global, 0);
            DateTimeOffset? capturedAt = null;
            var write = Store(definition, new(roomId, [])).Mutate(key, before => before?.Value == value ? before
                : new(value, before is null ? capturedAt ??= clock.GetUtcNow() : before.CreatedAt,
                    capturedAt ??= clock.GetUtcNow()),
                definition.IsDurable ? resolved.Authorization : null);

            if (write.After is null) {
                return false;
            }

            if (write.Changed) {
                _changes.Enqueue(new(definition.RoomId, key, write.Before is null ? WiredVariableChangeKind.Created : WiredVariableChangeKind.Updated,
                write.Before, write.After, 0, 1)
                { Origin = 2 });
            }

            return true;
        }
    }

    /// <summary>
    /// A change another room made to a variable that lives here (through its reference): the room's own triggers hear of it as one
    /// from another room. The holder was named by the other room's index, so the caller says which unit it is here.
    /// </summary>
    internal void EnqueueForeign(WiredVariableChange change)
    {
        lock (_gate) {
            _changes.Enqueue(change with { Origin = 3 });
        }
    }

    public IReadOnlyList<WiredVariableChange> DrainChanges()
    {
        lock (_gate) {
            var result = _changes.ToArray();
            _changes.Clear();

            return result;
        }
    }

    // Departure only clears active values. Pickup/room unload must never call DeleteDefinition.
    public void HolderLeft(WiredVariableHolder holder) => _active.RemoveHolder(holder.Target, holder.StorageId);
    public void DetachDefinition(uint definitionId) => _active.DeleteDefinition(definitionId);

    /// <summary>Call only after the owning definition item is actually deleted; deletion failure propagates.</summary>
    public int DeleteDefinition(uint definitionId)
    {
        lock (_gate) {
            var removed = durable.DeleteDefinition(definitionId);

            return removed + _active.DeleteDefinition(definitionId);
        }
    }

    /// <summary>Owner-only menu operation. Clears values, retaining the definition and allowing future assignments.</summary>
    public int ClearValues(uint definitionId, WiredVariableTarget target, WiredVariableFrame frame, int origin = 2)
    {
        lock (_gate) {
            if (frame.RoomId != roomId || frame.Depth >= 32 || target is not (WiredVariableTarget.User or WiredVariableTarget.Furni)) {
                return 0;
            }

            var resolved = Resolve(new(target, $"custom:{definitionId}"), true);

            if (resolved?.Definition is not { } definition || resolved.Authorization is not { } authorization) {
                return 0;
            }

            var removed = Store(definition, frame).ClearValues(definition.ItemId, authorization);

            foreach (var (key, value) in removed) {
                var entityId = frame.Holders.FirstOrDefault(x => x.Target == key.Target && x.StorageId == key.HolderId).EntityId;
                _changes.Enqueue(new(definition.RoomId, key, WiredVariableChangeKind.Removed, value, null, entityId, frame.Depth + 1) { Origin = origin });
            }

            return removed.Count;
        }
    }

    /// <summary>
    /// Removes one holder's value, the definition staying. Like <see cref="ClearValues"/> it reaches holders that are not in the room
    /// (a permanent value of a user who left). Returns the removed value, or null when there was none or the variable is read-only.
    /// </summary>
    public WiredVariableValue? RemoveValue(uint definitionId, WiredVariableTarget target, long holderId, int entityId, int origin)
    {
        lock (_gate) {
            if (target is not (WiredVariableTarget.User or WiredVariableTarget.Furni)) {
                return null;
            }

            if (Resolve(new(target, $"custom:{definitionId}"), true) is not { Definition: { } definition } resolved) {
                return null;
            }

            var key = new WiredVariableKey(definition.ItemId, definition.Target, holderId);
            var write = (definition.IsDurable ? durable : _active).Mutate(key, _ => null, definition.IsDurable ? resolved.Authorization : null);

            if (write.Before is null || write.After is not null) {
                return null;
            }

            _changes.Enqueue(new(definition.RoomId, key, WiredVariableChangeKind.Removed, write.Before, null, entityId, 1)
            { Origin = origin, Shared = definition.Availability == WiredVariableAvailability.Shared });

            return write.Before;
        }
    }

    public IReadOnlyDictionary<WiredVariableKey, WiredVariableValue> GetStoredHolders(uint definitionId)
    {
        lock (_gate) {
            var local = directory.Find(definitionId);
            var resolved = local?.RoomId == roomId ? Resolve(new(local.Target, local.Token), false) : null;

            if (resolved?.Definition is not { } definition || definition.Target == WiredVariableTarget.Context) {
                return new Dictionary<WiredVariableKey, WiredVariableValue>();
            }

            var values = definition.IsDurable ? durable.GetHolders(definition.ItemId) : _active.GetHolders(definition.ItemId);

            return values.ToDictionary(x => x.Key with { DefinitionId = definitionId }, x => Held(definition, x.Value)!);
        }
    }

    /// <summary>One holder's kept values of the given permanent variables, by definition id. The holder may be offline.</summary>
    public IReadOnlyDictionary<uint, WiredVariableValue> ReadStored(IEnumerable<uint> definitionIds, WiredVariableTarget target, long holderId)
    {
        lock (_gate) {
            var definitions = definitionIds.Select(id => Resolve(new(target, $"custom:{id}"), false)?.Definition)
                .OfType<WiredVariableDefinition>().Where(x => x.IsDurable && x.Target == target).ToArray();
            var values = durable.ReadMany(definitions.Select(x => new WiredVariableKey(x.ItemId, target, holderId)).ToArray());

            return definitions.Where(x => values.ContainsKey(new(x.ItemId, target, holderId)))
                .ToDictionary(x => x.ItemId, x => Held(x, values[new(x.ItemId, target, holderId)])!);
        }
    }

    // A variable without a value reads -1 whatever an older save stored for it (see Change).
    private static WiredVariableValue? Held(WiredVariableDefinition definition, WiredVariableValue? value) =>
        value is not null && !definition.HasValue ? value with { Value = -1 } : value;

    public IReadOnlyList<WiredVariableDescription> DescribeDefinitions(IEnumerable<uint> definitionIds)
    {
        lock (_gate) {
            var authority = new ReadDirectory(directory);
            var result = new List<WiredVariableDescription>();

            foreach (var id in definitionIds.Distinct().Take(4096)) {
                var local = authority.Find(id);

                if (local?.RoomId != roomId) {
                    continue;
                }

                var resolved = Resolve(new(local.Target, local.Token), false, authority) ?? Listed(local, authority)?.Read;

                if (resolved is null) {
                    continue;
                }

                var readOnly = local.Target == WiredVariableTarget.Context || resolved.Authorization?.Lineage.Any(x => x.Link?.ReadOnly == true) == true;
                result.Add(new(local, HasValue(resolved), readOnly) { IsBuiltin = resolved.Builtin is not null });
            }

            return result.OrderBy(x => x.CatalogTarget).ThenBy(x => x.Definition.Name, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Definition.ItemId).ToArray();
        }
    }

    /// <summary>Remembers how a reference to a variable of another room resolves now, so it stays listed after its source is lost.</summary>
    public void RememberReference(uint definitionId)
    {
        lock (_gate) {
            var authority = new ReadDirectory(directory);

            if (authority.Find(definitionId) is { Link: { } link } local && local.RoomId == roomId && link.SourceRoomId != roomId
                && Resolve(new(local.Target, local.Token), false, authority) is { } read) {
                _listedReferences[definitionId] = new(local, read, Resolve(new(local.Target, local.Token), true, authority) is not null);
            }
        }
    }

    // The remembered resolution of a reference whose source item is gone, as after a pickup.
    private ListedReference? Listed(WiredVariableDefinition local, IWiredVariableDirectory authority) =>
        _listedReferences.TryGetValue(local.ItemId, out var listed) && listed.Local == local && local.Link is { } link
        && TryDefinitionId(link.Source.Token, out var sourceId) && authority.Find(sourceId) is null ? listed : null;

    // Native catalog resolution uses one directory for both authorization paths and only frozen derivation inputs.
    internal IReadOnlyList<WiredNativeEndpoint>? DescribeNativeDefinitions(
        IReadOnlyList<WiredVariableDefinition> captured, IReadOnlyDictionary<WiredVariableReference, WiredVariableDerivation> derivations,
        IReadOnlyList<WiredVariableDefinition> derivedDefinitions)
    {
        lock (_gate) {
            var authority = new ReadDirectory(directory);
            var result = new List<WiredNativeEndpoint>();

            foreach (var local in captured.Take(4096)) {
                var stored = authority.Find(local.ItemId);

                if (stored is null || stored.RoomId != roomId) {
                    continue;
                }

                var reference = new WiredVariableReference(stored.Target, stored.Token);
                var read = Resolve(reference, false, authority, derivations);
                var listed = read is null && stored == local ? Listed(local, authority) : null;

                if (read is null && listed is null) {
                    continue;
                }

                if (stored != local) {
                    return null;
                }

                read ??= listed!.Read;
                var writable = listed?.Writable ?? Resolve(reference, true, authority, derivations) is not null;
                var readOnly = read.Authorization?.Lineage.Any(value => value.Link?.ReadOnly == true) == true;
                result.Add(new(new(local, NativeHasValue(read), readOnly) { IsBuiltin = read.Builtin is not null },
                    read.Definition, read.Builtin, writable, read.Convert is not null));
            }

            foreach (var local in derivedDefinitions) {
                var read = Resolve(new(local.Target, local.Token), false, authority, derivations);

                if (read is not null) {
                    var write = Resolve(new(local.Target, local.Token), true, authority, derivations);
                    var derivation = derivations.GetValueOrDefault(new(local.Target, local.Token));
                    result.Add(new(new(local, derivation?.HasValue ?? true, write is null) { IsDerived = true, Interceptable = derivation?.Interceptable == true },
                        read.Definition, read.Builtin, write is not null, true));
                }
            }

            return result;
        }
    }

    public WiredVariableHolderPage ReadHolderPage(uint definitionId, int page, int size, int sort,
        IReadOnlyCollection<long>? holderFilter = null, IReadOnlyDictionary<long, string>? names = null)
    {
        lock (_gate) {
            var local = directory.Find(definitionId);
            var resolved = local?.RoomId == roomId ? Resolve(new(local.Target, local.Token), false) : null;

            if (resolved?.Definition is not { } definition || definition.Target == WiredVariableTarget.Context) {
                return new(0, Math.Max(1, page), Math.Clamp(size, 1, 200), []);
            }

            var store = definition.IsDurable ? durable : (IWiredVariableStore)_active;
            var result = store.ReadPage(definition.ItemId, definition.Target, page, size, sort, holderFilter, names);

            return result with { Holders = result.Holders.Select(x => x with { Key = x.Key with { DefinitionId = definitionId } }).ToArray() };
        }
    }

    private bool ValidateHolder(WiredVariableReference reference, WiredVariableHolder holder, WiredVariableFrame frame) =>
        frame.RoomId == roomId && reference.Target == holder.Target && frame.Contains(holder)
        && (holder.Target is WiredVariableTarget.Context or WiredVariableTarget.Global || holder.StableId != 0 || holder.IsTemporaryFurni);

    private static WiredVariableKey Key(WiredVariableDefinition definition, WiredVariableHolder holder) =>
        new(definition.ItemId, definition.Target, holder.StorageId);
    private IWiredVariableStore Store(WiredVariableDefinition definition, WiredVariableFrame frame) =>
        definition.Target == WiredVariableTarget.Context ? frame.Context : definition.IsDurable ? durable : _active;

    private bool HasValue(Resolved resolved) => resolved.Definition?.HasValue
        ?? (resolved.Builtin is { } builtin && builtins?.HasValue(builtin) == true);

    private static bool NativeHasValue(Resolved resolved) => resolved.Definition?.HasValue
        ?? (resolved.Builtin is { } builtin && RoomWiredBuiltinVariables.HasNumericValue(builtin));

    /// <summary>Whether the variable holds a number, as opposed to only existing (and carrying timestamps).</summary>
    internal bool CarriesValue(WiredVariableReference reference)
    {
        lock (_gate) {
            return Resolve(reference, false) is { } resolved && NativeHasValue(resolved);
        }
    }

    internal WiredVariableReference WriteTarget(WiredVariableReference reference)
    {
        lock (_gate) {
            return WriteTargetLocked(reference);
        }
    }

    // The authorized final target: the builtin it echoes, otherwise the stored definition at the end of the alias chain. A writable
    // part stays its own target, so one firing's writes to a part and to its parent are never folded into one operand chain.
    private WiredVariableReference WriteTargetLocked(WiredVariableReference reference)
        => Resolve(reference, true) is { } resolved
            ? resolved.Compose is not null ? reference : resolved.Builtin ?? new(reference.Target, resolved.Definition!.Token)
            : reference;

    private Resolved? Resolve(WiredVariableReference reference, bool writing, IWiredVariableDirectory? readDirectory = null,
        IReadOnlyDictionary<WiredVariableReference, WiredVariableDerivation>? frozenDerivations = null)
    {
        var authority = readDirectory ?? directory;

        if ((frozenDerivations is null ? derive?.Invoke(reference) : frozenDerivations.GetValueOrDefault(reference)) is { } derived) {
            if (writing && derived.Write is null || derived.Source == reference) {
                return null;
            }

            // A writable part writes through to its parent's stored value, so the parent itself must be writable.
            var source = Resolve(derived.Source, writing, authority, frozenDerivations);

            return source is null || derived.RequiresValue && !(frozenDerivations is null ? HasValue(source) : NativeHasValue(source))
                || derived.RequiresTimestamps && source.Definition is null
                || derived.RequiresCreationTime && source.Definition?.Target == WiredVariableTarget.Global
                || writing && source.Definition is not { HasValue: true }
                ? null : source with { Convert = derived.Convert, Compose = writing ? derived.Write : null };
        }

        var visited = new HashSet<uint>();
        var lineage = ImmutableArray.CreateBuilder<WiredVariableDefinition>();
        var expectedRoom = roomId;
        var owner = authority.GetRoomOwner(roomId);

        if (owner is null or 0) {
            return null;
        }

        while (true) {
            if (reference.Token.StartsWith("internal:", StringComparison.Ordinal)) {
                return expectedRoom == roomId ? new(null, reference, new(roomId, owner.Value, lineage.ToImmutable())) : null;
            }

            if (!TryDefinitionId(reference.Token, out var id) || !visited.Add(id) || visited.Count > 32) {
                return null;
            }

            var definition = authority.Find(id);

            if (definition is null || definition.RoomId != expectedRoom || definition.Target != reference.Target
                || definition.OwnerId != owner || authority.GetRoomOwner(expectedRoom) != owner) {
                return null;
            }

            // Give, Change and Remove through a pointer of any target change nothing and give no error.
            if (writing && definition.IsPointer) {
                return null;
            }

            lineage.Add(definition);

            if (definition.Link is not { } link) {
                return new(definition, null, new(roomId, owner.Value, lineage.ToImmutable()));
            }

            if (writing && link.ReadOnly) {
                return null;
            }

            if (link.Source.Target != reference.Target) {
                return null;
            }

            if (link.SourceRoomId != definition.RoomId) {
                if (reference.Target is not (WiredVariableTarget.User or WiredVariableTarget.Global)
                    || !TryDefinitionId(link.Source.Token, out var sourceId)) {
                    return null;
                }

                var source = authority.Find(sourceId);

                if (source is null || source.Availability != WiredVariableAvailability.Shared || source.Link is not null) {
                    return null;
                }
            }

            expectedRoom = link.SourceRoomId;
            reference = link.Source;
        }
    }

    /// <summary>
    /// Resolves a native catalog id to the current local reference. The id must parse to this target and name a definition
    /// the room's authority resolves now; anything else (foreign, deleted, retargeted) resolves to nothing.
    /// </summary>
    public bool TryResolveCatalogId(string catalogId, WiredVariableTarget expected, out WiredVariableReference reference)
    {
        reference = new(expected, "");

        if (!WiredVariableDescription.TryParseCatalogId(catalogId, out var target, out var token) || target != expected) {
            return false;
        }

        var candidate = new WiredVariableReference(target, token);

        lock (_gate) {
            if (Resolve(candidate, false) is null) {
                return false;
            }
        }

        reference = candidate;

        return true;
    }

    /// <summary>
    /// The key a proposed reference is refused with, or null. The variable must be shared in the room it names, and that room must have the same owner.
    /// A room holds one reference to a shared variable, a second one is refused. With <paramref name="stored"/>, a source that no longer exists is no refusal:
    /// a reference whose source was picked up keeps its settings and still opens, also after a room reload. A source placed again in another room, or saved again as local,
    /// still refuses the stored reference.
    /// </summary>
    public string? RefuseReference(WiredVariableDefinition proposed, bool stored = false)
    {
        if (proposed.Link is not { } link) {
            return null;
        }

        lock (_gate) {
            if (!TryDefinitionId(link.Source.Token, out var sourceId)) {
                return WiredVariableDefinitions.InvalidReferenceKey;
            }

            var found = directory.Find(sourceId);

            if (found is null && stored) {
                return null;
            }

            if (found is not { Availability: WiredVariableAvailability.Shared, Link: null } source
                || source.RoomId != link.SourceRoomId || source.Target != link.Source.Target) {
                return WiredVariableDefinitions.InvalidReferenceKey;
            }

            if (source.OwnerId != proposed.OwnerId) {
                return WiredVariableDefinitions.ReferenceMustOwnKey;
            }

            return directory.ListRoom(roomId).Any(other => other.ItemId != proposed.ItemId && other.Link is { } existing
                && existing.SourceRoomId == link.SourceRoomId && existing.Source == link.Source) ? WiredVariableDefinitions.ReferenceExistsKey : null;
        }
    }

    /// <summary>
    /// The variable a pointer stands for in this firing: the one whose box id its accessor context variable holds, of the pointer's own target, read for the same holder.
    /// An id that is no variable box of that target (nothing, 0, a missing id, a pointer, a variable of another target) reads nothing.
    /// </summary>
    private (WiredVariableReference Reference, WiredVariableDefinition Definition)? PointedAt(WiredVariableDefinition pointer, WiredVariableFrame frame)
    {
        if (!TryDefinitionId(pointer.PointerAccessor ?? "", out var accessorId)
            || Resolve(new(WiredVariableTarget.Context, $"custom:{accessorId}"), false)?.Definition is not { Target: WiredVariableTarget.Context, IsPointer: false }
            || frame.Context.Read(new(accessorId, WiredVariableTarget.Context, 0)) is not { Value: > 0 and <= uint.MaxValue } held) {
            return null;
        }

        // The accessor holds the id the client sees (@id, @event.variable_update.box_id), so a Builders Club box maps back to its stored id.
        var target = new WiredVariableReference(pointer.Target, $"custom:{BuildersClubItemIds.FromWire((uint)held.Value)}");

        return Resolve(target, false)?.Definition is { IsPointer: false } definition ? (target, definition) : null;
    }

    private WiredVariableValue? ReadPointer(WiredVariableDefinition pointer, WiredVariableHolder holder, WiredVariableFrame frame) =>
        PointedAt(pointer, frame) is { } target ? Read(target.Reference, holder, frame) : null;

    /// <summary>
    /// The variable a pointer stands for in this firing, or the reference itself when it is not a pointer; null when the pointer points at
    /// nothing.
    /// </summary>
    public WiredVariableReference? Unpoint(WiredVariableReference reference, WiredVariableFrame frame)
    {
        lock (_gate) {
            return Resolve(reference, false)?.Definition is { IsPointer: true } pointer ? PointedAt(pointer, frame)?.Reference : reference;
        }
    }

    /// <summary>True when the reference names a pointer variable.</summary>
    public bool IsPointer(WiredVariableReference reference)
    {
        lock (_gate) {
            return Resolve(reference, false)?.Definition is { IsPointer: true };
        }
    }

    /// <summary>False when the reference names a variable whose changes no Variable Changed trigger sees.</summary>
    public bool InterceptsChanges(WiredVariableReference reference)
    {
        lock (_gate) {
            return Resolve(reference, false)?.Definition is not { InterceptsChanges: false };
        }
    }

    /// <summary>
    /// What a text placeholder for this reference shows in this firing, or null when it stays as written. A pointer shows the variable it points at.
    /// A pointer that points at nothing leaves the placeholder, and a pointed variable without a value shows "null".
    /// </summary>
    internal WiredVariableTextSource? TextSource(WiredVariableReference reference, WiredVariableFrame frame)
    {
        lock (_gate) {
            var resolved = Resolve(reference, false);

            // When the variable's own box is picked up after the stack fired, while a delayed Show Message waits, its placeholder stays as written. Other unresolved variables keep showing "null".
            if (resolved is null && derive?.Invoke(reference) is null && TryDefinitionId(reference.Token, out var definitionId)
                && directory.Find(definitionId) is null) {
                return null;
            }

            if (resolved?.Definition is not { IsPointer: true } pointer) {
                // A placeholder for a variable without a value shows "null" also while the holder has it.
                return new(reference, derive?.Invoke(reference) is { HasValue: false } || resolved is { } found && !NativeHasValue(found));
            }

            return PointedAt(pointer, frame) is { } target ? new(target.Reference, !target.Definition.HasValue) : null;
        }
    }

    /// <summary>
    /// The key a new save of a named variable is refused with, or null. A name that is empty once cleaned is too short on every kind, a pointer name also needs 1 to 40 letters,
    /// digits or underscores, and the cleaned name must differ from every user, furni, global, context and pointer variable of the room. Echo, reference and quest variables
    /// do not count (a daily task variable is a user variable here and does). An older stored name is compared as it is stored.
    /// </summary>
    public string? RefuseName(uint itemId, string name, bool pointer)
    {
        if ((pointer ? WiredVariableDefinitions.RefuseName(name) : name.Length == 0 ? WiredVariableDefinitions.NameLengthKey : null) is { } badName) {
            return badName;
        }

        lock (_gate) {
            return directory.ListRoom(roomId).Any(other => other.ItemId != itemId && other.Link is null && other.Name == name)
                ? WiredVariableDefinitions.NameTakenKey : null;
        }
    }

    /// <summary>
    /// The key a new pointer save is refused with, or null. The name is checked first (length, syntax, then uniqueness), then the accessor.
    /// </summary>
    public string? RefusePointer(WiredVariableDefinition proposed) => RefuseName(proposed.ItemId, proposed.Name, pointer: true) ?? RefuseAccessor(proposed);

    /// <summary>
    /// The key a pointer's accessor is refused with, or null. The accessor must be a context variable of its own that holds a value (anything else, an echo or nothing, is a missing variable) and a pointer is never an accessor.
    /// </summary>
    public string? RefuseAccessor(WiredVariableDefinition proposed)
    {
        lock (_gate) {
            if (!TryDefinitionId(proposed.PointerAccessor ?? "", out var accessorId) || directory.Find(accessorId) is not { } accessor || accessor.RoomId != roomId) {
                return WiredVariableDefinitions.MissingVariableKey;
            }

            if (accessor.IsPointer) {
                return WiredVariableDefinitions.NeedsExecutionContextKey;
            }

            return accessor.Target != WiredVariableTarget.Context || !accessor.HasValue || accessor.Link is not null ? WiredVariableDefinitions.MissingVariableKey : null;
        }
    }

    /// <summary>The reference variables in the owner's rooms that read one shared variable of this room.</summary>
    public IReadOnlyList<WiredVariableDefinition> ListReferences(uint sharedItemId) => directory.ListReferences(roomId, sharedItemId);

    /// <summary>The shared variables this room's owner offers from their other rooms, in directory order.</summary>
    public IReadOnlyList<WiredSharedVariable> ListShared() => directory.GetRoomOwner(roomId) is { } owner ? directory.ListShared(owner, roomId) : [];

    /// <summary>
    /// A parent change seen through an interceptable part (a Time Utilities Value part): the change keyed by the part, with the part's
    /// before and after values. Null when the reference is not such a part of the changed variable.
    /// </summary>
    internal WiredVariableChange? ProjectPartChange(WiredVariableReference reference, WiredVariableChange change)
    {
        lock (_gate) {
            if (derive?.Invoke(reference) is not { } derived || derived.Write is null && !derived.Interceptable
                || !TryDefinitionId(reference.Token, out var partId)
                || Resolve(reference, derived.Write is not null) is not { Definition: { } parent } resolved
                || parent.ItemId != change.Key.DefinitionId || parent.Target != change.Key.Target || change.InternalKey.Length != 0) {
                return null;
            }

            var before = change.Before is null ? null : resolved.Convert!(change.Before);
            var after = change.After is null ? null : resolved.Convert!(change.After);

            // The part's own appearance decides the kind: a presence part (is_maxed) is created when it appears and removed when it goes, and
            // nothing happens while it stays absent. is_maxed fires on the writes that make it appear, keep it and remove it, not on a write while absent.
            if (before is null && after is null) {
                return null;
            }

            var kind = before is null ? WiredVariableChangeKind.Created : after is null ? WiredVariableChangeKind.Removed : WiredVariableChangeKind.Updated;

            return change with { Key = change.Key with { DefinitionId = partId }, Before = before, After = after, Kind = kind };
        }
    }

    public static bool TryDefinitionId(string token, out uint id) =>
        uint.TryParse(token.StartsWith("custom:", StringComparison.Ordinal) ? token[7..] : token,
            NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;

    private sealed record Resolved(WiredVariableDefinition? Definition, WiredVariableReference? Builtin, WiredVariableAuthorization? Authorization,
        Func<WiredVariableValue, WiredVariableValue?>? Convert = null, Func<long, long, long?>? Compose = null);

    private sealed record ListedReference(WiredVariableDefinition Local, Resolved Read, bool Writable);

    private sealed class ReadDirectory(IWiredVariableDirectory source) : IWiredVariableDirectory
    {
        private readonly Dictionary<uint, WiredVariableDefinition?> _definitions = [];
        private readonly Dictionary<uint, uint?> _owners = [];
        public WiredVariableDefinition? Find(uint itemId)
        {
            if (!_definitions.TryGetValue(itemId, out var value)) {
                _definitions[itemId] = value = source.Find(itemId);
            }

            return value;
        }
        public uint? GetRoomOwner(uint roomId)
        {
            if (!_owners.TryGetValue(roomId, out var value)) {
                _owners[roomId] = value = source.GetRoomOwner(roomId);
            }

            return value;
        }
    }
}

/// <summary>The variable a text placeholder reads, and whether it shows "null" whatever the holder has.</summary>
internal readonly record struct WiredVariableTextSource(WiredVariableReference Variable, bool Valueless);
