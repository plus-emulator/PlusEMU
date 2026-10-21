using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Selectors;

/// <summary>Room-owned action observations; avatar identity prevents VirtualId reuse from inheriting history.</summary>
public sealed class WiredSelectorRoomState
{
    private readonly Dictionary<RoomUser, (int Action, int Parameter, long At)> _actions = [];

    /// <summary>
    /// How long Users Performing Action still selects a user after an action, or null for an action it never remembers, which also
    /// leaves the earlier action in place. Observed on AIR WIN63-202610201032-107964540: a wave is selected until 5 s after it and
    /// respect 2 s but not 3 s after it; a blow, a laugh and a dance that has stopped are not selected.
    /// </summary>
    public static int? RememberedMilliseconds(int action) => (WiredAvatarAction)action switch
    {
        WiredAvatarAction.Kiss or WiredAvatarAction.Laugh or WiredAvatarAction.Dance => null,
        WiredAvatarAction.Respect => 2500,
        _ => 5000
    };

    public void Observe(WiredRuntimeEvent @event, long nowMilliseconds)
    {
        if (@event.Kind == WiredEventKind.AvatarAction && @event.Actor is { } user && RememberedMilliseconds(@event.Action) != null) {
            _actions[user] = (@event.Action, @event.Code, nowMilliseconds);
        }

        foreach (var stale in _actions.Where(x => nowMilliseconds >= x.Value.At
            && nowMilliseconds - x.Value.At > RememberedMilliseconds(x.Value.Action)).Select(x => x.Key).ToArray()) {
            _actions.Remove(stale);
        }

        if (@event.Kind == WiredEventKind.Leave && @event.Actor is { } leaving) {
            _actions.Remove(leaving);
        }
    }

    public (int Action, int Parameter, long At)? Read(RoomUser user) => _actions.TryGetValue(user, out var action) ? action : null;
    public void Reset() => _actions.Clear();
}
