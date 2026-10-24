using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint MixedBlockId = 80, MixedWalkOnTileId = 90, MixedWalkOffTileId = 91;

    // A stack with Move and Rotate User and Move and Rotate Furni sends one WiredMovements: the furni entry first, then the user.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MoveUserAndMoveFurniShareOneWiredMovementsFurniFirst(bool furniBoxFirst)
    {
        for (var seed = 0; seed < 8; seed++) {
            var (f, rider, packets) = MixedMoveRoom(furniBoxFirst, seed, userBox: true, furniBox: true);

            FireMixed(f, rider);

            var batch = Assert.Single(ReadMovementPackets(packets));
            Assert.Equal([(1, (int)MixedBlockId), (0, rider.VirtualId)], batch.Select(entry => (entry.Type, entry.Id)));
            Assert.Equal((7, 12), (f.Items[MixedBlockId].GetX, f.Items[MixedBlockId].GetY));
            Assert.Equal((5, 11), (rider.X, rider.Y));
        }
    }

    [Fact]
    public void MoveFurniAloneKeepsItsOwnWiredMovements()
    {
        var (f, rider, packets) = MixedMoveRoom(true, 0, userBox: false, furniBox: true);

        FireMixed(f, rider);

        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(1, (int)MixedBlockId)], batch.Select(entry => (entry.Type, entry.Id)));
    }

    [Fact]
    public void MoveUserAloneKeepsItsOwnWiredMovements()
    {
        var (f, rider, packets) = MixedMoveRoom(true, 0, userBox: true, furniBox: false);

        FireMixed(f, rider);

        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(0, rider.VirtualId)], batch.Select(entry => (entry.Type, entry.Id)));
        Assert.Equal((5, 11), (rider.X, rider.Y));
    }

    // The walk triggers of a wired move fire once each, and after the user entry has been sent.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void WalkTriggersAfterAWiredUserMoveFireOnceEach(bool furniBoxFirst, bool withFurniBox)
    {
        for (var seed = 0; seed < 4; seed++) {
            var (f, rider, packets) = MixedMoveRoom(furniBoxFirst, seed, userBox: true, furniBox: withFurniBox, walkTriggers: true);
            var observed = new List<(WiredEventKind Kind, bool UserEntrySent)>();
            f.Engine.ObserveEvent = (evt, _) =>
            {
                if (evt.Kind is WiredEventKind.WalkOn or WiredEventKind.WalkOff) {
                    observed.Add((evt.Kind, ReadMovementPackets(packets).SelectMany(batch => batch).Any(entry => entry.Type == 0)));
                }
            };

            FireMixed(f, rider);

            Assert.Equal(["WalkOff", "WalkOn"], observed.Select(entry => entry.Kind.ToString()).Order());
            Assert.All(observed, entry => Assert.True(entry.UserEntrySent));
            Assert.Equal((5, 11), (rider.X, rider.Y));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ShowMessageChatFollowsTheOneWiredMovementsOfAMixedMove(bool furniBoxFirst)
    {
        for (var seed = 0; seed < 8; seed++) {
            var (f, rider, packets) = MixedMoveRoom(furniBoxFirst, seed, userBox: true, furniBox: true, showMessage: true);

            FireMixed(f, rider);

            Assert.Single(ReadMovementPackets(packets));
            var moved = packets.FindIndex(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.WiredMovementsComposer);
            var chat = packets.FindIndex(bytes => IsChat(bytes) && System.Text.Encoding.UTF8.GetString(bytes).Contains("moved"));
            Assert.InRange(chat, moved + 1, int.MaxValue);
        }
    }

    private static void FireMixed(Fixture f, Plus.HabboHotel.Rooms.RoomUser rider)
    {
        f.Owned(() => rider.OnChat(0, "mx", false));
        f.Advance(500);
    }

    private static (Fixture Fixture, Plus.HabboHotel.Rooms.RoomUser Rider, List<byte[]> Packets) MixedMoveRoom(bool furniBoxFirst, int seed,
        bool userBox, bool furniBox, bool walkTriggers = false, bool showMessage = false)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));
        var moveUser = "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [2, -1],
            FurniSourceTypes = [],
            UserSourceTypes = [0],
            Delay = 0
        });
        var boxes = new List<(string Name, string Json)>();

        if (furniBox) {
            boxes.Add(("wf_act_move_rotate", MoveRotateConfig(5, MixedBlockId)));
        }

        if (userBox) {
            boxes.Add(("wf_act_move_rotate_user", moveUser));
        }

        if (!furniBoxFirst) {
            boxes.Reverse();
        }

        var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
        {
            (MixedBlockId, 6, 12, 0.0, 0, "bc_tile_1", null),
            (83, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("mx"))
        };

        for (var index = 0; index < boxes.Count; index++) {
            layout.Add((84u + (uint)index, 4, 14, 0.65 * (index + 1), 0, boxes[index].Name, boxes[index].Json));
        }

        if (showMessage) {
            layout.Add((89, 4, 14, 0.65 * (boxes.Count + 1), 0, "wf_act_show_message", ShownMessage("moved")));
        }

        if (walkTriggers) {
            layout.Add((MixedWalkOnTileId, 5, 11, 0.0, 0, "bc_tile_1", null));
            layout.Add((MixedWalkOffTileId, 4, 11, 0.0, 0, "bc_tile_1", null));
            layout.Add((95, 8, 16, 0.0, 0, "wf_trg_walks_on_furni", JsonSerializer.Serialize(new WiredConfiguration
            { IntParams = [100], SelectedItems = [MixedWalkOnTileId] })));
            layout.Add((96, 9, 16, 0.0, 0, "wf_trg_walks_off_furni", JsonSerializer.Serialize(new WiredConfiguration
            { IntParams = [100], SelectedItems = [MixedWalkOffTileId] })));
        }

        var f = new Fixture(layout.ToArray(), live: true);
        f.SetEngineCallback("_effectOrderRandom", new Random(seed));
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.Advance(1000);

        return (f, rider, CapturePublication(rider));
    }
}
