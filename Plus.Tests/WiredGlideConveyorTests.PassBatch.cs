using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // One say fires stack A (toggle five furni, show a message) and stack B (toggle two furni, move two furni). The tick sends
    // one ObjectsDataUpdate with all seven states and all six box flashes, then one WiredMovements with both furni.
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

    // The ObjectsDataUpdate and the WiredMovements of one firing are adjacent. A Show Message chat comes before the data update
    // or after the WiredMovements, never between them.
    [Fact]
    public void SayStackThatTogglesMovesAndShowsAMessageSendsTheChatAfterTheWiredMovements()
    {
        var f = new Fixture(
        [
            (5, 6, 8, 0.0, 0, "wf_colortile", null),
            (20, 9, 12, 0.0, 0, "bc_tile_1", null),
            (30, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("bx")),
            (31, 4, 14, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [5], 100)),
            (32, 4, 14, 1.3, 0, "wf_act_move_rotate", MoveRotateConfig(5, 20)),
            (33, 4, 14, 1.95, 0, "wf_act_show_message", ShownMessage("r115 c"))
        ], live: true);
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.Advance(1000);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "bx", false));
        f.Advance(200);

        var message = System.Text.Encoding.UTF8.GetBytes("r115 c");
        var order = packets.Select(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) switch
        {
            ServerPacketHeader.ObjectsDataUpdateComposer => "data",
            ServerPacketHeader.WiredMovementsComposer => "movements",
            _ when bytes.AsSpan().IndexOf(message) >= 0 => "message",
            _ => null
        }).OfType<string>();
        Assert.Equal(["data", "movements", "message"], order);
    }

    // A toggled furni whose height depends on its state sends its own ObjectUpdate. When an earlier stack of the same say has moved furni,
    // that stack's WiredMovements goes out first.
    [Fact]
    public void ToggleOfAFurniWithStateHeightsSendsTheEarlierStacksWiredMovementsFirst()
    {
        const uint block = 51;
        var f = new Fixture(
        [
            (block, 6, 8, 0.0, 0, "bc_block_0_recolorable", null),
            (20, 9, 12, 0.0, 0, "bc_tile_1", null),
            (40, 5, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("bx")),
            (41, 5, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(5, 20)),
            (30, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("bx")),
            (31, 4, 14, 0.65, 0, "wf_act_toggle_state", ObservedNative("wf_act_toggle_state", [0], [block], 100))
        ], live: true);

        // As the furniture table defines it: five states, 1.0 high at state 0 and 0.25 lower per state.
        var definition = f.Items[block].Definition;
        definition.Height = 1.0;
        definition.Modes = 5;
        definition.AdjustableHeights = [1.0, 0.75, 0.5, 0.25, 0.0];
        definition.InteractionName = "recolorable";
        definition.InteractionType = Plus.HabboHotel.Items.InteractionType.Recolorable;
        f.Items[block].ExtraData = new Plus.HabboHotel.Items.DataFormat.RecolorableDataFormat();

        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        // Runs the move stack (40) before the toggle stack (30).
        f.SetEngineCallback("_effectOrderRandom", new StackOrderRandom(true));
        f.Advance(1000);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "bx", false));
        f.Advance(200);

        var frames = packets.Select(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes))
            .Where(header => header is ServerPacketHeader.WiredMovementsComposer or ServerPacketHeader.ObjectUpdateComposer);
        Assert.Equal([ServerPacketHeader.WiredMovementsComposer, ServerPacketHeader.ObjectUpdateComposer], frames);
        Assert.Equal("1", f.Items[block].LegacyDataString);
    }

    // One Move and Rotate User box whose selector picks two users sends one WiredMovements with both user moves.
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

    // With Movement Down and Rotation Clockwise, one box that picks two users sends one WiredMovements with four entries: both
    // moves, then both turns.
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
