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
        floor.SetState(2, 2, 0, []);
        _room.GetRoomItemHandler().LoadFurniture([floor]);
        var module = InspectionInstallModule();

        await ObjectWrite(0, floor.Id, "-110", 0, 0);
        Assert.Equal("0", floor.LegacyDataString);
        await ObjectWrite(0, floor.Id, "-123", 100, 0);
        Assert.Equal(1.0, floor.GetZ, 3);
        Assert.Equal(new[] { "@state", "@altitude" }, module.DrainChanges().Select(change => change.InternalKey));
    }

    [Fact]
    public async Task AirInspectionWriteNeedsModifyRightsAndAMatchingTarget()
    {
        WallSnapshotInstallStore();
        var floor = Furni(302, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        floor.RoomId = RoomId;
        floor.Definition.Modes = 2;
        floor.ExtraData = new LegacyDataFormat { Data = "1" };
        floor.SetState(2, 2, 0, []);
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

    private Task ObjectWrite(int target, uint objectId, string variableId, int value, int operation) =>
        new WiredSetObjectVariableValueEvent(new WiredVariableMenuService()).Parse(_room, _client,
            ClientPacket(target, unchecked((int)objectId), variableId, value, operation));
}
