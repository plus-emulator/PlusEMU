using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Incoming.Rooms.AI.Bots;
using Plus.Communication.Packets.Incoming.Rooms.AI.Pets;
using Plus.Communication.Packets.Outgoing;
using Plus.Core.Settings;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.AI;
using Plus.HabboHotel.Rooms.AI.Speech;
using Plus.HabboHotel.Users;
using Xunit;

namespace Plus.Tests;

// AIR's Shift+click rotate and Alt-move of a placed monsterplant (MovePet 636) or rentable bot (MoveEntityInFlat 2282).
public sealed class RoomUnitMoveTests
{
    private const int RoomOwnerId = 1;
    private const int UnitOwnerId = 7;
    private const int RightsHolderId = 8;
    private const int VisitorId = 9;
    private const int StaffId = 10;
    private const int PetId = 12;
    private const int BotId = 31;

    [Fact]
    public async Task AirHeadersRouteBothPacketsWithEveryFieldDecoded()
    {
        var world = new World();
        var (client, _) = world.Client(RoomOwnerId, "owner");
        var calls = new List<(string Service, object?[] Arguments)>();
        var pets = CatalogSnapshotTestSupport.Proxy<IPetPlacementService>((method, arguments) =>
        {
            calls.Add(("pets." + method, arguments));

            return null;
        });
        var bots = CatalogSnapshotTestSupport.Proxy<IBotManagementService>((method, arguments) =>
        {
            calls.Add(("bots." + method, arguments));

            return null;
        });
        using var manager = new PacketManager([new MovePetEvent(pets), new MoveEntityInFlatEvent(bots)], NullLogger<PacketManager>.Instance);
        var movePet = HabbiconTestSupport.Incoming(PetId, 3, 1, 6);
        var moveBot = HabbiconTestSupport.Incoming(4, 0, 3, 2);

        await manager.TryExecutePacket(client, 636, movePet);
        await manager.TryExecutePacket(client, 2282, moveBot);

        Assert.False(movePet.HasDataRemaining());
        Assert.False(moveBot.HasDataRemaining());
        Assert.Equal(2, calls.Count);
        Assert.Equal("pets.Move", calls[0].Service);
        Assert.Equal(new object?[] { world.Room, client, PetId, 3, 1, 6 }, calls[0].Arguments);
        Assert.Equal("bots.Move", calls[1].Service);
        Assert.Equal(new object?[] { world.Room, client, 4, 0, 3, 2 }, calls[1].Arguments);
    }

    [Fact]
    public void PetOwnerRotatesItsPetInPlaceAndTheRoomSeesIt()
    {
        var world = new World();
        var (owner, _) = world.Client(UnitOwnerId, "petowner");
        world.Pet.UpdateNeeded = false;

        // Directions are 45-degree steps, so 10 turns the pet to 2.
        world.Pets.Move(world.Room, owner, PetId, 1, 1, 10);

        Assert.Equal((1, 1, 2, 2), (world.Pet.X, world.Pet.Y, world.Pet.RotHead, world.Pet.RotBody));
        Assert.Empty(world.PetStore.Moves);
        Assert.Equal((1, 1, 2, 2, false), world.BroadcastFor(world.Pet));
    }

    [Fact]
    public void RoomControllerMovesSomeoneElsesPetAfterStoringTheTile()
    {
        var world = new World();
        var (builder, _) = world.Client(RightsHolderId, "builder");
        var data = Assert.IsType<Pet>(world.Pet.PetData);
        world.PetStore.BeforeMove = move =>
        {
            Assert.Equal((PetId, UnitOwnerId, 42u, 42u, 1, 3), (move.PetId, move.OwnerId, move.PreviousRoomId, move.RoomId, move.X, move.Y));
            Assert.Equal((1, 1), (world.Pet.X, world.Pet.Y));
        };

        world.Pets.Move(world.Room, builder, PetId, 1, 3, 4);

        Assert.Single(world.PetStore.Moves);
        Assert.Equal((1, 3, 4, 4), (world.Pet.X, world.Pet.Y, world.Pet.RotHead, world.Pet.RotBody));
        Assert.Equal((1, 3), (data.X, data.Y));
        Assert.Contains(world.Pet, world.Room.GetGameMap().GetRoomUsers(new(1, 3)));
        Assert.DoesNotContain(world.Pet, world.Room.GetGameMap().GetRoomUsers(new(1, 1)));
        Assert.Equal((1, 3, 4, 4, false), world.BroadcastFor(world.Pet));
    }

