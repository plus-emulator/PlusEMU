namespace Plus.HabboHotel.Items.Wired;

/// <summary>
/// The room's decaying wired execution cost, as the monitor's usage row shows it. Every 500 ms step multiplies the cost by
/// 0.6, and every engine pass that ran boxes adds to it. Written at the end of each outermost pass and read under the engine lock.
/// </summary>
internal sealed class WiredExecutionCost
{
    // After a busy queue stops the cost falls about x0.6 every 500 ms, and a lone 500 ms repeater holds it near 1.0.
    public const int StepMs = 500;
    public const double Decay = 0.6;

    // Approximate: one pass of one execution adds 0.4, so one such pass per step settles at 0.4 / (1 - 0.6) = 1.0, the idle floor.
    public const double PassWeight = 0.05;
    public const double ExecutionWeight = 0.35;

    private long _stepStart = long.MinValue;
    private double _value;

    public void Record(long now, int executions)
    {
        Roll(now);

        if (executions > 0) {
            _value += PassWeight + executions * ExecutionWeight;
        }
    }

    public double Read(long now)
    {
        Roll(now);

        return _value;
    }

    private void Roll(long now)
    {
        if (_stepStart == long.MinValue) {
            _stepStart = now;

            return;
        }

        var steps = (now - _stepStart) / StepMs;

        if (steps <= 0) {
            return;
        }

        // 0.6^64 is far below anything the monitor can show.
        _value = steps >= 64 ? 0 : _value * Math.Pow(Decay, steps);
        _stepStart += steps * StepMs;
    }
}
