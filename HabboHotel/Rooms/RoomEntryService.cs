using Plus.Communication.Packets.Outgoing.Rooms.Chat;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms.Polls;

namespace Plus.HabboHotel.Rooms;

public interface IRoomEntryService
{
    void Enter(GameClient session);
}

public sealed class RoomEntryService(IQuestManager quests, TimeProvider clock, IRoomPollService polls, IRoomWordQuizService quizzes) : IRoomEntryService
{
    public void Enter(GameClient session)
    {
        var habbo = session.GetHabbo();
        var room = habbo.CurrentRoom;

        if (room == null) {
            return;
        }

        // The server sends the room data when it opens the room; a repeated or legacy request for it changes nothing.
        if (room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id) is { } admitted && ReferenceEquals(admitted.GetClient(), session)) {
            return;
        }

        if (!room.GetRoomUserManager().AddAvatarToRoom(session)) {
            room.GetRoomUserManager().RemoveUserFromRoom(session, false);

            return;
        }

        room.SendObjects(session);
        habbo.Messenger?.NotifyChangesToFriends();

        if (habbo.HabboStats is { QuestId: > 0 } stats) {
            quests.QuestReminder(session, stats.QuestId);
        }

        // After the entrant's own Users entry come RoomEntryInfo, RoomEvent, the configuration item states, the Wired environment and the labels, then the controller level, Wired
        // permissions, YouAreOwner, the room rating and the raid protection capability. The trigger-text notice follows the room-enter packet in the same tick.
        session.Send(new RoomEntryInfoComposer(room.RoomId, room.CheckRights(session, true)));
        room.GetWired()?.SendTriggerTextNotice(session);
        session.Send(new RoomEventComposer(RoomEventSnapshot.Capture(room.Data, room.Promotion)));
        Plus.HabboHotel.Items.ConfigurationItemStates.SendTo(session, room);
        room.GetWired()?.SendEnvironment(session);
        // WIRED Label bubbles: the full list this user may see, once the room is entered.
        room.GetWired()?.SendLabels(session);
        // Enter Room stacks run here, and their flashes come before the controller packets.
        room.GetWired()?.TriggerEvent(WiredBoxType.TriggerRoomEnter, habbo);
        room.GetRoomUserManager().SendEntryRights(session);
        session.Send(new Plus.Communication.Packets.Outgoing.Navigator.RoomRatingComposer(room.Score, !(habbo.RatedRooms.Contains(room.RoomId) || room.OwnerId == habbo.Id)));

        if (room.GetRoomUserManager().GetRoomUserByHabbo(habbo.Id) is { CurrentEffect: > 0 } entrant) {
            session.Send(new Plus.Communication.Packets.Outgoing.Rooms.Avatar.AvatarEffectComposer(entrant.VirtualId, entrant.CurrentEffect));
        }

        session.Send(new Plus.Communication.Packets.Outgoing.Rooms.Session.RaidProtectionCapabilityComposer(room.RoomId, room.OwnerId == habbo.Id));
        var now = clock.GetUtcNow();

        if (habbo.FloodUntil is { } floodUntil && now < floodUntil) {
            session.Send(new FloodControlComposer(RemainingFloodSeconds(now, floodUntil)));
        }

        polls.Offer(session);
        quizzes.Show(session);
    }

    internal static int RemainingFloodSeconds(DateTimeOffset now, DateTimeOffset floodUntil) =>
        (int)Math.Clamp((long)Math.Ceiling((floodUntil - now).TotalSeconds), 0, int.MaxValue);
}
