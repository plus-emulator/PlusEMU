using Plus.HabboHotel.Catalog;
using Plus.HabboHotel.Catalog.Utilities;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

/// <summary>WIRED Room Linker (wf_room_linker, sprite 16744): a linked pair like a teleporter, read by Teleport to Room.</summary>
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void RoomLinkerInteractionParsesAndCountsAsALinkedPair()
    {
        Assert.Equal(InteractionType.WiredRoomLinker, InteractionTypes.GetTypeFromString("wired_room_linker"));
        Assert.True(InteractionTypes.IsLinkedPair(InteractionType.WiredRoomLinker));
        Assert.True(InteractionTypes.IsLinkedPair(InteractionType.Teleport));
        Assert.False(InteractionTypes.IsLinkedPair(InteractionType.None));
        Assert.True(ItemGrants.NeedsLinkedData(new ItemDefinition { InteractionType = InteractionType.WiredRoomLinker }));
    }

    [Fact]
    public void RoomLinkersAreNotGiftWrappedLikeTeleporters()
    {
        var definition = new ItemDefinition { InteractionType = InteractionType.WiredRoomLinker, AllowGift = true };
        var offer = new CatalogOffer { Products = [new CatalogProduct { Type = CatalogProductType.Furni, Definition = definition, Amount = 1 }] };
        Assert.False(ItemUtility.CanGiftItem(offer));
        definition.InteractionType = InteractionType.None;
        Assert.True(ItemUtility.CanGiftItem(offer));
    }

    [Fact]
    public void RoomLinkerForwardsToThePlacedPartnersRoomAndNowhereWhenThePartnerIsUnplaced()
    {
        var linker = MakeItem(5, "wf_room_linker");
        linker.Definition.InteractionType = InteractionType.WiredRoomLinker;
        linker.ExtraData = new LegacyDataFormat();
        Assert.Equal(new WiredRoomForwarding.Destination(42, 6),
            WiredRoomForwarding.Resolve([linker], "", id => id == 6 ? 42u : 0, id => id == 5 ? 6u : 0, TestLogging.Logger));
        Assert.Null(WiredRoomForwarding.Resolve([linker], "", _ => 0, id => id == 5 ? 6u : 0, TestLogging.Logger));
        Assert.Null(WiredRoomForwarding.Resolve([linker], "", _ => throw new Exception(), _ => 0, TestLogging.Logger));
    }

    [Fact]
    public void UserForwardedThroughARoomLinkerArrivesOnThePartnerWithoutFlashingIt()
    {
        using var f = new TeleportFixture();
        var (destination, _, _) = World();
        destination.Id = 42;
        var partner = MakeItem(6, "wf_room_linker");
        partner.Definition.InteractionType = InteractionType.WiredRoomLinker;
        partner.Definition.Modes = 1;
        partner.ExtraData = new LegacyDataFormat { Data = "" };
        Assert.True(destination.GetRoomItemHandler().SetFloorItem(null!, partner, 2, 1, 0, true, false, false));
        f.Habbo.IsTeleporting = true;
        f.Habbo.TeleportingRoomId = 42;
        f.Habbo.TeleporterId = partner.Id;
        f.Habbo.Gender = "M";
        f.Habbo.Look = "test";
        f.Habbo.Motto = "";
        f.Habbo.HabboStats = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "", 0);
        f.Habbo.Access = EditorTestSupport.Access([]);
        Assert.True(destination.GetRoomUserManager().AddAvatarToRoom(f.Client));
        var joined = Assert.IsType<RoomUser>(destination.GetRoomUserManager().GetRoomUserByHabbo(f.Habbo.Id));
        Assert.Equal((2, 1), (joined.X, joined.Y));
        Assert.Equal(new WiredRoomEntrySnapshot(WiredRoomEntryMethod.Teleport, partner.Id), joined.WiredRoomEntry);
        Assert.Equal("", partner.LegacyDataString);
        Assert.Equal(0, partner.InteractingUser2);
        Assert.False(f.Habbo.IsTeleporting);
        Assert.Equal(0u, f.Habbo.TeleporterId);
    }
}
