using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.HabboHotel.Users.Inventory.Pets;
using Xunit;

namespace Plus.Tests;

// A pet cannot be placed on a tile that an area hider hides, just like Habbo: the owner gets the invalid tile error.
public partial class PlacedFurniRoomTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void APetCannotBePlacedOnATileAHiderHides(bool hiderOn)
    {
        PlaceAreaHider(100, $"{(hiderOn ? 1 : 0)}\n2\n2\n2\n2\n0\n1\n0");
        var pet = new Pet(81, 7, 0, "pixel", 0, "0", "ffffff", 0, 100, 100, 0,
            DateTimeOffset.FromUnixTimeMilliseconds(1609459200123), 0, 0, 0, 0, 0, 0, 0, "", "owner");
        _client.GetHabbo().Inventory = new InventoryComponent
        {
            Furniture = new FurnitureInventoryComponent([], []),
            Pets = new PetsInventoryComponent([pet])
        };
        var settings = Proxy<Plus.Core.Settings.ISettingsManager>((method, args) =>
            method == "TryGetValue" && (string)args[0]! == "room.pets.placement_limit" ? "5" : null);
        _client.Packets.Clear();

        new PetPlacementService(new AcceptingPetStore(), TestGameClientManager.Empty, settings).Place(_room, _client, pet.PetId, 2, 2);

        Assert.Equal(hiderOn, !pet.PlacedInRoom);
        var error = _client.Packets.Where(packet => packet.Header == ServerPacketHeader.RoomErrorNotifComposer).Select(packet => packet.Body).ToList();
        Assert.Equal(hiderOn ? [new byte[] { 0, 0, 0, 4 }] : [], error);
    }
}
