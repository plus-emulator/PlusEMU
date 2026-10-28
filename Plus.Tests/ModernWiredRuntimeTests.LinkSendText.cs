using System.Reflection;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Modern.Triggers;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

/// <summary>The text a Send Message or Send Dataframe box delivers to a Receive box.</summary>
public partial class ModernWiredRuntimeTests
{
    private const uint LinkTextVariable = 10;

    private static (TeleportFixture F, Item L1, Item L2) LinkTextRoom()
    {
        var f = new TeleportFixture();
        f.Room.Id = 1;
        f.Room.OwnerId = 5;
        Assert.True(f.Engine.Remove(100));
        var store = new LinkTextStore(f.Room.Id);
        f.Room.GetWired().LinkStore = store;
        var (l1, l2) = (LinkFurni(f, 50, 0, 1), LinkFurni(f, 51, 0, 2));
        store.Join(l1.Id, 7, false);
        store.Join(l2.Id, 7, true);

        return (f, l1, l2);
    }

    private static void LinkTextLog(TeleportFixture f, string text)
    {
        var log = Assert.IsType<WiredModernAction>(f.Room.GetWired().CreateConfiguredBox(Placed(f, MakeItem(121, "wf_act_log"), 2, 2), Descriptor("wf_act_log")));
        Assert.True(WiredNativeEditorProjection.TryCompile(121, Descriptor("wf_act_log"),
            WiredNativeEditorProjection.DefaultNative(Descriptor("wf_act_log")) with { OwnedIntParams = [0], Text = text }, out var logged));
        log.ApplyConfiguration(logged);
        Assert.True(f.Engine.Add(log));
    }

    private static string[] LinkTextLogs(TeleportFixture f) => [.. f.Room.GetWired().ReadLogs(1, 50).Entries.Select(entry => entry.Reason)];

    private static void LinkTextSendMessage(TeleportFixture f, Item sender, int source, string text) =>
        OctoberBox<WiredModernAction>(f, 110, "wf_act_send_message", 0, 0, native => native with
        {
            OwnedIntParams = [source],
            Text = text,
            PrimaryItems = [new(sender.Id, false)],
            FurniSourceTypes = [100]
        });

    private static void LinkTextReceiveMessage(TeleportFixture f, Item receiver) =>
        OctoberBox<WiredModernTrigger>(f, 120, "wf_trg_receive_message", 2, 2, native => native with
        {
            PrimaryItems = [new(receiver.Id, false)],
            FurniSourceTypes = [100]
        });

    private static void LinkTextSendDataframe(TeleportFixture f, Item sender, int mode, int literal, int target, string[] variables) =>
        OctoberBox<WiredModernAction>(f, 110, "wf_act_send_dataframe", 0, 0, native => native with
        {
            OwnedIntParams = [1, mode, literal, target, .. Enumerable.Range(0, 11).SelectMany(_ => new[] { 0, 0, -20 })],
            Text = "[[\"num\",\"\"]]",
            VariableIds = [.. variables, .. Enumerable.Repeat(WiredVariableAbsent.Id, 12 - variables.Length)],
            PrimaryItems = [new(sender.Id, false)],
            FurniSourceTypes = [100, .. Enumerable.Repeat(200, 12)],
            UserSourceTypes = [.. Enumerable.Repeat(0, 12)]
        });

    private static void LinkTextReceiveDataframe(TeleportFixture f, Item receiver, int count, string rows) =>
        OctoberBox<WiredModernTrigger>(f, 120, "wf_trg_receive_dataframe", 2, 2, native => native with
        {
            OwnedIntParams = [count, .. Enumerable.Repeat(0, 12)],
            Text = rows,
            PrimaryItems = [new(receiver.Id, false)],
            FurniSourceTypes = [100]
        });

    [Fact]
    public void LinkSendMessageDeliversAnEmptyCustomMessage()
    {
        var (f, l1, l2) = LinkTextRoom();
        using var _f = f;
        LinkTextSendMessage(f, l1, 1, "");
        LinkTextReceiveMessage(f, l2);
        LinkTextLog(f, "got [$(trigger_text)]");

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal(["got []"], LinkTextLogs(f));
    }

