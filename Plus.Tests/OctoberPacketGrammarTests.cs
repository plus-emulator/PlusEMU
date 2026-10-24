using System.Collections.Immutable;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.WiredVariables;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public sealed class OctoberPacketGrammarTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RemovalBytesFollowOctoberSourceAndEndImmediatelyAfterManagerFlag(bool wall)
    {
        var bytes = TypedFurnitureStateTests.Payload(wall
            ? new ItemRemoveComposer(7, 0x01020304)
            : new ObjectRemoveComposer(7, false, 0x01020304));
        byte[] expected = wall ? [0, 1, 55, 1, 2, 3, 4, 0]
            : [0, 1, 55, 0, 1, 2, 3, 4, 0, 0, 0, 0, 0];
        Assert.Equal(expected, bytes);
        var packet = new FlashIncomingPacket { Buffer = bytes };
        Assert.Equal("7", packet.ReadString());

        if (!wall) {
            Assert.False(packet.ReadBool());
        }

        Assert.Equal(0x01020304, packet.ReadInt());

        if (!wall) {
            Assert.Equal(0, packet.ReadInt());
        }

        Assert.False(packet.ReadBool());
        Assert.False(packet.HasDataRemaining());
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(0, false, true)]
    [InlineData(1, false, false)]
    [InlineData(1, true, true)]
    [InlineData(2, false, true)]
    [InlineData(2, true, false)]
    public void NativeVariableConsumesExactlyTheSourceConnectorAndFallbackBranches(int connectorKind, bool fallback, bool context)
    {
        ImmutableArray<KeyValuePair<int, string>>? connector = connectorKind switch
        {
            0 => null,
            1 => [],
            _ => [new(-7, "seven"), new(19, "nineteen")]
        };
        var variable = new WiredNativeVariable("ctx:17", 0, "counter", 999, -20,
            false, true, true, false, true, false, false, true, connector)
        {
            RequiresExecutionContext = context,
            TextConnectorFallback = fallback ? "fallback é" : null
        };
        var bytes = Write(packet => variable.Write(packet, false));
        ReadSourceRecord(bytes, "ctx:17", 0, -20, [false, true, true, false, true, false, false, true],
            context, connector?.ToArray(), variable.TextConnectorFallback);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void ScalarVariableWriterUsesTheSameOctoberExecutionAndFallbackOrder(bool connector, bool fallback, bool context)
    {
        var variable = new WiredVariableDescription(new(17, 1, 5, "counter",
            context ? WiredVariableTarget.Context : WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true), true, false)
        {
            TextConnector = connector ? new Dictionary<long, string> { [-7] = "seven", [19] = "nineteen" } : new Dictionary<long, string>(),
            TextConnectorFallback = fallback ? "fallback é" : null
        };
        var bytes = Write(packet => WiredAllVariablesDiffComposer.WriteVariable(packet, variable));
        ReadSourceRecord(bytes, context ? "ctx:17" : "user:17", context ? 3 : 1, context ? 3 : 1,
            [true, !context, true, true, false, false, true, true], false,
            connector ? WiredTextConnector.ToWire(variable.TextConnector).ToArray() : null, variable.TextConnectorFallback);
    }

    [Fact]
    public void ContextVariablesAreNotMarkedAsRequiringAnExecutionContext()
    {
        // Habbo's variable list sends requiresExecutionContext false for all of its variables, context ones included.
        var definition = new WiredVariableDefinition(17, 1, 5, "counter", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true);
        var description = new WiredVariableDescription(definition, true, false);
        var native = WiredNativeCatalogProjection.Project(new(description, definition, null, true, false), null);
        Assert.False(description.RequiresExecutionContext);
        Assert.False(native.RequiresExecutionContext);
        Assert.Null(native.TextConnectorFallback);
    }

    private static byte[] Write(Action<IOutgoingPacket> write)
    {
        using var stream = PlusMemoryStream.GetStream();
        write(new FlashOutgoingPacket(stream));

        return stream.ToArray()[6..];
    }

    private static void ReadSourceRecord(byte[] bytes, string id, int type, int target, bool[] capabilities,
        bool context, KeyValuePair<int, string>[]? connector, string? fallback)
    {
        // Constructor lines 56-64 and getter bindings 141-153 put context before timestamps.
        // Fallback is inside the connector branch at lines 71-81.
        var packet = new FlashIncomingPacket { Buffer = bytes };
        Assert.Equal(id, packet.ReadString());
        Assert.Equal(type, packet.ReadInt());
        Assert.Equal("counter", packet.ReadString());
        Assert.Equal(type == 0 ? 999 : 1, packet.ReadInt());
        Assert.Equal(target, packet.ReadInt());
        Assert.Equal(capabilities[..6], Enumerable.Range(0, 6).Select(_ => packet.ReadBool()));
        Assert.Equal(context, packet.ReadBool());
        Assert.Equal(capabilities[6], packet.ReadBool());
        Assert.Equal(capabilities[7], packet.ReadBool());
        Assert.Equal(connector is not null, packet.ReadBool());

        if (connector is not null) {
            Assert.Equal(connector.Length, packet.ReadInt());

            foreach (var pair in connector) {
                Assert.Equal(pair.Key, packet.ReadInt());
                Assert.Equal(pair.Value, packet.ReadString());
            }

            Assert.Equal(fallback is not null, packet.ReadBool());

            if (fallback is not null) {
                Assert.Equal(fallback, packet.ReadString());
            }
        }

        Assert.False(packet.HasDataRemaining());
    }
}
