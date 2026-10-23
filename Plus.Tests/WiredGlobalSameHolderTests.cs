using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

// Observed on AIR WIN63-202610201032-107964540 with two room variables (7 and 3): Change Variable Value with same holder on and a global
// destination reads the reference like it does with the option off, a reference of another kind (furni or user) changes nothing, and Give
// Variable on a global destination gives nothing in every shape.
public sealed class WiredGlobalSameHolderTests
{
    private const string Destination = "room:20", Reference = "room:21", FurniVariable = "furni:10";
    private const int Global = 3, Furni = 1, User = 0;

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    public void ChangeVariableValueReadsAGlobalReferenceWithOrWithoutSameHolder(int sameHolder, long expected)
    {
        var (module, executor, frame, holder) = World();

        Assert.True(executor.Execute("wf_act_change_var_val", Config([Global, 0, 1, 0, Global, 0, 0, 0, 0, sameHolder], Destination, Reference), frame));

        Assert.Equal(expected, module.Read(new(WiredVariableTarget.Global, "custom:20"), holder, frame)!.Value);
    }

    [Fact]
    public void ChangeVariableValueAddsAGlobalToItselfWithSameHolder()
    {
        var (module, executor, frame, holder) = World();

        Assert.True(executor.Execute("wf_act_change_var_val", Config([Global, 1, 1, 0, Global, 0, 0, 0, 0, 1], Destination, Destination), frame));

        Assert.Equal(14, module.Read(new(WiredVariableTarget.Global, "custom:20"), holder, frame)!.Value);
    }

    [Theory]
    [InlineData(Furni, FurniVariable)]
    [InlineData(User, "user:11")]
    public void ChangeVariableValueWithSameHolderReadingAnotherKindChangesNothing(int referenceTarget, string reference)
    {
        var (module, executor, frame, holder) = World();

        Assert.False(executor.Execute("wf_act_change_var_val", Config([Global, 0, 1, 0, referenceTarget, 0, 0, 0, 0, 1], Destination, reference), frame));

        Assert.Equal(7, module.Read(new(WiredVariableTarget.Global, "custom:20"), holder, frame)!.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void GiveVariableOnAGlobalGivesNothing(int sameHolder)
    {
        var (module, executor, frame, holder) = World();

        Assert.False(executor.Execute("wf_act_give_var", Config([Global, 0, 0, 0, 0, 1, Global, sameHolder], Destination, Reference), frame));

        Assert.Equal(7, module.Read(new(WiredVariableTarget.Global, "custom:20"), holder, frame)!.Value);
    }

    private static WiredConfiguration Config(int[] ints, params string[] ids) => new() { IntParams = [.. ints], VariableIds = [.. ids] };

    private static (WiredVariableModule, WiredVariableExecutors, WiredVariableFrame, WiredVariableHolder) World()
    {
        var holder = new WiredVariableHolder(WiredVariableTarget.Global, 0, 0);
        var frame = new WiredVariableFrame(1, []) { Trigger = [holder] };
        var module = new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), TimeProvider.System);
        Assert.True(module.SaveGlobalValue(20, 7));
        Assert.True(module.SaveGlobalValue(21, 3));

        return (module, new WiredVariableExecutors(module, TimeProvider.System), frame, holder);
    }

    private sealed class Directory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            20 or 21 => new(id, 1, 5, "global" + id, WiredVariableTarget.Global, WiredVariableAvailability.RoomActive, true),
            10 => new(id, 1, 5, "furni", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true),
            11 => new(id, 1, 5, "user", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true),
            _ => null
        };

        public uint? GetRoomOwner(uint roomId) => 5;
    }
}
