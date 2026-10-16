using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Variables;

public static class WiredMonitorActions
{
    public const int Fetch = 0;
    public const int Clear = 1;
}

public interface IWiredMonitorService
{
    void ShowMonitor(Room room, GameClient session, int action);
    void ShowLogs(Room room, GameClient session, int page, int size, int level, int source, string query);
    void ShowRoomStats(Room room, GameClient session);
    void ShowErrorLogs(Room room, GameClient session);
    void ClearErrorLogs(Room room, GameClient session);
}

/// <summary>Monitor and room log inspection; owns the inspect and manage rights, the rate gates and every response it sends.</summary>
public sealed class WiredMonitorService(IWiredRequestGateService gates) : IWiredMonitorService
{
    public void ShowMonitor(Room room, GameClient session, int action)
    {
        var settings = room.GetWired().Settings;

        if (!settings.CanInspect(session)) {
            return;
        }

        if (action == WiredMonitorActions.Clear) {
            // A clear has its own gate so a poll cannot swallow it. AIR enables Clear for Wired modify rights (WiredMenuController.hasWritePermission).
            if (!settings.CanModify(session) || !gates.TryPass(session, WiredRequestKind.MonitorClear)) {
                return;
            }

            room.GetWired().ClearLogs();
        }
        else if (!gates.TryPass(session, WiredRequestKind.MonitorFetch)) {
            return;
        }

        session.Send(new WiredMonitorDataComposer(room.GetWired().ReadMonitor()));
    }

    // The client's pages are 1-based, and the filter text is trimmed before it is matched.
    public void ShowLogs(Room room, GameClient session, int page, int size, int level, int source, string query)
    {
        if (!room.GetWired().Settings.CanInspect(session) || !gates.TryPass(session, WiredRequestKind.RoomLogPage)) {
            return;
        }

        query = query.Trim();
        var result = room.GetWired().ReadLogs(Math.Max(1, page) - 1, size, level, query, source);
        session.Send(new WiredRoomLogPageComposer(result, level, source, query));
    }

    public void ShowRoomStats(Room room, GameClient session)
    {
        if (!room.GetWired().Settings.CanInspect(session) || !gates.TryPass(session, WiredRequestKind.RoomStats)) {
            return;
        }

        session.Send(new WiredRoomStatsComposer(room.GetWired().ReadRoomStats()));
    }

    public void ShowErrorLogs(Room room, GameClient session)
    {
        if (!room.GetWired().Settings.CanInspect(session) || !gates.TryPass(session, WiredRequestKind.ErrorLogs)) {
            return;
        }

        session.Send(new WiredErrorLogsComposer(room.GetWired().ReadErrors()));
    }

    // The cleared rows are sent back at once, so the tab shows all ten at 0 / -1 without waiting for its next poll.
    public void ClearErrorLogs(Room room, GameClient session)
    {
        var settings = room.GetWired().Settings;

        if (!settings.CanInspect(session) || !settings.CanModify(session) || !gates.TryPass(session, WiredRequestKind.ErrorClear)) {
            return;
        }

        room.GetWired().ClearErrors();
        session.Send(new WiredErrorLogsComposer(room.GetWired().ReadErrors()));
    }
}