    [Fact]
    public void LinkSendMessageFromATriggerWithoutTextDeliversNull()
    {
        var (f, l1, l2) = LinkTextRoom();
        using var _f = f;
        LinkTextSendMessage(f, l1, 0, "");
        LinkTextReceiveMessage(f, l2);
        LinkTextLog(f, "got [$(trigger_text)]");

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal(["got [null]"], LinkTextLogs(f));
    }

    [Fact]
    public void LinkReceiveDataframePrintsNullForAKeyThatWasNotSent()
    {
        var (f, l1, l2) = LinkTextRoom();
        using var _f = f;
        LinkTextSendDataframe(f, l1, 0, 5, -20, []);
        LinkTextReceiveDataframe(f, l2, 2, "[[\"num\",\"numph\"],[\"nokey\",\"nkph\"]]");
        LinkTextLog(f, "n=[$(numph)] k=[$(nkph)]");

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal(["n=[5] k=[null]"], LinkTextLogs(f));
    }

    [Theory]
    [InlineData("1=one\n5=five\n_=other", 9, "other")]
    [InlineData("1=one\n5=five\n_=other", 5, "five")]
    [InlineData("_=other", 9, "other")]
    [InlineData("1=uno", 9, "9")]
    [InlineData("2147483648=big\n-2147483649=small", 2147483648, "big")]
    [InlineData("2147483648=big\n-2147483649=small", -2147483649, "small")]
    [InlineData("2147483648=big\n_=other", 4294967296, "other")]
    public void LinkSendDataframeVariableTextUsesTheConnectorFallback(string connector, long value, string expected)
    {
        var (f, l1, l2) = LinkTextRoom();
        using var _f = f;
        var variables = f.Room.GetWired().Variables;
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(variables, new WiredVariableModule(f.Room.Id, new LinkTextDirectory(), new MemoryWiredVariableStore(), TimeProvider.System));
        var definitionItem = new Item { Id = LinkTextVariable, RoomId = f.Room.Id, OwnerId = 5, Definition = new() { Id = LinkTextVariable, InteractionName = "wf_var_room", Width = 1, Length = 1, Type = ItemType.Floor } };
        definitionItem.SetState(3, 3, 0);
        f.Items[LinkTextVariable] = definitionItem;
        var definitionBox = new WiredVariableDefinitionBox(f.Room, definitionItem, Descriptor("wf_var_room"));
        WiredNativeTestSupport.InstallLegacyVariableDraft(definitionBox, new() { IntParams = [1], Text = "count" });
        variables.ConfigurationLoaded(definitionBox);
        var connectorItem = new Item { Id = 170, RoomId = f.Room.Id, OwnerId = 5, Definition = new() { Id = 170, InteractionName = "wf_xtra_var_text_connector", Width = 1, Length = 1, Type = ItemType.Floor } };
        connectorItem.SetState(3, 3, 0);
        f.Items[170] = connectorItem;
        var connectorBox = Assert.IsType<WiredVariableMetadataBox>(variables.CreateBox(connectorItem));
        WiredNativeTestSupport.InstallLegacyVariableDraft(connectorBox, new() { Text = connector });
        variables.ConfigurationLoaded(connectorBox);
        var context = new WiredRuntimeContext(f.Room, new(WiredEventKind.Enter), new(() => [], () => []), new LinkTextOperations());
        Assert.True(variables.Module.Mutate(new(WiredVariableTarget.Global, "custom:10"), new(WiredVariableTarget.Global, 0, 0), WiredVariableMutation.Set, value,
            WiredVariableRuntimeFrames.Create(context)));
        LinkTextSendDataframe(f, l1, 2, 0, -10, ["room:10"]);
        LinkTextReceiveDataframe(f, l2, 1, "[[\"num\",\"numph\"]]");
        LinkTextLog(f, "t=[$(numph)]");

        f.Fire(land: false);
        f.Advance(50);

        Assert.Equal([$"t=[{expected}]"], LinkTextLogs(f));
    }

