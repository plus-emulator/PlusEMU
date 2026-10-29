using System.Reflection;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>
/// Delayed Give, Change and Remove Variable with pointers, as built in the client: every box is saved from its raw packet
/// through the real save event, a real speech trigger fires the stack and the room cycle runs its delayed boxes pulse by pulse. The
/// Show Message text is what the client receives.
/// </summary>
public partial class PlacedFurniRoomTests
{
    private const uint DpUa = 401, DpUb = 402, DpUt = 403, DpFa = 404, DpGa = 405, DpCt = 406, DpCtx = 407;
    private const uint DpPu = 408, DpPf = 409, DpPg = 410, DpPc = 411, DpHolder = 420;

    // Variable targets: 0 furni, 1 user, -10 global, -20 context.
    private const int DpFurni = 0, DpUser = 1, DpGlobal = -10, DpContext = -20;

    private uint _dpNext = 301;
    private double _dpZ;
    private (int X, int Y) _dpTile = (1, 1);
    private WiredConfigurationService? _dpService;

    // A furni pointer as the operand, read on the switch H. With a delay of 1 or 2 it reads like pf_a; without a delay it is ignored. The
    // pointer shown through a Text Output Variable on H reads 21 in every case.
    [Theory]
    [InlineData("f1a", 1, true, "f1a t=121 b=2 f=21 p=21")]
    [InlineData("f1b", 1, false, "f1b t=121 b=2 f=21 p=21")]
    [InlineData("f2a", 2, true, "f2a t=121 b=2 f=21 p=21")]
    [InlineData("f2b", 2, false, "f2b t=121 b=2 f=21 p=21")]
    [InlineData("f0a", 0, true, "f0a t=100 b=2 f=21 p=21")]
    [InlineData("f0b", 0, false, "f0b t=121 b=2 f=21 p=21")]
    public async Task ADelayedChangeReadsAFurniPointerOperandLikeThePointedVariable(string tag, int delay, bool pointer, string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        await FurniOperandReset();
        await DpEffect("wf_act_change_var_val", [DpUser, 1, 1, 0, 0, DpFurni, 0], ["user:" + DpUt, "furni:" + (pointer ? DpPf : DpFa)],
            furni: [0, 101], users: [0, 0], secondary: [DpHolder], delay: delay);
        await FurniOperandOutputs(tag);

        Assert.Equal([expected], DpFire(wired, actor, clock, 5));
    }

    [Theory]
    [InlineData("f1c", 1, true, "f1c t=100 b=21 f=21 p=21")]
    [InlineData("f1d", 1, false, "f1d t=100 b=21 f=21 p=21")]
    [InlineData("f2c", 2, true, "f2c t=100 b=21 f=21 p=21")]
    [InlineData("f2d", 2, false, "f2d t=100 b=21 f=21 p=21")]
    [InlineData("f0c", 0, true, "f0c t=100 b=2 f=21 p=21")]
    [InlineData("f0d", 0, false, "f0d t=100 b=21 f=21 p=21")]
    public async Task ADelayedGiveReadsAFurniPointerOperandLikeThePointedVariable(string tag, int delay, bool pointer, string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        await FurniOperandReset();
        await DpEffect("wf_act_give_var", [DpUser, 0, 0, 1, 1, DpFurni, 0], ["user:" + DpUb, "furni:" + (pointer ? DpPf : DpFa)],
            furni: [0, 101], users: [0, 0], secondary: [DpHolder], delay: delay);
        await FurniOperandOutputs(tag);

        Assert.Equal([expected], DpFire(wired, actor, clock, 5));
    }

