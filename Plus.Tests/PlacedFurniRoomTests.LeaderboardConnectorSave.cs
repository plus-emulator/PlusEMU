using System.Reflection;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// A Text Connector save or pickup on the tile of a published Variable Leaderboard sends the room one snapshot with the board's revision
// one higher at the next tick of the 10 s push grid; the client then subscribes to the new revision and gets the rows with the new
// text. A connector on another tile sends nothing.
public partial class PlacedFurniRoomTests
{
    private const uint BoardVariable = 1101, BoardConnector = 1102, BoardBox = 1103, OtherVariable = 1104, OtherConnector = 1105;

    // The grid ticks fall on whole 10 s of the clock; the scenario times below are ms after Origin, so the ticks are at ...4687.
    private static readonly DateTimeOffset BoardOrigin = DateTimeOffset.FromUnixTimeMilliseconds(1_760_000_000_000 - 4687);

    [Fact]
    public async Task AConnectorSaveOnTheBoardTileRepublishesAtTheNextGridTick()
    {
        var (clock, service, revision) = await PublishedBoard();

        foreach (var (saveAt, text, tickAt, chunkAt) in new[] { (16368, "a", 24687, 24832), (28666, "b", 34687, 34828), (40966, "c", 44687, 44838),
                     (53266, "d", 54687, 54863), (65566, "e", 74687, 74846) }) {
            await SaveBoardConnector(clock, service, saveAt, "5=" + text);
            CycleUntil(clock, saveAt, tickAt - 1);
            Assert.Empty(BoardSnapshots());

            CycleAt(clock, tickAt);
            Assert.Equal(++revision, Assert.Single(BoardSnapshots()));
            Assert.Empty(BoardChunks());

            await Resubscribe(clock, chunkAt, revision);
            Assert.Equal((revision, "5", text), Assert.Single(BoardChunks()));
        }
    }

    [Fact]
    public async Task ABurstOfConnectorSavesInOneWindowSendsOneSnapshot()
    {
        var (clock, service, revision) = await PublishedBoard();

        await SaveBoardConnector(clock, service, 77865, "6=x");
        CycleAt(clock, 78000);
        await SaveBoardConnector(clock, service, 78367, "6=y");
        CycleAt(clock, 78500);
        await SaveBoardConnector(clock, service, 78870, "6=z");
        CycleUntil(clock, 78870, 84686);
        Assert.Empty(BoardSnapshots());

        CycleAt(clock, 84687);
        Assert.Equal(revision + 1, Assert.Single(BoardSnapshots()));
        await Resubscribe(clock, 84830, revision + 1);
        Assert.Equal((revision + 1, "5", (string?)null), Assert.Single(BoardChunks()));

        CycleUntil(clock, 84830, 104000);
        Assert.Empty(BoardSnapshots());
    }

    [Fact]
    public async Task ASaveAfterTheTickOfAnEarlierSaveGetsItsOwnSnapshot()
    {
        var (clock, service, revision) = await PublishedBoard();

        // The same burst with the grid tick between the second and the third save, on a grid shifted by 4135 ms.
        await SaveBoardConnector(clock, service, 73787, "6=x");
        await SaveBoardConnector(clock, service, 74314, "6=y");
        CycleAt(clock, 74687);
        Assert.Equal(revision + 1, Assert.Single(BoardSnapshots()));
        await SaveBoardConnector(clock, service, 74764, "6=z");
        CycleUntil(clock, 74764, 84686);
        Assert.Empty(BoardSnapshots());

        CycleAt(clock, 84687);
        Assert.Equal(revision + 2, Assert.Single(BoardSnapshots()));
    }

    [Fact]
    public async Task SavingTheSameConnectorTextAgainStillRepublishes()
    {
        var (clock, service, revision) = await PublishedBoard();
        // Only the last save of the burst before it.
        await SaveBoardConnector(clock, service, 78870, "6=z");
        CycleAt(clock, 84687);
        Assert.Equal(++revision, Assert.Single(BoardSnapshots()));

        await SaveBoardConnector(clock, service, 104363, "6=z");
        CycleUntil(clock, 104363, 104686);
        Assert.Empty(BoardSnapshots());
        CycleAt(clock, 104687);

        Assert.Equal(revision + 1, Assert.Single(BoardSnapshots()));
        await Resubscribe(clock, 104848, revision + 1);
        Assert.Equal((revision + 1, "5", (string?)null), Assert.Single(BoardChunks()));
    }

