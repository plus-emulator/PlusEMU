using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    private const uint SpeakerId = 1;

    // The moves of all Move/Rotate boxes of one firing go out in one WiredMovements. Two boxes move two blocks into the same
    // tile; the block that moves second still lands on top.
    [Fact]
    public void MoveRotateBoxesOfOneFiringShareOneWiredMovements()
    {
        var f = new Fixture(
        [
            (80, 6, 12, 0.0, 0, "bc_tile_1", null),
            (81, 8, 12, 0.0, 0, "bc_tile_1", null),
            (83, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("mv")),
            (84, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(5, 80)),
            (85, 4, 14, 1.3, 0, "wf_act_move_rotate", MoveRotateConfig(7, 81))
        ], live: true);
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.Advance(1000);
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "mv", false));
        f.Advance(200);

        // The boxes of one firing run in a random order, so either block can end on top.
        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(1, 80, 6, 7), (1, 81, 8, 7)], batch.Select(entry => (entry.Type, entry.Id, entry.FromX, entry.ToX)).Order());
        Assert.All(batch, entry => Assert.Equal(f.Items[(uint)entry.Id].GetZ.ToString("0.0", CultureInfo.InvariantCulture), entry.ToZ));
        Assert.Equal([(7, 12, 0.0), (7, 12, 1.0)], new[] { f.Items[80], f.Items[81] }.Select(item => (item.GetX, item.GetY, item.GetZ)).Order());
    }

    // With the projectile add-on, one firing sends one WiredMovements with every flying furni, and the shooter's direction is the
    // last entry of that packet.
    [Fact]
    public void ProjectileMovesAndTheShooterDirectionShareOneWiredMovements()
    {
        var f = new Fixture(
        [
            (80, 9, 12, 0.0, 0, "wf_colortile", null),
            (81, 9, 10, 0.0, 0, "wf_antenna2", null),
            (83, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("pj")),
            (84, 4, 14, 0.65, 0, "wf_act_move_rotate", MoveRotateConfig(7, 80, 81)),
            (85, 4, 14, 1.3, 0, "wf_xtra_rotate_to_dir", null)
        ], live: true);
        f.Items[81].Definition.Directions = [0, 2, 4, 6];
        var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
        f.Advance(1000);
        rider.RotBody = 2;
        rider.RotHead = 2;
        var packets = CapturePublication(rider);

        f.Owned(() => rider.OnChat(0, "pj", false));
        f.Advance(200);

        var batch = Assert.Single(ReadMovementPackets(packets));
        Assert.Equal([(1, 80, 9, 8), (1, 81, 9, 8)], batch[..^1].Select(entry => (entry.Type, entry.Id, entry.FromX, entry.ToX)).Order());
        Assert.Equal((3, rider.VirtualId, 6, 6), (batch[^1].Type, batch[^1].Id, batch[^1].FromX, batch[^1].ToX));
        Assert.Equal((8, 12), (f.Items[80].GetX, f.Items[80].GetY));
        Assert.Equal((8, 10), (f.Items[81].GetX, f.Items[81].GetY));
    }

    private static string SaysConfig(string text) => JsonSerializer.Serialize(new WiredConfiguration { IntParams = [1, 0, 0], Text = text });

    // The editor's movement option (5 moves east, 7 moves west) with no rotation, on the picked furni.
    private static string MoveRotateConfig(int movement, params uint[] items)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate", out var descriptor));

        return "native:" + JsonSerializer.Serialize(new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [movement, 0],
            PrimaryItems = [.. items.Select(id => new WiredNativeItemReference(id, false))],
            FurniSourceTypes = [100],
            Delay = 0
        });
    }

    // One entry: its type, the furni or avatar id, and for furni the x values and the landing z. A direction entry (type 3)
    // reports its body and head direction in place of the x values.
    private sealed record PacketEntry(int Type, int Id, int FromX, int ToX, string ToZ);

    private static List<PacketEntry[]> ReadMovementPackets(IEnumerable<byte[]> packets)
    {
        var result = new List<PacketEntry[]>();

        foreach (var bytes in packets.Where(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.WiredMovementsComposer)) {
            var position = 6;
            int Int()
            {
                var value = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(position));
                position += 4;

                return value;
            }
            string String()
            {
                var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan(position));
                var value = System.Text.Encoding.UTF8.GetString(bytes, position + 2, length);
                position += 2 + length;

                return value;
            }
            bool Bool() => bytes[position++] != 0;
            var entries = new List<PacketEntry>();

            for (var count = Int(); count > 0; count--) {
                var type = Int();

                if (type == 3) {
                    entries.Add(new(type, Int(), Int(), Int(), ""));
                    continue;
                }

                Assert.InRange(type, 0, 1);
                var fromX = Int();
                _ = Int();
                var toX = Int();
                _ = Int();
                _ = String();
                var toZ = String();
                var id = Int();

                if (type == 0) {
                    _ = Int();
                }

                _ = Int();
                _ = Int();

                if (type == 0) {
                    _ = Int();
                }

                if (Bool()) {
                    _ = Int();
                }

                if (type == 1 && Bool()) {
                    _ = Int();
                }

                entries.Add(new(type, id, fromX, toX, toZ));
            }

            Assert.Equal(bytes.Length, position);
            result.Add([.. entries]);
        }

        return result;
    }
}
