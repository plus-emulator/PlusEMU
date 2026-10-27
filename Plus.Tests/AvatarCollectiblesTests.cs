using System.Buffers.Binary;
using System.Text;
using Dapper;
using MySqlConnector;
using Plus.Communication.Packets.Incoming;
using Plus.Communication.Packets.Incoming.Avatar;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Avatar;
using Plus.Database.Migrator;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Collectibles;
using Xunit;

namespace Plus.Tests;

// Avatar collectibles on a fresh install with Database/Migrations/103_AvatarCollectibles.sql applied twice.
public sealed class AvatarCollectiblesTests
{
    private const string NormalLook = "hd-180-1.ch-210-66.lg-270-82";
    private const string Shark = "hd-180-1.ch-3030-110.pt-6512-0";
    private const string Wisp = "hd-600-1.ch-3030-110.pt-6322-0";

    [Fact]
    public void HeadersMatchAir()
    {
        Assert.Equal(56u, ClientPacketHeader.GetSelectedNftWardrobeOutfitEvent);
        Assert.Equal(489u, ClientPacketHeader.SaveUserNftWardrobeEvent);
        Assert.Equal(3377u, ServerPacketHeader.SelectedNftWardrobeOutfitComposer);
    }

    [Fact]
    public void SelectedOutfitComposerWritesTheTokenThenTheFallbackLook()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();

        new SelectedNftWardrobeOutfitComposer("12", NormalLook, "M").Compose(packet);

