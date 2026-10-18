using Plus.Communication.Packets.Incoming.WiredVariables;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // The AIR inspection tab writes a furni's @state (-110) and @altitude (-123) through class_3770 (AIR WIN63-202610131318-860371703
    // WiredMenuInspectionTab.as:193): target 0 (furni), the furni id, the variable id, the value and operation 0 (set).
    [Fact]
    public async Task AirInspectionWriteSetsFloorStateAndAltitude()
    {
        WallSnapshotInstallStore();
        var floor = Furni(302, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        floor.RoomId = RoomId;
        floor.Definition.Modes = 2;
        floor.ExtraData = new LegacyDataFormat { Data = "1" };
        floor.SetState(2, 2, 0);
        _room.GetRoomItemHandler().LoadFurniture([floor]);
        var module = InspectionInstallModule();

        await ObjectWrite(0, floor.Id, "-110", 0, 0);
        Assert.Equal("0", floor.LegacyDataString);
        await ObjectWrite(0, floor.Id, "-123", 100, 0);
        Assert.Equal(1.0, floor.GetZ, 3);
        Assert.Equal(new[] { "@state", "@altitude" }, module.DrainChanges().Select(change => change.InternalKey));
    }

    // A furni placed from the inventory can have no state text yet (wf_floor_switch1 on our hotel); it reads 0 and takes a write.
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task AirInspectionWriteSetsTheStateOfAFurniWithNoStateYet(int value)
    {
        WallSnapshotInstallStore();
        var floor = Furni(302, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        floor.RoomId = RoomId;
        floor.Definition.Modes = 3;
        floor.ExtraData = new LegacyDataFormat { Data = "" };
        floor.SetState(2, 2, 0);
        _room.GetRoomItemHandler().LoadFurniture([floor]);
        var module = InspectionInstallModule();

        await ObjectWrite(0, floor.Id, "-110", value, 0);
        Assert.Equal(value.ToString(), floor.LegacyDataString);
        Assert.Equal(new[] { "@state" }, module.DrainChanges().Select(change => change.InternalKey));
    }

    [Fact]
    public async Task AirInspectionWriteNeedsModifyRightsAndAMatchingTarget()
    {
        WallSnapshotInstallStore();
        var floor = Furni(302, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        floor.RoomId = RoomId;
        floor.Definition.Modes = 2;
        floor.ExtraData = new LegacyDataFormat { Data = "1" };
        floor.SetState(2, 2, 0);
        _room.GetRoomItemHandler().LoadFurniture([floor]);
        var module = InspectionInstallModule();

        await ObjectWrite(1, floor.Id, "-110", 0, 0); // a user target naming a furni variable
        await ObjectWrite(0, floor.Id, "-110", 0, 3); // no such operation
        _client.GetHabbo().Username = "visitor";
        await ObjectWrite(0, floor.Id, "-110", 0, 0);

        Assert.Equal("1", floor.LegacyDataString);
        Assert.Empty(module.DrainChanges());
    }

    [Fact]
    public void AirInspectionWriteDecodesTheFullFrameOnly()
    {
        Assert.True(WiredSetObjectVariableValueEvent.TryRead(ClientPacket(0, 302, "-110", 1, 0), out var request));
        Assert.Equal(new WiredObjectVariableWrite(0, 302, "-110", 1, 0), request);
        Assert.False(WiredSetObjectVariableValueEvent.TryRead(ClientPacket(0, 302, "-110", 1, 0, 9), out _));
        Assert.False(WiredSetObjectVariableValueEvent.TryRead(ClientPacket(0, 302, "", 1, 0), out _));
        Assert.False(WiredSetObjectVariableValueEvent.TryRead(ClientPacket(0, 302, "-110", 1), out _));
    }

    // The AIR inspection tab reads one object through class_3018 [type, id] and gets WiredObjectInspectionData back (AIR
    // WIN63-202610151745-318700621 package_121/WiredObjectInspectionData.as:25-64): type, id, id/value pairs, and for a furni the boxes
    // picking it. Observed on hhus: a furni reply is (0, item id, values, []).
    [Fact]
    public async Task AirInspectionReadListsAFurnisValuesInTheAirLayout()
    {
        WallSnapshotInstallStore();
        var floor = Furni(302, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        floor.RoomId = RoomId;
        floor.Definition.Modes = 2;
        floor.ExtraData = new LegacyDataFormat { Data = "1" };
        floor.SetState(2, 2, 0);
        _room.GetRoomItemHandler().LoadFurniture([floor]);
        InspectionInstallModule();
        _client.Packets.Clear();

        await new WiredGetVariablesForObjectEvent(new WiredVariableMenuService()).Parse(_room, _client, ClientPacket(0, 302));

        var reply = Assert.Single(_client.Packets, packet => packet.Header == Plus.Communication.Packets.Outgoing.ServerPacketHeader.WiredVariablesForObjectComposer);
        var input = new Plus.Communication.Flash.FlashIncomingPacket { Buffer = reply.Body };
        Assert.Equal((0, 302), (input.ReadInt(), input.ReadInt()));
        var values = Enumerable.Range(0, input.ReadInt()).Select(_ => (input.ReadString(), input.ReadInt())).ToDictionary();
        Assert.Equal(1, values["-110"]);
        Assert.Equal(0, input.ReadInt());
        Assert.False(input.HasDataRemaining());
        Assert.True(WiredGetVariablesForObjectEvent.TryRead(ClientPacket(-10, 0), out var type, out _) && type == -10);
        Assert.False(WiredGetVariablesForObjectEvent.TryRead(ClientPacket(2, 302), out _, out _));
        Assert.False(WiredGetVariablesForObjectEvent.TryRead(ClientPacket(0, 302, 1), out _, out _));
    }

    private Task ObjectWrite(int target, uint objectId, string variableId, int value, int operation) =>
        new WiredSetObjectVariableValueEvent(new WiredVariableMenuService()).Parse(_room, _client,
            ClientPacket(target, unchecked((int)objectId), variableId, value, operation));
}