    // Give and Remove Variable with a furni, global or context pointer as the destination. With a delay of 1 they act on the pointed variable
    // like the direct control; without a delay they change nothing. A global keeps its value either way.
    [Theory]
    [InlineData(DpFurni, "a1", "give", 1, true, "a1 f=7 g=33 c=44")]
    [InlineData(DpFurni, "a2", "give", 1, false, "a2 f=7 g=33 c=44")]
    [InlineData(DpFurni, "a3", "remove", 1, true, "a3 f=null g=33 c=44")]
    [InlineData(DpFurni, "a4", "remove", 1, false, "a4 f=null g=33 c=44")]
    [InlineData(DpFurni, "a5", "give", 0, true, "a5 f=21 g=33 c=44")]
    [InlineData(DpFurni, "a6", "remove", 0, true, "a6 f=21 g=33 c=44")]
    [InlineData(DpGlobal, "a1", "give", 1, true, "a1 f=21 g=33 c=44")]
    [InlineData(DpGlobal, "a2", "give", 1, false, "a2 f=21 g=33 c=44")]
    [InlineData(DpGlobal, "a3", "remove", 1, true, "a3 f=21 g=33 c=44")]
    [InlineData(DpGlobal, "a4", "remove", 1, false, "a4 f=21 g=33 c=44")]
    [InlineData(DpGlobal, "a5", "give", 0, true, "a5 f=21 g=33 c=44")]
    [InlineData(DpGlobal, "a6", "remove", 0, true, "a6 f=21 g=33 c=44")]
    [InlineData(DpContext, "a1", "give", 1, true, "a1 f=21 g=33 c=7")]
    [InlineData(DpContext, "a2", "give", 1, false, "a2 f=21 g=33 c=7")]
    [InlineData(DpContext, "a3", "remove", 1, true, "a3 f=21 g=33 c=null")]
    [InlineData(DpContext, "a4", "remove", 1, false, "a4 f=21 g=33 c=null")]
    [InlineData(DpContext, "a5", "give", 0, true, "a5 f=21 g=33 c=44")]
    [InlineData(DpContext, "a6", "remove", 0, true, "a6 f=21 g=33 c=44")]
    public async Task ADelayedGiveOrRemoveThroughAPointerActsOnThePointedVariable(int target, string tag, string action, int delay, bool pointer,
        string expected)
    {
        var (direct, through, prefix) = target switch
        {
            DpFurni => (DpFa, DpPf, "furni:"),
            DpGlobal => (DpGa, DpPg, "room:"),
            _ => (DpCt, DpPc, "ctx:")
        };
        uint[]? picks = target == DpFurni ? [DpHolder] : null;
        var (wired, actor, clock) = await DelayedPointerRoom();
        await DpGive(DpFurni, "furni:" + DpFa, 21, picks: [DpHolder]);
        await DpEffect("wf_act_change_var_val", [DpGlobal, 0, 0, 0, 33, -10, 0], ["room:" + DpGa, "n"], furni: [0, 0], users: [0, 0]);
        await DpGive(DpContext, "ctx:" + DpCt, 44);
        await DpGive(DpContext, "ctx:" + DpCtx, direct);
        var variable = prefix + (pointer ? through : direct);

        if (action == "give") {
            await DpGive(target, variable, 7, picks, delay);
        }
        else {
            await DpEffect("wf_act_remove_var", [target], [variable], furni: [picks is null ? 0 : 100], users: [0], picks: picks, delay: delay);
        }

        await DpShow($"{tag} f=$(f) g=$(g) c=$(c)", 5);
        await DpOutput("f", DpFurni, "furni:" + DpFa, picks: [DpHolder]);
        await DpOutput("g", DpGlobal, "room:" + DpGa);
        await DpOutput("c", DpContext, "ctx:" + DpCt);

        Assert.Equal([expected], DpFire(wired, actor, clock, 5));
    }

    // A precise delay (saved as minus the milliseconds) acts on the pointed variable like a delay of 1.
    [Theory]
    [InlineData(DpFurni, "p1", "give", -50, "p1 f=7 g=33 c=44")]
    [InlineData(DpFurni, "p2", "remove", -50, "p2 f=null g=33 c=44")]
    [InlineData(DpContext, "p3", "give", -50, "p3 f=21 g=33 c=7")]
    [InlineData(DpContext, "p4", "remove", -500, "p4 f=21 g=33 c=null")]
    public async Task APreciseDelayedGiveOrRemoveThroughAPointerActsOnThePointedVariable(int target, string tag, string action, int delay,
        string expected) => await ADelayedGiveOrRemoveThroughAPointerActsOnThePointedVariable(target, tag, action, delay, true, expected);