    [Theory]
    [InlineData(RoomOwnerId, "owner", true)]
    [InlineData(RightsHolderId, "builder", true)]
    [InlineData(UnitOwnerId, "petowner", true)]
    [InlineData(VisitorId, "visitor", false)]
    public void PetMoveNeedsRoomControlOrPetOwnership(int id, string username, bool allowed)
    {
        var world = new World();
        var (actor, _) = world.Client(id, username);

        world.Pets.Move(world.Room, actor, PetId, 0, 3, 6);

        Assert.Equal(allowed ? (0, 3, 6) : (1, 1, 0), (world.Pet.X, world.Pet.Y, world.Pet.RotBody));
        Assert.Equal(allowed ? 1 : 0, world.PetStore.Moves.Count);
    }

    [Theory]
    [InlineData(2, 2, 0)] // void tile in the layout
    [InlineData(4, 0, 0)] // past the layout edge
    [InlineData(-1, 1, 0)]
    [InlineData(3, 3, 0)] // an avatar stands here
    [InlineData(2, 1, 0)] // the bot stands here
    [InlineData(1, 1, -1)]
    [InlineData(0, 3, -2)]
    public void InvalidPetMovesAreIgnored(int x, int y, int direction)
    {
        var world = new World();
        var (owner, _) = world.Client(RoomOwnerId, "owner");

        world.Pets.Move(world.Room, owner, PetId, x, y, direction);

        Assert.Equal((1, 1, 0, 0), (world.Pet.X, world.Pet.Y, world.Pet.RotHead, world.Pet.RotBody));
        Assert.Empty(world.PetStore.Moves);
    }

    [Fact]
    public void PetMovesFromAnotherRoomOrForAnUnknownPetAreIgnored()
    {
        var world = new World();
        var (owner, _) = world.Client(RoomOwnerId, "owner");

        world.Pets.Move(world.Room, owner, 99, 0, 3, 2);
        world.Pets.Move(world.Room, owner, world.Pet.VirtualId, 0, 3, 2);
        owner.GetHabbo().CurrentRoom = null;
        world.Pets.Move(world.Room, owner, PetId, 0, 3, 2);

        Assert.Equal((1, 1, 0), (world.Pet.X, world.Pet.Y, world.Pet.RotBody));
        Assert.Empty(world.PetStore.Moves);
    }

    [Fact]
    public void PetStoreFailureLeavesThePetWhereItWas()
    {
        var world = new World();
        var (owner, _) = world.Client(RoomOwnerId, "owner");
        world.PetStore.Result = false;

        world.Pets.Move(world.Room, owner, PetId, 0, 3, 2);

        Assert.Equal((1, 1, 0), (world.Pet.X, world.Pet.Y, world.Pet.RotBody));
        Assert.Contains(world.Pet, world.Room.GetGameMap().GetRoomUsers(new(1, 1)));
        Assert.Equal((1, 1), (world.Pet.PetData!.X, world.Pet.PetData.Y));
    }

