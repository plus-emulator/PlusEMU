using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>Request-scoped menu queries; callers must check room editing rights before constructing a response.</summary>
public sealed class WiredVariableMenu(Room room, WiredRoomVariables variables)
{
    public WiredVariableCatalog Catalog() => variables.Catalog();

    public bool Write(WiredVariableTarget target, int targetId, uint definitionId, long value, WiredVariableMutation mutation, string token = "")
    {
        if (target is not (WiredVariableTarget.User or WiredVariableTarget.Furni or WiredVariableTarget.Global)) {
            return false;
        }

        if (token.Length > 0 && (definitionId != 0 || !(token.StartsWith("internal:@", StringComparison.Ordinal) || token.StartsWith("internal:~", StringComparison.Ordinal)) || mutation != WiredVariableMutation.Set && !RoomWiredBuiltinVariables.SupportsPresenceMutation(new(target, token)))) {
            return false;
        }

        if (token.Length == 0 && definitionId == 0) {
            return false;
        }

        var (frame, _) = LiveHolders(target);
        WiredVariableHolder holder;

        if (target == WiredVariableTarget.Global) {
            if (targetId != 0 && targetId != room.Id) {
                return false;
            }

            holder = new(target, 0, 0);

            if (mutation == WiredVariableMutation.Replace) {
                mutation = WiredVariableMutation.Set;
            }
        }
        else if (target == WiredVariableTarget.User) {
            var user = room.GetRoomUserManager().GetRoomUsers().FirstOrDefault(x => MenuEntityId(x) == targetId);

            if (user is null) {
                return false;
            }

            holder = WiredVariableRuntimeFrames.UserHolder(user);
        }
        else {
            holder = frame.Holders.FirstOrDefault(x => x.Target == target && x.EntityId == targetId);

            if (holder == default) {
                return false;
            }
        }

        var changed = variables.Module.Change(new(target, token.Length > 0 ? token : $"custom:{definitionId}"), holder, mutation, _ => value, frame, origin: 2, notifyUnchanged: true);

        if (changed) {
            variables.InvalidateFx();
        }

        return changed;
    }
    public int Clear(WiredVariableTarget target, uint definitionId)
    {
        var (frame, _) = LiveHolders(target);
        var count = variables.Module.ClearValues(definitionId, target, frame);

        if (count > 0) {
            variables.InvalidateFx();
        }

        return count;
    }

    /// <summary>
    /// One object's values for the inspection tab, keyed by the native catalogue id the client knows the variable by. Observed on AIR
    /// WIN63-202610151745-318700621: a user lists its built-in values and its custom and derived ones; an absent part is left out.
    /// A furni also lists the Wired boxes that pick it. Null for an unknown object or target.
    /// </summary>
    public (IReadOnlyList<(string Id, long Value)> Values, IReadOnlyList<uint> ConfiguredIn)? ReadObject(int type, int objectId)
    {
        WiredVariableTarget target;

        switch (type) {
            case 0: target = WiredVariableTarget.Furni; break;
            case 1: target = WiredVariableTarget.User; break;
            case -10: target = WiredVariableTarget.Global; break;
            default: return null;
        }

        if (!variables.TryCaptureNativeCatalog(out var catalog)) {
            return null;
        }

        var (frame, _) = LiveHolders(target);
        WiredVariableHolder holder;
        Item? item = null;

        if (target == WiredVariableTarget.User) {
            if (room.GetRoomUserManager().GetRoomUserByVirtualId(objectId) is not { } user) {
                return null;
            }

            holder = WiredVariableRuntimeFrames.UserHolder(user);
        }
        else if (target == WiredVariableTarget.Furni) {
            item = room.GetRoomItemHandler().GetItem((uint)objectId);

            if (item is null) {
                return null;
            }

            holder = WiredVariableRuntimeFrames.FurniHolder(item);
        }
        else {
            holder = new(WiredVariableTarget.Global, 0, 0);
        }

        var rows = catalog!.Variables.Where(row => row.Target == type)
            .Select(row => (row.Id, Parsed: WiredVariableDescription.TryParseCatalogId(row.Id, out var parsed, out var token) && parsed == target
                ? new WiredVariableReference(parsed, token) : (WiredVariableReference?)null))
            .Where(row => row.Parsed is not null).ToArray();
        using var reads = variables.Module.CaptureReads(rows.Select(row => row.Parsed!), frame);
        var values = new List<(string, long)>();

        foreach (var (id, reference) in rows) {
            if (reads.Read(reference!, holder, frame) is { } value) {
                values.Add((id, value.Value));
            }
        }

        var configured = new List<uint>();

        if (item is not null) {
            foreach (var floor in room.GetRoomItemHandler().GetFloor) {
                if (room.GetWired().TryGet(floor.Id, out var box) && box is Configuration.IWiredConfiguredItem boxConfig
                    && (boxConfig.Configuration.SelectedItems.Contains(item.Id) || boxConfig.Configuration.SecondarySelectedItems.Contains(item.Id))) {
                    configured.Add(floor.Id);
                }
            }
        }

        return (values, configured);
    }