    // One Give (no override, value 0) and one Change (+1) through a user pointer, both with a precise delay: each firing creates the pointed
    // variable on the triggering user when it is missing and adds 1.
    [Theory]
    [InlineData(-50, "u1 a=1")]
    [InlineData(-500, "u1 a=1")]
    [InlineData(1, "u1 a=1")]
    [InlineData(0, "u1 a=null")]
    public async Task APreciseDelayedGiveAndChangeThroughAUserPointerCountOnTheTriggeringUser(int delay, string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        await DpGive(DpContext, "ctx:" + DpCtx, DpUa);
        await DpEffect("wf_act_give_var", [DpUser, 0, 0, 0, 0, -10, 0], ["user:" + DpPu, "n"], furni: [0, 200], users: [0, 200], delay: delay);
        await DpEffect("wf_act_change_var_val", [DpUser, 1, 0, 0, 1, -10, 0], ["user:" + DpPu, "n"], furni: [0, 200], users: [0, 200], delay: delay);
        await DpShow("u1 a=$(a)", 5);
        await DpOutput("a", DpUser, "user:" + DpUa);

        Assert.Equal([expected], DpFire(wired, actor, clock, 5));
    }

    // A user pointer operand after a delay of 1, 2 or 5 reads like pu_a.
    [Theory]
    [InlineData("a1", 1, "pointer", "a1 t=107 b=2 a=7")]
    [InlineData("a2", 2, "pointer", "a2 t=107 b=2 a=7")]
    [InlineData("a3", 5, "pointer", "a3 t=107 b=2 a=7")]
    [InlineData("a4", 5, "direct", "a4 t=107 b=2 a=7")]
    [InlineData("a5", 5, "give", "a5 t=100 b=7 a=7")]
    public async Task ADelayedBoxReadsAUserPointerOperandAfterAnyDelay(string tag, int delay, string shape, string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        await UserOperandReset();

        if (shape == "give") {
            await DpGiveFrom(DpPu, delay);
        }
        else {
            await DpChangeFrom(shape == "pointer" ? DpPu : DpUa, delay);
        }

        await UserOperandOutputs(tag);

        Assert.Equal([expected], DpFire(wired, actor, clock, 9));
    }

    // The accessor is set to the pu_b box after the delayed Change was reached, at once or with a delay that ends before the Change runs. The
    // pointer is read when the box runs, with the accessor as it is then. Control: a plain pu_a operand set to 50 first.
    [Theory]
    [InlineData("a6", 5, 0, "a6 t=102 b=2 a=7")]
    [InlineData("a7", 5, 1, "a7 t=102 b=2 a=7")]
    [InlineData("a8", 5, 3, "a8 t=102 b=2 a=7")]
    [InlineData("a10", 1, 0, "a10 t=102 b=2 a=7")]
    [InlineData("a9", 5, 3, "a9 t=150 b=2 a=50")]
    public async Task ADelayedBoxReadsThePointerWithTheAccessorAsItIsWhenTheBoxRuns(string tag, int delay, int moved, string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        await UserOperandReset();

        if (tag == "a9") {
            await DpChangeFrom(DpUa, delay);
            await DpGive(DpUser, "user:" + DpUa, 50, delay: moved);
        }
        else {
            await DpChangeFrom(DpPu, delay);
            await DpGive(DpContext, "ctx:" + DpCtx, DpUb, delay: moved);
        }

        await UserOperandOutputs(tag);

        Assert.Equal([expected], DpFire(wired, actor, clock, 9));
    }

