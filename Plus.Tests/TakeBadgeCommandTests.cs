using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Outgoing;
using Plus.Database;
using Plus.HabboHotel.Badges;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Chat.Commands.Moderator;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Badges;
using Plus.HabboHotel.Users.Inventory;
using Plus.HabboHotel.Users.Inventory.Badges;
using Xunit;

namespace Plus.Tests;

public sealed class TakeBadgeCommandTests
{
    private const string MissingCode = "Please enter the code of the badge you'd like to remove!";
    private const string NotLoaded = "Oops, that user's badges are not loaded (ADM) !";
    private const string NotOwned = "Oops, that user does not have this badge (ADM) !";
    private const string RemovedFromBob = "You have removed the badge ADM from Bob!";
    private const string RemovedOwn = "You have removed your badge ADM!";
    private const string TakenNotice = "A badge has been removed from you!";

    [Fact]
    public async Task StaffRemovesAnotherUsersBadgeFromInventoryAndTheDatabase()
    {
        var writes = new List<Write>();
        var (room, staff, bob, bystander) = RoomWith(writes, bobSlot: 0);
        var command = Command(writes);

        await command.Execute(staff.Client, room, bob.Habbo, ["ADM"]);

        Assert.False(bob.Habbo.Inventory!.Badges.HasBadge("ADM"));
        AssertDeleted(writes, bob.Habbo.Id, "ADM");
        Assert.Equal(0, BadgeCount(bob.Sent));
        AssertWorn(bob.Sent, bob.Habbo.Id, 0, exactly: 1);
        Assert.Equal(TakenNotice, Alert(bob.Sent));
        Assert.Equal(RemovedFromBob, Whisper(staff.Sent));
        Assert.Empty(bystander.Sent);

        bob.Sent.Clear();
        staff.Sent.Clear();
        await command.Execute(staff.Client, room, bob.Habbo, ["ADM"]);

        Assert.Equal(NotOwned, Whisper(staff.Sent));
        Assert.Empty(bob.Sent);
        Assert.Single(writes);
    }

    [Fact]
    public async Task StaffRemovesAWornBadgeForEveryoneInTheRoom()
    {
        var writes = new List<Write>();
        var (room, staff, bob, bystander) = RoomWith(writes, bobSlot: 1);

        await Command(writes).Execute(staff.Client, room, bob.Habbo, ["ADM"]);

        Assert.False(bob.Habbo.Inventory!.Badges.HasBadge("ADM"));
        AssertWorn(bob.Sent, bob.Habbo.Id, 0);
        AssertWorn(bystander.Sent, bob.Habbo.Id, 0, exactly: 1);
        Assert.Equal(RemovedFromBob, Whisper(staff.Sent));
    }

    [Fact]
    public async Task MissingBadgeCodeDoesNotRemoveAnything()
    {
        var writes = new List<Write>();
        var (room, staff, bob, _) = RoomWith(writes, bobSlot: 0);

        await Command(writes).Execute(staff.Client, room, bob.Habbo, []);

        Assert.True(bob.Habbo.Inventory!.Badges.HasBadge("ADM"));
        Assert.Empty(writes);
        Assert.Empty(bob.Sent);
        Assert.Equal(MissingCode, Whisper(staff.Sent));
    }

    [Fact]
    public async Task MissingBadgeDoesNotRemoveAnything()
    {
        var writes = new List<Write>();
        var (room, staff, bob, _) = RoomWith(writes, bobSlot: 0);
        bob.Habbo.Inventory = new InventoryComponent();

        await Command(writes).Execute(staff.Client, room, bob.Habbo, ["ADM"]);

        Assert.Empty(writes);
        Assert.Empty(bob.Sent);
        Assert.Equal(NotOwned, Whisper(staff.Sent));
    }

    [Fact]
    public async Task UnloadedBadgesDoNotRemoveAnything()
    {
        var writes = new List<Write>();
        var (room, staff, bob, _) = RoomWith(writes, bobSlot: 0);
        bob.Habbo.Inventory = null;

        await Command(writes).Execute(staff.Client, room, bob.Habbo, ["ADM"]);

        Assert.Empty(writes);
        Assert.Empty(bob.Sent);
        Assert.Equal(NotLoaded, Whisper(staff.Sent));
    }

    [Fact]
    public async Task StaffWhoDoesNotOutrankTheTargetDoesNotRemoveTheBadge()
    {
        var writes = new List<Write>();
        var (room, staff, bob, _) = RoomWith(writes, bobSlot: 0, staffWeight: 40, bobWeight: 50);

        await Command(writes).Execute(staff.Client, room, bob.Habbo, ["ADM"]);

        Assert.True(bob.Habbo.Inventory!.Badges.HasBadge("ADM"));
        Assert.Empty(writes);
        Assert.Empty(bob.Sent);
        Assert.Empty(staff.Sent);
    }

