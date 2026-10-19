using System.Drawing;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Rooms.Furni;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Interactor;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.PathFinding;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private Item OccupiedGate(InteractionType kind = InteractionType.Gate, string state = "1")
    {
        var gate = ClosableGate(kind, width: 2, state: state);
        var occupant = ExecutorActor(NonAnchor(gate).X, NonAnchor(gate).Y);
        _room.GetGameMap().AddUserToMap(occupant, NonAnchor(gate));

        return gate;
    }

    [Fact]
    public void GateClickKeepsOpenANonAnchorOccupiedFootprint()
    {
        var gate = OccupiedGate();
        Task.Run(() => new InteractorGate().OnTrigger(_client, gate, 0, true)).Wait();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(1, Gates.PendingCount);
        ExecutorTick();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(0, Gates.PendingCount);
    }

    [Fact]
    public void GateWiredToggleQueuesOffTheOwner()
    {
        var gate = OccupiedGate();
        Task.Run(() => new InteractorGate().OnWiredTrigger(gate)).Wait();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(1, Gates.PendingCount);
        ExecutorTick();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(0, Gates.PendingCount);
    }

    [Fact]
    public void GenericSwitchKeepsOccupiedGateOpen()
    {
        UseGameService("get_QuestManager", Proxy<IQuestManager>((_, _) => null));
        var gate = OccupiedGate();
        Task.Run(() => new InteractorGenericSwitch(TestItemRuntime.Quests, TestItemRuntime.Rewards).OnTrigger(_client, gate, 0, true)).Wait();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(1, Gates.PendingCount);
        ExecutorTick();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(0, Gates.PendingCount);
    }

    [Fact]
    public void GuildGateAutoCloseChecksTheWholeFootprint()
    {
        var gate = OccupiedGate(InteractionType.GuildGate);
        gate.RequestUpdate(1, true);
        gate.ProcessUpdates();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(0, gate.UpdateCounter);
    }

    [Fact]
    public void VipGateAutoCloseChecksOccupancy()
    {
        var gate = OccupiedGate(InteractionType.GateVip);
        gate.RequestUpdate(1, true);
        gate.ProcessUpdates();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(0, gate.UpdateCounter);
    }


    [Fact]
    public void ModernToggleKeepsOccupiedGateOpen()
    {
        var gate = OccupiedGate();
        var action = ToggleAction(gate, out var context);
        Assert.True(action.Execute(context));
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(1, Gates.PendingCount);
        ExecutorTick();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(0, Gates.PendingCount);
    }

    [Fact]
    public void VariableStateWriteQueuesAnOccupiedGateClose()
    {
        var gate = OccupiedGate();
        var notices = new List<(Item, WiredVariableFrame, string)>();
        var module = GateVariables(notices);
        var holder = WiredVariableRuntimeFrames.FurniHolder(gate);
        var frame = new WiredVariableFrame(_room.Id, [holder]);
        var resolutions = 0;
        module.ResolutionHook = _ => resolutions++;
        Assert.True(Task.Run(() => module.Mutate(StateReference, holder, WiredVariableMutation.Set, 0, frame)).Result);
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Empty(notices);
        Assert.Equal(1, Gates.PendingCount);
        ExecutorTick();
        Assert.Equal("1", gate.LegacyDataString);
        Assert.Equal(0, Gates.PendingCount);
        Assert.Equal(2, resolutions);
        Assert.Empty(notices);
    }

    [Fact]
    public void MannequinPacketsRejectNonMannequinItems()
    {
        var gate = OccupiedGate();
        _client.GetHabbo().Gender = "M";
        _client.GetHabbo().Look = "hd-180-1.ch-210-66";
        _client.GetHabbo().Clothing = new();
        new SetMannequinFigureEvent(new RoomItemMetadataService(Proxy<IRoomItemMetadataStore>((_, _) => throw new InvalidOperationException("Wrong type must not persist")), null!)).Parse(_client, ClientPacket((int)gate.Id)).Wait();
        Assert.Equal("1", gate.LegacyDataString);
    }

    [Fact]
    public void FastWiredPassEntersTheRoomOwnerScope()
    {
        var owned = true;
        _room.RunFastPass(() => owned = RoomOwnerScope.IsOwner(_room));
        Assert.True(owned);
    }
}
