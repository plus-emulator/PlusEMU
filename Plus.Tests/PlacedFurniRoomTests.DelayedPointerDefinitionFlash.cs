using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint PointedUser = 1001, PointedTotal = 1002, PointedFurni = 1003, PointedGlobal = 1004, PointedContext = 1005, PointedAccessor = 1006;
    private const uint PointerUser = 1011, PointerFurni = 1012, PointerGlobal = 1013, PointerContext = 1014, PointedHolder = 1020;

    private static readonly uint[] PointerBoxes = [PointerUser, PointerFurni, PointerGlobal, PointerContext];
    private static readonly uint[] PointedBoxes = [PointedUser, PointedTotal, PointedFurni, PointedGlobal, PointedContext, PointedAccessor];

    // Observed on AIR WIN63-202610201032-107964540: a Give, Change or Remove Variable that runs after a delay and writes through a pointer
    // flashes the definition box of the variable the pointer points at, with the same state and in the same batch as the same write on that
    // variable itself. The pointer box never flashes. The stack's own batch only holds the definitions its Give boxes reset.
    [Theory]
    [InlineData("change user pointer", true, PointedUser, new[] { "2", "2", "2" })]
    [InlineData("change user", true, PointedUser, new[] { "2", "2", "2" })]
    [InlineData("give user pointer", true, PointedUser, new[] { "7", "7", "7" })]
    [InlineData("give user", true, PointedUser, new[] { "7", "7", "7" })]
    [InlineData("remove user pointer", true, PointedUser, new[] { "8", "8", "8" })]
    [InlineData("remove user", true, PointedUser, new[] { "8", "8", "8" })]
    [InlineData("change furni pointer", true, PointedFurni, new[] { "2", "2", "2" })]
    [InlineData("change global pointer", true, PointedGlobal, new[] { "2", "0", "1" })]
    [InlineData("change context pointer", true, PointedContext, new[] { "2", "2", "2" })]
    [InlineData("change total by user pointer", true, PointedTotal, new[] { "2", "2", "2" })]
    [InlineData("change total by user", true, PointedTotal, new[] { "2", "2", "2" })]
    // Without Execute In Order a Change Variable Value is written when its stack's batch flushes.
    [InlineData("change user pointer", false, PointedUser, new[] { "2", "2", "2" })]
    [InlineData("change furni pointer", false, PointedFurni, new[] { "2", "2", "2" })]
    [InlineData("change global pointer", false, PointedGlobal, new[] { "2", "0", "1" })]
    [InlineData("change context pointer", false, PointedContext, new[] { "2", "2", "2" })]
    public async Task ADelayedWriteThroughAPointerFlashesThePointedDefinition(string box, bool inOrder, uint flashed, string[] states)
    {
        var (wired, actor, clock) = await PreparePointedStack(box, inOrder, delay: 1);

        foreach (var expected in states) {
            var (own, delayed) = ClickPointedStack(wired, actor, clock);

            Assert.DoesNotContain(own, entry => PointerBoxes.Contains(entry.Id));
            Assert.DoesNotContain(own, entry => entry.Id == flashed && entry.State is "2" or "0" or "1" or "8");
            Assert.Equal([(flashed, expected)], Assert.Single(delayed));
        }
    }

    // Observed on AIR WIN63-202610201032-107964540: without a delay a write through a pointer is ignored, so no later batch follows and the
    // stack's own batch only holds the definitions its Give boxes reset.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AWriteThroughAPointerWithoutADelayFlashesNoDefinition(bool inOrder)
    {
        var (wired, actor, clock) = await PreparePointedStack("change user pointer", inOrder, delay: 0);

        for (var click = 0; click < 3; click++) {
            var (own, delayed) = ClickPointedStack(wired, actor, clock);

            Assert.DoesNotContain(own, entry => PointerBoxes.Contains(entry.Id));
            Assert.Contains(own, entry => entry.Id == PointedUser && entry.State is "6" or "7");
            Assert.Empty(delayed);
        }
    }

    private async Task<(Plus.HabboHotel.Rooms.Instance.WiredComponent Wired, Plus.HabboHotel.Rooms.RoomUser Actor, SpeechClock Clock)>
        PreparePointedStack(string box, bool inOrder, int delay)
    {
        var clock = new SpeechClock();
        var (wired, actor) = PrepareSpeech(clock);
        var module = new WiredVariableModule(RoomId, new PointedDirectory(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, module);
        typeof(Plus.HabboHotel.Rooms.Instance.WiredComponent).GetField("_configurationStore", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired, new CanonicalStore(null));
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());
        PlaceDefinition(PointedUser, "wf_var_user", 0);
        PlaceDefinition(PointedTotal, "wf_var_user", 1);
        PlaceDefinition(PointedFurni, "wf_var_furni", 2);
        PlaceDefinition(PointedGlobal, "wf_var_room", 3);
        PlaceDefinition(PointedContext, "wf_var_context", 0, 2);
        PlaceDefinition(PointedAccessor, "wf_var_context", 1, 2);
        PlaceDefinition(PointerUser, "wf_var_pointer", 2, 2);
        PlaceDefinition(PointerFurni, "wf_var_pointer", 3, 2);
        PlaceDefinition(PointerGlobal, "wf_var_pointer", 2, 0);
        PlaceDefinition(PointerContext, "wf_var_pointer", 3, 0);
        var holder = Furni(PointedHolder, InteractionType.None, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, holder, 3, 1, 0, true, false, false));

        AddSpeechBox(401, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, 0);

        if (inOrder) {
            PlaceFlashedBox(402, "wf_xtra_exec_in_order", 1, 0.5);
        }

        var pointed = box switch
        {
            _ when box.Contains("furni") => PointedFurni,
            _ when box.Contains("global") => PointedGlobal,
            _ when box.Contains("context") => PointedContext,
            _ => PointedUser
        };
        // Each click resets the variables: user 7 and 100, furni 21 on the holder, context 44, and the accessor names the pointed box.
        object[][] saves =
        [
            [403, 7, 1, 0, 7, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"user:{PointedUser}", "n", 0],
            [404, 7, 1, 0, 100, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"user:{PointedTotal}", "n", 0],
            [405, 7, 0, 0, 21, 1, 0, -10, 0, "", 1, (int)PointedHolder, 0, 1, 100, 1, 0, 2, $"furni:{PointedFurni}", "n", 0],
            [406, 7, -20, 0, 44, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"ctx:{PointedContext}", "n", 0],
            [407, 7, -20, 0, (int)pointed, 1, 0, -10, 0, "", 0, 0, 1, 0, 1, 0, 2, $"ctx:{PointedAccessor}", "n", 0],
            box switch
            {
                "change user pointer" => [408, 7, 1, 1, 0, 0, 5, -10, 0, "", 0, delay, 2, 0, 200, 2, 0, 200, 2, $"user:{PointerUser}", "n", 0],
                "change user" => [408, 7, 1, 1, 0, 0, 5, -10, 0, "", 0, delay, 2, 0, 200, 2, 0, 200, 2, $"user:{PointedUser}", "n", 0],
                "give user pointer" => [408, 7, 1, 0, 5, 1, 0, -10, 0, "", 0, delay, 1, 0, 1, 0, 2, $"user:{PointerUser}", "n", 0],
                "give user" => [408, 7, 1, 0, 5, 1, 0, -10, 0, "", 0, delay, 1, 0, 1, 0, 2, $"user:{PointedUser}", "n", 0],
                "remove user pointer" => [408, 1, 1, "", 0, delay, 1, 100, 1, 0, 1, $"user:{PointerUser}", 0],
                "remove user" => [408, 1, 1, "", 0, delay, 1, 100, 1, 0, 1, $"user:{PointedUser}", 0],
                "change furni pointer" => [408, 7, 0, 1, 0, 0, 1, -10, 0, "", 1, (int)PointedHolder, delay, 2, 100, 200, 2, 0, 200, 2,
                    $"furni:{PointerFurni}", "n", 0],
                "change global pointer" => [408, 7, -10, 0, 0, 0, 9, -10, 0, "", 0, delay, 2, 0, 200, 2, 0, 200, 2, $"room:{PointerGlobal}", "n", 0],
                "change context pointer" => [408, 7, -20, 1, 0, 0, 1, -10, 0, "", 0, delay, 2, 0, 200, 2, 0, 200, 2, $"ctx:{PointerContext}", "n", 0],
                "change total by user pointer" => [408, 7, 1, 1, 1, 0, 0, 1, 0, "", 0, delay, 2, 0, 0, 2, 0, 0, 2, $"user:{PointedTotal}",
                    $"user:{PointerUser}", 0],
                "change total by user" => [408, 7, 1, 1, 1, 0, 0, 1, 0, "", 0, delay, 2, 0, 0, 2, 0, 0, 2, $"user:{PointedTotal}",
                    $"user:{PointedUser}", 0],
                _ => throw new ArgumentOutOfRangeException(nameof(box))
            }
        ];

        for (var index = 0; index < saves.Length; index++) {
            var id = (uint)(int)saves[index][0];
            PlaceFlashedBox(id, index == saves.Length - 1 ? box.StartsWith("give") ? "wf_act_give_var" : box.StartsWith("remove")
                ? "wf_act_remove_var" : "wf_act_change_var_val" : "wf_act_give_var", 1, 1 + index);
            await new SaveWiredEffectConfigEvent(service).Parse(_client, ClientPacket(saves[index]));
        }

        Assert.Equal(saves.Length, _client.Packets.Count(packet => packet.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.HideWiredConfigComposer));
        wired.FlushFlashes();

        return (wired, actor, clock);
    }

    // One click: the stack's own batch, then every batch the next two seconds send, as the definition and pointer boxes they hold.
    private ((uint Id, string State)[] Own, (uint Id, string State)[][] Later) ClickPointedStack(
        Plus.HabboHotel.Rooms.Instance.WiredComponent wired, Plus.HabboHotel.Rooms.RoomUser actor, SpeechClock clock)
    {
        static (uint Id, string State)[] Definitions((uint Id, string State)[] batch) =>
            [.. batch.Where(entry => PointerBoxes.Contains(entry.Id) || PointedBoxes.Contains(entry.Id))];

        _client.Packets.Clear();
        clock.Now = clock.Now.AddSeconds(3);
        actor.OnChat(0, "go", false);
        wired.OnFastCycle();
        var own = DataUpdates(_client).Select(Definitions).Where(batch => batch.Length > 0).ToArray();
        _client.Packets.Clear();

        for (var step = 0; step < 40; step++) {
            clock.Now = clock.Now.AddMilliseconds(50);
            wired.OnFastCycle();
        }

        return (Assert.Single(own), [.. DataUpdates(_client).Select(Definitions).Where(batch => batch.Length > 0)]);
    }

    private void PlaceDefinition(uint id, string name, int x, int y)
    {
        var item = Furni(id, InteractionType.WiredVariable, WiredBoxType.None);
        item.Definition.InteractionName = item.Definition.ItemName = name;
        item.ExtraData = new Plus.HabboHotel.Items.DataFormat.LegacyDataFormat { Data = "0" };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, x, y, 0, true, false, false));
    }

    private sealed class PointedDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            PointedUser => Plain(id, "pu_a", WiredVariableTarget.User),
            PointedTotal => Plain(id, "pu_t", WiredVariableTarget.User),
            PointedFurni => Plain(id, "pf_a", WiredVariableTarget.Furni),
            PointedGlobal => Plain(id, "pg_a", WiredVariableTarget.Global),
            PointedContext => Plain(id, "pc_t", WiredVariableTarget.Context),
            PointedAccessor => Plain(id, "pctx", WiredVariableTarget.Context),
            PointerUser => Pointer(id, "pp_u", 1),
            PointerFurni => Pointer(id, "pp_f", 0),
            PointerGlobal => Pointer(id, "pp_g", -10),
            PointerContext => Pointer(id, "pp_c", -20),
            _ => null
        };

        private static WiredVariableDefinition Plain(uint id, string name, WiredVariableTarget target) =>
            new(id, RoomId, 7, name, target, WiredVariableAvailability.RoomActive, true);

        private static WiredVariableDefinition Pointer(uint id, string name, int airTarget)
        {
            Assert.True(WiredVariableDefinitions.TryDecode("wf_var_pointer", id, RoomId, 7,
                new() { IntParams = [airTarget], VariableIds = [$"ctx:{PointedAccessor}"], Text = name }, out var definition, out var error), error);

            return definition!;
        }
    }
}
