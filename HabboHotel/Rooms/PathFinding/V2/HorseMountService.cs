using Plus.HabboHotel.Rooms.AI;
using Plus.Communication.Packets.Outgoing.Rooms.AI.Pets;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Pets.Locale;

namespace Plus.HabboHotel.Rooms.PathFinding;

internal sealed class HorseMountService(Room room, RoomNavigation navigation, MovementContext context)
{
    private sealed record PendingMount(RoomUser Horse, long HorseLifetime, long RiderLifetime, int GoalX, int GoalY, IPetLocale Locale)
    {
        public int Waited { get; set; }
    }

    private const int MaxWalkTicks = 240;

    // Riders who asked to mount from a distance and are walking up to the horse.
    private readonly Dictionary<RoomUser, PendingMount> _pending = new();

    internal void Ride(RoomUser rider, RoomUser horse, bool mount, IPetLocale locale)
    {
        var lifetime = horse.Movement.LifetimeId;
        var sequence = horse.Movement.Commands.Read()?.Sequence ?? 0;
        navigation.RunOwner(rider, (actor, discardThrough) =>
        {
            if (!Attached(horse, lifetime)) {
                return;
            }

            _pending.Remove(actor);

            if (mount && !actor.RidingHorse && !horse.RidingHorse && !NextTo(actor, horse)) {
                WalkUp(actor, horse, lifetime, locale);

                return;
            }

            if (mount) {
                TryMount(actor, horse, discardThrough, sequence, locale);
            }
            else {
                TryDismount(actor, horse, discardThrough, sequence);
            }

            room.SendPacket(new PetHorseFigureInformationComposer(PetAppearanceSnapshots.Horse(horse)));
        });
    }

    internal void DetachPickedUpHorse(RoomUser horse)
    {
        var rider = room.GetRoomUserManager().GetRoomUserByVirtualId(horse.HorseId);

        if (rider == null) {
            return;
        }

        var lifetime = horse.Movement.LifetimeId;
        navigation.RunOwner(rider, (actor, discardThrough) =>
        {
            if (horse.Movement.LifetimeId != lifetime || actor.HorseId != horse.VirtualId) {
                return;
            }

            navigation.CancelThrough(actor, discardThrough);
            ClearGroup(actor, horse);
            actor.ApplyEffect(-1);
            PostureService.Apply(room, navigation.Grid, actor);
            context.RefreshMembership(actor);
            ResumeDismounted(actor, 1, discardThrough);
        });
    }

    // A rider has to stand next to the horse to get on; from further away they walk up first.
    internal void TickPending()
    {
        if (_pending.Count == 0) {
            return;
        }

        foreach (var (rider, pending) in _pending.ToList()) {
            var gone = rider.Movement.LifetimeId != pending.RiderLifetime || rider.Movement.State != NavState.Active
                || !Attached(pending.Horse, pending.HorseLifetime);
            var redirected = rider.GoalX != pending.GoalX || rider.GoalY != pending.GoalY;
            var arrived = rider.X == pending.GoalX && rider.Y == pending.GoalY && !rider.HasStatus("mv");

            if (!gone && !redirected && !arrived && ++pending.Waited < MaxWalkTicks) {
                continue;
            }

            _pending.Remove(rider);

            if (!gone && arrived && NextTo(rider, pending.Horse)) {
                Ride(rider, pending.Horse, true, pending.Locale);
            }
        }
    }

    private void WalkUp(RoomUser rider, RoomUser horse, long horseLifetime, IPetLocale locale)
    {
        var map = room.GetGameMap();
        var target = Neighbours(horse)
            .Where(tile => map.CanWalk(tile.X, tile.Y, false) && !map.SquareHasUsers(tile.X, tile.Y))
            .OrderBy(tile => Math.Max(Math.Abs(tile.X - rider.X), Math.Abs(tile.Y - rider.Y)))
            .Cast<(int X, int Y)?>()
            .FirstOrDefault();

        if (target is not { } tile) {
            return;
        }

        rider.MoveTo(tile.X, tile.Y);
        _pending[rider] = new(horse, horseLifetime, rider.Movement.LifetimeId, tile.X, tile.Y, locale);
    }

