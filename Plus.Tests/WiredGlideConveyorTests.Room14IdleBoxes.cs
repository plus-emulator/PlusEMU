using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint IdleBoxId = 900;

    public static TheoryData<string> RegisteredBoxNames()
    {
        var data = new TheoryData<string>();

        foreach (var name in RegisteredNames()) {
            data.Add(name);
        }

        return data;
    }

    private static IEnumerable<string> RegisteredNames() =>
        WiredBoxRegistry.All.Select(descriptor => descriptor.CanonicalName).Order(StringComparer.Ordinal);

    private static (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json) IdleBox(string name) => (IdleBoxId, 6, 2, 0.0, 0, name, null);

    // Observed on hhus (R95-FQ): a pulse is one frame with every tile home to home and the rider carried, whatever else stands
    // in the room. An idle box of any registered type on a far tile cannot see the carried tile land on the rider.
    [Theory]
    [MemberData(nameof(RegisteredBoxNames))]
    public void Room14IdleBoxElsewhereKeepsTheCarriedPulseInOneFrame(string name)
    {
        var f = RunRoom14Queue(4, 13, 7, [.. Room14FastQueueRows(), IdleBox(name)]);

        var box = Assert.IsAssignableFrom<IWiredConfiguredItem>(f.Box(IdleBoxId));
        Assert.Equal(name, box.Descriptor.CanonicalName);
    }

    // Every box type the server can create is covered by the replay above.
    [Fact]
    public void Room14IdleBoxReplayCoversEveryServerBoxType()
    {
        var server = typeof(WiredStackEngine).Assembly.GetTypes()
            .Where(type => typeof(IWiredItem).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false })
            .ToHashSet();
        var replayed = RegisteredNames()
            .Select(name => new Fixture([.. Room14FastQueueRows(), IdleBox(name)], live: true, colourTileHeight: 0.2).Box(IdleBoxId).GetType())
            .ToHashSet();

        Assert.Empty(server.Except(replayed).Select(type => type.FullName));
    }

    // A collision trigger that shares its tile with an action can observe the tile landing on the rider, so that pulse still
    // splits before the collision stack runs.
    [Fact]
    public void Room14CollisionStackElsewhereStillSplitsTheCarriedPulse()
    {
        var layout = Room14FastQueueRows().Append(IdleBox("wf_trg_collision")).Append((IdleBoxId + 1, 6, 2, 0.65, 0, "wf_act_log", null)).ToArray();
        var f = new Fixture(layout, live: true, colourTileHeight: 0.2);
        var rider = f.Walker(1, 4, 13, 7, 13);
        var packets = CapturePublication(rider);

        f.Advance(250);

        Assert.True(packets.Count(IsMovementBody) > 1);
        Assert.Equal((8, 13), (rider.X, rider.Y));
    }
}
