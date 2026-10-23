using Plus.HabboHotel.Items.Wired.Configuration;

namespace Plus.HabboHotel.Items.Wired.Modern.Triggers;

/// <summary>
/// Per-room timer state. The room tick supplies monotonic time; this module creates no timers
/// or threads. Forget a removed box, and clear on room disposal.
/// </summary>
public sealed class WiredTimedTriggers
{
    private const long StateUpdateMs = WiredFlashStates.StateUpdateMilliseconds;
    private const long WiredTickMs = WiredFlashStates.WiredTickMilliseconds;
    private readonly Dictionary<uint, long> _nextPeriodicAt = [];
    private readonly Dictionary<uint, long> _firedEpoch = [];

    public bool TryFire(string name, uint boxId, WiredConfiguration config,
        long nowMs, long elapsedMs, long resetEpoch)
    {
        var units = config.IntParams.IsDefaultOrEmpty ? 1 : config.IntParams[0];

        if (units < (name == "wf_trg_at_given_time" ? 0 : 1)) {
            return false;
        }

        if (IsAtTime(name)) {
            // Both asset variants use the current one-shot editor in half-second units. The caller measures elapsed time from the first
            // room tick after the reset (see FirstRoomTickAfter), so the pulses land on the room's 500 ms grid.
            var targetMs = AtTimeMilliseconds(config);

            if (elapsedMs < targetMs || _firedEpoch.TryGetValue(boxId, out var fired) && fired == resetEpoch) {
                return false;
            }

            _firedEpoch[boxId] = resetEpoch;

            return true;
        }

        var stepMs = name switch
        {
            "wf_trg_periodically" => 500L,
            "wf_trg_period_short" => 50L,
            "wf_trg_period_long" => 5000L,
            _ => 0L
        };

        if (stepMs == 0) {
            return false;
        }

        var intervalMs = units * stepMs;
        // A 500 ms "Repeat every" timer first fires on the next 500 ms room boundary after it is armed (the save time does not set the phase) and then stays on that grid,
        // the same ticks as the walk steps. The 5 s timer keeps the same grid. The 50 ms timer counts on the 50 ms Wired ticks.
        var onGrid = stepMs % StateUpdateMs == 0;

        if (!_nextPeriodicAt.TryGetValue(boxId, out var next)) {
            _nextPeriodicAt[boxId] = onGrid ? NextBoundary(nowMs) + intervalMs - StateUpdateMs : nowMs - nowMs % WiredTickMs + intervalMs;

            return false;
        }

        if (nowMs < next) {
            return false;
        }

        // A late poll fires once. A grid timer counts the next period from its 500 ms boundary. Another timer, such as a 200 ms Repeat Short,
        // counts from its due time, so it does not drift or lose a tick; a poll a whole period late skips the missed periods instead of firing them in a burst.
        _nextPeriodicAt[boxId] = onGrid ? nowMs - nowMs % StateUpdateMs + intervalMs : next + ((nowMs - next) / intervalMs + 1) * intervalMs;

        return true;
    }

    /// <summary>
    /// A box placed, saved or moved when the room timer has already passed its time counts as fired until the next timer reset.
    /// </summary>
    public void Join(string name, uint boxId, WiredConfiguration config, long roomTimerMs, long resetEpoch)
    {
        if (IsAtTime(name) && roomTimerMs >= AtTimeMilliseconds(config)) {
            _firedEpoch[boxId] = resetEpoch;
        }
    }

    private static bool IsAtTime(string name) => name is "wf_trg_at_given_time" or "wf_trg_at_time_long";

    private static long AtTimeMilliseconds(WiredConfiguration config) => (config.IntParams.IsDefaultOrEmpty ? 1 : config.IntParams[0]) * 500L;

    private static long NextBoundary(long nowMs) => (nowMs / StateUpdateMs + 1) * StateUpdateMs;

    /// <summary>
    /// The first 500 ms room tick strictly after the instant. At Given Time pulses count from the next room tick after the reset, on the room grid.
    /// </summary>
    public static long FirstRoomTickAfter(long nowMs) => NextBoundary(nowMs);

    /// <summary>
    /// A room timer reset restarts an armed repeater on the first room tick after the reset; an unarmed one stays unarmed.
    /// </summary>
    public void ResetPeriodic(uint boxId, long nowMs)
    {
        if (_nextPeriodicAt.ContainsKey(boxId)) {
            _nextPeriodicAt[boxId] = FirstRoomTickAfter(nowMs);
        }
    }

    public void Forget(uint boxId)
    {
        _nextPeriodicAt.Remove(boxId);
        _firedEpoch.Remove(boxId);
    }

    public void Clear()
    {
        _nextPeriodicAt.Clear();
        _firedEpoch.Clear();
    }
}
