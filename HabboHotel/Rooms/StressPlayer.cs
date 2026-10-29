namespace Plus.HabboHotel.Rooms;

/// <summary>What the room shows for a :stress player. A heavy player rides a horse on a roller and does not roam.</summary>
internal sealed record StressPlayer(string Name, string Look, string Gender, bool Heavy);
