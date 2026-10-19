using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(true, 100, false)]
    [InlineData(true, 200, false)]
    [InlineData(false, 100, false)]
    [InlineData(false, 200, false)]
    [InlineData(true, 100, true)]
    [InlineData(true, 200, true)]
    public async Task NativeChangeVariableUsesCapturedWallAndFloorHoldersWithoutAcceptingReplacements(bool targetWall, int source, bool replace)
    {
        var directory = new WallBuiltinDirectory(_room);
        directory.Definitions[900] = new(900, RoomId, 7, "wallcheck", WiredVariableTarget.Furni,
            WiredVariableAvailability.RoomActive, true);
        var module = InspectionInstallModule(directory);
        var (box, store) = CanonicalBox("wf_act_change_var_val", null!);
        var wall = TemporaryWallTemplate();
        var floor = Furni(302, InteractionType.None, WiredBoxType.None);
        floor.RoomId = RoomId;
        floor.OwnerId = 7;
        floor.UserId = 7;
        box.Item.UserId = 7;
        box.Item.OwnerId = 7;
        _room.GetRoomItemHandler().LoadFurniture([box.Item, wall, floor]);
        var target = targetWall ? wall : floor;
        var wireId = targetWall ? -checked((int)BuildersClubItemIds.ToWire(wall.Id)) : checked((int)floor.Id);
        object[] picks = source == 100 ? [wireId] : [];
        object[] fields = [601, 7, 0, 0, 0, 0, 111, -10, 0, "", picks.Length, .. picks,
            0, 2, source, 200, 2, 0, 200, 2, "furni:900", WiredVariableAbsent.WireId, 0];
        await CanonicalHandler().Parse(_client, ClientPacket(fields));
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredValidationErrorComposer);
        var saved = Assert.Single(store.Saves);
        Assert.True(WiredNativeEditorProjection.TryProject(box.Item, box.Descriptor, saved, out var reopened));
        Assert.Equal(source, reopened.FurniSourceTypes[0]);

        if (source == 100) {
            Assert.Equal(targetWall, Assert.Single(reopened.PrimaryItems).Wall);
            Assert.Equal(target.Id, Assert.Single(reopened.PrimaryItems).ItemId);
        }

        var reference = new WiredVariableReference(WiredVariableTarget.Furni, "custom:900");
        var holders = new[] { wall, floor }.Select(WiredVariableRuntimeFrames.FurniHolder).ToArray();
        var inspection = new WiredVariableFrame(RoomId, holders);

        foreach (var holder in holders) {
            Assert.True(module.Mutate(reference, holder, WiredVariableMutation.Give, 0, inspection));
        }

        var context = WallSnapshotContext();

        if (source == 200) {
            context.SelectorPool.FurniIds.Add(target.Id);
        }

        if (replace) {
            var replacement = WallSnapshotItem(wall.Id, wall.WallCoordinates);
            replacement.OwnerId = 7;
            replacement.UserId = 7;
            _room.GetRoomItemHandler().LoadFurniture([box.Item, replacement, floor]);
        }

        var wallBefore = module.Read(reference, holders[0], inspection)?.Value;
        var floorBefore = module.Read(reference, holders[1], inspection)?.Value;
        Assert.Equal(!replace, Assert.IsType<WiredVariableConfiguredBox>(box).Execute(context));
        Assert.Equal(replace ? wallBefore : targetWall ? 111 : 0, module.Read(reference, holders[0], inspection)?.Value);
        Assert.Equal(replace ? floorBefore : targetWall ? 0 : 111, module.Read(reference, holders[1], inspection)?.Value);
        Assert.DoesNotContain(wall, context.Targets.ResolveFurni(context, [], WiredSources.AllRoom));
    }
}
