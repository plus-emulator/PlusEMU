using System.Numerics;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>Active Volt operator codes; values follow observed signed 64-bit Habbo arithmetic.</summary>
public static class WiredVariableArithmetic
{
    public static bool IsSupported(int operation) => operation is >= 0 and <= 6 or 40 or 41 or 50 or 60
        or >= 100 and <= 105 or >= 110 and <= 122;
    public static bool IsUnary(int operation) => operation is 60 or 103 or 110;
    public static long Apply(int operation, long current, long operand, Random? random = null)
    {
        if (!IsSupported(operation)) {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        return operation switch
        {
            0 => operand,
            1 => unchecked(current + operand),
            2 => unchecked(current - operand),
            3 => unchecked(current * operand),
            // Observed on AIR WIN63-202610151745-318700621: a division by 0 saturates by the sign of the value (7/0 reads back 2147483647,
            // -7/0 -2147483648, 0/0 0) and a modulo by 0 gives 0.
            4 => operand == 0 ? current switch { > 0 => long.MaxValue, < 0 => long.MinValue, _ => 0 }
                : current == long.MinValue && operand == -1 ? long.MinValue : current / operand,
            5 => Power(current, operand),
            6 => operand == 0 || current == long.MinValue && operand == -1 ? 0 : current % operand,
            // Observed r92g r92g-minmax (AIR WIN63-202610131318-860371703, value 5): "set minimum" 40 is a floor (8 -> 8, 3 -> 5),
            // "set maximum" 41 a cap (8 -> 5, 3 -> 3).
            40 => Math.Max(current, operand),
            41 => Math.Min(current, operand),
            50 => RandomValue(operand, random ?? Random.Shared),
            60 => current < 0 ? unchecked(-current) : current,
            100 => current & operand,
            101 => current | operand,
            102 => current ^ operand,
            103 => ~current,
            104 => current << (int)(operand & 63),
            105 => current >> (int)(operand & 63),
            110 => BitOperations.PopCount((ulong)current),
            115 => operand is >= 0 and < 64 ? (long)((ulong)current >> (int)operand & 1) : 0,
            116 => operand is >= 0 and < 64 ? current | (1L << (int)operand) : current,
            117 => operand is >= 0 and < 64 ? current & ~(1L << (int)operand) : current,
            118 => operand is >= 0 and < 64 ? current ^ (1L << (int)operand) : current,
            _ => Scan(operation, current, operand)
        };
    }
    private static long Power(long current, long operand)
    {
        if (operand < 0) {
            return current switch { 1 => 1, -1 => (operand & 1) == 0 ? 1 : -1, _ => 0 };
        }

        var result = 1L;

        while (operand != 0) {
            if ((operand & 1) != 0) {
                result = unchecked(result * current);
            }

            operand >>= 1;
            current = unchecked(current * current);
        }

        return result;
    }
    private static long RandomValue(long operand, Random random)
    {
        if (operand <= 0) {
            return 0;
        }

        // Observed on AIR WIN63-202610151745-318700621: the bound is exclusive (bound 1 always gives 0, bound 5 gives 0 to 4).
        return random.NextInt64(operand);
    }
    private static int Scan(int operation, long current, long operand)
    {
        var forward = operation is 111 or 112 or 119 or 120;
        var high = operation is 112 or 114 or 120 or 122;
        var start = unchecked(operand + (operation >= 119 ? (forward ? 1 : -1) : 0));

        for (var bit = start; bit is >= 0 and < 64; bit += forward ? 1 : -1) {
            if ((((ulong)current >> (int)bit & 1) != 0) == high) {
                return (int)bit;
            }
        }

        return -1;
    }
}