    public WiredVariableMenuSnapshot Snapshot()
    {
        var catalog = Catalog();
        var (frame, _) = LiveHolders(WiredVariableTarget.Global);
        var references = catalog.Variables.Where(x => x.Definition.Target != WiredVariableTarget.Context)
            .Select(x => new WiredVariableReference(x.Definition.Target, x.Definition.Token)).ToArray();
        using var reads = variables.Module.CaptureReads(references, frame);
        var assignments = new List<WiredVariableStoredHolder>();

        foreach (var variable in catalog.Variables.Where(x => x.Definition.Target != WiredVariableTarget.Context)) {
            var reference = new WiredVariableReference(variable.Definition.Target, variable.Definition.Token);
            var holders = variable.Definition.Target == WiredVariableTarget.Global ? [new WiredVariableHolder(WiredVariableTarget.Global, 0, 0)]
                : frame.Holders.Where(x => x.Target == variable.Definition.Target).ToArray();

            foreach (var holder in holders) {
                if (reads.Read(reference, holder, frame) is { } value) {
                    assignments.Add(new(new(variable.Definition.ItemId, holder.Target, holder.StorageId), "", value));
                }
            }
        }

        return new(room.Id, catalog.Variables, ToWireHolders(assignments));
    }

    public WiredVariableHolderPage Page(WiredVariableDescription variable, int page, int size, int userFilter, int sort)
    {
        var (frame, names) = LiveHolders(variable.Definition.Target);
        var result = variable.IsBuiltin || variable.IsDerived ? WiredVariablePaging.Page(ReadLive(variable, frame, names), page, size, sort)
            : variables.Module.ReadHolderPage(variable.Definition.ItemId, page, size, sort,
            variable.Definition.Target == WiredVariableTarget.User && userFilter == 1
                ? frame.Holders.Where(x => x.Target == WiredVariableTarget.User).Select(x => x.StableId).ToArray() : null, names);

        return result with { Holders = ToWireHolders(result.Holders) };
    }

    // The unpaged overview only highlights live room entities. Offline holders use bounded pages.
    public IReadOnlyList<WiredVariableStoredHolder> Live(WiredVariableDescription variable)
    {
        var (frame, names) = LiveHolders(variable.Definition.Target);

        return ToWireHolders(ReadLive(variable, frame, names));
    }
    private IReadOnlyList<WiredVariableStoredHolder> ReadLive(WiredVariableDescription variable, WiredVariableFrame frame,
        IReadOnlyDictionary<long, string> names)
    {
        if (variable.Definition.Target == WiredVariableTarget.Context) {
            return [];
        }

        var reference = new WiredVariableReference(variable.Definition.Target, variable.Definition.Token);
        var holders = variable.Definition.Target == WiredVariableTarget.Global
            ? new[] { new WiredVariableHolder(WiredVariableTarget.Global, 0, 0) }
            : frame.Holders.Where(x => x.Target == variable.Definition.Target).ToArray();
        using var reads = variables.Module.CaptureReads([reference], frame);
        var result = new List<WiredVariableStoredHolder>();

        foreach (var holder in holders) {
            if (reads.Read(reference, holder, frame) is { } value) {
                result.Add(new(new(variable.Definition.ItemId, holder.Target, holder.StorageId), names.GetValueOrDefault(holder.StorageId) ?? "", value));
            }
        }

        return result;
    }
    private IReadOnlyList<WiredVariableStoredHolder> ToWireHolders(IReadOnlyList<WiredVariableStoredHolder> holders)
    {
        var bots = room.GetRoomUserManager().GetRoomUsers().Where(x => x.IsBot)
            .ToDictionary(x => WiredVariableRuntimeFrames.UserHolder(x).StableId, MenuEntityId);

        return holders.Where(x => x.Key.Target != WiredVariableTarget.User || x.Key.HolderId > 0 || bots.GetValueOrDefault(x.Key.HolderId) != 0)
            .Select(x => x.Key.Target == WiredVariableTarget.Global
                ? x with { Key = x.Key with { HolderId = room.Id }, Name = room.Name }
                : x.Key.Target == WiredVariableTarget.User && x.Key.HolderId < 0 ? x with { Key = x.Key with { HolderId = bots[x.Key.HolderId] } } : x).ToArray();
    }
    public static int MenuEntityId(RoomUser user)
    {
        if (!user.IsBot) {
            return user.HabboId;
        }

        var id = user.IsPet ? user.PetData?.PetId ?? 0 : user.BotData.Id;

        return id is > 0 and <= 1073741823 ? -(id * 2 + (user.IsPet ? 0 : 1)) : 0;
    }
    private (WiredVariableFrame Frame, IReadOnlyDictionary<long, string> Names) LiveHolders(WiredVariableTarget target)
    {
        var frame = room.GetWired().CaptureVariableInspectionFrame();
        var users = frame.RuntimeContext!.UserIdentity.Values.Where(user => !user.IsBot);
        var items = frame.RuntimeContext.FurniIdentity.Values;
        var names = new Dictionary<long, string>();

        if (target == WiredVariableTarget.User) {
            foreach (var user in users) {
                names[WiredVariableRuntimeFrames.UserHolder(user).StableId] = user.IsBot ? user.BotData.Name : user.GetUsername();
            }
        }
        else if (target == WiredVariableTarget.Furni) {
            foreach (var item in items) {
                names[WiredVariableRuntimeFrames.FurniHolder(item).StorageId] = item.Definition.PublicName;
            }
        }
        else {
            names[0] = room.Name;
        }

        return (frame, names);
    }
}
