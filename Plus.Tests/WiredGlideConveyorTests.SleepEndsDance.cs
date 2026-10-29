using System.Buffers.Binary;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Avatar;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

// The sleep expression ends a dance with a Dance 0 packet before the Expression packet, and sends Sleep true only when the avatar is not asleep yet.
public sealed partial class WiredGlideConveyorTests
{
    [Fact]
    public void TheSleepExpressionEndsADanceWithDanceZeroBeforeTheExpressionAndSleepTrue()
    {
        var room = new ExpressionRoom();
        room.Dance(1);
        room.Wait(1000);
        Assert.False(room.Owner.IsAsleep);
        room.Frames.Clear();

        room.Express(5);

        Assert.Equal([ServerPacketHeader.DanceComposer, ServerPacketHeader.ActionComposer, ServerPacketHeader.SleepComposer],
            room.Frames.Select(frame => frame.Header).ToArray());
        Assert.Equal(room.Owner.VirtualId, BinaryPrimitives.ReadInt32BigEndian(room.Frames[0].Payload));
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(room.Frames[0].Payload.AsSpan(4)));
        Assert.Equal(1, room.Frames[2].Payload[4]);
        Assert.Equal(0, room.Owner.DanceId);
        Assert.True(room.Owner.IsAsleep);
    }

    [Fact]
    public void TheSleepExpressionOfAnAvatarThatDoesNotDanceSendsNoDancePacket()
    {
        var room = new ExpressionRoom();

        room.Express(5);

        Assert.Equal([ServerPacketHeader.ActionComposer, ServerPacketHeader.SleepComposer], room.Frames.Select(frame => frame.Header).ToArray());
    }

    [Fact]
    public void TheSleepExpressionOfASleepingAvatarSendsTheExpressionAndNoSecondSleepPacket()
    {
        var room = new ExpressionRoom();
        room.Express(5);
        room.Wait(2000);
        Assert.True(room.Owner.IsAsleep);
        room.Frames.Clear();

        room.Express(5);
        room.Wait(1000);

        Assert.Equal([ServerPacketHeader.ActionComposer], room.Frames.Select(frame => frame.Header).ToArray());
        Assert.True(room.Owner.IsAsleep);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(8)]
    public void EveryOtherExpressionEndsADanceWithDanceZeroBeforeTheExpression(int expression)
    {
        var room = new ExpressionRoom();
        room.Dance(1);
        room.Wait(1000);
        room.Frames.Clear();

        room.Express(expression);

        Assert.Equal([ServerPacketHeader.DanceComposer, ServerPacketHeader.ActionComposer], room.Frames.Select(frame => frame.Header).ToArray());
        Assert.Equal(0, BinaryPrimitives.ReadInt32BigEndian(room.Frames[0].Payload.AsSpan(4)));
        Assert.Equal(expression, BinaryPrimitives.ReadInt32BigEndian(room.Frames[1].Payload.AsSpan(4)));
        Assert.Equal(0, room.Owner.DanceId);
    }

    [Fact]
    public void AnExpressionOfAnAvatarThatDoesNotDanceSendsNoDancePacket()
    {
        var room = new ExpressionRoom();

        room.Express(1);

        Assert.Equal([ServerPacketHeader.ActionComposer], room.Frames.Select(frame => frame.Header).ToArray());
    }

    private sealed class ExpressionRoom
    {
        private readonly Fixture _fixture;
        private readonly RoomAvatarActionService _actions = new(TimeProvider.System,
            CatalogSnapshotTestSupport.Proxy<IQuestManager>((_, _) => null), TestNavigationRewards.Instance);

        public RoomUser Owner { get; }
        public List<(uint Header, byte[] Payload)> Frames { get; }

        public ExpressionRoom()
        {
            _fixture = new Fixture([(ObservedSubjectId, 9, 9, 0.0, 0, "bc_tile_1", null)], live: true);
            Owner = _fixture.Walker(1, 6, 10, 6, 11);
            _fixture.Advance(1000);
            Frames = CaptureFlashFrames(Owner);
        }

        public void Express(int expression) => _fixture.Owned(() =>
            new ActionEvent(_actions).Parse(_fixture.Room, Owner.GetClient()!, IntPacket(expression)).GetAwaiter().GetResult());

        public void Dance(int dance) => _fixture.Owned(() =>
            new DanceEvent(_actions).Parse(_fixture.Room, Owner.GetClient()!, IntPacket(dance)).GetAwaiter().GetResult());

        public void Wait(int milliseconds) => _fixture.Advance(milliseconds);

        private static FlashIncomingPacket IntPacket(int value)
        {
            var body = new byte[4];
            BinaryPrimitives.WriteInt32BigEndian(body, value);

            return new() { Buffer = body };
        }
    }
}
