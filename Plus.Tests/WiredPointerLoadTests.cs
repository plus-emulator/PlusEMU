using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// A pointer saved before the name rules existed still loads. The name rules are checked on save only, so a room never loses a stored
/// pointer when it opens.
/// </summary>
public sealed class WiredPointerLoadTests
{
    [Theory]
    [InlineData("")]
    [InlineData("ptr 1")]
    [InlineData("a-pointer")]
    public void AStoredPointerWithAnOldNameStillLoads(string name)
    {
        var stored = new WiredConfiguration { IntParams = [1], VariableIds = ["ctx:40"], Text = name };

        Assert.True(WiredVariableDefinitions.TryDecode("wf_var_pointer", 77, 2, 5, stored, out var definition, out var error), error);
        Assert.Equal(name, definition!.Name);
    }

    [Theory]
    [InlineData("", "wiredfurni.error.variables.name_length")]
    [InlineData("ptr 1", "wiredfurni.error.variables.name_syntax")]
    public void ASaveWithABadNameIsStillRefused(string name, string key)
    {
        var module = new WiredVariableModule(2, new SharedAddonDirectory(), new MemoryWiredVariableStore(), new FixedTimeProvider(DateTimeOffset.FromUnixTimeMilliseconds(1)));
        var proposed = new WiredVariableDefinition(77, 2, 5, name, WiredVariableTarget.User, WiredVariableAvailability.UserActive, true, PointerAccessor: "ctx:40");

        Assert.Equal(key, module.RefusePointer(proposed));
    }
}