    // A row in variable text mode sends the Text Connector label of the variable a pointer points at. In number mode it sends the number.
    [Theory]
    [InlineData(2, "user:650", 0, "seven")]
    [InlineData(2, "user:610", 0, "seven")]
    [InlineData(2, "user:650", 1, "seven")]
    [InlineData(1, "user:650", 0, "7")]
    [InlineData(1, "user:650", 1, "7")]
    public void LinkSendDataframeReadsTheConnectorOfThePointedVariable(int mode, string variable, int delay, string expected)
    {
        using var p = new PointerStack();
        var f = p.S.F;
        f.Room.OwnerId = (int)PointedOwner;
        var store = new LinkTextStore(f.Room.Id);
        f.Room.GetWired().LinkStore = store;
        var (l1, l2) = (LinkFurni(f, 50, 4, 1), LinkFurni(f, 51, 4, 2));
        store.Join(l1.Id, 7, false);
        store.Join(l2.Id, 7, true);
        var variables = f.Room.GetWired().Variables;
        var definitionItem = Placed(f, MakeItem(UserA, "wf_var_user"), 3, 3);
        var definitionBox = new WiredVariableDefinitionBox(f.Room, definitionItem, Descriptor("wf_var_user"));
        WiredNativeTestSupport.InstallLegacyVariableDraft(definitionBox, new() { IntParams = [1, 0], Text = "pu_a" });
        variables.ConfigurationLoaded(definitionBox);
        var connectorBox = Assert.IsType<WiredVariableMetadataBox>(variables.CreateBox(Placed(f, MakeItem(170, "wf_xtra_var_text_connector"), 3, 3)));
        WiredNativeTestSupport.InstallLegacyVariableDraft(connectorBox, new() { Text = "7=seven" });
        variables.ConfigurationLoaded(connectorBox);

        p.SetUser(UserA, f.User, 7);
        p.Point(UserA);
        p.Native("wf_act_send_dataframe", [1, mode, 0, AirUser, .. Enumerable.Range(0, 11).SelectMany(_ => new[] { 0, 0, -20 })],
            [variable, .. Enumerable.Repeat(WiredVariableAbsent.Id, 11)], "[[\"num\",\"\"]]", [100, .. Enumerable.Repeat(200, 12)],
            [.. Enumerable.Repeat(0, 12)], [l1.Id], delay: delay);
        LinkTextReceiveDataframe(f, l2, 1, "[[\"num\",\"numph\"]]");
        LinkTextLog(f, "t=[$(numph)]");

        p.FireAndWait(50 + delay * 500);

        Assert.Equal([$"t=[{expected}]"], LinkTextLogs(f));
    }

    private sealed class LinkTextStore(uint roomId) : IWiredLinkStore
    {
        private readonly Dictionary<uint, WiredLinkEndpoint> _endpoints = [];

        public void Join(uint itemId, uint networkId, bool receiveLocal) => _endpoints[itemId] = new(itemId, networkId, receiveLocal);

        public WiredLinkEndpoint? Endpoint(uint itemId) => _endpoints.GetValueOrDefault(itemId);

        public IReadOnlyList<WiredLinkConnection> Members(uint networkId) =>
            [.. _endpoints.Values.Where(endpoint => endpoint.NetworkId == networkId).Select(endpoint => new WiredLinkConnection(roomId, "", endpoint.ItemId, endpoint.ReceiveLocal))];

        public IReadOnlyList<WiredLinkNetwork> Networks(int ownerId, uint roomId) => throw new NotSupportedException();

        public IReadOnlyList<WiredLinkConnection> Connections(int ownerId, uint networkId) => throw new NotSupportedException();

        public WiredLinkResult Select(int ownerId, uint itemId, uint roomId, uint networkId, bool receiveLocal) => throw new NotSupportedException();

        public WiredLinkResult Create(int ownerId, string name, out uint networkId) => throw new NotSupportedException();

        public WiredLinkResult Rename(int ownerId, uint networkId, string name) => throw new NotSupportedException();

        public WiredLinkResult Delete(int ownerId, uint networkId, out IReadOnlyList<WiredLinkConnection> detached) => throw new NotSupportedException();
    }

    private sealed class LinkTextDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 5;

        public WiredVariableDefinition? Find(uint id) => id == LinkTextVariable
            ? new(id, 1, 5, "count", WiredVariableTarget.Global, WiredVariableAvailability.RoomActive, true) : null;
    }

    private sealed class LinkTextOperations : IWiredRuntimeOperations
    {
        public bool CallStacks(WiredRuntimeContext context, IEnumerable<Item> targets, bool negative = false) => throw new NotSupportedException();
        public bool SendSignal(WiredRuntimeContext context, IEnumerable<Item> receivers, WiredSelection selection, bool negative = false) => throw new NotSupportedException();
        public void ResetTimers(IEnumerable<Item> targets) => throw new NotSupportedException();
    }
}