    // The pointed pu_a or the accessor pctx is removed at delay 1, before the delay-5 box runs: nothing is added or given.
    [Theory]
    [InlineData("a11", "change", "pu_a", "a11 t=100 b=2 a=null")]
    [InlineData("a12", "give", "pu_a", "a12 t=100 b=2 a=null")]
    [InlineData("a13", "change", "pctx", "a13 t=100 b=2 a=7")]
    [InlineData("a14", "direct", "pu_a", "a14 t=100 b=2 a=null")]
    public async Task ADelayedBoxReadsNothingWhenThePointedOrAccessorVariableWasRemoved(string tag, string shape, string removed, string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        await UserOperandReset();

        if (shape == "give") {
            await DpGiveFrom(DpPu, 5);
        }
        else {
            await DpChangeFrom(shape == "direct" ? DpUa : DpPu, 5);
        }

        if (removed == "pu_a") {
            await DpEffect("wf_act_remove_var", [DpUser], ["user:" + DpUa], furni: [100], users: [0], delay: 1);
        }
        else {
            await DpEffect("wf_act_remove_var", [DpContext], ["ctx:" + DpCtx], furni: [100], users: [0], delay: 1);
        }

        await UserOperandOutputs(tag);

        Assert.Equal([expected], DpFire(wired, actor, clock, 9));
    }

    // A box is picked up 700 ms after the click, while a delay-5 Change pu_t += pp_u and Give pu_b = pp_u wait. Picking up the pointer box,
    // the pointed pu_a box or the accessor box leaves t and b unchanged, as does picking up pu_a with a plain pu_a operand. Once the pu_a
    // box is gone, its placeholder stays as written.
    [Theory]
    [InlineData("x1", DpPu, true, "x1 t=100 b=2 a=7")]
    [InlineData("x2", DpUa, true, "x2 t=100 b=2 a=$(a)")]
    [InlineData("x3", DpUa, false, "x3 t=100 b=2 a=$(a)")]
    [InlineData("x4", DpCtx, true, "x4 t=100 b=2 a=7")]
    [InlineData("x5", 0u, true, "x5 t=107 b=7 a=7")]
    public async Task ABoxPickedUpWhileTheDelayedBoxWaitsLeavesTheValuesUnchanged(string tag, uint pickedUp, bool pointer, string expected)
    {
        var (wired, actor, clock) = await DelayedPointerRoom();
        await UserOperandReset();
        await DpChangeFrom(pointer ? DpPu : DpUa, 5);
        await DpGiveFrom(pointer ? DpPu : DpUa, 5);
        await UserOperandOutputs(tag);

        var messages = DpFire(wired, actor, clock, 9, elapsed =>
        {
            if (elapsed == 700 && pickedUp != 0) {
                PickupObject().Parse(_client, ClientPacket(0, (int)pickedUp)).GetAwaiter().GetResult();
                Assert.Null(_room.GetRoomItemHandler().GetItem(pickedUp));
            }
        });

        Assert.Equal([expected], messages);
    }

    private async Task FurniOperandReset()
    {
        await DpGive(DpUser, "user:" + DpUt, 100);
        await DpGive(DpUser, "user:" + DpUb, 2);
        await DpGive(DpFurni, "furni:" + DpFa, 21, picks: [DpHolder]);
        await DpGive(DpContext, "ctx:" + DpCtx, DpFa);
    }

    private async Task FurniOperandOutputs(string tag)
    {
        await DpShow($"{tag} t=$(t) b=$(b) f=$(f) p=$(p)", 5);
        await DpOutput("t", DpUser, "user:" + DpUt);
        await DpOutput("b", DpUser, "user:" + DpUb);
        await DpOutput("f", DpFurni, "furni:" + DpFa, picks: [DpHolder]);
        await DpOutput("p", DpFurni, "furni:" + DpPf, picks: [DpHolder]);
    }

    private async Task UserOperandReset()
    {
        await DpGive(DpUser, "user:" + DpUa, 7);
        await DpGive(DpUser, "user:" + DpUt, 100);
        await DpGive(DpUser, "user:" + DpUb, 2);
        await DpGive(DpContext, "ctx:" + DpCtx, DpUa);
    }