    [Fact]
    public void RoomControllerMovesAndRotatesABotByItsRoomIndex()
    {
        var world = new World();
        var (builder, _) = world.Client(RightsHolderId, "builder");
        var bot = world.Bot.BotData!;
        world.BotStore.BeforeSave = () => Assert.Equal((2, 1, 4), (world.Bot.X, world.Bot.Y, bot.Rot));

        world.Bots.Move(world.Room, builder, world.Bot.VirtualId, 3, 0, 6);

        Assert.Equal((BotId, 42u, 3, 0, 0d, 6), Assert.Single(world.BotStore.Positions));
        Assert.Equal((3, 0, 6, 6), (world.Bot.X, world.Bot.Y, world.Bot.RotHead, world.Bot.RotBody));
        Assert.Equal((3, 0, 6), (bot.X, bot.Y, bot.Rot));
        Assert.Contains(world.Bot, world.Room.GetGameMap().GetRoomUsers(new(3, 0)));
        Assert.DoesNotContain(world.Bot, world.Room.GetGameMap().GetRoomUsers(new(2, 1)));
        Assert.Equal((3, 0, 6, 6, false), world.BroadcastFor(world.Bot));
        world.BotStore.BeforeSave = null;

        world.Bots.Move(world.Room, builder, world.Bot.VirtualId, 3, 0, 1);

        Assert.Equal((BotId, 42u, 3, 0, 0d, 1), world.BotStore.Positions[^1]);
        Assert.Equal((3, 0, 1, 1), (world.Bot.X, world.Bot.Y, world.Bot.RotHead, world.Bot.RotBody));
        Assert.Equal(1, bot.Rot);
    }

    [Fact]
    public void BotRoomIndexMustNameAPlacedBot()
    {
        var world = new World();
        var (owner, _) = world.Client(RoomOwnerId, "owner");
        var speech = new List<RandomSpeech>();
        var stressBot = new RoomBot(-1, 42, "generic", "stand", "Stress", "", "hr-100", 3, 1, 0, 0, 0, 0, 0, 0, ref speech, "M", 0,
            RoomOwnerId, false, 7, false, 0)
        { IsTemporary = true };
        var stress = world.Room.GetRoomUserManager().DeployBot(stressBot, null!);

        // An avatar's or pet's room index, the bot's database id and a temporary stress bot are all refused.
        foreach (var index in new[] { world.Avatar.VirtualId, world.Pet.VirtualId, BotId, stress.VirtualId }) {
            world.Bots.Move(world.Room, owner, index, 0, 3, 2);
        }

        Assert.Empty(world.BotStore.Positions);
        Assert.Equal((3, 3, 0), (world.Avatar.X, world.Avatar.Y, world.Avatar.RotBody));
        Assert.Equal((1, 1, 0), (world.Pet.X, world.Pet.Y, world.Pet.RotBody));
        Assert.Equal((2, 1, 4), (world.Bot.X, world.Bot.Y, world.Bot.RotBody));
        Assert.Equal((3, 1), (stress.X, stress.Y));
    }

    [Theory]
    [InlineData(RoomOwnerId, "owner", false, true)]
    [InlineData(RightsHolderId, "builder", false, true)]
    [InlineData(UnitOwnerId, "botowner", false, true)]
    [InlineData(StaffId, "staff", true, true)]
    [InlineData(VisitorId, "visitor", false, false)]
    public void BotMoveNeedsRoomControlBotOwnershipOrTheBotOverride(int id, string username, bool botOverride, bool allowed)
    {
        var world = new World();
        var (actor, _) = world.Client(id, username, botOverride ? [PermissionKeys.BotPlaceAnyOverride] : []);

        world.Bots.Move(world.Room, actor, world.Bot.VirtualId, 0, 3, 2);

        Assert.Equal(allowed ? (0, 3, 2) : (2, 1, 4), (world.Bot.X, world.Bot.Y, world.Bot.RotBody));
        Assert.Equal(allowed ? 1 : 0, world.BotStore.Positions.Count);
    }

