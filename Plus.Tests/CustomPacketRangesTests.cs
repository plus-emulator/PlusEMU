using System.Reflection;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public class CustomPacketRangesTests
{
    // Highest official header of either direction in WIN63-202610121709-785561319 and WIN63-202610131318-860371703
    // (outgoing 4101, ClaimDailyTask); every id above it and below the placeholders is ours.
    private const uint HighestOfficialId = 4101;

    public static TheoryData<string, PacketRangeDirection> Tables => new()
    {
        { nameof(ClientPacketHeader), PacketRangeDirection.ClientToServer },
        { nameof(ServerPacketHeader), PacketRangeDirection.ServerToClient }
    };

    [Theory]
    [MemberData(nameof(Tables))]
    public void EveryPrivateHeaderIdLiesInADeclaredRangeOfItsDirection(string table, PacketRangeDirection direction)
    {
        var undeclared = Headers(table)
            .Where(header => header.Id is > HighestOfficialId and < CustomPacketRanges.PlaceholderStart)
            .Where(header => CustomPacketRanges.Find(direction, header.Id) == null)
            .Select(header => $"{header.Name}={header.Id}");

        Assert.Empty(undeclared);
    }

    [Fact]
    public void RangesOfOneDirectionDoNotOverlapAndHaveUniqueNames()
    {
        Assert.Equal(CustomPacketRanges.All.Count, CustomPacketRanges.All.Select(range => range.Name).Distinct().Count());

        foreach (var group in CustomPacketRanges.All.GroupBy(range => range.Direction)) {
            var ordered = group.OrderBy(range => range.Start).ToArray();

            Assert.All(ordered, range => Assert.True(range.Start <= range.End, range.Name));

            for (var i = 1; i < ordered.Length; i++) {
                Assert.True(ordered[i - 1].End < ordered[i].Start, $"{ordered[i - 1].Name} overlaps {ordered[i].Name}");
            }
        }
    }

    [Fact]
    public void ReservedRendererIdsStayFreeAndInsideAClientRange()
    {
        var incoming = Headers(nameof(ClientPacketHeader)).ToDictionary(header => header.Id, header => header.Name);

        Assert.All(CustomPacketRanges.ReservedClientPackets, packet =>
        {
            Assert.False(incoming.ContainsKey(packet.Id), $"{packet.RendererConstant} {packet.Id} is taken by {incoming.GetValueOrDefault(packet.Id)}");
            Assert.NotNull(CustomPacketRanges.Find(PacketRangeDirection.ClientToServer, packet.Id));
        });
    }

    private static IEnumerable<(string Name, uint Id)> Headers(string table) =>
        (table == nameof(ClientPacketHeader) ? typeof(ClientPacketHeader) : typeof(ServerPacketHeader))
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.IsLiteral)
        .Select(field => (field.Name, (uint)field.GetRawConstantValue()!));
}
