using System.Collections.Immutable;
using System.Reflection;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Users.Inventory.Furniture;
using System.Collections.Concurrent;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// Group g3, Variable Capturer (wf_xtra_text_input_variable): a "says" keyword holding the capturer's token, fired through the engine.
public partial class ModernWiredRuntimeTests
{
    private sealed class G3ContextCatalog : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 5;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            10 => new(10, 1, 5, "amount", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true),
            11 => new(11, 1, 5, "count", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true),
            12 => new(12, 1, 5, "plain", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, false),
            _ => null
        };
    }

    /// <summary>Records what the stack's context variables held when the effect ran.</summary>
    private sealed class G3Recorder(Room room, Item item, WiredVariableModule module, params string[] tokens) : IWiredContextualAction
    {
        public List<long?[]> Runs { get; } = [];
        public Room Instance { get; set; } = room;
        public Item Item { get; set; } = item;
        public WiredBoxDescriptor Descriptor { get; } = Descriptor("wf_act_toggle_state") with { Support = WiredBoxSupport.Implemented };
        public WiredConfiguration Configuration { get; } = new();
        public WiredBoxType Type => WiredBoxType.None;
        public ConcurrentDictionary<uint, Item> SetItems { get; set; } = new();
        public string StringData { get; set; } = "";
        public string ItemsData { get; set; } = "";
        public bool BoolData { get; set; }
        public bool IsNegative => false;
        public bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
        {
            validated = proposed;
            error = "";

            return true;
        }
        public void ApplyConfiguration(WiredConfiguration validated) { }
        public void HandleSave(IIncomingPacket packet) => throw new NotSupportedException();
        public bool Execute(params object[] arguments) => false;
        public bool Execute(WiredRuntimeContext context)
        {
            var holder = new WiredVariableHolder(WiredVariableTarget.Context, 0, 0);
            Runs.Add(tokens.Select(token => context.VariableFrame is { } frame
                ? module.Read(new(WiredVariableTarget.Context, token), holder, frame)?.Value : null).ToArray());

            return true;
        }
    }

    private sealed record G3CapturerRig(TeleportFixture F, WiredVariableModule Module, WiredRoomVariables Variables, G3Recorder Recorder, WiredModernTrigger Says);

    private static WiredVariableTextInputBox G3Capturer(TeleportFixture f, WiredRoomVariables variables, uint id, string name, string variable, int display = 1, int x = 0, int y = 0)
    {
        var item = new Item { Id = id, Definition = new() { InteractionName = "wf_xtra_text_input_variable", Width = 1, Length = 1, Type = ItemType.Floor } };
        item.SetState(x, y, 0, Gamemap.GetAffectedTiles(1, 1, x, y, 0));
        f.Items[id] = item;
        f.Room.GetGameMap().AddToMap(item);
        var box = Assert.IsType<WiredVariableTextInputBox>(variables.CreateBox(item));
        Assert.True(WiredNativeEditorProjection.TryCompile(id, box.Descriptor, WiredNativeEditorProjection.DefaultNative(box.Descriptor) with { OwnedIntParams = [display], VariableIds = [variable], Text = name }, out var runtime));
        Assert.True(box.TryValidateConfiguration(runtime, out var validated, out var error), error);
        box.ApplyConfiguration(validated);
        variables.ConfigurationLoaded(box);

        return box;
    }

    private static G3CapturerRig G3CapturerStack(string keyword, int matchMode = 0, params (string Name, string Variable, int Display)[] capturers)
    {
        var f = new TeleportFixture();
        f.Engine.Remove(f.Action.Item.Id);
        f.Items.TryRemove(f.Action.Item.Id, out _);
        f.Engine.Remove(f.Trigger.Item.Id);
        f.Room.Id = 1;
        f.Room.OwnerId = 5;
        var variables = f.Room.GetWired().Variables;
        var module = new WiredVariableModule(1, new G3ContextCatalog(), new MemoryWiredVariableStore(), TimeProvider.System);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(variables, module);
        f.Engine.CaptureSpeech = (context, trigger) => variables.CaptureSpeech(context, trigger);
        var says = new WiredModernTrigger(f.Room, MakeItem(110, "wf_trg_says_something"), Descriptor("wf_trg_says_something"));
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(says, new() { IntParams = [matchMode, 0, 0], Text = keyword }, out var saysConfig, out var saysError), saysError);
        says.ApplyConfiguration(saysConfig);
        f.Items[110] = says.Item;
        Assert.True(f.Engine.Add(says));
        var number = 120u;

        foreach (var (name, variable, display) in capturers) {
            var box = G3Capturer(f, variables, number++, name, variable, display);
            Assert.True(f.Engine.Add(box));
        }

        var recorder = new G3Recorder(f.Room, MakeItem(130, "wf_act_toggle_state"), module, "custom:10", "custom:11", "custom:12");
        f.Items[130] = recorder.Item;
        Assert.True(f.Engine.Add(recorder));

        return new(f, module, variables, recorder, says);
    }

    private static void G3Say(G3CapturerRig rig, string message)
    {
        rig.F.Engine.DispatchSynchronously(new WiredRuntimeEvent(WiredEventKind.Speech) { Actor = rig.F.User, Message = message });
        rig.F.Advance(50);
    }

    /// <summary>A text connector on the tile of the context variable's definition, as the editor lays them out.</summary>
    private static void G3Connector(G3CapturerRig rig, uint definition, string text)
    {
        var f = rig.F;
        var definitionItem = new Item { Id = definition, RoomId = 1, OwnerId = 5, Definition = new() { Id = definition, InteractionName = "wf_var_context", Width = 1, Length = 1, Type = ItemType.Floor } };
        definitionItem.SetState(3, 3, 0, Gamemap.GetAffectedTiles(1, 1, 3, 3, 0));
        f.Items[definition] = definitionItem;
        var definitionBox = new WiredVariableDefinitionBox(f.Room, definitionItem, Descriptor("wf_var_context"));
        WiredNativeTestSupport.InstallLegacyVariableDraft(definitionBox, new() { IntParams = [1], Text = "count" });
        rig.Variables.ConfigurationLoaded(definitionBox);
        var connectorItem = new Item { Id = 170, RoomId = 1, OwnerId = 5, Definition = new() { Id = 170, InteractionName = "wf_xtra_var_text_connector", Width = 1, Length = 1, Type = ItemType.Floor } };
        connectorItem.SetState(3, 3, 0, Gamemap.GetAffectedTiles(1, 1, 3, 3, 0));
        f.Items[170] = connectorItem;
        var connector = Assert.IsType<WiredVariableMetadataBox>(rig.Variables.CreateBox(connectorItem));
        WiredNativeTestSupport.InstallLegacyVariableDraft(connector, new() { Text = text });
        rig.Variables.ConfigurationLoaded(connector);
    }

    [Theory]
    [InlineData("price #(amount)", "the price 9 please", 9)]
    [InlineData("price #(amount)", "price 250 and more", 250)]
    [InlineData("price #(amount)", "PRICE 75", 75)]
    [InlineData("#(amount) coins", "give 12 coins now", 12)]
    public void G3AKeywordThatIsPartOfALineHandsOverTheTypedWordAndTheLineGoesOn(string keyword, string said, long expected)
    {
        var rig = G3CapturerStack(keyword, 0, ("amount", "ctx:10", 0));
        using var f = rig.F;
        G3Say(rig, said);
        Assert.Equal(expected, Assert.Single(rig.Recorder.Runs)[0]);
    }

    [Theory]
    [InlineData("price two")]
    [InlineData("price")]
    [InlineData("pricey 3")]
    public void G3AKeywordThatIsPartOfALineStillNeedsANumberWhereTheTokenStands(string said)
    {
        var rig = G3CapturerStack("price #(amount)", 0, ("amount", "ctx:10", 0));
        using var f = rig.F;
        G3Say(rig, said);
        Assert.Empty(rig.Recorder.Runs);
    }

    [Theory]
    [InlineData("price #(amount)", "price 250", 250)]
    [InlineData("price #(amount)", "PRICE 75", 75)]
    [InlineData("price #(amount)", "price -12", -12)]
    [InlineData("price #(amount)", "price 2147483648", 2147483648)]
    [InlineData("price #amount#", "price 9", 9)]
    [InlineData("price #(Amount)", "price 31", 31)]
    public void G3SayingTheKeywordPutsTheTypedNumberInTheContextVariableOfThatFiring(string keyword, string said, long expected)
    {
        var rig = G3CapturerStack(keyword, 1, ("amount", "ctx:10", 0));
        using var f = rig.F;
        G3Say(rig, said);
        Assert.Equal(expected, Assert.Single(rig.Recorder.Runs)[0]);
        Assert.Empty(f.Errors);
    }

    [Theory]
    [InlineData("price two")]
    [InlineData("price 1.5")]
    [InlineData("price 9223372036854775808")]
    [InlineData("hello")]
    [InlineData("price 250 and more")]
    public void G3AnUnreadableOrNonMatchingLineFiresNothing(string said)
    {
        var rig = G3CapturerStack("price #(amount)", 1, ("amount", "ctx:10", 0));
        using var f = rig.F;
        G3Say(rig, said);
        Assert.Empty(rig.Recorder.Runs);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void G3TheKeywordMayCaptureTwoVariablesAndAFollowingLineStartsFromNothing()
    {
        var rig = G3CapturerStack("set #(amount) x #(count)", 1, ("amount", "ctx:10", 0), ("count", "ctx:11", 0));
        using var f = rig.F;
        G3Say(rig, "set 7 x 3");
        G3Say(rig, "set 8 x 4");
        Assert.Equal(new long?[][] { [7, 3, null], [8, 4, null] }, rig.Recorder.Runs);
        // A context variable lives for one firing: nothing was kept for the room.
        Assert.Null(rig.Module.Read(new(WiredVariableTarget.Context, "custom:10"), new(WiredVariableTarget.Context, 0, 0), new WiredVariableFrame(1, [])));
    }

    [Fact]
    public void G3AWordOnlyCaptureReadsTheConnectorTextOfThePickedVariable()
    {
        var rig = G3CapturerStack("size #(count)", 1, ("count", "ctx:11", 1));
        using var f = rig.F;
        // No text connector is on the variable yet, so a word is only read as the number it spells.
        G3Say(rig, "size big");
        Assert.Empty(rig.Recorder.Runs);
        G3Connector(rig, 11, "1=small\n2=big");
        G3Say(rig, "size BIG");
        G3Say(rig, "size small");
        // A number is not a word of the connector, and a word the connector lacks reads as nothing.
        G3Say(rig, "size 2");
        G3Say(rig, "size huge");
        Assert.Equal(new long?[][] { [null, 2, null], [null, 1, null] }, rig.Recorder.Runs);
    }

    [Fact]
    public void G3NumericModeIgnoresTheConnectorAndReadsTheNumber()
    {
        var rig = G3CapturerStack("size #(count)", 1, ("count", "ctx:11", 0));
        using var f = rig.F;
        G3Connector(rig, 11, "1=small\n2=big");
        G3Say(rig, "size 2");
        G3Say(rig, "size big");
        Assert.Equal(new long?[][] { [null, 2, null] }, rig.Recorder.Runs);
    }

    [Fact]
    public void G3AKeywordWithoutAnyCapturerOnTheTileIsAnOrdinaryKeyword()
    {
        var rig = G3CapturerStack("price #(amount)", 0);
        using var f = rig.F;
        G3Say(rig, "price 250");
        Assert.Empty(rig.Recorder.Runs);
        G3Say(rig, "the price #(amount) is up");
        Assert.Single(rig.Recorder.Runs);
        Assert.All(rig.Recorder.Runs[0], value => Assert.Null(value));
    }

    [Fact]
    public void G3ACapturerRefusesAnUnnamedBadlyNamedOrDuplicateCapture()
    {
        var rig = G3CapturerStack("price #(amount)", 1, ("amount", "ctx:10", 0));
        using var f = rig.F;
        var descriptor = Descriptor("wf_xtra_text_input_variable");

        string? Error(string name, string variable, uint id, int x)
        {
            var item = new Item { Id = id, Definition = new() { InteractionName = "wf_xtra_text_input_variable", Width = 1, Length = 1, Type = ItemType.Floor } };
            item.SetState(x, 0, 0, Gamemap.GetAffectedTiles(1, 1, x, 0, 0));
            f.Items[id] = item;
            var box = Assert.IsType<WiredVariableTextInputBox>(rig.Variables.CreateBox(item));
            var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [0], VariableIds = [variable], Text = name };

            string? failure = null;

            return WiredNativeEditorProjection.TryCompile(id, descriptor, native, out var runtime) && box.TryValidateConfiguration(runtime, out _, out failure) ? null : failure;
        }

        Assert.Null(Error("other", "ctx:11", 140, 0));
        Assert.Equal("wiredfurni.error.placeholder.name_length", Error("", "ctx:11", 141, 0));
        Assert.Equal("wiredfurni.error.placeholder.name_length", Error(new string('a', 41), "ctx:11", 142, 0));
        Assert.Equal("wiredfurni.error.placeholder.name_syntax", Error("two words", "ctx:11", 143, 0));
        Assert.Equal("wiredfurni.error.placeholder.name_syntax", Error("a-b", "ctx:11", 144, 0));
        Assert.Equal("wiredfurni.error.placeholder.name_syntax", Error("a(b)", "ctx:11", 145, 0));
        // The same name twice in one stack, in any case, is refused; the same name on another tile is another stack.
        Assert.Equal("wiredfurni.error.link.destination_conflict", Error("Amount", "ctx:11", 146, 0));
        Assert.Null(Error("amount", "ctx:11", 147, 2));
    }

    [Fact]
    public void G3OnlyAContextVariableThatTheRoomOwnsCanBePicked()
    {
        using var f = new TeleportFixture();
        var variables = f.Room.GetWired().Variables;
        var item = new Item { Id = 150, Definition = new() { InteractionName = "wf_xtra_text_input_variable" } };
        var box = Assert.IsType<WiredVariableTextInputBox>(variables.CreateBox(item));
        var native = WiredNativeEditorProjection.DefaultNative(box.Descriptor) with { OwnedIntParams = [0], Text = "amount" };

        foreach (var variable in new[] { "n", "0", "user:10", "furni:10", "room:10", "custom:10" }) {
            if (WiredNativeEditorProjection.TryCompile(150, box.Descriptor, native with { VariableIds = [variable] }, out var runtime)) {
                Assert.False(box.TryValidateConfiguration(runtime, out _, out _), variable);
            }
        }

        Assert.True(WiredNativeEditorProjection.TryCompile(150, box.Descriptor, native with { VariableIds = ["ctx:10"] }, out var accepted));
        Assert.True(box.TryValidateConfiguration(accepted, out _, out var error), error);
    }
}