    [Theory]
    [InlineData(2, 2, 0)]
    [InlineData(0, 4, 0)]
    [InlineData(3, 3, 0)]
    [InlineData(1, 1, 0)]
    [InlineData(2, 1, -1)]
    public void InvalidBotMovesAreIgnored(int x, int y, int direction)
    {
        var world = new World();
        var (owner, _) = world.Client(RoomOwnerId, "owner");

        world.Bots.Move(world.Room, owner, world.Bot.VirtualId, x, y, direction);

        Assert.Equal((2, 1, 4), (world.Bot.X, world.Bot.Y, world.Bot.RotBody));
        Assert.Equal(4, world.Bot.BotData!.Rot);
        Assert.Empty(world.BotStore.Positions);
    }

    [Fact]
    public void BotStoreFailureLeavesTheBotWhereItWas()
    {
        var world = new World();
        var (owner, _) = world.Client(RoomOwnerId, "owner");
        world.BotStore.Fail = true;

        Assert.Throws<InvalidOperationException>(() => world.Bots.Move(world.Room, owner, world.Bot.VirtualId, 0, 3, 2));

        Assert.Equal((2, 1, 4), (world.Bot.X, world.Bot.Y, world.Bot.RotBody));
        Assert.Equal((2, 1, 4), (world.Bot.BotData!.X, world.Bot.BotData.Y, world.Bot.BotData.Rot));
        Assert.Contains(world.Bot, world.Room.GetGameMap().GetRoomUsers(new(2, 1)));
    }

    private sealed class World
    {
        private readonly List<(uint Header, byte[] Payload)> _viewerPackets;

        public World()
        {
            var model = new RoomModel("unit-move-test", 0, 0, 0, 0, "0000\r0000\r00x0\r0000", 0, 0, false);
            Room = new Room(new RoomData { Id = 42, OwnerId = RoomOwnerId, OwnerName = "owner", Type = "private", AllowPets = true, Model = model }, [],
                TestLogging.Navigation, TestLogging.Logger, TestRoomAchievements.Unused, TestRoomOwners.Unused);
            Set(Room, "_gamemap", new Gamemap(Room, model, TestLogging.Navigation, TestRoomSettings.Empty, TestGroupManager.Empty, TestNavigationDatabase.Instance, TestNavigationRewards.Instance));
            Set(Room, "_roomUserManager", new RoomUserManager(Room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(), TestChatEmotions.Unused, TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel));
            Set(Room, "_userSnapshots", CatalogSnapshotTestSupport.Proxy<IRoomUserSnapshotService>((_, _) => null));
            Room.UsersWithRights = [RightsHolderId];
            var manager = Room.GetRoomUserManager();

            var pet = NewPet();
            var speech = new List<RandomSpeech>();
            Pet = manager.DeployBot(new(PetId, 42, "pet", "freeroam", pet.Name, "", pet.Look, 1, 1, 0, 0, 0, 0, 0, 0, ref speech, "", 0,
                UnitOwnerId, false, 0, false, 0), pet);
            Bot = manager.DeployBot(new(BotId, 42, "generic", "stand", "Helper", "", "hr-100", 2, 1, 0, 4, 0, 0, 0, 0, ref speech, "M", 0,
                UnitOwnerId, false, 7, false, 0), null!);
            Room.GetGameMap().UpdateUserMovement(new(1, 1), new(1, 1), Pet);
            Room.GetGameMap().UpdateUserMovement(new(2, 1), new(2, 1), Bot);

            var (viewer, packets) = HabbiconTestSupport.Client(new Habbo { Id = VisitorId, Username = "visitor", CurrentRoom = Room });
            _viewerPackets = packets;
            Avatar = new RoomUser(VisitorId, 42, 50, Room, viewer, TestChatEmotions.Unused, TestRewardProgress.Unused) { X = 3, Y = 3, InternalRoomId = 50 };
            var users = (ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager)
                .GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
            Assert.True(users.TryAdd(Avatar.VirtualId, Avatar));
            Room.GetGameMap().AddUserToMap(Avatar, new(3, 3));

            Pets = new(PetStore, TestGameClientManager.Empty, CatalogSnapshotTestSupport.Proxy<ISettingsManager>((_, _) => null));
            Bots = new(BotStore, null!);
        }

