using Plus.Communication.Packets.Outgoing.WiredVariables;
using Plus.HabboHotel.Cache;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>A Manage window write: entity type (1 Habbo), user id, catalogue id, value and action (0 set, 1 create, 2 delete).</summary>
public sealed record WiredPermanentVariableWrite(int EntityType, int EntityId, string VariableId, int Value, int Action);

public interface IWiredVariableHolderService
{
    void ShowAllHolders(Room room, GameClient session, string id);
    void DeleteAllHolders(Room room, GameClient session, string id);
    void ShowOwnersPage(Room room, GameClient session, string id, int page, int size, int sort, int userType);
    void ShowPermanentVariables(Room room, GameClient session, int entityType, int entityId);
    void SetPermanentVariable(Room room, GameClient session, WiredPermanentVariableWrite request);
}

/// <summary>
/// The Variables tab's Highlight, Delete and Manage buttons. A request without the right it needs (inspect to read, modify to change)
/// gets no answer.
/// </summary>
public sealed class WiredVariableHolderService(ICacheManager users) : IWiredVariableHolderService
{
    private const int Habbo = 1;

    public void ShowAllHolders(Room room, GameClient session, string id)
    {
        if (!CanInspect(room, session)) {
            return;
        }

        var menu = Menu(room);

        if (menu.Catalog().Find(id) is not { HasHolders: true } variable) {
            session.Send(new WiredMenuErrorComposer(WiredMenuError.NoHolders));

            return;
        }

        session.Send(new WiredAllVariableHoldersComposer(variable, menu.LiveEntities(variable)));
    }

    // Internal ("@") and smart ("~") ids are never cleared and get no answer. Only the owner clears a variable from all holders.
    public void DeleteAllHolders(Room room, GameClient session, string id)
    {
        if (!CanModify(room, session) || id is "" or ['@' or '~', ..]) {
            return;
        }

        var menu = Menu(room);

        if (menu.Catalog().Find(id) is not { IsPermanent: true } variable) {
            session.Send(new WiredMenuErrorComposer(WiredMenuError.Failed));
        }
        else if (!room.GetWired().Settings.CanManage(session)) {
            session.Send(new WiredMenuErrorComposer(WiredMenuError.OwnerOnly));
        }
        else {
            menu.Clear(variable.Definition.Target, variable.Definition.ItemId);
        }
    }

    // User type -1 and 0 list everybody and 1 lists Habbos. Only Habbos keep permanent values, so other types list nobody.
    public void ShowOwnersPage(Room room, GameClient session, string id, int page, int size, int sort, int userType)
    {
        if (!CanInspect(room, session) || page < 1) {
            return;
        }

        var menu = Menu(room);

        if (size is < 1 or > 50 || menu.Catalog().Find(id) is not { IsPermanent: true, Definition.Target: WiredVariableTarget.User } variable) {
            session.Send(new WiredMenuErrorComposer(WiredMenuError.NoOwners));

            return;
        }

        var result = variable.HasValue && userType is -1 or 0 or Habbo ? menu.Owners(variable, page, size, sort) : new(0, page, size, []);
        result = result with
        {
            Holders = result.Holders.Select(x => x with { Name = users.GenerateUser(checked((int)x.Key.HolderId))?.Username ?? x.Name }).ToArray()
        };
        session.Send(new WiredUserVariablesListComposer(id, result, userType, sort));
    }

    // Users always see their own list. Another user is only shown while holding a permanent variable of the room.
    public void ShowPermanentVariables(Room room, GameClient session, int entityType, int entityId)
    {
        if (!CanInspect(room, session)) {
            return;
        }

        var values = entityType == Habbo ? Menu(room).PermanentValues(entityId) : [];

        if (entityType != Habbo || values.Count == 0 && entityId != session.GetHabbo().Id) {
            session.Send(new WiredMenuErrorComposer(WiredMenuError.UnknownEntity));

            return;
        }

        SendPermanentValues(session, entityId, values);
    }

    /// <summary>
    /// Create and delete need a user who holds a permanent variable of the room. Set on a user who does not hold the variable succeeds
    /// and changes nothing. A variable without a value is never set and only created with value 0. Any other action is a set.
    /// </summary>
    public void SetPermanentVariable(Room room, GameClient session, WiredPermanentVariableWrite request)
    {
        if (!CanModify(room, session)) {
            return;
        }

        var menu = Menu(room);
        var variable = menu.Catalog().Find(request.VariableId);
        var held = request.EntityType == Habbo ? menu.PermanentValues(request.EntityId) : [];
        var success = request.EntityType == Habbo && variable is { IsPermanent: true, Definition.Target: WiredVariableTarget.User }
            && (held.Count > 0 || users.GenerateUser(request.EntityId) is not null)
            && request.Action switch
            {
                1 => held.Count > 0 && (variable.HasValue || request.Value == 0)
                    && menu.WritePermanent(variable, request.EntityId, WiredVariableMutation.Replace, request.Value),
                2 => held.Count > 0 && menu.WritePermanent(variable, request.EntityId, WiredVariableMutation.Remove, 0),
                _ => held.All(x => x.Variable.Definition.ItemId != variable.Definition.ItemId)
                    || variable.HasValue && menu.WritePermanent(variable, request.EntityId, WiredVariableMutation.Set, request.Value)
            };

        if (!success) {
            session.Send(new WiredMenuErrorComposer(WiredMenuError.Failed));
            session.Send(new WiredSetUserPermanentVariableResultComposer(false));

            return;
        }

        session.Send(new WiredSetUserPermanentVariableResultComposer(true));
        SendPermanentValues(session, request.EntityId, menu.PermanentValues(request.EntityId));
    }

    private void SendPermanentValues(GameClient session, int userId,
        IReadOnlyList<(WiredVariableDescription Variable, WiredVariableValue Value)> values)
    {
        var habbo = session.GetHabbo();
        var (name, figure) = userId == habbo.Id ? (habbo.Username, habbo.Look)
            : users.GenerateUser(userId) is { } user ? (user.Username, user.Look) : ("", "");
        session.Send(new WiredUserPermanentVariablesComposer(userId, name, figure, values));
    }

    private static bool CanInspect(Room room, GameClient session) =>
        ReferenceEquals(session.GetHabbo().CurrentRoom, room) && room.GetWired().Settings.CanInspect(session);
    private static bool CanModify(Room room, GameClient session) =>
        ReferenceEquals(session.GetHabbo().CurrentRoom, room) && room.GetWired().Settings.CanModify(session);
    private static WiredVariableMenu Menu(Room room) => new(room, room.GetWired().Variables);
}