    [Fact]
    public async Task StaffCanRemoveTheirOwnBadge()
    {
        var writes = new List<Write>();
        var (room, staff, _, _) = RoomWith(writes, bobSlot: 0);
        staff.Habbo.Inventory = Inventory("ADM", 0);

        await Command(writes).Execute(staff.Client, room, staff.Habbo, ["ADM"]);

        Assert.False(staff.Habbo.Inventory.Badges.HasBadge("ADM"));
        AssertDeleted(writes, staff.Habbo.Id, "ADM");
        Assert.Equal(RemovedOwn, Whisper(staff.Sent));
        Assert.DoesNotContain(staff.Sent, packet => packet.Header == ServerPacketHeader.BroadcastMessageAlertComposer);
    }

    [Fact]
    public void FreshInstallGrantsTakeBadgeWhereverItGrantsGiveBadge()
    {
        var grants = Grants(File.ReadAllText(HabbiconPacketTests.Repo("Database/FreshInstall.sql")));
        var give = grants.Where(grant => grant.Permission == "command.givebadge").Select(grant => grant.Role).Order().ToArray();
        var take = grants.Where(grant => grant.Permission == "command.takebadge").Select(grant => grant.Role).Order().ToArray();

        Assert.NotEmpty(give);
        Assert.Equal(give, take);
    }

    [Fact]
    public void ExistingHotelsCopyTakeBadgeFromGiveBadge()
    {
        var sql = File.ReadAllText(HabbiconPacketTests.Repo("Database/Migrations/120_TakeBadgeCommand.sql"));

        Assert.Contains("INSERT IGNORE INTO role_permissions (role_id, permission_key)", sql, StringComparison.Ordinal);
        Assert.Contains("SELECT role_id, 'command.takebadge' FROM role_permissions WHERE permission_key = 'command.givebadge'", sql, StringComparison.Ordinal);
    }

    private static TakeBadgeCommand Command(List<Write> writes) =>
        new(new BadgeManager(new RecordingDatabase(writes), TestGameClientManager.Empty, NullLogger<BadgeManager>.Instance));

