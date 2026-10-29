using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Rooms.Games.Teams;
using Plus.HabboHotel.Rooms.PathFinding;

namespace Plus.HabboHotel.Rooms.Games;

/// <summary>Writes a football round started and stopped by Control Clock to every high score board in the room.</summary>
internal sealed class RoomHighscores(Room room, TimeProvider time)
{
    private sealed record Round(WiredClockTransition Start, long Started, long Generation);
    private sealed record Completion(Round Round, HighscoreBatch? Batch, IReadOnlyList<HighscoreWrite> Writes);
    private IRoomHighscoreStore? _store;
    private Round? _round;
    private Completion? _pending;
    private long _generation;
    private bool _enrolled;
    internal bool Enrolled => Volatile.Read(ref _enrolled);

    internal void Initialize(IRoomHighscoreStore store)
    {
        _store ??= store;
        Enroll(); // Called during unpublished RoomData initialization.
    }
    internal void Invalidate() => Interlocked.Increment(ref _generation);
    internal void Dispose()
    {
        Invalidate();
        _round = null;
        _pending = null;
    }

    private void Enroll()
    {
        if (_enrolled || _store == null) {
            return;
        }

        var items = room.GetRoomItemHandler().GetFloor.ToArray();

        if (items.Any(item => item.Definition.ItemName == "fball_counter") && items.Any(SupportedBoard)) {
            Volatile.Write(ref _enrolled, true);
        }
    }
    internal void OnOwnedPass()
    {
        if (!RoomOwnerScope.IsOwner(room)) {
            return;
        }

        Enroll();

        if (_pending == null || _store == null) {
            return;
        }

        var pending = _pending;
        var handler = room.GetRoomItemHandler();
        var retry = pending.Batch == null;

        if (retry) {
            if (!SourceStillLive(pending.Round)) {
                _pending = null;

                return;
            }

            if (!handler.TryRegisterHighscores(pending.Writes, out var batch)) {
                return;
            }

            pending = pending with { Batch = batch };
            _pending = pending;
        }

        var result = handler.ResolveHighscores(pending.Batch!, _store, retry);

        // Reconciliation never creates a new candidate. An all-prior result releases its old registration.
        if (result == HighscoreResolution.Candidate) {
            _pending = null;
        }
        else if (result == HighscoreResolution.Prior) {
            _pending = SourceStillLive(pending.Round) ? pending with { Batch = null } : null;
        }
    }
    internal bool CanBeginOwned(Item item, WiredClockOrigin origin, RoomUser? actor)
    {
        if (!Enrolled || !RoomOwnerScope.IsOwner(room) || origin != WiredClockOrigin.ModernWired
            || item.Definition.ItemName != "fball_counter") {
            return true;
        }

        OnOwnedPass();

        // An all-prior reconciliation can release its registration for one frozen retry.
        if (_pending is { Batch: null }) {
            OnOwnedPass();
        }

        return _pending == null;
    }
    internal void Observe(WiredClockTransition transition)
    {
        if (!RoomOwnerScope.IsOwner(room) || transition.Origin != WiredClockOrigin.ModernWired
            || transition.Reason is not (WiredClockReason.Start or WiredClockReason.Stop)) {
            Invalidate();

            return;
        }

        if (transition.OtherFootballClockRunning) {
            Invalidate();

            return;
        }

        if (transition.Reason == WiredClockReason.Stop) {
            Complete(transition);
        }
    }
    // Called AFTER the existing game Reset, before Soccer/GameStarts.
    internal void Start(WiredClockTransition transition)
    {
        _round = null;

        if (!Enrolled || _pending != null || !RoomOwnerScope.IsOwner(room) || transition.Origin != WiredClockOrigin.ModernWired
            || transition.OtherFootballClockRunning || transition.Reason != WiredClockReason.Start
            || !(transition.PreviousReason is null or WiredClockReason.Stop
                || transition.PreviousReason == WiredClockReason.Reset && transition.PreviousOrigin == WiredClockOrigin.ModernWired)
            || transition.Item.Definition.ItemName != "fball_counter") {
            return;
        }

        if (!room.GetRoomItemHandler().GetFloor.Any(SupportedBoard)) {
            return;
        }

        _round = new(transition, time.GetTimestamp(), Interlocked.Read(ref _generation));
    }
    private void Complete(WiredClockTransition stop)
    {
        var round = _round;
        _round = null; // Completion is consumed before persistence/output/reentrant Stop.

        if (round == null || _store == null || stop.WasRunning == false || stop.Reason != WiredClockReason.Stop
            || !ReferenceEquals(stop.Clock, round.Start.Clock) || stop.Sequence != round.Start.Sequence + 1 || !SourceStillLive(round)) {
            return;
        }

        var seconds = time.GetElapsedTime(round.Started, time.GetTimestamp()).TotalSeconds;

        if (seconds < 0 || seconds > int.MaxValue) {
            return;
        }

        var teams = Teams();

        if (teams.Count == 0) {
            return;
        }

        var writes = new List<HighscoreWrite>();

        foreach (var item in room.GetRoomItemHandler().GetFloor.Where(SupportedBoard).OrderBy(item => item.Id)) {
            var board = (HighscoreDataFormat)item.ExtraData;
            var prior = board.Serialize();
            var data = new HighscoreDataFormat();
            data.Store(prior);

            HighscoreRules.Apply(data, teams, (int)seconds);
            var candidate = data.Serialize();

            if (candidate != prior) {
                writes.Add(new(item, item.Placement, item.Definition, room.Id, item.OwnerId, prior, candidate));
            }
        }

        if (writes.Count == 0) {
            return;
        }

        var handler = room.GetRoomItemHandler();

        if (!handler.TryCaptureHighscores(writes, _store, out var batch)) {
            return;
        }

        _pending = new(round, batch!, batch!.Writes);
        var result = handler.ResolveHighscores(batch!, _store, true);

        if (result == HighscoreResolution.Candidate) {
            _pending = null;
        }
        else if (result == HighscoreResolution.Prior) {
            _pending = _pending with { Batch = null };
        }
        // Unknown is quarantined; all-prior can retry only these frozen candidates on a later owned pass.
    }
    // The players in each team when the round stops, in room order. Bots and leaving users never count.
    private IReadOnlyList<HighscoreTeam> Teams() => room.GetRoomUserManager().GetRoomUsers()
        .Where(user => user != null && !user.IsBot && user.Team != Team.None && user.Movement.State != NavState.Removing)
        .GroupBy(user => user.Team)
        .OrderBy(group => group.Key)
        .Select(group => new HighscoreTeam(group.Select(user => user.GetUsername()).ToArray(), room.GetGameManager().Points[(int)group.Key]))
        .ToArray();
    private bool SourceStillLive(Round round) => round.Generation == Interlocked.Read(ref _generation)
        && ReferenceEquals(room.GetRoomItemHandler().GetItem(round.Start.ItemId), round.Start.Item)
        && round.Start.Item.GetX == round.Start.X && round.Start.Item.GetY == round.Start.Y
        && round.Start.Item.GetZ == round.Start.Z && round.Start.Item.Rotation == round.Start.Rotation
        && round.Start.Item.Id == round.Start.ItemId && round.Start.Item.RoomId == round.Start.RoomId && round.Start.RoomId == room.Id
        && round.Start.Item.OwnerId == round.Start.OwnerId && round.Start.Item.Definition.Id == round.Start.BaseItem
        && round.Start.Item.Definition.ItemName == round.Start.DefinitionName
        && round.Start.Item.Placement == round.Start.Placement && ReferenceEquals(round.Start.Item.Definition, round.Start.Definition);
    private static bool SupportedBoard(Item item) => item.IsFloorItem && !item.IsTemporary && item.Definition.InteractionType == InteractionType.None
        && HighscoreDataFormat.TryDefinition(item.Definition.ItemName, out var kind, out var clear)
        && item.ExtraData is HighscoreDataFormat data && data.ScoreType == kind && data.ClearType == clear;
}