    private async Task UserOperandOutputs(string tag)
    {
        await DpShow($"{tag} t=$(t) b=$(b) a=$(a)", 9);
        await DpOutput("t", DpUser, "user:" + DpUt);
        await DpOutput("b", DpUser, "user:" + DpUb);
        await DpOutput("a", DpUser, "user:" + DpUa);
    }

    /// <summary>Change Variable: pu_t += the user variable, read on the triggering user.</summary>
    private Task DpChangeFrom(uint operand, int delay) => DpEffect("wf_act_change_var_val", [DpUser, 1, 1, 0, 0, DpUser, 0],
        ["user:" + DpUt, "user:" + operand], furni: [0, 0], users: [0, 0], delay: delay);

    /// <summary>Give Variable: pu_b = the user variable, read on the triggering user.</summary>
    private Task DpGiveFrom(uint source, int delay) => DpEffect("wf_act_give_var", [DpUser, 0, 0, 1, 1, DpUser, 0],
        ["user:" + DpUb, "user:" + source], furni: [0, 0], users: [0, 0], delay: delay);

    /// <summary>
    /// The room with the variable boxes of these tests placed, the switch H and the trigger: a speech trigger with Execute in Order on
    /// one tile, where every later box stands above the last one.
    /// </summary>
    private async Task<(WiredComponent Wired, RoomUser Actor, SpeechClock Clock)> DelayedPointerRoom(bool executeInOrder = true,
        IItemDataManager? definitions = null)
    {
        var clock = new SpeechClock();
        var (wired, actor) = PrepareSpeech(clock, definitions);
        var module = new WiredVariableModule(RoomId, new DelayedPointerDirectory(_room), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, module);
        _dpService = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());

        foreach (var (id, name, x) in new[] { (DpUa, "wf_var_user", 0), (DpUb, "wf_var_user", 0), (DpUt, "wf_var_user", 0),
            (DpFa, "wf_var_furni", 1), (DpGa, "wf_var_room", 1), (DpCt, "wf_var_context", 2), (DpCtx, "wf_var_context", 2),
            (DpPu, "wf_var_pointer", 3), (DpPf, "wf_var_pointer", 3), (DpPg, "wf_var_pointer", 3), (DpPc, "wf_var_pointer", 3) }) {
            PlaceDefinition(id, name, x);
        }