        public Room Room { get; }
        public RoomUser Pet { get; }
        public RoomUser Bot { get; }
        public RoomUser Avatar { get; }
        public RecordingPetStore PetStore { get; } = new();
        public RecordingBotStore BotStore { get; } = new();
        public PetPlacementService Pets { get; }
        public BotManagementService Bots { get; }

        public (GameClient Client, List<(uint Header, byte[] Payload)> Sent) Client(int id, string username, string[]? rights = null) =>
            HabbiconTestSupport.Client(new Habbo { Id = id, Username = username, CurrentRoom = Room, Access = EditorTestSupport.Access(rights ?? []) });

        // The room's next status flush is what every viewer sees: the tile, both rotations and whether it walks.
        public (int X, int Y, int Head, int Body, bool Walking) BroadcastFor(RoomUser unit)
        {
            _viewerPackets.Clear();
            Room.GetRoomUserManager().SerializeStatusUpdates();
            var packet = new FlashIncomingPacket { Buffer = _viewerPackets.Last(sent => sent.Header == ServerPacketHeader.UserUpdateComposer).Payload };
            var count = packet.ReadInt();

            for (var i = 0; i < count; i++) {
                var (id, x, y) = (packet.ReadInt(), packet.ReadInt(), packet.ReadInt());
                packet.ReadString();
                var (head, body) = (packet.ReadInt(), packet.ReadInt());
                packet.ReadInt();
                var status = packet.ReadString();

                if (id == unit.VirtualId) {
                    return (x, y, head, body, status.Contains("/mv "));
                }
            }

            throw new InvalidOperationException($"No status for {unit.VirtualId}");
        }

        private static Pet NewPet()
        {
            var pet = (Pet)RuntimeHelpers.GetUninitializedObject(typeof(Pet));
            pet.PetId = PetId;
            pet.OwnerId = UnitOwnerId;
            pet.RoomId = 42;
            pet.PlacedInRoom = true;
            pet.X = pet.Y = 1;
            pet.Name = "Plant";
            pet.OwnerName = "petowner";
            pet.Type = 16;
            pet.Race = "0";
            pet.Color = "ffffff";
            pet.GnomeClothing = "";
            pet.ExperienceLevels = [100];

            return pet;
        }

        private static void Set(Room room, string field, object value) =>
            typeof(Room).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, value);
    }

    private sealed class RecordingPetStore : IPetRoomStore
    {
        public bool Result { get; set; } = true;
        public Action<PetRoomMove>? BeforeMove { get; set; }
        public List<PetRoomMove> Moves { get; } = [];
        public bool TryMove(PetRoomMove move)
        {
            BeforeMove?.Invoke(move);
            Moves.Add(move);

            return Result;
        }
    }

    private sealed class RecordingBotStore : IBotManagementStore
    {
        public bool Fail { get; set; }
        public Action? BeforeSave { get; set; }
        public List<(int BotId, uint RoomId, int X, int Y, double Z, int Rotation)> Positions { get; } = [];
        public void SavePosition(int botId, uint roomId, int x, int y, double z, int rotation)
        {
            BeforeSave?.Invoke();

            if (Fail) {
                throw new InvalidOperationException("forced persistence failure");
            }

            Positions.Add((botId, roomId, x, y, z, rotation));
        }
        public BotPlacementData Place(int botId, int ownerId, uint roomId, int x, int y) => throw new NotSupportedException();
        public void PickUp(int botId, uint roomId) => throw new NotSupportedException();
        public void SaveAppearance(int botId, uint roomId, string look, string gender) => throw new NotSupportedException();
        public IReadOnlyList<string> SaveSpeech(int botId, uint roomId, IReadOnlyList<string> speech, bool automatic, int interval, bool mix) =>
            throw new NotSupportedException();
        public void SaveWalkingMode(int botId, uint roomId, string mode) => throw new NotSupportedException();
        public void SaveName(int botId, uint roomId, string name) => throw new NotSupportedException();
    }
}
