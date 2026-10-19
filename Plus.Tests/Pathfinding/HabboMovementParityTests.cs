using System.Globalization;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboStraightObstructionAcceptsAnEqualLengthDetour(bool layered)
    {
        var actor = HabboParityActor(7, 7, layered);
        var path = HabboParityWalk(actor, 7, 12, next =>
        {
            if (next == (7, 9)) {
                HabboParityMove(100, 7, 10);
            }
        });

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9), (6, 10), (7, 11), (7, 12));
        Assert.Contains((6, 10, 0.01), path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboStraightObstructionRejectsADetourOneStepLonger(bool layered)
    {
        HabboParityRoom();
        HabboParityMove(101, 7, 10);
        var actor = LayeredActor(6, 7, new() { LayeringEnabled = layered });
        var path = HabboParityWalk(actor, 6, 12, next =>
        {
            if (next == (6, 9)) {
                HabboParityMove(100, 6, 10);
            }
        });

        HabboParityAssertStopped(actor, path, (6, 7), (6, 8), (6, 9));
        Assert.Equal(0.01, actor.Z);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboRemovedInitialObstacleRemainsUnavailableUntilANewClick(bool layered)
    {
        HabboParityRoom();
        HabboParityMove(103, 8, 10);
        HabboParityMove(101, 6, 10);
        var actor = LayeredActor(7, 7, new() { LayeringEnabled = layered });
        var path = HabboParityWalk(actor, 7, 12, next =>
        {
            if (next == (7, 8)) {
                HabboParityMove(101, 9, 2);
            }

            if (next == (7, 9)) {
                HabboParityMove(100, 7, 10);
            }
        });

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9));
        var fresh = HabboParityWalk(actor, 7, 12);
        HabboParityAssertStopped(actor, fresh, (7, 9), (6, 10), (7, 11), (7, 12));
        Assert.Contains((6, 10, 0.01), fresh);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboReplanDoesNotAnticipateADistantNewObstacle(bool layered)
    {
        var actor = HabboParityActor(7, 7, layered);
        var path = HabboParityWalk(actor, 7, 12, next =>
        {
            if (next == (7, 8)) {
                HabboParityMove(102, 6, 11);
            }

            if (next == (7, 9)) {
                HabboParityMove(100, 7, 10);
            }
        });

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9), (6, 10));
        Assert.Equal(0.01, actor.Z);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboInitialChairIsExcludedFromAReplannedTransitRoute(bool layered)
    {
        HabboParityRoom();
        HabboParityMove(103, 6, 10);
        HabboParityMove(101, 8, 10);
        var actor = LayeredActor(7, 7, new() { LayeringEnabled = layered });
        var path = HabboParityWalk(actor, 7, 12, next =>
        {
            if (next == (7, 9)) {
                HabboParityMove(100, 7, 10);
            }
        });

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9));
        Assert.False(actor.HasStatus("sit"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboNewChairOnAnExistingRouteIsCrossedWithoutSitting(bool layered)
    {
        var actor = HabboParityActor(7, 7, layered);
        var path = HabboParityWalk(actor, 7, 12, next =>
        {
            if (next == (7, 9)) {
                HabboParityMove(103, 7, 10);
            }

            Assert.False(actor.HasStatus("sit"));
        });

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9), (7, 10), (7, 11), (7, 12));
        Assert.All(path, step => Assert.Equal(0d, step.Z));
        Assert.False(actor.HasStatus("sit"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboNewChairAtTheGoalSuppliesTheArrivalPosture(bool layered)
    {
        var actor = HabboParityActor(7, 7, layered);
        var path = HabboParityWalk(actor, 7, 12, next =>
        {
            if (next == (7, 10)) {
                HabboParityMove(103, 7, 12);
            }
        });

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9), (7, 10), (7, 11), (7, 12));
        Assert.Equal(0d, actor.Z);
        Assert.StartsWith("1.0", actor.Statusses["sit"]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboNewBedOnAnExistingRouteIsCrossedAtItsBaseWithoutLying(bool layered)
    {
        var actor = HabboParityActor(7, 7, layered);
        var path = HabboParityWalk(actor, 7, 12, next =>
        {
            if (next == (7, 9)) {
                HabboParityMove(104, 6, 10);
            }

            Assert.False(actor.HasStatus("lay"));
        });

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9), (7, 10), (7, 11), (7, 12));
        Assert.Contains((7, 10, 0.01), path);
        Assert.Contains((7, 11, 0.01), path);
        Assert.False(actor.HasStatus("lay"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboDiagonalObstructionAllowsOneMorePhysicalStep(bool layered)
    {
        var actor = HabboParityActor(5, 7, layered);
        var baseline = HabboParityWalk(actor, 8, 10);
        HabboParityAssertStopped(actor, baseline, (5, 7), (6, 8), (7, 9), (8, 10));
        HabboParityWalk(actor, 5, 7);
        Assert.Equal((5, 7), (actor.X, actor.Y));
        var path = HabboParityWalk(actor, 8, 10, next =>
        {
            if (next == (6, 8)) {
                HabboParityMove(100, 7, 9);
            }
        });

        HabboParityAssertStopped(actor, path, (5, 7), (6, 8), (6, 9), (7, 10), (8, 10));
        Assert.Contains((6, 9, 0.01), path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboRepeatedDiagonalObstructionsAllowTwoMorePhysicalSteps(bool layered)
    {
        var actor = HabboParityActor(5, 7, layered);
        var baseline = HabboParityWalk(actor, 8, 10);
        HabboParityAssertStopped(actor, baseline, (5, 7), (6, 8), (7, 9), (8, 10));
        HabboParityWalk(actor, 5, 7);
        Assert.Equal((5, 7), (actor.X, actor.Y));
        var path = HabboParityWalk(actor, 8, 10, next =>
        {
            if (next == (6, 8)) {
                HabboParityMove(100, 7, 9);
            }

            if (next == (6, 9)) {
                HabboParityMove(101, 7, 10);
            }
        });

        HabboParityAssertStopped(actor, path, (5, 7), (6, 8), (6, 9), (6, 10), (7, 11), (8, 10));
        Assert.Contains((6, 10, 0.01), path);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboDiagonalObstructionUsesXWhenTheYComponentWasAlreadyBlocked(bool layered)
    {
        HabboParityRoom();
        HabboParityMove(101, 6, 9);
        var actor = LayeredActor(5, 7, new() { LayeringEnabled = layered });
        var path = HabboParityWalk(actor, 8, 10, next =>
        {
            if (next == (6, 8)) {
                HabboParityMove(100, 7, 9);
            }
        });

        HabboParityAssertStopped(actor, path, (5, 7), (6, 8), (7, 8), (8, 9), (8, 10));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboBlockedDiagonalGoalStillProducesAnOrthogonalStep(bool layered)
    {
        var actor = HabboParityActor(5, 7, layered);
        var path = HabboParityWalk(actor, 7, 9, next =>
        {
            if (next == (6, 8)) {
                HabboParityMove(100, 7, 9);
            }
        });

        HabboParityAssertStopped(actor, path, (5, 7), (6, 8), (6, 9));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HabboBlockedStraightGoalStopsBeforeEnteringIt(bool layered)
    {
        var actor = HabboParityActor(7, 7, layered);
        var path = HabboParityWalk(actor, 7, 12, next =>
        {
            if (next == (7, 11)) {
                HabboParityMove(100, 7, 12);
            }
        });

        HabboParityAssertStopped(actor, path, (7, 7), (7, 8), (7, 9), (7, 10), (7, 11));
    }

    private RoomUser HabboParityActor(int x, int y, bool layered)
    {
        HabboParityRoom();

        return LayeredActor(x, y, new() { LayeringEnabled = layered });
    }

    private void HabboParityRoom()
    {
        FallbackModel(string.Join('\r', new[] {
            "xxxxxxxxxxxx", "xxxxxxxxxxxx", "xxxxx111111x", "xxxxx111111x",
            "xxxx1111111x", "xxxxx111111x", "xxxxx111111x", "xxxxx000000x",
            "xxxxx000000x", "xxx00000000x", "xxx00000000x", "xxx00000000x",
            "xxx00000000x", "xxxxxxxxxxxx", "xxxxxxxxxxxx", "xxxxxxxxxxxx"
        }));
        _room.RoomBlockingEnabled = true;
        HabboParityItem(110, 3, 9, 2, 2, 0.01, walkable: true);
        HabboParityItem(111, 5, 9, 2, 2, 0.01, walkable: true);
        HabboParityItem(112, 3, 11, 2, 2, 0.01, walkable: true);
        HabboParityItem(113, 5, 11, 2, 2, 0.01, walkable: true);
        HabboParityItem(100, 6, 2, 1, 1, 1.05);
        HabboParityItem(101, 9, 2, 1, 1, 1.05);
        HabboParityItem(102, 7, 2, 2, 1, 1.05);
        HabboParityItem(103, 10, 9, 1, 1, 1, seat: true);
        HabboParityItem(104, 3, 10, 2, 3, 1.8, interaction: InteractionType.Bed, rotation: 2);
        HabboParityItem(105, 9, 10, 2, 2, 1);
        HabboParityItem(106, 9, 9, 1, 1, 1, seat: true);
        HabboParityItem(107, 9, 12, 1, 1, 1, seat: true);
        HabboParityItem(108, 10, 12, 1, 1, 1, seat: true);
        HabboParityItem(114, 7, 5, 1, 1, 1, seat: true);
        HabboParityItem(115, 8, 5, 1, 1, 1, seat: true);
        HabboParityItem(116, 9, 5, 1, 1, 1, seat: true);
        HabboParityItem(117, 8, 4, 1, 1, 0.6875, walkable: true);
    }

    private void HabboParityItem(uint id, int x, int y, int width, int length, double height,
        bool walkable = false, bool seat = false, InteractionType interaction = InteractionType.None, int rotation = 0)
    {
        var item = Furni(id, interaction, WiredBoxType.None);
        item.Definition.Width = width;
        item.Definition.Length = length;
        item.Definition.Height = height;
        item.Definition.Walkable = walkable;
        item.Definition.IsSeat = seat;
        var z = y <= 6 ? 1d : x <= 6 && y >= 9 && id < 110 ? 0.01 : 0d;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, x, y, rotation, true, false, false, height: z));
    }

    private void HabboParityMove(uint id, int x, int y)
    {
        var z = y <= 6 ? 1d : x <= 6 && y >= 9 ? 0.01 : 0d;
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(_room.GetRoomItemHandler().GetItem(id), x, y, z));
    }

    private List<(int X, int Y, double Z)> HabboParityWalk(RoomUser actor, int x, int y,
        Action<(int X, int Y)>? onAnnounced = null)
    {
        var path = new List<(int X, int Y, double Z)> { (actor.X, actor.Y, actor.Z) };
        var announced = new HashSet<(int X, int Y)>();
        actor.MoveTo(x, y);

        for (var tick = 0; tick < 24; tick++) {
            ExecutorTick();
            var position = (actor.X, actor.Y, actor.Z);

            if (path[^1] != position) {
                path.Add(position);
            }

            if (!actor.Statusses.TryGetValue("mv", out var movement)) {
                continue;
            }

            var coordinates = movement.Split(',');
            var next = (int.Parse(coordinates[0], CultureInfo.InvariantCulture), int.Parse(coordinates[1], CultureInfo.InvariantCulture));

            if (announced.Add(next)) {
                onAnnounced?.Invoke(next);
            }
        }

        return path;
    }

    private static void HabboParityAssertStopped(RoomUser actor, List<(int X, int Y, double Z)> path,
        params (int X, int Y)[] expected)
    {
        Assert.Equal(expected, path.Select(step => (step.X, step.Y)).ToArray());
        Assert.False(actor.IsWalking);
        Assert.False(actor.HasStatus("mv"));
    }
}