        var holder = Furni(DpHolder, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, holder, 0, 0, 0, true, false, false));
        AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, _dpZ++);

        if (executeInOrder) {
            PlaceSavedBox(_dpNext++, "wf_xtra_exec_in_order", 1, 1, _dpZ++);
        }

        return (wired, actor, clock);
    }

    /// <summary>Give Variable with a constant and "override existing"; furni destinations are the picked furni.</summary>
    private Task DpGive(int target, string variable, long value, uint[]? picks = null, int delay = 0) => DpEffect("wf_act_give_var",
        [target, 0, (int)value, 1, 0, -10, 0], [variable, "n"], furni: [picks is null ? 0 : 100], users: [0], picks: picks, delay: delay);

    private Task DpShow(string text, int delay) => DpEffect("wf_act_show_message", [0, 202, -1], [], text: text, furni: [], users: [0], delay: delay);

    /// <summary>Text Output Variable in number mode: the triggering user, or the picked furni.</summary>
    private Task DpOutput(string placeholder, int target, string variable, uint[]? picks = null) => DpSave(WiredBoxCategory.Addon,
        "wf_xtra_text_output_variable", [0, target, 0], $"{placeholder}\t,", picks ?? [], null, [picks is null ? 0 : 100], [0], [variable], []);

    private Task DpEffect(string name, int[] ints, string[] variables, string text = "", int[]? furni = null, int[]? users = null,
        uint[]? picks = null, uint[]? secondary = null, int delay = 0) =>
        DpSave(WiredBoxCategory.Action, name, ints, text, picks ?? [], delay, furni ?? [0], users ?? [0], variables, secondary ?? []);

    /// <summary>Places the next box of the stack and saves it from the raw editor packet through the real save event.</summary>
    private async Task DpSave(WiredBoxCategory category, string name, int[] ints, string text, uint[] picks, int? delay, int[] furni,
        int[] users, string[] variables, uint[] secondary)
    {
        var id = _dpNext++;
        PlaceSavedBox(id, name, _dpTile.X, _dpTile.Y, _dpZ++);
        List<object> body = [(int)id, ints.Length, .. ints.Cast<object>(), text, picks.Length, .. picks.Select(pick => (object)(int)pick)];

        if (delay is { } pulses) {
            body.Add(pulses);
        }

        body.AddRange([furni.Length, .. furni.Cast<object>(), users.Length, .. users.Cast<object>(), variables.Length, .. variables.Cast<object>(),
            secondary.Length, .. secondary.Select(pick => (object)(int)pick)]);
        _client.Packets.Clear();
        IPacketEvent handler = category switch
        {
            WiredBoxCategory.Addon => new SaveWiredAddonConfigEvent(_dpService!),
            WiredBoxCategory.Condition => new SaveWiredConditionConfigEvent(_dpService!),
            _ => new SaveWiredEffectConfigEvent(_dpService!)
        };
        await handler.Parse(_client, ClientPacket([.. body]));
        Assert.True(_client.Packets.Any(packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer), $"{name} was not saved");
    }

    /// <summary>
    /// The user says the trigger word, then the room cycle runs in 50 ms steps until the Show Message after the given pulses (500 ms
    /// each) has run. The hook runs at each step with the time since the click.
    /// </summary>
    private string[] DpFire(WiredComponent wired, RoomUser actor, SpeechClock clock, int pulses, Action<int>? at = null)
    {
        wired.FlushFlashes();
        _client.Packets.Clear();
        actor.OnChat(0, "go", false);

        for (var elapsed = 50; elapsed <= (pulses + 1) * 500; elapsed += 50) {
            clock.Now = clock.Now.AddMilliseconds(50);
            at?.Invoke(elapsed);
            wired.OnFastCycle();
        }

        return SpeechMessages(ServerPacketHeader.WhisperComposer);
    }

    /// <summary>The variables of these tests. Like the database directory, a variable whose box left the room is not found.</summary>
    private sealed class DelayedPointerDirectory(Room room) : IWiredVariableDirectory
    {
        private readonly Dictionary<uint, WiredVariableDefinition> _definitions = new()
        {
            [DpUa] = Plain(DpUa, "pu_a", WiredVariableTarget.User),
            [DpUb] = Plain(DpUb, "pu_b", WiredVariableTarget.User),
            [DpUt] = Plain(DpUt, "pu_t", WiredVariableTarget.User),
            [DpFa] = Plain(DpFa, "pf_a", WiredVariableTarget.Furni),
            [DpGa] = Plain(DpGa, "pg_a", WiredVariableTarget.Global),
            [DpCt] = Plain(DpCt, "pc_t", WiredVariableTarget.Context),
            [DpCtx] = Plain(DpCtx, "pctx", WiredVariableTarget.Context),
            [DpPu] = Pointer(DpPu, "pp_u", DpUser),
            [DpPf] = Pointer(DpPf, "pp_f", DpFurni),
            [DpPg] = Pointer(DpPg, "pp_g", DpGlobal),
            [DpPc] = Pointer(DpPc, "pp_c", DpContext)
        };

        private static WiredVariableDefinition Plain(uint id, string name, WiredVariableTarget target) =>
            new(id, RoomId, 7, name, target, WiredVariableAvailability.RoomActive, true);

        private static WiredVariableDefinition Pointer(uint id, string name, int target)
        {
            Assert.True(WiredVariableDefinitions.TryDecode("wf_var_pointer", id, RoomId, 7,
                new() { IntParams = [target], VariableIds = [$"ctx:{DpCtx}"], Text = name }, out var definition, out var error), error);

            return definition!;
        }

        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => room.GetRoomItemHandler().GetItem(id) is null ? null : _definitions.GetValueOrDefault(id);
    }
}
