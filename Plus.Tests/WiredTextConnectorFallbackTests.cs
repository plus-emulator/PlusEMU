using System.Runtime.CompilerServices;
using Plus.Communication.Packets.Outgoing.WiredVariables;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public class WiredTextConnectorFallbackTests
{
    // Observed on AIR WIN63-202610201032-107964540: a variable whose connector holds only a fallback still has a connector,
    // with no entries and the fallback after them. Without a fallback the flag after the entries is false.
    [Theory]
    [InlineData(null, new[] { 1 }, new object[] { true, 1, 1, "one", false })]
    [InlineData("other", new[] { 1 }, new object[] { true, 1, 1, "one", true, "other" })]
    [InlineData("other", new int[0], new object[] { true, 0, true, "other" })]
    [InlineData("", new int[0], new object[] { true, 0, true, "" })]
    [InlineData(null, new int[0], new object[] { false })]
    public void TheCatalogEntryCarriesTheFallback(string? fallback, int[] keys, object[] expectedTail)
    {
        var variable = new WiredVariableDescription(new(10, 1, 5, "score", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true), true, false)
        {
            TextConnector = keys.ToDictionary(key => key, _ => "one"),
            TextConnectorFallback = fallback
        };
        var composer = new WiredAllVariablesDiffComposer(new(42, true, [], [variable]));
        var packet = new HabbiconTestSupport.RecordingPacket();
        composer.Compose(packet);
        var written = packet.Writes.SkipWhile(value => value is not "score").Skip(12).ToArray();
        Assert.Equal(expectedTail, written);
    }

    [Fact]
    public void TheFallbackChangesTheCatalogHash()
    {
        var variable = new WiredVariableDescription(new(10, 1, 5, "score", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true), true, false)
        { TextConnector = new Dictionary<int, string> { [1] = "one" } };
        Assert.NotEqual(variable.Hash, (variable with { TextConnectorFallback = "x" }).Hash);
        Assert.NotEqual((variable with { TextConnectorFallback = "x" }).Hash, (variable with { TextConnectorFallback = "y" }).Hash);
        Assert.NotEqual(new WiredVariableDescription(variable.Definition, true, false).Hash,
            new WiredVariableDescription(variable.Definition, true, false) { TextConnectorFallback = "" }.Hash);
    }

    // Observed on AIR WIN63-202610201032-107964540: in text mode a value with no label shows the fallback, a labelled value
    // keeps its label, a holder without the variable shows "null" and number mode never reads the connector.
    [Theory]
    [InlineData(1, 7, "other", "w seven")]
    [InlineData(1, 9, "other", "w other")]
    [InlineData(1, 9, "", "w ")]
    [InlineData(1, 9, "[Empty]", "w [Empty]")]
    [InlineData(1, 9, null, "w 9")]
    [InlineData(0, 9, "other", "w 9")]
    [InlineData(0, 7, "other", "w 7")]
    public void TheFallbackReplacesAnUnlabelledNumberInTextMode(int textMode, int? value, string? fallback, string expected)
    {
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        room.Id = 1;
        var context = new WiredRuntimeContext(room, new(WiredEventKind.Enter), new(() => [], () => []), new Operations());
        var frame = WiredVariableRuntimeFrames.Create(context);
        context.VariableFrame = frame;
        var module = new WiredVariableModule(1, new Directory(), new MemoryWiredVariableStore(), TimeProvider.System);
        Assert.True(module.InitializeGlobal(10));

        if (value is { } v) {
            Assert.True(module.Mutate(new(WiredVariableTarget.Global, "custom:10"), new(WiredVariableTarget.Global, 0, 0), WiredVariableMutation.Set, v, frame));
        }

        Assert.True(WiredBoxRegistry.TryGet("wf_xtra_text_output_variable", out var descriptor));
        var connector = new WiredTextConnector(new Dictionary<int, string> { [7] = "seven" }, fallback);
        var box = new WiredVariableAddonBox(room, new Item { Id = 90 }, descriptor, module, _ => connector);
        WiredNativeTestSupport.Install(box, WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [0, -10, textMode],
            VariableIds = ["room:10"],
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            Text = "r80vroom"
        });

        Assert.True(box.Apply(context));
        Assert.Equal(expected, context.Policy.FormatText(context, "w $(r80vroom)"));
    }

    private sealed class Operations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }

    private sealed class Directory : IWiredVariableDirectory
    {
        public WiredVariableDefinition? Find(uint itemId) => itemId == 10
            ? new(itemId, 1, 5, "r80vroom", WiredVariableTarget.Global, WiredVariableAvailability.RoomActive, true) : null;
        public uint? GetRoomOwner(uint roomId) => 5;
    }
}
