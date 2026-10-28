using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Leaderboard;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// A board's overlay number follows the furni id order of the published boards. A board whose overlay number moves, because another board
// was saved with a lower id or a board left, gets the next revisions after the saved board's own, in overlay order.
public partial class PlacedFurniRoomTests
{
    [Fact]
    public async Task SavingABoardWithALowerIdMovesTheRevisionOfTheBoardItPushesDown()
    {
        var (clock, service, revision) = await PublishedBoard();
        PlaceSavedBox(LowerBoard, "wf_xtra_var_leaderboard", 1, 3, 1);

        await SaveBoardOf(clock, service, 20000, "lower", LowerBoard);

        Assert.Equal([(1, revision + 1, "lower"), (2, revision + 2, "lb probe")], BoardOverlays());
    }

    [Fact]
    public async Task SavingABoardWithAHigherIdLeavesTheOtherBoardsAlone()
    {
        var (clock, service, revision) = await PublishedBoard();
        PlaceSavedBox(HigherBoard, "wf_xtra_var_leaderboard", 3, 3, 1);

        await SaveBoardOf(clock, service, 20000, "higher", HigherBoard);

        Assert.Equal([(1, revision, "lb probe"), (2, revision + 1, "higher")], BoardOverlays());
    }

    [Fact]
    public async Task EveryBoardThatMovesGetsTheNextRevisionInOverlayOrder()
    {
        var (clock, service, revision) = await PublishedBoard();
        PlaceSavedBox(HigherBoard, "wf_xtra_var_leaderboard", 3, 3, 1);
        PlaceSavedBox(LowerBoard, "wf_xtra_var_leaderboard", 1, 3, 1);
        await SaveBoardOf(clock, service, 20000, "higher", HigherBoard);

        await SaveBoardOf(clock, service, 24000, "lower", LowerBoard);

        Assert.Equal([(1, revision + 2, "lower"), (2, revision + 3, "lb probe"), (3, revision + 4, "higher")], BoardOverlays());
    }

    [Fact]
    public async Task SavingTheLowerBoardAgainKeepsTheOtherBoardsRevision()
    {
        var (clock, service, revision) = await PublishedBoard();
        PlaceSavedBox(LowerBoard, "wf_xtra_var_leaderboard", 1, 3, 1);
        await SaveBoardOf(clock, service, 20000, "lower", LowerBoard);
        await SaveBoardOf(clock, service, 24000, "lower", LowerBoard);

        Assert.Equal([(1, revision + 3, "lower"), (2, revision + 2, "lb probe")], BoardOverlays());
    }

    // A board that leaves the room moves the boards behind it up, and they get new revisions in overlay order when the snapshot goes out
    // at the next tick of the push grid.
    [Fact]
    public async Task PickingUpTheLowerBoardMovesTheRevisionOfTheBoardBehindIt()
    {
        var (clock, service, revision) = await PublishedBoard();
        PlaceSavedBox(LowerBoard, "wf_xtra_var_leaderboard", 1, 3, 1);
        await SaveBoardOf(clock, service, 20000, "lower", LowerBoard);
        _client.GetHabbo().Inventory = new InventoryComponent { Furniture = new FurnitureInventoryComponent([], []) };

        clock.Now = BoardOrigin.AddMilliseconds(24000);
        _client.Packets.Clear();
        await PickupObject().Parse(_client, ClientPacket(0, (int)LowerBoard));
        Assert.Null(_room.GetRoomItemHandler().GetItem(LowerBoard));
        Assert.Empty(BoardOverlays());
        CycleUntil(clock, 24000, 24686);
        Assert.Empty(BoardOverlays());

        CycleAt(clock, 24687);
        Assert.Equal([(1, revision + 3, "lb probe")], BoardOverlays());
    }

    private const uint LowerBoard = 1090, HigherBoard = 1190;

    // Overlay, revision and title of every board in the snapshot of the last packet batch.
    private (int Overlay, int Revision, string Title)[] BoardOverlays()
    {
        var snapshots = _client.Packets.Where(packet => packet.Header == ServerPacketHeader.WiredVariableLeaderboardSnapshotComposer).ToArray();

        if (snapshots.Length == 0) {
            return [];
        }

        var packet = snapshots[^1];
        var input = new FlashIncomingPacket { Buffer = packet.Body };
        var boards = new (int, int, string)[input.ReadInt()];

        for (var index = 0; index < boards.Length; index++) {
            var overlay = input.ReadInt();
            var revision = input.ReadInt();
            var title = input.ReadString();
            input.ReadString();

            for (var skip = 0; skip < 5; skip++) {
                input.ReadInt();
            }

            for (var column = input.ReadInt(); column > 0; column--) {
                input.ReadInt();
                input.ReadString();
                input.ReadInt();
            }

            for (var choice = input.ReadInt(); choice > 0; choice--) {
                input.ReadString();
                input.ReadString();
            }

            input.ReadString();
            input.ReadInt();
            boards[index] = (overlay, revision, title);
        }

        return boards;
    }

    private async Task SaveBoardOf(SpeechClock clock, WiredConfigurationService service, int at, string title, uint board = BoardBox)
    {
        int[] ints = [0, 3, 0, 1, 5, -1, 0, 0, 0, 0, 0, 0, 0, 0, 3, 0, 2, 0, 0];
        var text = title + "\t\tName\tValue\tText" + new string('\t', 12);
        clock.Now = BoardOrigin.AddMilliseconds(at);
        _client.Packets.Clear();
        await new SaveWiredAddonConfigEvent(service).Parse(_client, ClientPacket([(int)board, ints.Length, .. ints.Cast<object>(), text, 0, 0, 0,
            WiredLeaderboardConfig.VariableCount, .. Enumerable.Repeat((object)"0", WiredLeaderboardConfig.VariableCount), 0]));
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
    }
}
