using System.Drawing;
using Plus.Communication.Packets.Outgoing.Rooms.Avatar;

namespace Plus.HabboHotel.Rooms.PathFinding;

internal sealed record PendingRollerLanding(Point Destination, long Revision);

public sealed class ActorTickService(Room room)
{
    private readonly TransportLandingService _landings = new(room);

    public bool BeforeMovement(RoomUser actor)
    {
        if (!room.GetRoomUserManager().ValidateMovementActor(actor)) {
            return false;
        }

        actor.CompleteWake();
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

        // The standing avatar of a user gets a UserUpdate one room tick after a roller slide and a second one tick later.
        if (actor.IsRolling && DefersToRoller(actor)) {
            actor.RollerDeferred = true;
        }
        else if (actor.IsRolling && actor.RollerDelay-- <= 0) {
            Land(actor);
        }
        else if (actor.RollerSettling && !actor.IsRolling) {
            actor.RollerSettling = false;
            actor.UpdateNeeded = true;
        }

        return true;
    }
    // A unit on a roller that steps again this tick is not landed: it gets no update and no Walks On between pulses.
    private bool DefersToRoller(RoomUser actor)
        => actor.RollerDelay <= 0 && room.GetRoomItemHandler().RollersStepThisTick
            && room.GetGameMap().GetAllRoomItemForSquare(actor.X, actor.Y).Any(RollerIdentity.IsRoller);
    private void Land(RoomUser actor)
    {
        actor.IsRolling = false;
        actor.RollerDeferred = false;
        actor.UpdateNeeded = true;
        actor.RollerSettling = !actor.IsBot;
        LandRoller(actor);
    }
    // A deferred unit the roller phase did not move lands now.
    public void LandDeferred(RoomUser actor)
    {
        if (actor.RollerDeferred && actor.IsRolling) {
            Land(actor);
        }

        actor.RollerDeferred = false;
    }
    private void LandRoller(RoomUser actor)
    {
        var landing = actor.PendingLanding;
        actor.PendingLanding = null;

        if (landing != null && actor.Movement.LocationRevision == landing.Revision && actor.Movement.State == NavState.Active
            && !actor.IsWalking && actor.Movement.PendingCount == 0) {
            _landings.TriggerWalkOn(actor, landing.Destination);
        }
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
