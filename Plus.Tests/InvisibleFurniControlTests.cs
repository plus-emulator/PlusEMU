using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.HabboHotel.Items;
using Xunit;
using static Plus.Tests.HabbiconTestSupport;

namespace Plus.Tests;

public sealed class InvisibleFurniControlTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ConfigurationItemStatesCarriesTheFourRoomSwitchesWithChooserSecondAndInvisibleFurniLast(bool chooserDisabled, bool invisibleFurni)
    {
        var packet = new RecordingPacket();
        var composer = new ConfigurationItemStatesComposer(chooserDisabled, invisibleFurni);
        composer.Compose(packet);
        Assert.Equal(3847u, composer.MessageId);
        Assert.Equal(new object[] { false, chooserDisabled, false, invisibleFurni }, packet.Writes);
    }

    [Fact]
    public void TheControllerHasItsOwnInteractionAndAMigrationThatOnlyRepairsDefaults()
    {
        Assert.Equal(InteractionType.InvisibleFurniControl, InteractionTypes.GetTypeFromString("invis_control"));
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/78_InvisibleFurniControlInteraction.sql"));
        Assert.Contains("item_name = 'conf_invis_control'", sql);
        Assert.Contains("interaction_type IN ('default', 'default_floor', 'conf_invis_control')", sql);
    }
}
