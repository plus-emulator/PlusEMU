using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.AI.Bots;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Bots;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    // A bot placed on a colour tile (stack height 0.2) stands at Z 0.2, like an avatar.
    [Fact]
    public async Task ABotPlacedOnFurniStandsOnTopOfIt()
    {
        Add(10, 2, 2, height: 0.2);
        Admit(_client, 7, null);
        _client.GetHabbo().Inventory = new InventoryComponent { Bots = new([new Bot(31, 7, "Robbie", "", "hr-100", "M")]) };
        _room.GetRoomUserManager().SerializeStatusUpdates();
        _client.Packets.Clear();

        await new PlaceBotEvent(new BotManagementService(new PlacingBotStore(), null!)).Parse(_room, _client, HabbiconTestSupport.Incoming(31, 2, 2));
        _room.GetRoomUserManager().SerializeStatusUpdates();

        var bot = Assert.IsType<HabboHotel.Rooms.RoomUser>(_room.GetRoomUserManager().GetBotByName("Robbie"));
        var users = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.UsersComposer).Body };
        Assert.Equal(1, users.ReadInt());
        users.ReadInt();
        users.ReadString();
        users.ReadString();
        users.ReadString();
        Assert.Equal((bot.VirtualId, 2, 2, "0.2"), (users.ReadInt(), users.ReadInt(), users.ReadInt(), users.ReadString()));
        var update = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.UserUpdateComposer).Body };
        Assert.Equal(1, update.ReadInt());
        Assert.Equal((bot.VirtualId, 2, 2, "0.2"), (update.ReadInt(), update.ReadInt(), update.ReadInt(), update.ReadString()));
    }

    private sealed class PlacingBotStore : IBotManagementStore
    {
        public BotPlacementData Place(int botId, int ownerId, uint roomId, int x, int y) => new("generic", "stand", false, 7, false, 0, []);
        public void PickUp(int botId, uint roomId) => throw new NotSupportedException();
        public void SaveAppearance(int botId, uint roomId, string look, string gender) => throw new NotSupportedException();
        public IReadOnlyList<string> SaveSpeech(int botId, uint roomId, IReadOnlyList<string> speech, bool automatic, int interval, bool mix) =>
            throw new NotSupportedException();
        public void SaveWalkingMode(int botId, uint roomId, string mode) => throw new NotSupportedException();
        public void SaveName(int botId, uint roomId, string name) => throw new NotSupportedException();
        public void SavePosition(int botId, uint roomId, int x, int y, double z, int rotation) => throw new NotSupportedException();
    }
}
