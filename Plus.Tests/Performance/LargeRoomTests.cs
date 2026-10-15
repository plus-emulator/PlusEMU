using System.Diagnostics;
using Plus.Communication.Flash;
using Xunit;
using Xunit.Abstractions;

namespace Plus.Tests.Performance;

public sealed class LargeRoomTests(ITestOutputHelper output)
{
    [Fact]
    public void ThousandBotsCommitAndBroadcastInA1044TileRoom()
    {
        var fixture = RoomPerformanceFixture.Create(1000, 1, 36, 29);
        Assert.Equal(1044, fixture.Map.GameMap.Length);
        var packets = new List<byte[]>();
        fixture.Clients[0].SendCallback = args =>
        {
            packets.Add(args.MemoryBuffer.Span.Slice(args.Offset, args.Count).ToArray());

            return false;
        };

        foreach (var observer in fixture.Users) {
            observer.UpdateNeeded = false;
        }

        var samples = new List<double>();

        for (var cycle = 0; cycle < 30; cycle++) {
            packets.Clear();
            var beforeX = fixture.Bots[0].X;
            fixture.PrepareNextWalkingStep();
            var started = Stopwatch.GetTimestamp();
            fixture.Manager.OnCycle();
            fixture.Manager.OnCycle();
            fixture.Manager.SerializeStatusUpdates();
            samples.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);

            Assert.All(fixture.Bots, bot => Assert.NotEqual(beforeX, bot.X));
            Assert.Equal(1000, fixture.Map.GetRoomUsers(fixture.Bots[0].Coordinate).Count(user => user.IsBot));
            Assert.All(fixture.Bots, bot => Assert.False(bot.UpdateNeeded));
            var packet = new FlashIncomingPacket { Buffer = packets.Last()[6..] };
            Assert.Equal(1000, packet.ReadInt());
            var ids = new HashSet<int>();

            for (var i = 0; i < 1000; i++) {
                Assert.True(ids.Add(packet.ReadInt()));
                packet.ReadInt(); // x
                packet.ReadInt(); // y
                packet.ReadString(); // z
                packet.ReadInt(); // head
                packet.ReadInt(); // body
                packet.ReadInt(); // AIR 202610131318 entry int before statuses
                packet.ReadString(); // statuses
            }
        }

        samples.Sort();
        output.WriteLine($"1044 tiles / 1000 bots: two movement cycles + status broadcast; p50={samples[15]:F3}ms, p95={samples[28]:F3}ms. In-memory transport, precomputed paths, no AI or sockets.");
    }
}
