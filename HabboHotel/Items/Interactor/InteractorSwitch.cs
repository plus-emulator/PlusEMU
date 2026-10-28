using System.Drawing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Items.Interactor;

internal class InteractorSwitch(IQuestManager quests, IRewardTrackManager rewards) : IFurniInteractor, IApproachInteractor
{
    public int ActionKind => ApproachActionKind.FloorSwitch;

    public void OnPlace(GameClient? session, Item item) { }

    public void OnRemove(GameClient? session, Item item) { }

    public void OnTrigger(GameClient? session, Item item, int request, bool hasRights)
    {
        var itemRoom = item.GetRoom();

        if (itemRoom == null) {
            return;
        }

        if (session == null) {
            return;
        }

        var user = itemRoom.GetRoomUserManager().GetRoomUserByHabbo(session.GetHabbo().Id);

        if (user == null) {
            return;
        }

        if (Gamemap.TilesTouching(item.GetX, item.GetY, user.X, user.Y)) {
            Toggle(session, item);
        }
        else if (NearestTouchingTile(itemRoom, item, user) is { } tile) {
            user.ApproachItem(item, ActionKind, tile);
        }
    }

    // A click from a distance walks to the nearest tile that touches the switch and uses it on arrival; a later click replaces the walk.
    public bool StartFromApproach(Item item, RoomUser user)
    {
        if (item.GetRoom() is not { } room || user.GetClient() is not { } session || session.GetHabbo() is null
            || !Gamemap.TilesTouching(item.GetX, item.GetY, user.X, user.Y)) {
            return false;
        }

        FurnitureUseService.UseApproached(room, session, item);

        return true;
    }

    private static Point? NearestTouchingTile(Room room, Item item, RoomUser user)
    {
        Point? best = null;
        var bestDistance = int.MaxValue;
        var map = room.GetGameMap();

        for (var dy = -1; dy <= 1; dy++) {
            for (var dx = -1; dx <= 1; dx++) {
                var x = item.GetX + dx;
                var y = item.GetY + dy;

                if ((dx == 0 && dy == 0) || !map.ValidTile(x, y) || !map.SquareIsOpen(x, y, false)) {
                    continue;
                }

                var distance = (x - user.X) * (x - user.X) + (y - user.Y) * (y - user.Y);

                if (distance < bestDistance) {
                    best = new(x, y);
                    bestDistance = distance;
                }
            }
        }

        return best;
    }

    private void Toggle(GameClient session, Item item)
    {
        var modes = item.Definition.Modes - 1;

        if (modes <= 0) {
            return;
        }

        quests.ProgressUserQuest(session, QuestType.FurniSwitch);
        var before = item.LegacyDataString;
        var currentMode = 0;
        var newMode = 0;

        if (!int.TryParse(item.LegacyDataString, out currentMode)) { }

        if (currentMode <= 0) {
            newMode = 1;
        }
        else if (currentMode >= modes) {
            newMode = 0;
        }
        else {
            newMode = currentMode + 1;
        }

        item.LegacyDataString = newMode.ToString();
        item.UpdateUsedState();

        if (!string.Equals(before, item.LegacyDataString, StringComparison.Ordinal)) {
            rewards.Progress(session, RewardTrackActions.SwitchItemState);
        }
    }

    public void OnWiredTrigger(Item item) { }
}