        Assert.Equal(new object[] { "12", NormalLook, "M" }, packet.Writes);
    }

    [RoomComponentDatabaseFact]
    public async Task TheListHoldsOnlyTheUsersOwnCollectiblesInTheOrderTheyWereGiven()
    {
        await InHotel(async hotel =>
        {
            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Dennis, []);
            await hotel.Give(hotel.Staff(Wisp, "f"), hotel.Other, ["clothes"]);
            await hotel.Give(hotel.Staff(Wisp, "f"), hotel.Dennis, ["genesis"]);

            var items = await hotel.List(hotel.Dennis);

            Assert.Equal([(Shark, "M", "habbo:avatar"), (Wisp, "F", "habbo:avatar_genesis")], items.Select(i => (i.Figure, i.Gender, i.ContractKey)));
            Assert.All(items, i => Assert.Equal(i.Id, i.TokenId));
            Assert.True(int.Parse(items[0].Id) < int.Parse(items[1].Id));
            Assert.Equal([(Wisp, "F", "habbo:clothes")], (await hotel.List(hotel.Other)).Select(i => (i.Figure, i.Gender, i.ContractKey)));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task TheCommandNamesTheCollectionAndGivesNothingForAnUnknownOne()
    {
        await InHotel(async hotel =>
        {
            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Dennis, ["avatar"]);
            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Dennis, ["clothes"]);
            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Dennis, ["bogus"]);

            Assert.Equal(["habbo:avatar", "habbo:clothes"], (await hotel.List(hotel.Dennis)).Select(i => i.ContractKey));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task StaffCannotGiveToAnEqualOrHigherRankButMayGiveThemselves()
    {
        await InHotel(async hotel =>
        {
            var staff = hotel.Staff(Shark, "m");
            await hotel.Give(staff, hotel.User(9, "peer", NormalLook, weight: 50), []);
            await hotel.Give(staff, staff, []);

            Assert.Empty(await hotel.List(hotel.User(9, "peer", NormalLook, weight: 50)));
            Assert.Equal([Shark], (await hotel.List(staff)).Select(i => i.Figure));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task AUserWhoAlreadyListedTheirCollectiblesGetsTheNewOneRightAway()
    {
        await InHotel(async hotel =>
        {
            await hotel.List(hotel.Dennis);
            hotel.Sent(hotel.Dennis).Clear();
            hotel.Sent(hotel.Other).Clear();

            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Dennis, []);
            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Other, []);

            var pushed = Assert.Single(hotel.Sent(hotel.Dennis), p => p.Header == ServerPacketHeader.UserNftWardrobeComposer);
            Assert.Equal([Shark], ReadList(pushed.Payload).Select(i => i.Figure));
            Assert.DoesNotContain(hotel.Sent(hotel.Other), p => p.Header == ServerPacketHeader.UserNftWardrobeComposer);
        });
    }

    [RoomComponentDatabaseFact]
    public async Task NoWornCollectibleSendsNoSelectedOutfit()
    {
        await InHotel(async hotel =>
        {
            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Dennis, []);

            Assert.Null(await hotel.Selected(hotel.Dennis));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task WearingACollectibleChangesTheLookAndRemembersTheOneItReplaced()
    {
        await InHotel(async hotel =>
        {
            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Dennis, []);
            var shark = Assert.Single(await hotel.List(hotel.Dennis));
            hotel.Sent(hotel.Dennis).Clear();

            await hotel.Wear(hotel.Dennis, shark.Id);

            Assert.Equal((Shark, "M"), hotel.SavedLook(hotel.Dennis.Id));
            Assert.Equal(Shark, hotel.Dennis.Look);
            Assert.Contains(hotel.Sent(hotel.Dennis), p => p.Header == ServerPacketHeader.AvatarAspectUpdateComposer);
            Assert.Equal((shark.TokenId, NormalLook, "M"), await hotel.Selected(hotel.Dennis));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task SwitchingToAnotherCollectibleKeepsTheOriginalLookAsFallback()
    {
        await InHotel(async hotel =>
        {
            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Dennis, []);
            await hotel.Give(hotel.Staff(Wisp, "f"), hotel.Dennis, []);
            var items = await hotel.List(hotel.Dennis);

            await hotel.Wear(hotel.Dennis, items[0].Id);
            await hotel.Wear(hotel.Dennis, items[1].Id);

            Assert.Equal((Wisp, "F"), hotel.SavedLook(hotel.Dennis.Id));
            Assert.Equal((items[1].TokenId, NormalLook, "M"), await hotel.Selected(hotel.Dennis));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task AnyOtherLookChangeTakesTheCollectibleOff()
    {
        await InHotel(async hotel =>
        {
            await hotel.Give(hotel.Staff(Shark, "m"), hotel.Dennis, []);
            await hotel.Wear(hotel.Dennis, Assert.Single(await hotel.List(hotel.Dennis)).Id);

            // A normal save in the avatar editor, a mannequin or a command writes another look.
            hotel.Connection.Execute("UPDATE users SET look = 'hd-190-1.ch-215-66', gender = 'M' WHERE id = 7");
            hotel.Dennis.Look = "hd-190-1.ch-215-66";

            Assert.Null(await hotel.Selected(hotel.Dennis));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task SomeoneElsesOrAnUnknownCollectibleIsIgnored()
    {
        await InHotel(async hotel =>
        {
            await hotel.Give(hotel.Staff(Wisp, "f"), hotel.Other, []);
            var theirs = Assert.Single(await hotel.List(hotel.Other));
            hotel.Sent(hotel.Dennis).Clear();

            await hotel.Wear(hotel.Dennis, theirs.Id);
            await hotel.Wear(hotel.Dennis, "nope");
            await hotel.Wear(hotel.Dennis, "999999");

            Assert.Equal((NormalLook, "M"), hotel.SavedLook(hotel.Dennis.Id));
            Assert.Equal(NormalLook, hotel.Dennis.Look);
            Assert.Empty(hotel.Sent(hotel.Dennis));
            Assert.Null(await hotel.Selected(hotel.Dennis));
        });
    }

    [RoomComponentDatabaseFact]
    public async Task EveryRoleThatMayGiveBadgesMayGiveCollectibles()
    {
        await InHotel(hotel =>
        {
            Assert.NotEmpty(hotel.Connection.Query<int>("SELECT role_id FROM role_permissions WHERE permission_key = 'command.givebadge'"));
            Assert.Equal(
                hotel.Connection.Query<int>("SELECT role_id FROM role_permissions WHERE permission_key = 'command.givebadge' ORDER BY role_id"),
                hotel.Connection.Query<int>("SELECT role_id FROM role_permissions WHERE permission_key = 'command.givecollectible' ORDER BY role_id"));
            Assert.Equal(1, hotel.Connection.QuerySingle<int>("SELECT COUNT(*) FROM acl_permissions WHERE `key` = 'command.givecollectible'"));

            return Task.CompletedTask;
        });
    }

    private static List<NftWardrobeItem> ReadList(byte[] payload)
    {
        var reader = new PayloadReader(payload);
        var items = new List<NftWardrobeItem>();

        for (var count = reader.Int(); count > 0; count--) {
            items.Add(new(reader.String(), reader.String(), reader.String(), reader.String(), reader.String()));
        }

        return items;
    }

    private sealed class PayloadReader(byte[] payload)
    {
        private int _at;

        public int Int()
        {
            var value = BinaryPrimitives.ReadInt32BigEndian(payload.AsSpan(_at, 4));
            _at += 4;

            return value;
        }

        public string String()
        {
            var length = BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(_at, 2));
            var value = Encoding.UTF8.GetString(payload, _at + 2, length);
            _at += 2 + length;

            return value;
        }
    }

    private sealed class Hotel(MySqlConnection connection, NftWardrobeService collectibles)
    {
        private readonly Dictionary<int, (Habbo Habbo, List<(uint Header, byte[] Payload)> Sent)> _users = new();

        public MySqlConnection Connection => connection;
        public Habbo Dennis => User(7, "dennis", NormalLook);
        public Habbo Other => User(8, "other", NormalLook);

        public Habbo Staff(string look, string gender)
        {
            var staff = User(1, "staff", look, weight: 50, permissions: ["*"]);
            staff.Look = look;
            staff.Gender = gender;

            return staff;
        }

        public Habbo User(int id, string name, string look, int weight = 1, string[]? permissions = null)
        {
            if (_users.TryGetValue(id, out var known)) {
                return known.Habbo;
            }

            connection.Execute("INSERT IGNORE INTO users (id, username, look, gender) VALUES (@id, @name, @look, 'M')", new { id, name, look });
            var habbo = new Habbo { Id = id, Username = name, Look = look, Gender = "m", Access = EditorTestSupport.Access(permissions ?? [], weight) };
            var (client, sent) = HabbiconTestSupport.Client(habbo);
            habbo.Client = client;
            _users[id] = (habbo, sent);

            return habbo;
        }

        public List<(uint Header, byte[] Payload)> Sent(Habbo habbo) => _users[habbo.Id].Sent;

        public (string, string) SavedLook(int id) => connection.QuerySingle<(string, string)>("SELECT look, gender FROM users WHERE id = @id", new { id });

        public Task Give(Habbo staff, Habbo target, string[] parameters) =>
            new GiveCollectibleCommand(collectibles).Execute(staff.Client!, null!, target, parameters);

        public async Task<List<NftWardrobeItem>> List(Habbo habbo)
        {
            var sent = Sent(habbo);
            var before = sent.Count;
            await new GetUserNftWardrobeEvent(collectibles).Parse(habbo.Client!, HabbiconTestSupport.Incoming());
            var (header, payload) = Assert.Single(sent.Skip(before));
            Assert.Equal(ServerPacketHeader.UserNftWardrobeComposer, header);

            return ReadList(payload);
        }

        public async Task<(string Token, string Figure, string Gender)?> Selected(Habbo habbo)
        {
            var sent = Sent(habbo);
            var before = sent.Count;
            await new GetSelectedNftWardrobeOutfitEvent(collectibles).Parse(habbo.Client!, HabbiconTestSupport.Incoming());
            var reply = sent.Skip(before).ToList();

            if (reply.Count == 0) {
                return null;
            }

            var (header, payload) = Assert.Single(reply);
            Assert.Equal(ServerPacketHeader.SelectedNftWardrobeOutfitComposer, header);
            var reader = new PayloadReader(payload);

            return (reader.String(), reader.String(), reader.String());
        }

        public Task Wear(Habbo habbo, string id) =>
            new SaveUserNftWardrobeEvent(collectibles).Parse(habbo.Client!, HabbiconTestSupport.Incoming(id));
    }

    private static async Task InHotel(Func<Hotel, Task> run)
    {
        var options = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ROOM_COMPONENT_DATABASE")!) { AllowUserVariables = true };
        await using var admin = new MySqlConnection(options.ConnectionString);
        await admin.OpenAsync();
        var schema = "avatar_collectibles_" + Guid.NewGuid().ToString("N");
        await admin.ExecuteAsync($"CREATE DATABASE `{schema}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");

        try {
            options.Database = schema;
            await using var connection = new MySqlConnection(options.ConnectionString);
            await connection.OpenAsync();

            foreach (var file in new[] { "Database/FreshInstall.sql", "Database/Migrations/103_AvatarCollectibles.sql", "Database/Migrations/103_AvatarCollectibles.sql" }) {
                foreach (var statement in SqlScript.Split(await File.ReadAllTextAsync(HabbiconPacketTests.Repo(file)))) {
                    await connection.ExecuteAsync(statement, commandTimeout: 300);
                }
            }

            await run(new Hotel(connection, new NftWardrobeService(new HabbiconDatabaseTests.TestDatabase(options.ConnectionString))));
        }
        finally {
            await admin.ExecuteAsync($"DROP DATABASE IF EXISTS `{schema}`");
        }
    }
}
