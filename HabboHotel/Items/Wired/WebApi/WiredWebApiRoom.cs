using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.WebApi;

/// <summary>
/// The Web API's view of a loaded room. Reads come from the stored values, so a user who left still has their permanent values;
/// writes go through the variable module like the inspection menu's, as external changes, and need the holder in the room.
/// </summary>
public sealed class WiredWebApiRoom(Room room) : IWiredWebApiRoom
{
    // WiredVariableChange.Origin: 1 is an external write.
    private const int External = 1;
    private const string AddonName = "wf_xtra_var_web_api";

    private WiredRoomVariables RoomVariables => room.GetWired().Variables;

    public bool HasAddon(uint itemId) => room.GetRoomItemHandler()?.GetItem(itemId) is { } item && item.Definition.WiredDescriptor?.CanonicalName == AddonName;

    public IReadOnlyList<WiredWebApiVariable> Variables() => RoomVariables.Catalog().Variables
        .Where(x => !x.IsBuiltin && !x.IsDerived && x.Definition.Target is WiredVariableTarget.User or WiredVariableTarget.Furni or WiredVariableTarget.Global)
        .Select(x => new WiredWebApiVariable(x.Definition.ItemId, x.Definition.Name, x.Definition.Target, x.HasValue, x.ReadOnly)).ToArray();

    public WiredWebApiEntity? Find(WiredWebApiTargetKind kind, long id)
    {
        switch (kind) {
            case WiredWebApiTargetKind.Users:
                var user = id is > 0 and <= int.MaxValue ? room.GetRoomUserManager().GetRoomUserByHabbo((int)id) : null;

                return user is null ? null : new(kind, id, user.GetUsername(), id, true);
            case WiredWebApiTargetKind.Pets or WiredWebApiTargetKind.Bots:
                return Bots(kind).FirstOrDefault(x => x.Id == id);
            case WiredWebApiTargetKind.Furni or WiredWebApiTargetKind.WallItems:
                return id is > 0 and <= uint.MaxValue && Item(kind, (uint)id) is { } item ? new(kind, id, item.Definition.PublicName, item.Id, true) : null;
            default:
                // Builders Club furni does not exist on this hotel.
                return null;
        }
    }

    public WiredWebApiEntity? FromStorage(WiredWebApiTargetKind kind, long storageId) => kind switch
    {
        WiredWebApiTargetKind.Users => storageId > 0 ? Find(kind, storageId) ?? new(kind, storageId, null, storageId, false) : null,
        WiredWebApiTargetKind.Pets or WiredWebApiTargetKind.Bots => storageId < 0 ? Bots(kind).FirstOrDefault(x => x.StorageId == storageId) : null,
        _ => Find(kind, storageId)
    };

    public IReadOnlyDictionary<long, WiredVariableValue> Values(WiredWebApiVariable variable)
    {
        var module = RoomVariables.Module;

        if (variable.Target == WiredVariableTarget.Global) {
            var value = module.Read(Reference(variable), new(WiredVariableTarget.Global, 0, 0), Frame());

            return value is null ? new Dictionary<long, WiredVariableValue>() : new Dictionary<long, WiredVariableValue> { [0] = value };
        }

        return module.GetStoredHolders(variable.DefinitionId).Where(x => x.Key.Target == variable.Target).ToDictionary(x => x.Key.HolderId, x => x.Value);
    }

    public WiredWebApiWriteOutcome Write(WiredWebApiVariable variable, WiredWebApiEntity? entity, WiredWebApiWriteMode mode, long value)
    {
        if (variable.ReadOnly) {
            return WiredWebApiWriteOutcome.NotAllowed;
        }

        var frame = Frame();
        WiredVariableHolder holder;

        if (variable.Target == WiredVariableTarget.Global) {
            holder = new(WiredVariableTarget.Global, 0, 0);
        }
        else if (entity is null || Holder(entity, frame) is not { } live) {
            return WiredWebApiWriteOutcome.NotPresent;
        }
        else {
            holder = live;
        }

        var held = variable.Target == WiredVariableTarget.Global || Values(variable).ContainsKey(holder.StorageId);

        if (mode == WiredWebApiWriteMode.Patch && !held) {
            return WiredWebApiWriteOutcome.NoValue;
        }

        if (!variable.HasValue && (mode == WiredWebApiWriteMode.Patch || variable.Target == WiredVariableTarget.Global)) {
            return WiredWebApiWriteOutcome.NotAllowed;
        }

        if (!variable.HasValue && held) {
            return WiredWebApiWriteOutcome.Written;
        }

        var mutation = held ? WiredVariableMutation.Set : WiredVariableMutation.Give;

        if (!RoomVariables.Module.Change(Reference(variable), holder, mutation, _ => value, frame, External, notifyUnchanged: true)) {
            return WiredWebApiWriteOutcome.NotAllowed;
        }

        RoomVariables.InvalidateFx();

        return WiredWebApiWriteOutcome.Written;
    }

