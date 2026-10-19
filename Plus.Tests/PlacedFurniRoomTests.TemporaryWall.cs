using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(WiredPlaceAltitudeType.SourceAltitude, 0, 0, 0, ":w=5,0 l=8,40 r a=247")]
    [InlineData(WiredPlaceAltitudeType.OnTopOfTargetLocation, 0, 0, 0, ":w=5,0 l=8,119 r a=0")]
    [InlineData(WiredPlaceAltitudeType.SourceAltitude, 0, 0, 100, ":w=5,0 l=8,8 r a=347")]
    [InlineData(WiredPlaceAltitudeType.SourceAltitude, 1, 0, 0, ":w=6,0 l=8,40 r a=247")]
    [InlineData(WiredPlaceAltitudeType.SourceAltitude, 0, 1, 0, ":w=5,1 l=8,40 r a=247")]
    public void TemporaryWallCopiesUseSavedWorldAltitudeAndWallCoordinates(WiredPlaceAltitudeType altitude, int x, int y, int z, string expected)
    {
        var template = TemporaryWallTemplate();
        Viewer();
        _client.Packets.Clear();
        Assert.True(SpawnTemporaryWall(template, new(Altitude: altitude, OffsetX: x, OffsetY: y, OffsetAltitudeHundredths: z)));
        var copy = Assert.Single(_room.GetRoomItemHandler().GetWall.Where(item => item.IsTemporary));
        Assert.Equal(expected, copy.WallCoordinates);
        Assert.Equal("1", copy.LegacyDataString);
        Assert.Equal(TemporaryItemIds.First, BuildersClubItemIds.ToWire(copy.Id));
        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ItemsComposer);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ItemAddComposer);
    }

    [Fact]
    public async Task TemporaryWallAndFloorWithSameWireIdRemainIndependentThroughOwnerPackets()
    {
        var template = TemporaryWallTemplate();
        var floor = TemporaryCopy();
        Assert.True(SpawnTemporaryWall(template, new(Altitude: WiredPlaceAltitudeType.SourceAltitude)));
        var wall = Assert.Single(_room.GetRoomItemHandler().GetWall.Where(item => item.IsTemporary));
        Assert.Equal(BuildersClubItemIds.ToWire(floor.Id), BuildersClubItemIds.ToWire(wall.Id));
        Assert.NotEqual(floor.Id, wall.Id);
        var context = WallSnapshotContext();
        context.Triggering.FurniIds.Add(wall.Id);
        context.SelectorPool.FurniIds.Add(floor.Id);
        Assert.Same(wall, Assert.Single(context.Targets.ResolveFurni(context, [], WiredSources.Trigger)));
        Assert.Same(floor, Assert.Single(context.Targets.ResolveFurni(context, [], WiredSources.Selector)));
        Assert.Empty(context.Targets.ResolveFurni(context, [wall.Id], WiredSources.Selected));
        Assert.NotEqual(WiredVariableRuntimeFrames.FurniHolder(floor).StorageId, WiredVariableRuntimeFrames.FurniHolder(wall).StorageId);
        var wire = unchecked((int)BuildersClubItemIds.ToWire(wall.Id));
        var use = new FurnitureUseService(new FurnitureUseStore(EditorTestSupport.UntouchableDatabase()), Proxy<IQuestManager>((_, _) => null));
        await new UseWallItemEvent(use).Parse(_room, _client, ClientPacket(wire, 0));
        Assert.Equal("0", wall.LegacyDataString);
        Assert.Equal("0", floor.LegacyDataString);
        await new MoveWallItemEvent(PlacementService(() => { })).Parse(_room, _client, ClientPacket(wire, ":w=5,0 l=12,40 r"));
        Assert.Equal(":w=5,0 l=12,40 r", wall.WallCoordinates);
        Assert.Equal((1, 1), (floor.GetX, floor.GetY));
        var pickup = new RoomItemPickupService(TestGameClientManager.Empty, Proxy<IQuestManager>((_, _) => null),
            new RoomItemPickupStore(EditorTestSupport.UntouchableDatabase()));
        await new PickupObjectEvent(pickup).Parse(_client, ClientPacket(1, wire));
        Assert.Null(_room.GetRoomItemHandler().GetItem(wall.Id));
        Assert.Same(floor, _room.GetRoomItemHandler().GetItem(floor.Id));
        Assert.True(SpawnTemporaryWall(template, new()));
        Assert.Equal(TemporaryItemIds.First + 1, BuildersClubItemIds.ToWire(Assert.Single(_room.GetRoomItemHandler().GetWall.Where(item => item.IsTemporary)).Id));
    }

    [Fact]
    public void TemporaryWallUnloadDiscardsCopiesAndRestartsItsIndependentSequence()
    {
        var template = TemporaryWallTemplate();
        Assert.True(SpawnTemporaryWall(template, new()));
        var old = Assert.Single(_room.GetRoomItemHandler().GetWall.Where(item => item.IsTemporary));
        _room.GetRoomItemHandler().LoadFurniture([template]);
        Assert.False(_room.GetRoomItemHandler().OwnsTemporary(old));
        Assert.Null(old.GetRoom());
        Assert.True(SpawnTemporaryWall(template, new()));
        var fresh = Assert.Single(_room.GetRoomItemHandler().GetWall.Where(item => item.IsTemporary));
        Assert.Equal(TemporaryItemIds.First, BuildersClubItemIds.ToWire(fresh.Id));
        Assert.NotSame(old, fresh);
    }

    [Fact]
    public void TemporaryWallNativePicksRoundTripWithoutAliasingFloorAndExposeWireIdentity()
    {
        var template = TemporaryWallTemplate();
        Assert.True(SpawnTemporaryWall(template, new()));
        var wall = Assert.Single(_room.GetRoomItemHandler().GetWall.Where(item => item.IsTemporary));
        var wire = checked((int)TemporaryItemIds.First);
        Assert.True(WiredLegacyProtocol.TryReadNative(ClientPacket(0, "", 2, wire, -wire, 0, 0, 0, 0, 0),
            WiredBoxCategory.Action, out var native));
        Assert.Equal(TemporaryItemIds.First, native.PrimaryItems[0].ItemId);
        Assert.Equal(wall.Id, native.PrimaryItems[1].ItemId);
        Assert.Equal(-wire, native.PrimaryItems[1].WireId);
        Assert.True(Plus.Communication.Packets.Incoming.WiredVariables.WiredUserVariableUpdateEvent.TryRead(
            ClientPacket(1, -wire, 400, 9), false, out var write));
        Assert.Equal(unchecked((int)wall.Id), write!.TargetId);
        Assert.True(Plus.Communication.Packets.Incoming.WiredVariables.WiredGetVariablesForObjectEvent.TryRead(
            ClientPacket(0, -wire), out _, out var objectId));
        Assert.Equal(unchecked((int)wall.Id), objectId);
        Assert.True(Plus.Communication.Packets.Incoming.WiredVariables.WiredVariableInspectionRequestEvent.TryRead(
            ClientPacket(1, 1, (int)RoomId, 1, wire, 1), out var request));
        Assert.Equal(wall.Id, unchecked((uint)request!.EntityId));
        var metadata = TypedFurnitureStateTests.Payload(new Plus.Communication.Packets.Outgoing.Rooms.Engine.FurnitureMetadataComposer(
            [RoomItemSnapshot.Capture(wall)]));
        Assert.Equal(0, System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(metadata));
        var holder = WiredVariableRuntimeFrames.FurniHolder(wall);
        var frame = new WiredVariableFrame(RoomId, [holder]);
        Assert.Equal(-wire, new RoomWiredBuiltinVariables(_room).Read(new(holder.Target, "internal:@id"), holder, frame)?.Value);
    }

    [Theory]
    [InlineData("pickup")]
    [InlineData("unload")]
    [InlineData("dispose")]
    public void TemporaryWallRemovalDropsItsActiveVariableValues(string operation)
    {
        var template = TemporaryWallTemplate();
        Assert.True(SpawnTemporaryWall(template, new()));
        var wall = Assert.Single(_room.GetRoomItemHandler().GetWall.Where(item => item.IsTemporary));
        var module = _room.GetWired().Variables.Module;
        var active = (MemoryWiredVariableStore)typeof(WiredVariableModule).GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(module)!;
        var key = new WiredVariableKey(900, WiredVariableTarget.Furni, WiredVariableRuntimeFrames.FurniHolder(wall).StorageId);
        active.Mutate(key, _ => new(42, null, null));

        if (operation == "pickup") {
            Assert.True(new RoomItemPickupService(TestGameClientManager.Empty, Proxy<IQuestManager>((_, _) => null),
                new RoomItemPickupStore(EditorTestSupport.UntouchableDatabase())).TryPickUp(_client, wall.Id));
        }
        else if (operation == "unload") {
            _room.GetRoomItemHandler().LoadFurniture([template]);
        }
        else {
            _room.GetRoomItemHandler().Dispose();
        }

        Assert.Null(active.Read(key));
    }

    [Fact]
    public void TemporaryWallVariableHolderPageAndFxRemovalUseSignedWireId()
    {
        var template = TemporaryWallTemplate();
        Assert.True(SpawnTemporaryWall(template, new()));
        var id = Assert.Single(_room.GetRoomItemHandler().GetWall.Where(item => item.IsTemporary)).Id;
        var holder = new WiredVariableStoredHolder(new(900, WiredVariableTarget.Furni, unchecked((int)id)), "wall", new(42, null, null));
        var page = new WiredVariableHolderPage(1, 0, 10, [holder]);
        var packet = new Plus.Communication.Packets.Outgoing.WiredVariables.WiredVariableHoldersPageComposer("custom:900", page, 0, 0, false);
        using var reader = new BinaryReader(new MemoryStream(TypedFurnitureStateTests.Payload(packet)));
        TypedFurnitureStateTests.ReadString(reader);

        for (var n = 0; n < 5; n++) {
            TypedFurnitureStateTests.ReadInt(reader);
        }

        var wireId = TypedFurnitureStateTests.ReadInt(reader);
        Assert.Equal(-checked((int)TemporaryItemIds.First), wireId);
        Assert.True(Plus.Communication.Packets.Incoming.WiredVariables.WiredUserVariableUpdateEvent.TryRead(
            ClientPacket(1, wireId, 900, 42), false, out var write));
        Assert.Equal(unchecked((int)id), write!.TargetId);
        var key = new Plus.HabboHotel.Items.Wired.Variables.Fx.WiredVariableFxKey(1, "900", false, unchecked((int)id));
        var batch = new Plus.HabboHotel.Items.Wired.Variables.Fx.WiredVariableFxBatch(false, [], [], [], [key]);
        var fx = Assert.Single(Plus.Communication.Packets.Outgoing.WiredVariables.WiredVariableFxComposer.ComposeBatch(batch));
        using var fxReader = new BinaryReader(new MemoryStream(TypedFurnitureStateTests.Payload(fx)));
        Assert.Equal(1, TypedFurnitureStateTests.ReadInt(fxReader));
        Assert.Equal($"1|900|f|{wireId}", TypedFurnitureStateTests.ReadString(fxReader));
    }

    private Item TemporaryWallTemplate()
    {
        WallSnapshotInstallStore();
        WallGeometryModel();
        var definition = WallSnapshotItem(301, ":w=5,0 l=8,40 r a=247").Definition;
        definition.Id = 301;
        definition.Modes = 2;
        var template = new Item
        {
            Id = 301,
            IsBuildersClub = true,
            OwnerId = 7,
            UserId = 7,
            RoomId = RoomId,
            Definition = definition,
            ExtraData = FurniExtraData.Load(definition, "1", true),
            WallCoordinates = ":w=5,0 l=8,40 r a=247"
        };
        _room.GetRoomItemHandler().LoadFurniture([template]);
        typeof(RoomItemHandling).GetField("_store", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_room.GetRoomItemHandler(),
            Proxy<IRoomItemStore>((method, _) => throw new InvalidOperationException($"Temporary wall wrote {method}")));

        return template;
    }

    private bool SpawnTemporaryWall(Item template, WiredTemporaryPlacement policy) => WiredTemporaryFurnitureActions.Execute("wf_act_place_furni",
        Furni(400, InteractionType.WiredEffect, WiredBoxType.None), WallSnapshotContext(),
        new() { TemporaryPlacement = policy, Snapshots = [WiredRoomOperations.Capture(template)] },
        new TestWiredDefinitions(() => new() { [template.Definition.Id] = template.Definition }));
}
