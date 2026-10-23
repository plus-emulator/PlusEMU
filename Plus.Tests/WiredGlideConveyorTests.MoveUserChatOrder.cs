using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed on AIR WIN63-202610201032-107964540: a stack with Move and Rotate User and Show Message sends the data update, then the
    // WiredMovements, then the chat, whichever box comes first and with or without Execute in Order (75 of 96 firings; the other 21 had the
    // chat in the same millisecond, before the movement).
    [Theory]
    [InlineData(2, -1, true, false)]
    [InlineData(2, -1, false, false)]
    [InlineData(2, -1, true, true)]
    [InlineData(2, -1, false, true)]
    [InlineData(-1, 9, true, false)]
    [InlineData(-1, 9, false, false)]
    [InlineData(-1, 9, true, true)]
    [InlineData(-1, 9, false, true)]
    public void MoveRotateUserSendsItsWiredMovementsBeforeTheShowMessageChat(int movement, int rotation, bool showFirst, bool inOrder)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate_user", out var descriptor));
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [movement, rotation],
            FurniSourceTypes = [],
            UserSourceTypes = [0],
            Delay = 0
        };
        var move = "native:" + JsonSerializer.Serialize(native);

        for (var seed = 0; seed < (inOrder ? 1 : 8); seed++) {
            var layout = new List<(uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)>
            {
                (30, 4, 14, 0.0, 0, "wf_trg_says_something", SaysConfig("bx")),
                (31, 4, 14, 0.65, 0, showFirst ? "wf_act_show_message" : "wf_act_move_rotate_user", showFirst ? ShownMessage("moved") : move),
                (32, 4, 14, 1.3, 0, showFirst ? "wf_act_move_rotate_user" : "wf_act_show_message", showFirst ? move : ShownMessage("moved"))
            };

            if (inOrder) {
                layout.Add((33, 4, 14, 1.95, 0, "wf_xtra_exec_in_order", JsonSerializer.Serialize(new WiredConfiguration())));
            }

            var f = new Fixture(layout.ToArray(), live: true);
            f.SetEngineCallback("_effectOrderRandom", new Random(seed));
            var rider = f.Walker((int)SpeakerId, 4, 10, 4, 11);
            f.Advance(1000);
            var packets = CapturePublication(rider);

            f.Owned(() => rider.OnChat(0, "bx", false));
            f.Advance(200);

            var moved = packets.FindIndex(bytes => BinaryPrimitives.ReadUInt32BigEndian(bytes) == ServerPacketHeader.WiredMovementsComposer);
            var chat = packets.FindIndex(bytes => IsChat(bytes) && System.Text.Encoding.UTF8.GetString(bytes).Contains("moved"));
            Assert.InRange(moved, 0, int.MaxValue);
            Assert.InRange(chat, 0, int.MaxValue);
            Assert.True(moved < chat, $"WiredMovements before the chat (seed {seed})");
        }
    }
}
