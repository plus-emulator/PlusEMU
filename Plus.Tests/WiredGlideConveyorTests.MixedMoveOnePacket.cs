using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.HabboHotel.Items;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
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

    // The walk-on and walk-off stacks of a wired user move run after its WiredMovements, and each fires once.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public void WalkTriggersAfterAWiredUserMoveFireOnceEach(bool furniBoxFirst, bool withFurniBox)
    {
        for (var seed = 0; seed < 4; seed++) {
            var (f, rider, packets) = MixedMoveRoom(furniBoxFirst, seed, userBox: true, furniBox: withFurniBox, walkTriggers: true);

            FireMixed(f, rider);

            Assert.Single(ReadMovementPackets(packets));
            var moved = packets.FindIndex(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.WiredMovementsComposer);

            foreach (var text in new[] { "walked-on", "walked-off" }) {
                var chats = packets.Select((bytes, index) => (bytes, index))
                    .Where(entry => IsChat(entry.bytes) && System.Text.Encoding.UTF8.GetString(entry.bytes).Contains(text)).ToArray();
                Assert.Single(chats);
                Assert.True(chats[0].index > moved, $"{text} after the WiredMovements (seed {seed})");
            }

            Assert.Equal((5, 11), (rider.X, rider.Y));
        }
    }

    // A Move User box that adds no entry leaves the packets of a clicked furni mover as they are without it.
    [Fact]
    public async Task MoveUserBoxWithNoEntryDoesNotChangeTheFurniMovementPackets()
    {
        async Task<List<uint>> Headers(bool noEntryBox)
        {
            var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
            {
                (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
                (MixedBlockId, 6, 12, 0.0, 0, "bc_tile_1", null),
                (ClickStateTrigger, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(ObservedSubjectId)),
                (84, 4, 12, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(5, MixedBlockId))
            };

            if (noEntryBox) {
                layout.Add((85, 4, 12, 1.3, 0, "wf_act_move_rotate_user", MoveUserConfig(-1, -1)));
            }

            layout.Add((86, 4, 12, 1.95, 0, "wf_act_show_message", ShownMessage("moved")));
            var f = new Fixture(layout.ToArray(), live: true);
            f.SetEngineCallback("_effectOrderRandom", new EffectSwapRandom(false));
            var rider = f.Walker(1, 4, 10, 4, 11);
            f.Advance(1000);
            var frames = CaptureFlashFrames(rider);

            await new ClickFurniEvent(new FurnitureUseService(null!, null!)).Parse(f.Room, rider.GetClient()!,
                ClickPacket((int)ObservedSubjectId, 0));
            f.Advance(500);

            return [.. frames.Select(frame => frame.Header)];
        }

        Assert.Equal(await Headers(false), await Headers(true));
    }

    // A clicked stack with User To Furni, Move Furni and Show Message sends the packets in this order.
    [Fact]
    public async Task UserToFurniWithAFurniMoverKeepsItsPacketOrder()
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_user_to_furni", out var descriptor));
        var userToFurni = "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [0],
            PrimaryItems = [new WiredNativeItemReference(MixedWalkOnTileId, false)],
            FurniSourceTypes = [100],
            UserSourceTypes = [0],
            Delay = 0
        });
        var f = new Fixture(
        [
            (ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            (MixedBlockId, 6, 12, 0.0, 0, "bc_tile_1", null),
            (MixedWalkOnTileId, 7, 7, 0.0, 0, "bc_tile_1", null),
            (ClickStateTrigger, 4, 12, 0.0, 0, "wf_trg_click_furni", ObservedClick(ObservedSubjectId)),
            (84, 4, 12, 0.65, 0, "wf_act_user_to_furni", userToFurni),
            (85, 4, 12, 1.3, 0, "wf_act_move_rotate", MoveRotateConfig(5, MixedBlockId)),
            (86, 4, 12, 1.95, 0, "wf_act_show_message", ShownMessage("moved"))
        ], live: true);
        f.SetEngineCallback("_effectOrderRandom", new EffectSwapRandom(false));
        var rider = f.Walker(1, 4, 10, 4, 11);
        f.Advance(1000);
        var frames = CaptureFlashFrames(rider);

        await new ClickFurniEvent(new FurnitureUseService(null!, null!)).Parse(f.Room, rider.GetClient()!,
            ClickPacket((int)ObservedSubjectId, 0));
        f.Advance(500);

        Assert.Equal((7, 7), (rider.X, rider.Y));
        Assert.Equal("3841,3151,5110,1695,5110,1695,2011,2254", string.Join(",", frames.Select(frame => frame.Header)));
    }

    // The turn of a projectile shooter and the turn of a Move User box on the same user are both sent.
    [Fact]
    public void ProjectileTurnAndMoveUserTurnOfOneUserAreBothSent()
    {
        var f = new Fixture(
        [
            (80, 9, 12, 0.0, 0, "wf_colortile", null),
            (81, 9, 10, 0.0, 0, "wf_antenna2", null),
            (83, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("pj")),
            (84, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(7, 80, 81)),
            (85, 4, 14, 1.3, 0, "wf_xtra_rotate_to_dir", null),
            (86, 4, 14, 1.95, 0, "wf_act_move_rotate_user", MoveUserConfig(-1, 9))
        ], live: true);
        f.Items[81].Definition.Directions = [0, 2, 4, 6];
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.Advance(1000);
        rider.RotBody = 2;
        rider.RotHead = 2;
        var packets = CapturePublication(rider);

        FireMixed(f, rider, "pj");

        Assert.Equal(2, ReadMovementPackets(packets).SelectMany(batch => batch).Count(entry => entry.Type == 3 && entry.Id == rider.VirtualId));
    }

    private static string MoveUserConfig(int movement, int rotation)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));

        return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [movement, rotation],
            FurniSourceTypes = [],
            UserSourceTypes = [0],
            Delay = 0
        });
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

    private static void FireMixed(Fixture f, Plus.HabboHotel.Rooms.RoomUser rider, string say = "mx")
    {
        f.Owned(() => rider.OnChat(0, say, false));
        f.Advance(500);
    }

    private static (Fixture Fixture, Plus.HabboHotel.Rooms.RoomUser Rider, List<byte[]> Packets) MixedMoveRoom(bool furniBoxFirst, int seed,
        bool userBox, bool furniBox, bool walkTriggers = false, bool showMessage = false)
    {
        var moveUser = MoveUserConfig(2, -1);
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
            layout.Add((97, 8, 16, 0.65, 0, "wf_act_show_message", ShownMessage("walked-on")));
            layout.Add((96, 9, 16, 0.0, 0, "wf_trg_walks_off_furni", JsonSerializer.Serialize(new WiredConfiguration
            { IntParams = [100], SelectedItems = [MixedWalkOffTileId] })));
            layout.Add((98, 9, 16, 0.65, 0, "wf_act_show_message", ShownMessage("walked-off")));
        }

        var f = new Fixture(layout.ToArray(), live: true);
        f.SetEngineCallback("_effectOrderRandom", new Random(seed));
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.Advance(1000);

        return (f, rider, CapturePublication(rider));
    }
}
