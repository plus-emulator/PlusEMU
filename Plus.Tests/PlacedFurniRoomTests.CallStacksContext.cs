using Xunit;

namespace Plus.Tests;

/// <summary>
/// A stack started by Call Stacks begins with an empty context: it does not see the context variables of the calling stack, so a pointer
/// read there points at nothing. Every box is saved from its raw packet and a real speech trigger fires the stack on the room cycle.
/// </summary>
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(0, "callee p=$(p) a=7")]
    [InlineData(1, "callee p=$(p) a=7")]
    public async Task ACalledStackDoesNotSeeTheContextOfTheCallingStack(int callDelay, string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        var callee = await CalleeStack(async () =>
        {
            await DpShow("callee p=$(p) a=$(a)", 0);
            await DpOutput("p", DpUser, "user:" + DpPu);
            await DpOutput("a", DpUser, "user:" + DpUa);
        });

        await DpGive(DpUser, "user:" + DpUa, 7);
        await DpGive(DpContext, "ctx:" + DpCtx, DpUa);
        await DpEffect("wf_act_call_stacks", [], [], furni: [100], users: [], picks: [callee], delay: callDelay);

        Assert.Equal([expected], DpFire(wired, actor, clock, 3));
    }

    [Fact]
    public async Task ACalledStackReadsAContextVariableOfTheCallerAsEmpty()
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        var callee = await CalleeStack(async () =>
        {
            await DpShow("callee t=$(t)", 0);
            await DpOutput("t", DpContext, "ctx:" + DpCt);
        });

        await DpGive(DpContext, "ctx:" + DpCt, 3);
        await DpEffect("wf_act_call_stacks", [], [], furni: [100], users: [], picks: [callee]);

        Assert.Equal(["callee t=null"], DpFire(wired, actor, clock, 1));
    }

    [Fact]
    public async Task AContextWriteInACalledStackDoesNotReachTheCallingStack()
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        var callee = await CalleeStack(() => DpGive(DpContext, "ctx:" + DpCt, 5));

        await DpGive(DpContext, "ctx:" + DpCt, 3);
        await DpEffect("wf_act_call_stacks", [], [], furni: [100], users: [], picks: [callee]);
        await DpShow("caller t=$(t)", 1);
        await DpOutput("t", DpContext, "ctx:" + DpCt);

        Assert.Equal(["caller t=3"], DpFire(wired, actor, clock, 2));
    }

    [Fact]
    public async Task AWriteThroughAPointerInACalledStackChangesNothing()
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        var callee = await CalleeStack(() => DpEffect("wf_act_change_var_val", [DpUser, 1, 0, 0, 1, -10, 0], ["user:" + DpPu, "n"], furni: [0, 0],
            users: [0, 0], delay: 1));

        await DpGive(DpUser, "user:" + DpUa, 7);
        await DpGive(DpContext, "ctx:" + DpCtx, DpUa);
        await DpEffect("wf_act_call_stacks", [], [], furni: [100], users: [], picks: [callee]);
        await DpShow("caller a=$(a)", 3);
        await DpOutput("a", DpUser, "user:" + DpUa);

        Assert.Equal(["caller a=7"], DpFire(wired, actor, clock, 4));
    }

    /// <summary>
    /// Builds the called stack on tile (2, 2): a speech trigger that never fires, then the given boxes. Returns the id of its first box after
    /// the trigger, which the Call Stacks box picks.
    /// </summary>
    private async Task<uint> CalleeStack(Func<Task> boxes)
    {
        var (tile, z) = (_dpTile, _dpZ);
        _dpTile = (2, 2);
        _dpZ = 0;
        AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "never" }, _dpZ++, 2, 2);
        var first = _dpNext;
        await boxes();
        (_dpTile, _dpZ) = (tile, z);

        return first;
    }
}
