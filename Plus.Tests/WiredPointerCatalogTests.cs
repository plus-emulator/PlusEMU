using System.Collections.Immutable;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// How a pointer is listed in the variable catalogue. Observed on AIR WIN63-202610201032-107964540: it is a normal variable (type 0)
/// that is always listed with availability 999, can be created, deleted and written, cannot be intercepted, carries an empty text
/// connector and is marked as needing an execution context, which keeps it out of the pickers that run outside a stack.
/// </summary>
public sealed class WiredPointerCatalogTests
{
    private static WiredVariableDefinition Pointer(int target)
    {
        Assert.True(WiredVariableDefinitions.TryDecode("wf_var_pointer", 50, 1, 5,
            new() { IntParams = [target], VariableIds = ["ctx:40"], Text = "ptrv" }, out var definition, out var error), error);

        return definition!;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    [InlineData(-10)]
    public void APointerIsListedAsAWritableVariableThatNeedsAnExecutionContext(int pointerTarget)
    {
        var definition = Pointer(pointerTarget);
        var description = new WiredVariableDescription(definition, true, false);
        var native = WiredNativeCatalogProjection.Project(new(description, definition, null, false, false), null);

        Assert.True(description.RequiresExecutionContext);
        // The catalogue target is the target the pointer was saved with.
        Assert.Equal((0, 999, pointerTarget), (native.Type, native.Availability, native.Target));
        Assert.True(native.RequiresExecutionContext);
        Assert.True(native.CanCreateAndDelete);
        Assert.True(native.HasValue);
        Assert.True(native.CanWriteValue);
        Assert.False(native.CanInterceptChanges);
        Assert.Equal(ImmutableArray<KeyValuePair<int, string>>.Empty, native.Connector);
    }

    [Fact]
    public void AnOrdinaryVariableDoesNotNeedAnExecutionContext()
    {
        var definition = new WiredVariableDefinition(10, 1, 5, "score", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true);
        var description = new WiredVariableDescription(definition, true, false);
        var native = WiredNativeCatalogProjection.Project(new(description, definition, null, true, false), null);

        Assert.False(description.RequiresExecutionContext);
        Assert.False(native.RequiresExecutionContext);
        Assert.Null(native.Connector);
    }
}
