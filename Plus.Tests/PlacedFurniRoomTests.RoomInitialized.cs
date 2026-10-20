using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // Observed on AIR WIN63-202610201032-107964540: the entrant gets the room-wide Wired visual state after the empty Users packet and
    // before the floor items, so the Room Initialized effects are already in place when the furni appear.
    [Fact]
    public void RoomEntrySendsTheWiredVisualStateBeforeTheFloorItems()
    {
        _room.GetWired().RememberHighlight(false, [1], 1, 100, 0x123456, 0, 0);

        EnterForNotice();

        var sent = _client.Sent.ToList();
        var state = sent.IndexOf(ServerPacketHeader.WiredFurniVisualizationStateComposer);
        Assert.True(state >= 0, "the entrant got no visual state");
        Assert.True(state > sent.IndexOf(ServerPacketHeader.UsersComposer));
        Assert.True(state < sent.IndexOf(ServerPacketHeader.ObjectsComposer));
        Assert.Single(sent, ServerPacketHeader.WiredFurniVisualizationStateComposer);
    }
}
