using System.Buffers.Binary;
using System.Text.Json;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredGlideConveyorTests
{
    // Observed r92g r92-move-rotate mrCw-w1 / mrCcw-w1 (AIR WIN63-202610131318-860371703, frames 39269 / 39535): a turn without a move
    // is one ObjectUpdate with the new rotation (cw 0 -> 2, ccw 0 -> 6) and no WiredMovements.
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 6)]
    public void MoveRotateTurnInPlaceIsAnObjectUpdateWithoutWiredMovements(int turn, int rotation)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_move_rotate", out var descriptor));
        var layout = new (uint Id, int X, int Y, double Z, int Rot, string Name, string? Json)[]
        {
            (MoveRotateSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null),
            (MoveRotateBoxId, 4, 12, 0.65, 0, "wf_act_move_rotate", null)
        };
        var f = new Fixture(layout, live: true);
        var rider = f.Walker(1, 4, 10, 4, 11);
        var room = f.MovementContext().Room;
        var subject = f.Items[MoveRotateSubjectId];
        var native = new WiredNativeEditorConfiguration
        {
            Category = descriptor.Category,
            NativeCode = descriptor.EditorCode,
            OwnedIntParams = [0, turn],
            PrimaryItems = [new(MoveRotateSubjectId, false)],
            FurniSourceTypes = [100],
            Delay = 0
        };
        var row = new ModernWiredRuntimeTests.StoredRuntimeRow(MoveRotateBoxId, "wf_act_move_rotate", 2, JsonSerializer.Serialize(native));
        var config = new SavedConfigurations([row]).Load(MoveRotateBoxId, descriptor);
        Assert.NotNull(config);
        var action = new WiredModernAction(room, f.Items[MoveRotateBoxId], descriptor, new(), _ => { }, (_, _, _) => { }, new(), TestLogging.Logger,
            TimeProvider.System, TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty, TestWiredDefinitions.Unused,
            TestItemRuntime.Travel);
        action.ApplyConfiguration(config);
        var headers = new List<uint>();
        rider.GetClient()!.SendCallback = args =>
        {
            headers.Add(BinaryPrimitives.ReadUInt32BigEndian(args.MemoryBuffer.ToArray()));

            return false;
        };

        f.Owned(() => action.Execute(f.MovementContext()));
        f.Advance(100);

        Assert.Equal((9, 9, rotation), (subject.GetX, subject.GetY, subject.Rotation));
        Assert.Contains(ServerPacketHeader.ObjectUpdateComposer, headers);
        Assert.DoesNotContain(ServerPacketHeader.WiredMovementsComposer, headers);
    }
}
