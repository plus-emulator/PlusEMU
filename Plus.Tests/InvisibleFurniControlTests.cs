using Plus.Communication.Packets.Outgoing.Rooms.Furni;
using Plus.HabboHotel.Items;
using Xunit;
using static Plus.Tests.HabbiconTestSupport;

namespace Plus.Tests;

public sealed class InvisibleFurniControlTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ConfigurationItemStatesCarriesTheFourRoomSwitchesWithInvisibleFurniLast(bool on)
    {
        var packet = new RecordingPacket();
        var composer = new ConfigurationItemStatesComposer(on);
        composer.Compose(packet);
        Assert.Equal(3660u, composer.MessageId);
        Assert.Equal(new object[] { false, false, false, on }, packet.Writes);
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
