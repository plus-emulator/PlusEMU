using Plus.Communication.Packets.Incoming.WiredVariables;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // The inspection tab writes a furni's @state (-110) and @altitude (-123): target 0 (furni), the furni id, the variable id, the value and operation 0 (set).
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

    // A furni placed from the inventory can have no state text yet; it reads 0 and takes a write.
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

    // An inspection write of @state leaves a Wired box (trigger, action, condition, selector, add-on, variable box or antenna) unchanged and shows no error, while a Wired floor switch still changes.
    [Theory]
    [InlineData("wf_act_move_rotate", InteractionType.WiredEffect, "")]
    [InlineData("wf_trg_says_something", InteractionType.WiredTrigger, "")]
    [InlineData("wf_cnd_var_val_match", InteractionType.WiredCondition, "")]
    [InlineData("wf_slc_furni_picks", InteractionType.WiredSelector, "")]
    [InlineData("wf_xtra_random", InteractionType.WiredAddon, "")]
    [InlineData("wf_var_furni", InteractionType.WiredVariable, "")]
    [InlineData("wf_antenna2", InteractionType.None, "antenna")]
    public async Task AirInspectionWriteLeavesTheStateOfAWiredBox(string name, InteractionType interaction, string interactionName)
    {
        WallSnapshotInstallStore();
        var box = Furni(303, interaction, WiredBoxType.None);
        box.RoomId = RoomId;
        box.Definition.ItemName = name;
        box.Definition.InteractionName = interactionName.Length > 0 ? interactionName : name;
        box.Definition.Modes = 2;
        box.ExtraData = new LegacyDataFormat { Data = "0" };
        box.SetState(3, 3, 0);
        var floor = Furni(302, InteractionType.WfFloorSwitch1, WiredBoxType.None);
        floor.RoomId = RoomId;
        floor.Definition.Modes = 2;
        floor.ExtraData = new LegacyDataFormat { Data = "0" };
        floor.SetState(2, 2, 0);
        _room.GetRoomItemHandler().LoadFurniture([box, floor]);
        var module = InspectionInstallModule();
        _client.Packets.Clear();

        await ObjectWrite(0, box.Id, "-110", 1, 0);

        Assert.Equal("0", box.LegacyDataString);
        Assert.Empty(module.DrainChanges());
        Assert.DoesNotContain(_client.Packets, packet => packet.Header is ServerPacketHeader.ObjectUpdateComposer
            or ServerPacketHeader.ObjectDataUpdateComposer or ServerPacketHeader.ObjectsDataUpdateComposer);

        await ObjectWrite(0, floor.Id, "-110", 1, 0);

        Assert.Equal("1", floor.LegacyDataString);
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

    // The inspection tab reads one object through [type, id] and gets WiredObjectInspectionData back: type, id, id/value pairs, and for a furni the boxes picking it. A furni reply is (0, item id, values, []).
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