    [Fact]
    public async Task AConnectorSaveOnAnotherTileSendsNoSnapshot()
    {
        var (clock, service, _) = await PublishedBoard();

        clock.Now = BoardOrigin.AddMilliseconds(119363);
        _client.Packets.Clear();
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket((int)OtherConnector, 0, "1=q", 0, 0, 0, 0, 0));
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        CycleUntil(clock, 119363, 134000);

        Assert.Empty(BoardSnapshots());
        Assert.Empty(BoardChunks());
    }

    [Fact]
    public async Task PickingUpTheBoardTileConnectorRepublishesAtTheNextGridTick()
    {
        var (clock, service, revision) = await PublishedBoard();
        // The board tile's last save before the pickup, then the save on the other tile.
        await SaveBoardConnector(clock, service, 104363, "6=z");
        CycleAt(clock, 104687);
        Assert.Equal(++revision, Assert.Single(BoardSnapshots()));
        clock.Now = BoardOrigin.AddMilliseconds(119363);
        _client.Packets.Clear();
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket((int)OtherConnector, 0, "1=q", 0, 0, 0, 0, 0));
        CycleUntil(clock, 119363, 134000);
        Assert.Empty(BoardSnapshots());
        _client.GetHabbo().Inventory = new InventoryComponent { Furniture = new FurnitureInventoryComponent([], []) };

        clock.Now = BoardOrigin.AddMilliseconds(134000);
        _client.Packets.Clear();
        await PickupObject().Parse(_client, ClientPacket(0, (int)BoardConnector));
        Assert.Null(_room.GetRoomItemHandler().GetItem(BoardConnector));
        Assert.Empty(BoardSnapshots());
        CycleUntil(clock, 134000, 134686);
        Assert.Empty(BoardSnapshots());

        CycleAt(clock, 134687);
        Assert.Equal(revision + 1, Assert.Single(BoardSnapshots()));
        await Resubscribe(clock, 134837, revision + 1);
        Assert.Equal((revision + 1, "5", (string?)null), Assert.Single(BoardChunks()));
    }

    /// <summary>
    /// User variable lbv (permanent) with a Text Connector and a published board on one tile, othv with its own connector on another
    /// tile. The board's own save sends the snapshot at once; the client subscribes and gets no rows. The user then gets 5 in lbv, and the
    /// row comes at the next grid tick.
    /// </summary>
    private async Task<(SpeechClock Clock, WiredConfigurationService Service, int Revision)> PublishedBoard()
    {
        var clock = new SpeechClock { Now = BoardOrigin.AddMilliseconds(1000) };
        var (wired, actor) = PrepareSpeech(clock);
        var module = new WiredVariableModule(RoomId, new BoardDirectory(), new MemoryWiredVariableStore(), clock);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(wired.Variables, module);
        wired.LeaderboardProfiles = new BoardProfiles();
        var service = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(),
            TestLogging.For<WiredConfigurationService>());

        BoardDefinition(BoardVariable, 0, "lbv");
        PlaceSavedBox(BoardConnector, "wf_xtra_var_text_connector", 0, 3, 1);
        PlaceSavedBox(BoardBox, "wf_xtra_var_leaderboard", 0, 3, 2);
        BoardDefinition(OtherVariable, 2, "othv");
        PlaceSavedBox(OtherConnector, "wf_xtra_var_text_connector", 2, 3, 1);

        // The board: Name, Value and Text columns, published by its title.
        int[] ints = [0, 3, 0, 1, 5, -1, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 2, 0, 0];
        var text = "lb probe\t\tName\tValue\tText" + new string('\t', 12);
        clock.Now = BoardOrigin.AddMilliseconds(1396);
        _client.Packets.Clear();
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket([(int)BoardBox, ints.Length, .. ints.Cast<object>(), text, 0, 0, 0,
            WiredLeaderboardConfig.VariableCount, .. Enumerable.Repeat((object)"0", WiredLeaderboardConfig.VariableCount), 0]));
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        var revision = Assert.Single(BoardSnapshots());

        await Resubscribe(clock, 1549, revision);
        Assert.Equal(revision, Assert.Single(EmptyBoardChunks()));

        clock.Now = BoardOrigin.AddMilliseconds(3001);
        var holder = WiredVariableRuntimeFrames.UserHolder(actor);
        Assert.True(module.Mutate(new(WiredVariableTarget.User, $"custom:{BoardVariable}"), holder, WiredVariableMutation.Give, 5,
            new(RoomId, [holder])));
        _client.Packets.Clear();
        CycleUntil(clock, 3001, 4686);
        Assert.Empty(BoardChunks());
        CycleAt(clock, 4687);
        Assert.Equal((revision, "5", (string?)null), Assert.Single(BoardChunks()));
        _client.Packets.Clear();

        return (clock, service, revision);
    }

    private void BoardDefinition(uint id, int x, string name)
    {
        PlaceDefinition(id, "wf_var_user", x);
        var item = Assert.IsType<Item>(_room.GetRoomItemHandler().GetItem(id));
        Assert.True(WiredBoxRegistry.TryGet("wf_var_user", out var descriptor));
        var definition = new WiredVariableDefinitionBox(_room, item, descriptor);
        WiredNativeTestSupport.InstallLegacyVariableDraft(definition, new() { IntParams = [1, 10], Text = name });
        _room.GetWired().Variables.ConfigurationLoaded(definition);
    }

    private async Task SaveBoardConnector(SpeechClock clock, WiredConfigurationService service, int at, string text)
    {
        clock.Now = BoardOrigin.AddMilliseconds(at);
        _client.Packets.Clear();
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket((int)BoardConnector, 0, text, 0, 0, 0, 0, 0));
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        Assert.Empty(BoardSnapshots());
    }

    // The room cycle every 500 ms after the given time up to the last one, then at the last one.
    private void CycleUntil(SpeechClock clock, int from, int last)
    {
        for (var at = from + 500; at < last; at += 500) {
            CycleAt(clock, at);
        }

        CycleAt(clock, last);
    }

    private void CycleAt(SpeechClock clock, int at)
    {
        clock.Now = BoardOrigin.AddMilliseconds(at);
        _room.GetWired().OnCycle();
    }

    // The client's own answer to a snapshot: the board's chunk 0 of its default variable, for the new revision.
    private async Task Resubscribe(SpeechClock clock, int at, int revision)
    {
        clock.Now = BoardOrigin.AddMilliseconds(at);
        var variable = Assert.Single(_room.GetWired().LeaderboardDescriptors()).DefaultVariableId;
        _client.Packets.Clear();
        await new WiredVariableLeaderboardSubscribeEvent().Parse(_client, ClientPacket(1, revision, variable, 1, 0));
    }

    private int[] BoardSnapshots() => [.. _client.Packets.Where(packet => packet.Header == ServerPacketHeader.WiredVariableLeaderboardSnapshotComposer)
        .Select(packet =>
        {
            var input = new FlashIncomingPacket { Buffer = packet.Body };
            Assert.Equal(1, input.ReadInt());
            Assert.Equal(1, input.ReadInt());

            return input.ReadInt();
        })];

    // The revisions of the chunks that hold no rows at all.
    private int[] EmptyBoardChunks() => [.. _client.Packets
        .Where(packet => packet.Header == ServerPacketHeader.WiredVariableLeaderboardChunkComposer).Select(packet =>
        {
            var input = new FlashIncomingPacket { Buffer = packet.Body };
            Assert.Equal(1, input.ReadInt());
            var revision = input.ReadInt();
            input.ReadString();
            Assert.Equal(0, input.ReadInt());
            Assert.Equal(0, input.ReadInt());
            Assert.Equal(0, input.ReadInt());

            return revision;
        })];

    private (int Revision, string Value, string? Text)[] BoardChunks() => [.. _client.Packets
        .Where(packet => packet.Header == ServerPacketHeader.WiredVariableLeaderboardChunkComposer).Select(packet =>
        {
            var input = new FlashIncomingPacket { Buffer = packet.Body };
            Assert.Equal(1, input.ReadInt());
            var revision = input.ReadInt();
            input.ReadString();
            Assert.Equal(0, input.ReadInt());
            Assert.Equal(1, input.ReadInt());
            Assert.Equal(1, input.ReadInt());
            Assert.Equal(1, input.ReadInt());
            Assert.Equal(7, input.ReadInt());
            input.ReadString();
            input.ReadString();
            var value = input.ReadString();

            for (var word = 0; word < 4; word++) {
                input.ReadInt();
            }

            if (input.ReadBool()) {
                input.ReadString();
            }

            return (revision, value, input.ReadBool() ? input.ReadString() : null);
        })];

    private sealed class BoardDirectory : IWiredVariableDirectory
    {
        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => id switch
        {
            BoardVariable => new(id, RoomId, 7, "lbv", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true),
            OtherVariable => new(id, RoomId, 7, "othv", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true),
            _ => null
        };
    }

    private sealed class BoardProfiles : IWiredLeaderboardProfiles
    {
        public IReadOnlyDictionary<long, (string Username, string Figure)> Find(IReadOnlyCollection<long> userIds) =>
            userIds.ToDictionary(id => id, id => ("u" + id, "fig"));
    }
}
