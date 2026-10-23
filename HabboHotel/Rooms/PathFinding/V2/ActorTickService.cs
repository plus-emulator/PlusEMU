using Plus.Communication.Packets.Outgoing.Rooms.Avatar;

namespace Plus.HabboHotel.Rooms.PathFinding;

public sealed class ActorTickService(Room room)
{
    public bool BeforeMovement(RoomUser actor)
    {
        if (!room.GetRoomUserManager().ValidateMovementActor(actor)) {
            return false;
        }

        actor.IdleTime++;
        actor.HandleSpamTicks();
        // A sign lasts its own time while the avatar stands still too.
        room.GetRoomUserManager().UpdateSignStatus(actor);

        if (!actor.IsBot && !actor.IsAsleep && actor.IdleTime >= 600) {
            actor.IsAsleep = true;
            room.SendPacket(new SleepComposer(actor.VirtualId, true));
        }

        if (actor.CarryItemId > 0 && --actor.CarryTimer <= 0) {
            actor.CarryItem(0);
        }

        if (room.GotFreeze()) {
            room.GetFreeze().CycleUser(actor);
        }

        // Observed on AIR WIN63-202610201032-107964540: the standing avatar of a user gets a UserUpdate one room tick after a roller slide and
        // a second one tick later. Bots were not observed.
        if (actor.IsRolling && actor.RollerDelay-- <= 0) {
            actor.IsRolling = false;
            actor.UpdateNeeded = true;
            actor.RollerSettling = !actor.IsBot;
        }
        else if (actor.RollerSettling && !actor.IsRolling) {
            actor.RollerSettling = false;
            actor.UpdateNeeded = true;
        }

        return true;
    }
    public void EndCycle()
    {
        var manager = room.GetRoomUserManager();
        var humans = manager.GetUserList().Count(actor => actor.Movement.State == NavState.Active
            && (!actor.IsBot || actor.BotAi == null));

        if (manager.UserCount != humans) {
            manager.UpdateUserCount(humans);
        }
    }
    public void AfterMovement(RoomUser actor)
    {
        if (actor.RidingHorse && !actor.IsBot && actor.GetClient()?.GetHabbo()?.Effects is { CurrentEffect: not 77 }) {
            actor.ApplyEffect(77);
        }

        if (actor.IsBot && actor.BotAi != null) {
            actor.BotAi.OnTimerTick();
        }
    }
}
