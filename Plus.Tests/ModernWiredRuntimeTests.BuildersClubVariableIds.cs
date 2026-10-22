using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// Builders Club furni in Wired values. Observed on AIR WIN63-202610201032-107964540: @id gives the id the client sees for a Builders Club
/// furni, and a pointer whose accessor holds that id reads the variable of that box.
/// </summary>
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void TheIdOfABuildersClubFurniIsTheIdTheClientSees()
    {
        using var p = new PointerStack();
        var block = p.S.Blocks[0];
        var wire = BuildersClubItemIds.Assign(block.Id);

        try {
            p.Output("i", AirFurni, "-100", furni: 100, picks: [block.Id]);

            Assert.Equal($"i={wire}", p.Shown("i=$(i)"));
        }
        finally {
            BuildersClubItemIds.Release(block.Id);
        }
    }

    [Fact]
    public void APointerReadsTheVariableBoxNamedByItsBuildersClubId()
    {
        using var p = new PointerStack();
        var wire = BuildersClubItemIds.Assign(UserA);

        try {
            p.SetUser(UserA, p.S.F.User, 7);
            p.Point(wire);
            p.Output("p", AirUser, "user:650");

            Assert.Equal("p=7", p.Shown("p=$(p)"));
        }
        finally {
            BuildersClubItemIds.Release(UserA);
        }
    }
}