    public bool Remove(WiredWebApiVariable variable, WiredWebApiEntity entity)
    {
        if (variable.ReadOnly || RoomVariables.Module.RemoveValue(variable.DefinitionId, variable.Target, entity.StorageId,
            entity.Present ? EntityId(entity) : 0, External) is null) {
            return false;
        }

        RoomVariables.InvalidateFx();

        return true;
    }

    public int Clear(WiredWebApiVariable variable)
    {
        if (variable.ReadOnly || variable.Target is not (WiredVariableTarget.User or WiredVariableTarget.Furni)) {
            return 0;
        }

        var removed = RoomVariables.Module.ClearValues(variable.DefinitionId, variable.Target, Frame(), External);

        if (removed > 0) {
            RoomVariables.InvalidateFx();
        }

        return removed;
    }

    private static WiredVariableReference Reference(WiredWebApiVariable variable) => new(variable.Target, $"custom:{variable.DefinitionId}");

    // The inspection frame, plus the room's pets and bots: they hold user variables in the room too.
    private WiredVariableFrame Frame()
    {
        var inspection = room.GetWired().CaptureVariableInspectionFrame();
        var bots = room.GetRoomUserManager().GetUserList().Where(x => x.IsBot).Select(WiredVariableRuntimeFrames.UserHolder);

        return new(room.Id, inspection.Holders.Concat(bots).Distinct().ToArray()) { RuntimeContext = inspection.RuntimeContext };
    }

    private WiredVariableHolder? Holder(WiredWebApiEntity entity, WiredVariableFrame frame)
    {
        WiredVariableHolder? holder = entity.Kind switch
        {
            WiredWebApiTargetKind.Users => room.GetRoomUserManager().GetRoomUserByHabbo((int)entity.Id) is { } user ? WiredVariableRuntimeFrames.UserHolder(user) : null,
            WiredWebApiTargetKind.Pets or WiredWebApiTargetKind.Bots => BotUsers(entity.Kind).FirstOrDefault(x => BotId(x) == entity.Id) is { } bot
                ? WiredVariableRuntimeFrames.UserHolder(bot) : null,
            WiredWebApiTargetKind.Furni or WiredWebApiTargetKind.WallItems => Item(entity.Kind, (uint)entity.Id) is { } item ? WiredVariableRuntimeFrames.FurniHolder(item) : null,
            _ => null
        };

        return holder is { } found && frame.Holders.Contains(found) ? found : null;
    }

    private int EntityId(WiredWebApiEntity entity) => entity.Kind switch
    {
        WiredWebApiTargetKind.Users => room.GetRoomUserManager().GetRoomUserByHabbo((int)entity.Id)?.VirtualId ?? 0,
        WiredWebApiTargetKind.Pets or WiredWebApiTargetKind.Bots => BotUsers(entity.Kind).FirstOrDefault(x => BotId(x) == entity.Id)?.VirtualId ?? 0,
        _ => unchecked((int)entity.Id)
    };

    private Item? Item(WiredWebApiTargetKind kind, uint id) => room.GetRoomItemHandler()?.GetItem(id) is { IsTemporary: false } item
        && (kind == WiredWebApiTargetKind.WallItems ? item.IsWallItem : item.IsFloorItem) ? item : null;

    private IEnumerable<RoomUser> BotUsers(WiredWebApiTargetKind kind) =>
        room.GetRoomUserManager().GetUserList().Where(x => x.IsBot && x.IsPet == (kind == WiredWebApiTargetKind.Pets));

    private static long BotId(RoomUser user) => user.IsPet ? user.PetData?.PetId ?? 0 : user.BotData?.Id ?? 0;

    private IEnumerable<WiredWebApiEntity> Bots(WiredWebApiTargetKind kind) => BotUsers(kind).Where(x => BotId(x) > 0)
        .Select(x => new WiredWebApiEntity(kind, BotId(x), x.IsPet ? x.PetData?.Name : x.BotData?.Name, WiredVariableRuntimeFrames.UserHolder(x).StableId, true));
}
