using Xunit;
using static Plus.Tests.SharedAddonIds;

namespace Plus.Tests;

/// <summary>
/// A reference variable in the catalogue. Observed on AIR WIN63-202610201032-107964540: it is listed as a normal variable (type 0) of
/// availability 20 whose changes can be intercepted, and it can be created, deleted and written.
/// </summary>
public sealed class WiredReferenceCatalogTests
{
    [Fact]
    public void AReferenceIsListedAsANormalInterceptableVariableOfAvailabilityTwenty()
    {
        var scene = new SharedAddonScene();
        var row = scene.B.Row("shref");

        Assert.NotNull(row);
        Assert.Equal(0, row!.Type);
        Assert.Equal(20, row.Availability);
        Assert.True(row.CanInterceptChanges);
        Assert.True(row.CanCreateAndDelete);
        Assert.True(row.HasValue);
        Assert.True(row.CanWriteValue);
    }

    [Fact]
    public void ASharedVariableKeepsItsOwnEntry()
    {
        var scene = new SharedAddonScene();
        var row = scene.A.Row("shv");

        Assert.NotNull(row);
        Assert.Equal(0, row!.Type);
        Assert.Equal(11, row.Availability);
    }
}
