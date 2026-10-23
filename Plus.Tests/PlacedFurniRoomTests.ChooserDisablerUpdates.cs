using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A use of the chooser disabler sends one ObjectDataUpdate and the room flags, no ObjectUpdate.
    [Fact]
    public void AUseOfTheChooserDisablerSendsNoObjectUpdate()
    {
        var item = PlaceChooserDisabler(86, on: false);

        item.Interactor.OnTrigger(_client, item, 0, true);

        Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.ConfigurationItemStatesComposer);
        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
        Assert.Equal("1", item.LegacyDataString);
    }

    [Fact]
    public void AWiredToggleOfTheChooserDisablerSendsNoObjectUpdate()
    {
        var item = PlaceChooserDisabler(87, on: false);

        item.Interactor.OnWiredTrigger(item);

        Assert.DoesNotContain(_client.Packets, packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer);
    }
}
