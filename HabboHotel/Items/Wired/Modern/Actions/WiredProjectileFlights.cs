using System.Drawing;
using System.Runtime.CompilerServices;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>Room-owned last projectile flights sampled at crossed tile centers.</summary>
public sealed class WiredProjectileFlights(Room room)
{
    private static readonly ConditionalWeakTable<Room, WiredProjectileFlights> Rooms = new();
    private readonly Dictionary<uint, Flight> _flights = [];
    public static WiredProjectileFlights For(Room room) => Rooms.GetValue(room, current => new(current));

    // Only these four are interceptable. A change fires on the room tick after the projectile crosses into a tile, never at launch.
    internal static readonly string[] Intercepted =
    [
        "@projectile.animation.tiles_traveled", "@projectile.animation.user_collisions", "@projectile.animation.furni_collisions",
        "@projectile.animation.is_traveling"
    ];

    public bool HasPending => _flights.Values.Any(flight => flight.Watched != null);

    public bool Begin(Item item, int sourceX, int sourceY, double sourceZ, int durationMs, long nowMs, int variableMask = 127,
        int? curveStrength = null)
    {
        if (!item.IsFloorItem || !ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), item)) {
            return false;
        }

        var sourceHundredths = (int)(sourceZ * 100);
        var targetHundredths = (int)(item.GetZ * 100);
        var cells = BuildCells(sourceX, sourceY, item.GetX, item.GetY);
        var map = room.GetGameMap();

        if (cells.Any(cell => !map.ValidTile(cell.X, cell.Y))) {
            return false;
        }

        // Length is in tiles; altitude is in hundredths.
        var length = Math.Sqrt(Math.Pow(item.GetX - sourceX, 2) + Math.Pow(item.GetY - sourceY, 2) + Math.Pow(item.GetZ - sourceZ, 2));
        int Altitude(int index)
        {
            if (index == 0) {
                return sourceHundredths;
            }

            if (index == cells.Count - 1) {
                return targetHundredths;
            }

            var t = (cells[index].Entry + cells[index + 1].Entry) / 2;

            return (int)Math.Round(sourceHundredths + (targetHundredths - sourceHundredths) * t
                + (curveStrength ?? 0) * length * t * (1 - t), MidpointRounding.AwayFromZero);
        }

        var altitudes = Enumerable.Range(0, cells.Count).Select(Altitude).ToArray();

        // Snapshot collisions at launch, excluding furniture below the sampled altitude.
        var flight = new Flight(item, cells, altitudes, sourceHundredths, targetHundredths, nowMs, durationMs, variableMask,
            cells.Select(cell => map.GetRoomUsers(new(cell.X, cell.Y)).DistinctBy(user => user.VirtualId).Count()).ToArray(),
            cells.Select((cell, index) => map.GetCoordinatedItems(new(cell.X, cell.Y)).Where(other => !ReferenceEquals(other, item)
                && altitudes[index] / 100d <= other.TotalHeight).DistinctBy(other => other.Id).Count()).ToArray());
        _flights[item.Id] = flight;
        flight.Watched = Intercepted.Select(key => Read(item, key, nowMs)).ToArray();

        return true;
    }

    public int? Read(Item item, string key, long nowMs)
    {
        if (!_flights.TryGetValue(item.Id, out var flight)) {
            return null;
        }

        if (!ReferenceEquals(room.GetRoomItemHandler().GetItem(item.Id), flight.Item)) {
            _flights.Remove(item.Id);

            return null;
        }

        if (!ReferenceEquals(flight.Item, item)) {
            return null;
        }

        // Projectile SubVariableParam ids: travelled, users, furni, X, Y, altitude, travelling.
        var bit = key switch
        {
            "@projectile.animation.tiles_traveled" => 0,
            "@projectile.animation.user_collisions" => 1,
            "@projectile.animation.furni_collisions" => 2,
            "@projectile.animation.position.x" => 3,
            "@projectile.animation.position.y" => 4,
            "@projectile.animation.position.altitude" => 5,
            "@projectile.animation.is_traveling" => 6,
            _ => -1
        };

        if (bit < 0 || (flight.VariableMask & (1 << bit)) == 0) {
            return null;
        }

        var progress = flight.DurationMs <= 0 ? 1 : Math.Clamp((nowMs - flight.StartedAtMs) / (double)flight.DurationMs, 0, 1);
        var last = flight.Cells.Count - 1;
        var travelled = 0;

        while (travelled < last && flight.Cells[travelled + 1].Entry <= progress) {
            travelled++;
        }

        var cell = flight.Cells[travelled];
        int Collisions(int[] counts) => counts.Skip(1).Take(travelled).Sum(); // Launch tile is not crossed into.

        // A move with no horizontal change has a single cell; it stays travelling for the whole duration and climbs linearly.
        var travelling = last == 0 ? progress < 1 : progress < flight.Cells[last].Entry;
        var altitude = last == 0 ? (int)Math.Round(flight.SourceZ + (flight.TargetZ - flight.SourceZ) * progress) : flight.Altitudes[travelled];

        return key switch
        {
            "@projectile.animation.furni_collisions" => Collisions(flight.FurniCounts),
            "@projectile.animation.user_collisions" => Collisions(flight.UserCounts),
            "@projectile.animation.tiles_traveled" => travelled,
            "@projectile.animation.is_traveling" => travelling ? -1 : null,
            "@projectile.animation.position.x" => cell.X,
            "@projectile.animation.position.y" => cell.Y,
            "@projectile.animation.position.altitude" => altitude,
            _ => null
        };
    }
    /// <summary>Interceptable values that moved since the last poll; a flight stops being watched once it has landed.</summary>
    public IReadOnlyList<(Item Item, string Key, int? Before, int? After)> Poll(long nowMs)
    {
        var changes = new List<(Item, string, int?, int?)>();

        foreach (var flight in _flights.Values.Where(flight => flight.Watched != null).ToArray()) {
            var watched = flight.Watched!;

            if (!ReferenceEquals(room.GetRoomItemHandler().GetItem(flight.Item.Id), flight.Item)) {
                flight.Watched = null;

                continue;
            }

            for (var index = 0; index < Intercepted.Length; index++) {
                var after = Read(flight.Item, Intercepted[index], nowMs);

                if (after != watched[index]) {
                    changes.Add((flight.Item, Intercepted[index], watched[index], after));
                    watched[index] = after;
                }
            }

            if (flight.DurationMs <= 0 || nowMs - flight.StartedAtMs >= flight.DurationMs || !_flights.ContainsKey(flight.Item.Id)) {
                flight.Watched = null;
            }
        }

        return changes;
    }

    public void Forget(Item item)
    {
        if (_flights.TryGetValue(item.Id, out var flight) && ReferenceEquals(flight.Item, item)) {
            _flights.Remove(item.Id);
        }
    }
    public void Clear() => _flights.Clear();
    public static IReadOnlyList<Point> BuildPath(int sourceX, int sourceY, int targetX, int targetY)
    {
        var dx = targetX - sourceX;
        var dy = targetY - sourceY;
        var steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
        var path = new List<Point>(steps + 1);

        for (var step = 0; step <= steps; step++) {
            var progress = steps == 0 ? 0d : step / (double)steps;
            path.Add(new((int)Math.Round(sourceX + dx * progress), (int)Math.Round(sourceY + dy * progress)));
        }

        return path;
    }
    /// <summary>Cells entered along the straight line, each with its entry progress; simultaneous axis crossings merge into one entry.</summary>
    private static List<(int X, int Y, double Entry)> BuildCells(int sourceX, int sourceY, int targetX, int targetY)
    {
        var events = new List<(double Progress, int X, int Y)>();

        void Crossings(int from, int to, bool horizontal)
        {
            var d = to - from;
            var step = Math.Sign(d);

            for (var k = 0; k < Math.Abs(d); k++) {
                events.Add(((k + 0.5) / Math.Abs(d), horizontal ? step : 0, horizontal ? 0 : step));
            }
        }

        Crossings(sourceX, targetX, true);
        Crossings(sourceY, targetY, false);
        var cells = new List<(int X, int Y, double Entry)> { (sourceX, sourceY, 0) };
        var x = sourceX;
        var y = sourceY;

        foreach (var group in events.OrderBy(e => e.Progress).GroupBy(e => Math.Round(e.Progress, 9))) {
            x += group.Sum(e => e.X);
            y += group.Sum(e => e.Y);
            cells.Add((x, y, group.Key));
        }

        return cells;
    }

    private sealed record Flight(Item Item, IReadOnlyList<(int X, int Y, double Entry)> Cells, int[] Altitudes, int SourceZ, int TargetZ,
        long StartedAtMs, int DurationMs, int VariableMask, int[] UserCounts, int[] FurniCounts)
    {
        public int?[]? Watched { get; set; }
    }
}
