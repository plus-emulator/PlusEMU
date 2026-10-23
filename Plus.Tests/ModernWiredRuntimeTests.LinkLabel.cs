using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Labels;
using Plus.HabboHotel.Items.Wired.Links;
using Xunit;

namespace Plus.Tests;

/// <summary>WIRED Link network window and WIRED Label bubbles.</summary>
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void LinkAndLabelInteractionsParse()
    {
        Assert.Equal(InteractionType.WiredLink, InteractionTypes.GetTypeFromString("wired_link"));
        Assert.Equal(InteractionType.WiredLabel, InteractionTypes.GetTypeFromString("wired_label"));
    }

    [Fact]
    public void LinkPacketsFollowTheClientParsers()
    {
        var networks = new[] { new WiredLinkNetwork(3, "Doors", 2, true) };
        var configuration = new HabbiconTestSupport.RecordingPacket();
        new WiredLinkConfigurationComposer(42, 9, "3", true, networks).Compose(configuration);
        Assert.Equal(new object[] { 42u, 9u, "3", true, 1, "3", "Doors", 2, true }, configuration.Writes);

        var result = new HabbiconTestSupport.RecordingPacket();
        new WiredLinkOperationResultComposer(42, 9, WiredLinkController.Delete, WiredLinkResult.Ok, new(9, 0, false), "3", []).Compose(result);
        Assert.Equal(new object[] { 42u, 9u, 4, 0, true, "0", false, "3", 0 }, result.Writes);

        var noConfiguration = new HabbiconTestSupport.RecordingPacket();
        new WiredLinkOperationResultComposer(42, 9, WiredLinkController.Rename, WiredLinkResult.InvalidName, null, "", []).Compose(noConfiguration);
        Assert.Equal(new object[] { 42u, 9u, 3, 3, false, "", 0 }, noConfiguration.Writes);

        var connections = new HabbiconTestSupport.RecordingPacket();
        new WiredLinkConnectionsComposer(42, 9, "3", [new(43, "Lobby", 10, true)]).Compose(connections);
        Assert.Equal(new object[] { 42u, 9u, "3", 1, 43u, "Lobby", 10u, false, true }, connections.Writes);

        var invalidated = new HabbiconTestSupport.RecordingPacket();
        new WiredLinkNetworkInvalidatedComposer(43, 10, "3").Compose(invalidated);
        Assert.Equal(new object[] { 43u, 10u, "3" }, invalidated.Writes);

        var open = new HabbiconTestSupport.RecordingPacket();
        new WiredLinkOpenComposer(9).Compose(open);
        Assert.Equal(new object[] { 9u }, open.Writes);
        Assert.Equal((2374u, 1199u, 2156u, 2593u, 3715u), (new WiredLinkOpenComposer(0).MessageId,
            new WiredLinkConfigurationComposer(0, 0, "", false, []).MessageId,
            new WiredLinkOperationResultComposer(0, 0, 0, WiredLinkResult.Ok, null, "", []).MessageId,
            new WiredLinkConnectionsComposer(0, 0, "", []).MessageId, new WiredLinkNetworkInvalidatedComposer(0, 0, "").MessageId));
    }

    [Fact]
    public async Task OnlyTheOwnerOfAWiredLinkOpensItsNetworks()
    {
        using var f = new TeleportFixture();
        var store = new ControllerLinkStore();
        f.Room.GetWired().LinkStore = store;
        var link = LinkFurni(f, 50, 2, 2);
        link.OwnerId = 1;
        var sent = Capture(f.Client);

        WiredLinkController.Open(f.Client, link);
        Assert.Equal(50, Reply(sent, ServerPacketHeader.WiredLinkOpenComposer).Int());

        await new WiredLinkRequestConfigEvent().Parse(f.Room, f.Client, EditorTestSupport.Incoming(50));
        var configuration = Reply(sent, ServerPacketHeader.WiredLinkConfigurationComposer);
        Assert.Equal((0, 50, "7", true, 1), (configuration.Int(), configuration.Int(), configuration.String(), configuration.Bool(), configuration.Int()));

        await new WiredLinkSaveSelectionEvent().Parse(f.Room, f.Client, EditorTestSupport.Incoming(50, "0", false));
        var saved = Reply(sent, ServerPacketHeader.WiredLinkOperationResultComposer);
        Assert.Equal((0, 50, WiredLinkController.Save, 0), (saved.Int(), saved.Int(), saved.Int(), saved.Int()));
        Assert.Equal((50u, 0u, false), store.Selected);

        // wiredlink.error.6: a network id that is not a canonical number.
        await new WiredLinkSaveSelectionEvent().Parse(f.Room, f.Client, EditorTestSupport.Incoming(50, "07", false));
        var invalid = Reply(sent, ServerPacketHeader.WiredLinkOperationResultComposer);
        Assert.Equal((0, 50, WiredLinkController.Save, 6, false), (invalid.Int(), invalid.Int(), invalid.Int(), invalid.Int(), invalid.Bool()));

        // wiredlink.error.7: a furni that is no WIRED Link of the room.
        await new WiredLinkDeleteNetworkEvent().Parse(f.Room, f.Client, EditorTestSupport.Incoming(999, "7"));
        Assert.Equal((0, 999, WiredLinkController.Delete, 7), Codes(Reply(sent, ServerPacketHeader.WiredLinkOperationResultComposer)));

        // Extra bytes get no answer; someone else's furni does not open and answers operations with wiredlink.error.1.
        await new WiredLinkRequestConfigEvent().Parse(f.Room, f.Client, EditorTestSupport.Incoming(50, 1));
        link.OwnerId = 2;
        WiredLinkController.Open(f.Client, link);
        await new WiredLinkRequestConfigEvent().Parse(f.Room, f.Client, EditorTestSupport.Incoming(50));
        Assert.Empty(sent);
        await new WiredLinkCreateNetworkEvent().Parse(f.Room, f.Client, EditorTestSupport.Incoming(50, "Doors"));
        var refused = Reply(sent, ServerPacketHeader.WiredLinkOperationResultComposer);
        Assert.Equal((0, 50, WiredLinkController.Create, 1), Codes(refused));
        Assert.Equal((false, "", 0), (refused.Bool(), refused.String(), refused.Int()));
        refused.End();
    }

    [Fact]
    public void LabelPacketsCarryLabelEntriesAndRemovals()
    {
        var full = new HabbiconTestSupport.RecordingPacket();
        new WiredLabelsComposer([new(5, "Hi", 2651759, 0, true)]).Compose(full);
        Assert.Equal(new object[] { 1, 0, 5u, "Hi", 2651759, 0, true }, full.Writes);

        var delta = new HabbiconTestSupport.RecordingPacket();
        new WiredLabelsUpdateComposer([], [5, 6]).Compose(delta);
        Assert.Equal(new object[] { 0, 2, 5u, 6u }, delta.Writes);
        Assert.Equal((1142u, 925u), (new WiredLabelsComposer([]).MessageId, new WiredLabelsUpdateComposer([], []).MessageId));
    }

    [Theory]
    [InlineData(4278912, 0, true, true)]
    [InlineData(5592405, 1, false, true)]
    [InlineData(5592405, 1, true, false)]
    [InlineData(123, 0, false, false)]
    [InlineData(4278912, 2, false, false)]
    public void LabelSettingsAreTheSettingsViewChoices(int color, int visibility, bool alwaysOpen, bool valid) =>
        Assert.Equal(valid, WiredLabel.ValidSettings(color, visibility, alwaysOpen));

    [Fact]
    public void LabelsDefaultToTheUpdateMessageDefaultsAndLeaveWithTheirFurni()
    {
        using var f = new TeleportFixture();
        var store = new MemoryLabelStore();
        store.Saved[61] = new(61, "Exit", 2651759, 0, false);
        f.Room.GetWired().LabelStore = store;
        var plain = LabelFurni(f, 60, 2, 2);
        LabelFurni(f, 61, 2, 1);

        Assert.Equal(new[] { WiredLabel.Default(60), store.Saved[61] }, f.Room.GetWired().Labels());

        var sent = Capture(f.Client);
        f.Room.GetWired().DetachRoomItem(plain);
        var removal = Reply(sent, ServerPacketHeader.WiredLabelsUpdateComposer);
        Assert.Equal((0, 1, 60), (removal.Int(), removal.Int(), removal.Int()));
        Assert.Equal(new[] { store.Saved[61] }, f.Room.GetWired().Labels());
    }

    private static (int Room, int Furni, int Operation, int Result) Codes(WireReader result) => (result.Int(), result.Int(), result.Int(), result.Int());

    [Fact]
    public void LinkResultCodesAreTheOfficialErrorTexts()
    {
        // The client shows ${wiredlink.error.N} for errors 1 to 8.
        Assert.Equal(new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8 }, new[]
        {
            WiredLinkResult.Ok, WiredLinkResult.OwnerOnly, WiredLinkResult.NetworkMissing, WiredLinkResult.InvalidName, WiredLinkResult.DuplicateName,
            WiredLinkResult.AlreadyConnected, WiredLinkResult.InvalidNetworkId, WiredLinkResult.LinkUnavailable, WiredLinkResult.NetworkLimit
        }.Select(result => (int)result));
    }

    private static Item LabelFurni(TeleportFixture f, uint id, int x, int y)
    {
        var item = MakeItem(id, "wf_label");
        item.Definition.InteractionName = WiredLabel.Interaction;

        return Placed(f, item, x, y);
    }

    private sealed class MemoryLabelStore : IWiredLabelStore
    {
        public Dictionary<uint, WiredLabel> Saved { get; } = [];
        public IReadOnlyDictionary<uint, WiredLabel> Load(IReadOnlyCollection<uint> furniIds) =>
            Saved.Where(pair => furniIds.Contains(pair.Key)).ToDictionary();
        public void Save(WiredLabel label) => Saved[label.FurniId] = label;
    }

    private sealed class ControllerLinkStore : IWiredLinkStore
    {
        public (uint Item, uint Network, bool ReceiveLocal) Selected { get; private set; }
        public WiredLinkEndpoint? Endpoint(uint itemId) => new(itemId, 7, true);
        public IReadOnlyList<WiredLinkNetwork> Networks(int ownerId, uint roomId) => ownerId == 1 ? [new(7, "Doors", 1, true)] : [];
        public WiredLinkResult Select(int ownerId, uint itemId, uint roomId, uint networkId, bool receiveLocal)
        {
            Selected = (itemId, networkId, receiveLocal);

            return WiredLinkResult.Ok;
        }
        public IReadOnlyList<WiredLinkConnection> Members(uint networkId) => [];
        public IReadOnlyList<WiredLinkConnection> Connections(int ownerId, uint networkId) => [];
        public WiredLinkResult Create(int ownerId, string name, out uint networkId) => throw new NotSupportedException();
        public WiredLinkResult Rename(int ownerId, uint networkId, string name) => throw new NotSupportedException();
        public WiredLinkResult Delete(int ownerId, uint networkId, out IReadOnlyList<WiredLinkConnection> detached) => throw new NotSupportedException();
    }
}
