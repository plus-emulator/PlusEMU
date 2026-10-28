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

/// <summary>The Variables tab's Highlight, Delete and Manage buttons.</summary>
public sealed class WiredVariableHolderService(ICacheManager users) : IWiredVariableHolderService
{
    public void ShowAllHolders(Room room, GameClient session, string id) { }
    public void DeleteAllHolders(Room room, GameClient session, string id) { }
    public void ShowOwnersPage(Room room, GameClient session, string id, int page, int size, int sort, int userType) { }
    public void ShowPermanentVariables(Room room, GameClient session, int entityType, int entityId) { }
    public void SetPermanentVariable(Room room, GameClient session, WiredPermanentVariableWrite request) { }
}
