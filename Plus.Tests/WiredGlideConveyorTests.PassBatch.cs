using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540: one say fires stack A (toggle five furni, show a message) and stack B (toggle two
    // furni, move two furni). The tick sends one ObjectsDataUpdate with all seven states and all six box flashes, then one
    // WiredMovements with both furni.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TwoStacksOfOneSaySendAllStatesAndFlashesInOneObjectsDataUpdateBeforeTheWiredMovements(bool secondStackFirst)
    {
        uint[] stackA = [5, 6, 7, 8, 9, 30, 31, 32];
        uint[] toggledByA = [5, 6, 7, 8, 9];
        uint[] toggledByB = [10, 11];
        var f = new Fixture(
        [
            (5, 6, 8, 0.0, 0, "wf_colortile", null),
            (6, 7, 8, 0.0, 0, "wf_colortile", null),
            (7, 8, 8, 0.0, 0, "wf_colortile", null),
            (8, 9, 8, 0.0, 0, "wf_colortile", null),
            (9, 10, 8, 0.0, 0, "wf_colortile", null),
            (10, 6, 9, 0.0, 0, "wf_colortile", null),
            (11, 7, 9, 0.0, 0, "wf_colortile", null),
            (20, 9, 12, 0.0, 0, "bc_tile_1", null),
            (21, 10, 12, 0.0, 0, "bc_tile_1", null),
            (30, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("bx")),
            (31, 4, 14, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], toggledByA, 100)),
            (32, 4, 14, 1.3, 0, "wf_act_show_message", ShownMessage("r115 a")),
            (40, 5, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("bx")),
            (41, 5, 14, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], toggledByB, 100)),
            (42, 5, 14, 1.3, 0, "wf_act_move_rotate", MoveRotateConfig(5, 20, 21))
        ], live: true);
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.SetEngineCallback("_effectOrderRandom", new StackOrderRandom(secondStackFirst));
        f.Advance(1000);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "bx", false));
        f.Advance(200);

        var frames = packets.Where(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes)
            is ServerPacketHeader.ObjectsDataUpdateComposer or ServerPacketHeader.WiredMovementsComposer).ToArray();
        Assert.Equal([ServerPacketHeader.ObjectsDataUpdateComposer, ServerPacketHeader.WiredMovementsComposer],
            frames.Select(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes)));
        var listed = ReadBatch((ServerPacketHeader.ObjectsDataUpdateComposer, frames[0][6..])).Select(entry => entry.Id).ToHashSet();
        Assert.Superset(new HashSet<uint>([.. stackA, 10, 11, 40, 41, 42]), listed);
        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(20, 9, 10), (21, 10, 11)], batch.Select(entry => (entry.Id, entry.FromX, entry.ToX)).Order());
    }

    // Observed on AIR WIN63-202610201032-107964540: one Move and Rotate User box whose selector picks two users sends one
    // WiredMovements with both user moves (5 of 5 firings).
    [Fact]
    public void MoveUserBoxMovingTwoUsersSendsOneWiredMovements()
    {
        var (f, rider, other) = TwoUserMoveRoom(movement: 4, rotation: -1);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "push", false));
        f.Advance(200);

        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(0, rider.VirtualId, 4, 4), (0, other.VirtualId, 7, 7)], batch.Select(entry => (entry.Type, entry.Id, entry.FromX, entry.ToX)));
        Assert.Equal([(4, 12), (7, 12)], new[] { rider, other }.Select(user => (user.X, user.Y)));
    }

    // Observed on AIR WIN63-202610201032-107964540: with Movement Down and Rotation Clockwise, one box that picks two users sends one
    // WiredMovements with four entries: both moves, then both turns.
    [Fact]
    public void MoveAndRotateUserBoxSendsBothMovesThenBothTurnsInOneWiredMovements()
    {
        var (f, rider, other) = TwoUserMoveRoom(movement: 4, rotation: 9);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "push", false));
        f.Advance(200);

        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(0, rider.VirtualId), (0, other.VirtualId), (3, rider.VirtualId), (3, other.VirtualId)],
            batch.Select(entry => (entry.Type, entry.Id)));
        Assert.Equal([(1, 1), (3, 3)], batch[2..].Select(entry => (entry.FromX, entry.ToX)));
    }

    private static (Fixture Fixture, Plus.HabboHotel.Rooms.RoomUser Rider, Plus.HabboHotel.Rooms.RoomUser Other) TwoUserMoveRoom(int movement,
        int rotation)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [movement, rotation],
            FurniSourceTypes = [],
            UserSourceTypes = [WiredSources.AllRoom],
            Delay = 0
        };
        var f = new Fixture(
        [
            (21, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("push")),
            (22, 4, 14, 0.65, 0, "wf_act_move_rotate_user", "native:" + JsonSerializer.Serialize(native))
        ], live: true);
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        var other = f.Walker(2, 7, 10, 7, 11);
        rider.RotBody = rider.RotHead = 0;
        other.RotBody = other.RotHead = 2;
        f.Advance(1000);

        return (f, rider, other);
    }

    private static string ShownMessage(string text) => "native:" + JsonSerializer.Serialize(new
    {
        Version = 2,
        Category = 1,
        NativeCode = 7,
        OwnedIntParams = new[] { 0, 34, -1 },
        Text = text,
        PrimaryItems = Array.Empty<int>(),
        SecondaryItems = Array.Empty<int>(),
        FurniSourceTypes = Array.Empty<int>(),
        UserSourceTypes = new[] { 0 },
        VariableIds = Array.Empty<int>(),
        Delay = 0
    });
}