    private static IEnumerable<(int X, int Y)> Neighbours(RoomUser horse)
    {
        for (var dx = -1; dx <= 1; dx++) {
            for (var dy = -1; dy <= 1; dy++) {
                if (dx != 0 || dy != 0) {
                    yield return (horse.X + dx, horse.Y + dy);
                }
            }
        }
    }

    private static bool NextTo(RoomUser rider, RoomUser horse)
        => Math.Abs(rider.X - horse.X) <= 1 && Math.Abs(rider.Y - horse.Y) <= 1;

    private bool Attached(RoomUser horse, long lifetime) => horse.Movement.LifetimeId == lifetime
        && horse.Movement.State == NavState.Active
        && ReferenceEquals(room.GetRoomUserManager().GetRoomUserByVirtualId(horse.VirtualId), horse);

    private void TryMount(RoomUser rider, RoomUser horse, long riderSequence, long horseSequence, IPetLocale locale)
    {
        if (horse.RidingHorse) {
            var speech = locale.GetValue("pet.alreadymounted");
            horse.Chat(speech[Random.Shared.Next(speech.Length)]);
        }
        else if (rider.RidingHorse) {
            rider.GetClient()?.SendNotification("You are already riding a horse!");
        }
        else {
            Mount(rider, horse, riderSequence, horseSequence);
        }
    }

    private void Mount(RoomUser rider, RoomUser horse, long riderSequence, long horseSequence)
    {
        var origin = (horse.X, horse.Y, horse.Z);
        var riderOrigin = (rider.X, rider.Y, rider.Z);
        var supportZ = room.GetGameMap().SqAbsoluteHeight(horse.X, horse.Y);
        horse.Statusses.Clear();
        rider.RidingHorse = horse.RidingHorse = true;
        rider.HorseId = horse.VirtualId;
        horse.HorseId = rider.VirtualId;
        navigation.ForcePlaceThrough(horse, horse.X, horse.Y, supportZ, ForceResolution.Highest, horseSequence);
        navigation.ForcePlaceThrough(rider, horse.X, horse.Y, supportZ, ForceResolution.Highest, riderSequence);
        rider.RotHead = horse.RotHead;
        rider.RotBody = horse.RotBody;
        rider.ApplyEffect(77);
        rider.UpdateNeeded = horse.UpdateNeeded = true;
        room.SendPacket(new SlideObjectBundleComposer(origin.X, origin.Y, origin.Z,
            horse.X, horse.Y, horse.Z, 0, horse.VirtualId, 0));
        room.SendPacket(new SlideObjectBundleComposer(riderOrigin.X, riderOrigin.Y, riderOrigin.Z,
            rider.X, rider.Y, rider.Z, 0, rider.VirtualId, 0));
    }

    private void TryDismount(RoomUser rider, RoomUser horse, long riderSequence, long horseSequence)
    {
        if (horse.HorseId != rider.VirtualId) {
            rider.GetClient()?.SendNotification("Could not dismount this horse - You are not riding it!");

            return;
        }

        navigation.CancelThrough(rider, riderSequence);
        navigation.CancelThrough(horse, horseSequence);

        foreach (var status in new[] { "sit", "lay", "snf", "eat", "ded", "jmp" }) {
            horse.Statusses.Remove(status);
        }

        ClearGroup(rider, horse);
        rider.ApplyEffect(-1);
        PostureService.Apply(room, navigation.Grid, rider);
        context.RefreshMembership(rider);
        context.RefreshMembership(horse);
        ResumeDismounted(rider, 2, riderSequence);
    }

    private static void ClearGroup(RoomUser rider, RoomUser horse)
    {
        rider.RidingHorse = horse.RidingHorse = false;
        rider.HorseId = horse.HorseId = 0;
        rider.UpdateNeeded = horse.UpdateNeeded = true;
    }

    private static void ResumeDismounted(RoomUser rider, int offset, long discardThrough)
    {
        if ((rider.Movement.Commands.Read()?.Sequence ?? 0) <= discardThrough) {
            rider.MoveTo(rider.X + offset, rider.Y + offset);
        }
    }
}