    private static (Room Room, Seat Staff, Seat Bob, Seat Bystander) RoomWith(List<Write> writes, int bobSlot, int staffWeight = 40, int bobWeight = 1)
    {
        _ = writes;
        var room = (Room)RuntimeHelpers.GetUninitializedObject(typeof(Room));
        var manager = new RoomUserManager(room, TestRoomUserStore.Instance, TimeProvider.System, new TestRewardProgress(), TestChatEmotions.Unused,
            TestBotAiFactory.Inert, TestGameClientManager.Empty, TestItemRuntime.Travel, TestItemRuntime.Settings);
        typeof(Room).GetField("_roomUserManager", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(room, manager);
        var users = (System.Collections.Concurrent.ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager)
            .GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(manager)!;
        var staff = Seat.Create(room, users, 10, 1, "Sam", Access(staffWeight, PermissionKeys.CommandTakebadge), null);
        var bob = Seat.Create(room, users, 20, 2, "Bob", Access(bobWeight), Inventory("ADM", bobSlot));
        var bystander = Seat.Create(room, users, 30, 3, "Ada", Access(1), null);

        return (room, staff, bob, bystander);
    }

    private static InventoryComponent Inventory(string code, int slot) => new()
    {
        Badges = new BadgesInventoryComponent(new Dictionary<string, Badge> { [code] = new(code, slot) })
    };

    private static UserAccess Access(int weight, params string[] permissions) => UserAccess.Create(
    [
        new RoleAssignment(new AccessRole(weight, "rank-" + weight, "Rank " + weight, weight, 1, "", weight > 1, permissions, new Dictionary<string, int>()))
    ]);

    private static void AssertDeleted(List<Write> writes, int userId, string badge)
    {
        var write = Assert.Single(writes);
        Assert.Contains("DELETE", write.Sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("user_badges", write.Sql, StringComparison.Ordinal);
        Assert.Equal(userId, write.UserId);
        Assert.Equal(badge, write.Badge);
    }

    private static int BadgeCount(List<(uint Header, byte[] Payload)> sent)
    {
        var packet = Packet(sent, ServerPacketHeader.BadgesComposer);
        Assert.Equal(1, packet.ReadInt());
        Assert.Equal(0, packet.ReadInt());

        return packet.ReadInt();
    }

    private static void AssertWorn(List<(uint Header, byte[] Payload)> sent, int userId, int count, int? exactly = null)
    {
        var worn = sent.Where(packet => packet.Header == ServerPacketHeader.HabboUserBadgesComposer)
            .Select(packet =>
            {
                var body = new FlashIncomingPacket { Buffer = packet.Payload };

                return (body.ReadInt(), body.ReadInt());
            }).ToArray();

        if (exactly is { } expected) {
            Assert.Equal(expected, worn.Length);
        }
        else {
            Assert.NotEmpty(worn);
        }

        Assert.All(worn, item => Assert.Equal((userId, count), item));
    }

    private static string Whisper(List<(uint Header, byte[] Payload)> sent)
    {
        var packet = Packet(sent, ServerPacketHeader.WhisperComposer);
        packet.ReadInt();

        return packet.ReadString();
    }

    private static string Alert(List<(uint Header, byte[] Payload)> sent)
    {
        var packet = Packet(sent, ServerPacketHeader.BroadcastMessageAlertComposer);

        return packet.ReadString();
    }

    private static FlashIncomingPacket Packet(List<(uint Header, byte[] Payload)> sent, uint header)
    {
        var match = Assert.Single(sent, packet => packet.Header == header);

        return new FlashIncomingPacket { Buffer = match.Payload };
    }

    private static IReadOnlyList<(int Role, string Permission)> Grants(string sql) =>
        GrantPattern.Matches(sql).Select(match => (int.Parse(match.Groups[1].Value), match.Groups[2].Value)).ToArray();

    private static readonly Regex GrantPattern = new(@"\((\d+),'(command\.[a-z0-9_]+)'\)", RegexOptions.CultureInvariant);

    private sealed record Seat(Habbo Habbo, GameClient Client, List<(uint Header, byte[] Payload)> Sent)
    {
        public static Seat Create(Room room, System.Collections.Concurrent.ConcurrentDictionary<int, RoomUser> users, int id, int virtualId,
            string username, UserAccess access, InventoryComponent? inventory)
        {
            var habbo = new Habbo { Id = id, Username = username, CurrentRoom = room, Access = access, Inventory = inventory };
            var (client, sent) = HabbiconTestSupport.Client(habbo);
            habbo.Client = client;
            var user = new RoomUser(id, 1, virtualId, room, client, TestChatEmotions.Unused, TestRewardProgress.Unused) { UserId = id };
            users[user.VirtualId] = user;

            return new(habbo, client, sent);
        }
    }

    private sealed record Write(string Sql, int UserId, string Badge);

    private sealed class RecordingDatabase(List<Write> writes) : IDatabase
    {
        public bool IsConnected() => true;
        public IDbConnection Connection() => new RecordingConnection(writes);
    }

    private sealed class RecordingConnection(List<Write> writes) : DbConnection
    {
        private ConnectionState _state = ConnectionState.Closed;
        [AllowNull] public override string ConnectionString { get; set; } = "";
        public override string Database => "";
        public override string DataSource => "";
        public override string ServerVersion => "";
        public override ConnectionState State => _state;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() => _state = ConnectionState.Closed;
        public override void Open() => _state = ConnectionState.Open;
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new RecordingCommand(writes) { Connection = this };
    }

    private sealed class RecordingCommand(List<Write> writes) : DbCommand
    {
        [AllowNull] public override string CommandText { get; set; } = "";
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new RecordingParameters();
        protected override DbTransaction? DbTransaction { get; set; }
        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            writes.Add(new(CommandText, IntParameter("userId"), StringParameter("badge")));

            return 1;
        }
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => new DataTable().CreateDataReader();

        private DbParameter Parameter(string name) => Parameters.Cast<DbParameter>().Single(parameter =>
            parameter.ParameterName.Contains(name, StringComparison.Ordinal));

        private int IntParameter(string name) => Parameter(name).Value switch
        {
            int number => number,
            long number => (int)number,
            _ => throw new InvalidOperationException(name)
        };

        private string StringParameter(string name) => Assert.IsType<string>(Parameter(name).Value);
    }

    private sealed class RecordingParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = "";
        public override int Size { get; set; }
        [AllowNull] public override string SourceColumn { get; set; } = "";
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class RecordingParameters : DbParameterCollection
    {
        private readonly List<DbParameter> _items = [];
        public override int Count => _items.Count;
        public override object SyncRoot => _items;
        public override int Add(object value)
        {
            _items.Add((DbParameter)value);

            return _items.Count - 1;
        }
        public override void AddRange(Array values)
        {
            foreach (var value in values) {
                Add(value!);
            }
        }
        public override void Clear() => _items.Clear();
        public override bool Contains(object value) => _items.Contains((DbParameter)value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => ((System.Collections.ICollection)_items).CopyTo(array, index);
        public override System.Collections.IEnumerator GetEnumerator() => _items.GetEnumerator();
        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);
        public override int IndexOf(string parameterName) => _items.FindIndex(parameter => parameter.ParameterName == parameterName);
        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);
        public override void Remove(object value) => _items.Remove((DbParameter)value);
        public override void RemoveAt(int index) => _items.RemoveAt(index);
        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));
        protected override DbParameter GetParameter(int index) => _items[index];
        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];
        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value) => _items[IndexOf(parameterName)] = value;
    }
}
