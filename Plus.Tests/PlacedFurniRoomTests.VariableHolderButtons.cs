using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms;
using Plus.Communication.Packets.Incoming.WiredVariables;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Cache;
using Plus.HabboHotel.Cache.Type;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Settings;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// The Variables tab's Highlight (all holders), Delete (all holders) and Manage (owners page, one user's permanent variables, one write) buttons,
// through their raw request packets in a real room with placed variable boxes.
public partial class PlacedFurniRoomTests
{
    private const uint VbUserActive = 501, VbUserPermanent = 502, VbUserShared = 503, VbUserNoValue = 504, VbFurniPermanent = 505,
        VbFurniRoom = 506, VbFurniNoValue = 507, VbGlobal = 508, VbContext = 509, VbEcho = 510, VbPointer = 511;
    private const uint VbHolderA = 601, VbHolderB = 602;
    private const int VbOffline = 20, VbIdle = 21;
    private static readonly DateTimeOffset VbBase = new(2026, 10, 28, 10, 0, 0, TimeSpan.Zero);

    private MemoryWiredVariableStore _vbStore = null!;
    private SpeechClock _vbClock = null!;
    private WiredComponent _vbWired = null!;
    private RoomUser _vbOwner = null!;
    private TestClient _vbGuest = null!;

    private static DateTimeOffset VbAt(int hours) => VbBase.AddHours(hours);

    private void VariableButtonsRoom()
    {
        _vbClock = new SpeechClock { Now = VbBase };
        (_vbWired, _vbOwner) = PrepareSpeech(_vbClock);
        _room.UsersWithRights = [];
        _client.GetHabbo().Look = "hd-7";
        _vbStore = new MemoryWiredVariableStore();
        var module = new WiredVariableModule(RoomId, new ButtonsDirectory(_room), new NamedStore(_vbStore), _vbClock);
        typeof(WiredRoomVariables).GetField("<Module>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_vbWired.Variables, module);
        _dpService = new WiredConfigurationService(TestWiredConfigurationStore.Instance, null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
        var boxes = new[] { (VbUserActive, "wf_var_user"), (VbUserPermanent, "wf_var_user"), (VbUserShared, "wf_var_user"), (VbUserNoValue, "wf_var_user"),
            (VbFurniPermanent, "wf_var_furni"), (VbFurniRoom, "wf_var_furni"), (VbFurniNoValue, "wf_var_furni"), (VbGlobal, "wf_var_room"), (VbContext, "wf_var_context"), (VbEcho, "wf_var_echo"), (VbPointer, "wf_var_pointer") };

        for (var i = 0; i < boxes.Length; i++) {
            var box = WiredBox(boxes[i].Item1, boxes[i].Item2, i % 4, 3);
            Assert.True(_vbWired.AddBox((IWiredItem)box));
            _vbWired.Variables.ConfigurationSaved(box);
        }

        foreach (var (id, x) in new[] { (VbHolderA, 0), (VbHolderB, 2) }) {
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, Furni(id, InteractionType.None, WiredBoxType.None), x, 0, 0, true, false, false));
        }

        AddSpeechBox(_dpNext++, "wf_trg_says_something", new() { IntParams = [1, 0, 0], Text = "go" }, _dpZ++);
        PlaceSavedBox(_dpNext++, "wf_xtra_exec_in_order", 1, 1, _dpZ++);
    }

    private TestClient VbVisitor(bool rights)
    {
        var visitor = AddLeaver(out var client);
        Assert.NotNull(visitor);

        if (rights) {
            _room.UsersWithRights.Add(LeaverId);
        }

        client.Packets.Clear();
        _vbGuest = client;

        return client;
    }

    private RoomUser VbVisitorUser => Assert.IsType<RoomUser>(_room.GetRoomUserManager().GetRoomUserByHabbo(LeaverId));

    /// <summary>A stored value of a permanent variable, as a holder who may be offline keeps it.</summary>
    private void VbSeed(uint definition, WiredVariableTarget target, long holder, long value, int createdHour = 0, int updatedHour = 0) =>
        _vbStore.Mutate(new(definition, target, holder), _ => new(value, VbAt(createdHour), VbAt(updatedHour)));

    /// <summary>A value given to a live holder through the room's variables.</summary>
    private void VbGive(uint definition, WiredVariableHolder holder, long value) =>
        Assert.True(_vbWired.Variables.Module.Mutate(new(holder.Target, $"custom:{definition}"), holder, WiredVariableMutation.Replace, value,
            _vbWired.CaptureVariableInspectionFrame()));

    private WiredVariableHolder VbFurni(uint id) => WiredVariableRuntimeFrames.FurniHolder(_room.GetRoomItemHandler().GetItem(id));

    private WiredVariableValue? VbStored(uint definition, WiredVariableTarget target, long holder) => _vbStore.Read(new(definition, target, holder));

    private async Task<(uint Header, byte[] Body)[]> VbAsk(RoomPacketEvent handler, TestClient client, params object[] body)
    {
        client.Packets.Clear();
        await handler.Parse(_room, client, ClientPacket(body));

        return client.Packets.ToArray();
    }

    private static WiredVariableHolderService VbService() => new(new ButtonsUsers());
    private Task<(uint Header, byte[] Body)[]> VbHolders(string id, TestClient? client = null) => VbAsk(new WiredGetAllVariableHoldersEvent(VbService()), client ?? _client, id);
    private Task<(uint Header, byte[] Body)[]> VbDelete(string id, TestClient? client = null) => VbAsk(new WiredDeleteAllVariableHoldersEvent(VbService()), client ?? _client, id);
    private Task<(uint Header, byte[] Body)[]> VbOwners(string id, int page, int size, int sort, int userType, TestClient? client = null) =>
        VbAsk(new WiredGetVariableOwnersPageEvent(VbService()), client ?? _client, id, page, size, sort, userType);
    private Task<(uint Header, byte[] Body)[]> VbPermanent(int entityType, int entityId, TestClient? client = null) =>
        VbAsk(new WiredGetUserPermanentVariablesEvent(VbService()), client ?? _client, entityType, entityId);
    private Task<(uint Header, byte[] Body)[]> VbSet(int entityType, int entityId, string id, int value, int action, TestClient? client = null) =>
        VbAsk(new WiredSetUserPermanentVariableEvent(VbService()), client ?? _client, entityType, entityId, id, value, action);

    private static (uint, byte[]) VbError(short code) => (ServerPacketHeader.WiredMenuErrorComposer, VbBody(code));
    private static (uint, byte[]) VbResult(bool success) => (ServerPacketHeader.WiredSetUserPermanentVariableResultComposer, VbBody(success));

    private static void VbEqual((uint Header, byte[] Body)[] expected, (uint Header, byte[] Body)[] actual)
    {
        Assert.Equal(expected.Select(packet => packet.Header), actual.Select(packet => packet.Header));

        for (var i = 0; i < expected.Length; i++) {
            Assert.Equal(Convert.ToHexString(expected[i].Body), Convert.ToHexString(actual[i].Body));
        }
    }

    /// <summary>A server packet body: big-endian ints, shorts and longs, booleans as one byte and short-prefixed UTF-8 strings.</summary>
    private static byte[] VbBody(params object[] values)
    {
        using var stream = new MemoryStream();

        foreach (var value in values) {
            switch (value) {
                case bool flag:
                    stream.WriteByte(flag ? (byte)1 : (byte)0);
                    break;
                case short number:
                    var two = new byte[2];
                    BinaryPrimitives.WriteInt16BigEndian(two, number);
                    stream.Write(two);
                    break;
                case long number:
                    var eight = new byte[8];
                    BinaryPrimitives.WriteInt64BigEndian(eight, number);
                    stream.Write(eight);
                    break;
                case string text:
                    var bytes = Encoding.UTF8.GetBytes(text);
                    var length = new byte[2];
                    BinaryPrimitives.WriteInt16BigEndian(length, (short)bytes.Length);
                    stream.Write(length);
                    stream.Write(bytes);
                    break;
                case object[] nested:
                    stream.Write(VbBody(nested));
                    break;
                default:
                    var four = new byte[4];
                    BinaryPrimitives.WriteInt32BigEndian(four, (int)value);
                    stream.Write(four);
                    break;
            }
        }

        return stream.ToArray();
    }

    // The variable block of the catalogue: id, target, name, availability, target, then the flags of a plain custom variable.
    private static object[] VbVariable(string id, int target, string name, int availability, bool hasValue) =>
        [id, target, name, availability, target, true, true, hasValue, hasValue, false, false, false, true, true, false];

    private static object[] VbStamp(int hour) => [VbAt(hour).ToUnixTimeMilliseconds(), VbAt(hour).UtcDateTime.ToString("dd-MM-yyyy HH:mm:ss")];

    // Highlight lists the users in the room who hold the variable, by room index from highest to lowest; a holder who left is not listed.
    [Fact]
    public async Task HighlightListsTheUsersInTheRoomWhoHoldAUserVariable()
    {
        VariableButtonsRoom();
        VbVisitor(rights: false);
        var visitor = VbVisitorUser;
        await DpGive(DpUser, "user:" + VbUserPermanent, 7);
        DpFire(_vbWired, _vbOwner, _vbClock, 1);
        VbGive(VbUserPermanent, WiredVariableRuntimeFrames.UserHolder(visitor), 3);
        VbSeed(VbUserPermanent, WiredVariableTarget.User, VbOffline, 9);

        var rows = new[] { (_vbOwner.VirtualId, 7), (visitor.VirtualId, 3) }.OrderByDescending(row => row.Item1)
            .SelectMany(row => new object[] { row.Item1, row.Item2 }).ToArray();
        VbEqual([(ServerPacketHeader.WiredAllVariableHoldersComposer,
            VbBody(2, VbVariable("user:502", 1, "up", 10, true), 2, rows))], await VbHolders("user:502"));
    }

    // Furni holders come by furni id from highest to lowest; a variable without a value lists -1.
    [Fact]
    public async Task HighlightListsTheFurniWhoHoldAFurniVariable()
    {
        VariableButtonsRoom();
        VbGive(VbFurniPermanent, VbFurni(VbHolderA), 4);
        VbGive(VbFurniPermanent, VbFurni(VbHolderB), 6);
        VbGive(VbFurniNoValue, VbFurni(VbHolderA), 0);

        VbEqual([(ServerPacketHeader.WiredAllVariableHoldersComposer,
            VbBody(1, VbVariable("furni:505", 2, "fp", 10, true), 2, (int)VbHolderB, 6, (int)VbHolderA, 4))], await VbHolders("furni:505"));
        VbEqual([(ServerPacketHeader.WiredAllVariableHoldersComposer,
            VbBody(1, VbVariable("furni:507", 2, "fn", 1, false), 1, (int)VbHolderA, -1))], await VbHolders("furni:507"));
    }

    [Theory]
    [InlineData("room:508")]
    [InlineData("ctx:509")]
    [InlineData("user:511")]
    [InlineData("999999")]
    [InlineData("@id")]
    [InlineData("~chest.available_amount")]
    public async Task HighlightOfAVariableWithoutHoldersAnswersErrorTwo(string id)
    {
        VariableButtonsRoom();

        VbEqual([VbError(2)], await VbHolders(id));
    }

    // An echo variable lists the holders of the variable it echoes; it has no owners page and is never cleared.
    [Fact]
    public async Task HighlightOfAnEchoListsTheHoldersOfItsVariable()
    {
        VariableButtonsRoom();
        VbGive(VbUserPermanent, WiredVariableRuntimeFrames.UserHolder(_vbOwner), 7);

        var reply = Assert.Single(await VbHolders("user:510"));
        Assert.Equal(ServerPacketHeader.WiredAllVariableHoldersComposer, reply.Header);
        var input = new FlashIncomingPacket { Buffer = reply.Body };
        Assert.Equal(2, input.ReadInt());
        Assert.Equal("user:510", input.ReadString());
        Assert.Equal(Convert.ToHexString(VbBody(1, _vbOwner.VirtualId, 7)), Convert.ToHexString(reply.Body[^12..]));
        VbEqual([VbError(3)], await VbOwners("user:510", 1, 10, 0, -1));
        VbEqual([VbError(1)], await VbDelete("user:510"));
        Assert.Equal(7, VbStored(VbUserPermanent, WiredVariableTarget.User, 7)!.Value);
    }

    // Set on an echo is refused like on any variable that is not permanent.
    [Fact]
    public async Task SetOnAnEchoVariableFails()
    {
        VbPermanentRoom();
        VbSeed(VbUserPermanent, WiredVariableTarget.User, 7, 4);

        foreach (var action in new[] { 0, 1, 2 }) {
            VbEqual([VbError(1), VbResult(false)], await VbSet(1, 7, "user:510", 5, action));
        }

        Assert.Equal(4, VbStored(VbUserPermanent, WiredVariableTarget.User, 7)!.Value);
    }

    [Fact]
    public async Task HighlightNeedsInspectRightsAndTheRoom()
    {
        VariableButtonsRoom();
        VbGive(VbUserPermanent, WiredVariableRuntimeFrames.UserHolder(_vbOwner), 7);
        var visitor = VbVisitor(rights: false);

        Assert.Empty(await VbHolders("user:502", visitor));
        Assert.Empty(await VbHolders("room:508", visitor));
        _client.GetHabbo().CurrentRoom = null;
        Assert.Empty(await VbHolders("user:502"));
    }

    // Delete clears a permanent variable from every holder, offline ones too, and answers nothing.
    [Theory]
    [InlineData(VbUserPermanent, WiredVariableTarget.User, 7, VbOffline)]
    [InlineData(VbUserShared, WiredVariableTarget.User, 7, VbOffline)]
    [InlineData(VbFurniPermanent, WiredVariableTarget.Furni, (int)VbHolderA, 999)]
    public async Task DeleteClearsEveryHolderOfAPermanentVariable(uint definition, WiredVariableTarget target, int present, int absent)
    {
        VariableButtonsRoom();
        VbSeed(definition, target, present, 5);
        VbSeed(definition, target, absent, 6);
        var id = (target == WiredVariableTarget.User ? "user:" : "furni:") + definition;

        Assert.Empty(await VbDelete(id));

        Assert.Empty(_vbStore.GetHolders(definition));
        var holders = Assert.Single(await VbHolders(id));
        Assert.Equal(ServerPacketHeader.WiredAllVariableHoldersComposer, holders.Header);
        Assert.Equal([0, 0, 0, 0], holders.Body[^4..]);
    }

    [Theory]
    [InlineData("user:501")]
    [InlineData("furni:506")]
    [InlineData("room:508")]
    [InlineData("ctx:509")]
    [InlineData("999999")]
    public async Task DeleteOfAVariableThatIsNotPermanentAnswersErrorOne(string id)
    {
        VariableButtonsRoom();
        VbGive(VbUserActive, WiredVariableRuntimeFrames.UserHolder(_vbOwner), 5);
        VbGive(VbFurniRoom, VbFurni(VbHolderA), 5);

        VbEqual([VbError(1)], await VbDelete(id));

        Assert.Equal(5, BinaryPrimitives.ReadInt32BigEndian(Assert.Single(await VbHolders("user:501")).Body.AsSpan()[^4..]));
        Assert.Equal(5, BinaryPrimitives.ReadInt32BigEndian(Assert.Single(await VbHolders("furni:506")).Body.AsSpan()[^4..]));
    }

    [Theory]
    [InlineData("@id")]
    [InlineData("~chest.available_amount")]
    [InlineData("")]
    public async Task DeleteOfAnInternalSmartOrEmptyIdIsDropped(string id)
    {
        VariableButtonsRoom();
        VbSeed(VbUserPermanent, WiredVariableTarget.User, VbOffline, 5);

        Assert.Empty(await VbDelete(id));
        Assert.Equal(5, VbStored(VbUserPermanent, WiredVariableTarget.User, VbOffline)!.Value);
    }

    // Only the owner clears all holders. A user who may change variables gets error 7; a user who may not gets nothing.
    [Fact]
    public async Task DeleteIsForTheOwnerOnly()
    {
        VariableButtonsRoom();
        VbSeed(VbUserPermanent, WiredVariableTarget.User, VbOffline, 5);
        var guest = VbVisitor(rights: false);

        Assert.Empty(await VbDelete("user:502", guest));
        _room.UsersWithRights.Add(LeaverId);
        Assert.True(_room.GetWired().Settings.TrySave(_client, 2, 2, "", out _));
        VbEqual([VbError(7)], await VbDelete("user:502", guest));
        Assert.Equal(5, VbStored(VbUserPermanent, WiredVariableTarget.User, VbOffline)!.Value);
    }

    // Three holders of a permanent user variable: the owner (30, created 1, updated 4), the guest (12, created 2, updated 5) and an
    // offline user (20, created 3, updated 3).
    private void VbOwnersRoom()
    {
        VariableButtonsRoom();
        VbVisitor(rights: false);
        VbSeed(VbUserPermanent, WiredVariableTarget.User, 7, 30, 1, 4);
        VbSeed(VbUserPermanent, WiredVariableTarget.User, LeaverId, 12, 2, 5);
        VbSeed(VbUserPermanent, WiredVariableTarget.User, VbOffline, 20, 3, 3);
    }

    private static object[] VbOwnerRow(int user, string name, int value, int created, int updated) =>
        [1, user, name, value, VbStamp(created), VbStamp(updated)];

    [Fact]
    public async Task OwnersPageListsEveryPermanentHolderWithTimes()
    {
        VbOwnersRoom();

        VbEqual([(ServerPacketHeader.WiredUserVariablesListComposer, VbBody("user:502", 3, 1, 10, 3,
            VbOwnerRow(7, "owner", 30, 1, 4), VbOwnerRow(VbOffline, "offline", 20, 3, 3), VbOwnerRow(LeaverId, "guest", 12, 2, 5), -1, 0))],
            await VbOwners("user:502", 1, 10, 0, -1));
    }

    private static (string Id, int Total, int Page, int Amount, string Users, int UserType, int Sort) VbReadOwners((uint Header, byte[] Body)[] packets)
    {
        var packet = Assert.Single(packets);
        Assert.Equal(ServerPacketHeader.WiredUserVariablesListComposer, packet.Header);
        var input = new FlashIncomingPacket { Buffer = packet.Body };
        var (id, total, page, amount) = (input.ReadString(), input.ReadInt(), input.ReadInt(), input.ReadInt());
        var users = new List<int>();

        for (var count = input.ReadInt(); count > 0; count--) {
            Assert.Equal(1, input.ReadInt());
            users.Add(input.ReadInt());
            input.ReadString();
            input.ReadInt();
            input.ReadInt();
            input.ReadInt();
            input.ReadString();
            input.ReadInt();
            input.ReadInt();
            input.ReadString();
        }

        var result = (id, total, page, amount, string.Join(",", users), input.ReadInt(), input.ReadInt());
        Assert.False(input.HasDataRemaining());

        return result;
    }

    // 0 highest value first, 1 lowest value first, 2 oldest creation first, 3 newest creation first, 4 oldest update first,
    // 5 newest update first; any other sort is 0 and is echoed as sent.
    [Theory]
    [InlineData(0, new[] { 7, VbOffline, LeaverId })]
    [InlineData(1, new[] { LeaverId, VbOffline, 7 })]
    [InlineData(2, new[] { 7, LeaverId, VbOffline })]
    [InlineData(3, new[] { VbOffline, LeaverId, 7 })]
    [InlineData(4, new[] { VbOffline, 7, LeaverId })]
    [InlineData(5, new[] { LeaverId, 7, VbOffline })]
    [InlineData(9, new[] { 7, VbOffline, LeaverId })]
    [InlineData(-1, new[] { 7, VbOffline, LeaverId })]
    public async Task OwnersPageSortsAndEchoesTheSort(int sort, int[] users)
    {
        VbOwnersRoom();

        Assert.Equal(("user:502", 3, 1, 10, string.Join(",", users), 0, sort), VbReadOwners(await VbOwners("user:502", 1, 10, sort, 0)));
    }

    [Fact]
    public async Task OwnersPageSplitsPagesAndAPageAfterTheEndIsEmpty()
    {
        VbOwnersRoom();

        Assert.Equal(("user:502", 3, 2, 1, $"{VbOffline}", -1, 0), VbReadOwners(await VbOwners("user:502", 2, 1, 0, -1)));
        Assert.Equal(("user:502", 3, 4, 1, "", -1, 0), VbReadOwners(await VbOwners("user:502", 4, 1, 0, -1)));
        Assert.Equal(("user:502", 3, 1, 50, $"7,{VbOffline},{LeaverId}", -1, 0), VbReadOwners(await VbOwners("user:502", 1, 50, 0, -1)));
    }

    // -1 and 0 list everybody and 1 lists Habbos; pets (2), bots (4) and any other type list nobody. The type is echoed as sent.
    [Theory]
    [InlineData(-1, 3)]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 0)]
    public async Task OwnersPageFiltersByUserType(int userType, int total)
    {
        VbOwnersRoom();

        var page = VbReadOwners(await VbOwners("user:502", 1, 10, 0, userType));
        Assert.Equal((total, total, userType), (page.Total, page.Users.Split(",", StringSplitOptions.RemoveEmptyEntries).Length, page.UserType));
    }

    [Theory]
    [InlineData("furni:505", 10)]
    [InlineData("room:508", 10)]
    [InlineData("ctx:509", 10)]
    [InlineData("user:501", 10)]
    [InlineData("@id", 10)]
    [InlineData("999999", 10)]
    [InlineData("user:502", 0)]
    [InlineData("user:502", -1)]
    [InlineData("user:502", 51)]
    public async Task OwnersPageOfAnotherVariableOrSizeAnswersErrorThree(string id, int size)
    {
        VbOwnersRoom();

        VbEqual([VbError(3)], await VbOwners(id, 1, size, 0, -1));
    }

    [Fact]
    public async Task OwnersPageBeforeTheFirstPageOrWithoutInspectRightsIsDropped()
    {
        VbOwnersRoom();
        Assert.Empty(await VbOwners("user:502", 0, 10, 0, -1));
        Assert.Empty(await VbOwners("user:502", -1, 10, 0, -1));
        Assert.Empty(await VbOwners("user:502", 1, 10, 0, -1, _vbGuest));
    }

    [Fact]
    public async Task OwnersPageOfAVariableWithoutAValueIsEmpty()
    {
        VbOwnersRoom();
        VbSeed(VbUserNoValue, WiredVariableTarget.User, VbOffline, -1);

        Assert.Equal(("user:504", 0, 1, 10, "", -1, 0), VbReadOwners(await VbOwners("user:504", 1, 10, 0, -1)));
    }

    // The offline user holds the permanent up (20) and the valueless un; the owner holds only the room-active ua.
    private void VbPermanentRoom()
    {
        VariableButtonsRoom();
        VbSeed(VbUserPermanent, WiredVariableTarget.User, VbOffline, 20, 3, 4);
        VbSeed(VbUserNoValue, WiredVariableTarget.User, VbOffline, -1, 1, 1);
        VbGive(VbUserActive, WiredVariableRuntimeFrames.UserHolder(_vbOwner), 5);
    }

    private static object[] VbPermanentRow(string id, int value, int created, int updated) => [id, value, VbStamp(created), VbStamp(updated)];

    private static (uint, byte[]) VbOfflineList(int value, int created, int updated) => (ServerPacketHeader.WiredUserPermanentVariablesComposer,
        VbBody(1, VbOffline, "offline", "hd-20", 2, VbPermanentRow("user:504", -1, 1, 1), VbPermanentRow("user:502", value, created, updated)));

    [Fact]
    public async Task PermanentListShowsAnOfflineUsersPermanentVariables()
    {
        VbPermanentRoom();

        VbEqual([VbOfflineList(20, 3, 4)], await VbPermanent(1, VbOffline));
    }

    [Fact]
    public async Task PermanentListOfTheRequesterIsAlwaysSent()
    {
        VbPermanentRoom();

        VbEqual([(ServerPacketHeader.WiredUserPermanentVariablesComposer, VbBody(1, 7, "owner", "hd-7", 0))], await VbPermanent(1, 7));
    }

    // Another user who holds no permanent variable of the room, an unknown user and every entity type other than Habbo answer error 4.
    [Theory]
    [InlineData(1, VbIdle)]
    [InlineData(1, 999)]
    [InlineData(0, VbOffline)]
    [InlineData(2, VbOffline)]
    [InlineData(4, VbOffline)]
    public async Task PermanentListOfAnotherEntityAnswersErrorFour(int entityType, int entityId)
    {
        VbPermanentRoom();

        VbEqual([VbError(4)], await VbPermanent(entityType, entityId));
    }

    [Fact]
    public async Task PermanentListNeedsInspectRights()
    {
        VbPermanentRoom();

        Assert.Empty(await VbPermanent(1, VbOffline, VbVisitor(rights: false)));
    }

    // Set keeps the creation time and moves the update time; 0, negative values and the largest int are accepted.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, -5)]
    [InlineData(0, int.MaxValue)]
    [InlineData(7, 11)]
    public async Task SetChangesAnOfflineUsersValue(int action, int value)
    {
        VbPermanentRoom();
        _vbClock.Now = VbAt(6);

        VbEqual([VbResult(true), VbOfflineList(value, 3, 6)], await VbSet(1, VbOffline, "user:502", value, action));
        Assert.Equal(new WiredVariableValue(value, VbAt(3), VbAt(6)), VbStored(VbUserPermanent, WiredVariableTarget.User, VbOffline));
    }

    [Fact]
    public async Task CreateReplacesTheValueAndResetsTheCreationTime()
    {
        VbPermanentRoom();
        _vbClock.Now = VbAt(6);

        VbEqual([VbResult(true), VbOfflineList(8, 6, 6)], await VbSet(1, VbOffline, "user:502", 8, 1));

        Assert.Equal(new WiredVariableValue(8, VbAt(6), VbAt(6)), VbStored(VbUserPermanent, WiredVariableTarget.User, VbOffline));
    }

    [Fact]
    public async Task CreateGivesAPermanentVariableToAUserWhoHoldsAnother()
    {
        VbPermanentRoom();
        _vbClock.Now = VbAt(6);

        var reply = await VbSet(1, VbOffline, "user:503", 4, 1);

        Assert.Equal([ServerPacketHeader.WiredSetUserPermanentVariableResultComposer, ServerPacketHeader.WiredUserPermanentVariablesComposer],
            reply.Select(packet => packet.Header));
        Assert.Equal(new WiredVariableValue(4, VbAt(6), VbAt(6)), VbStored(VbUserShared, WiredVariableTarget.User, VbOffline));
    }

    [Fact]
    public async Task DeleteOneRemovesTheUsersValue()
    {
        VbPermanentRoom();

        VbEqual([VbResult(true), (ServerPacketHeader.WiredUserPermanentVariablesComposer,
            VbBody(1, VbOffline, "offline", "hd-20", 1, VbPermanentRow("user:504", -1, 1, 1)))], await VbSet(1, VbOffline, "user:502", 0, 2));
        Assert.Null(VbStored(VbUserPermanent, WiredVariableTarget.User, VbOffline));
    }

    // A variable without a value cannot be set, and is only created with value 0.
    [Fact]
    public async Task AVariableWithoutAValueIsOnlyCreatedWithZero()
    {
        VbPermanentRoom();

        VbEqual([VbError(1), VbResult(false)], await VbSet(1, VbOffline, "user:504", 3, 0));
        VbEqual([VbError(1), VbResult(false)], await VbSet(1, VbOffline, "user:504", 5, 1));
        VbEqual([VbResult(true)], (await VbSet(1, VbOffline, "user:504", 0, 1))[..1]);
    }

    // Set on a user who does not hold the variable succeeds and changes nothing; create and delete on a user who holds no permanent variable fail.
    [Fact]
    public async Task AUserWhoHoldsNothingIsOnlyAnsweredForSet()
    {
        VbPermanentRoom();
        var empty = (ServerPacketHeader.WiredUserPermanentVariablesComposer, VbBody(1, VbIdle, "faraway", "hd-21", 0));

        VbEqual([VbResult(true), empty], await VbSet(1, VbIdle, "user:502", 4, 0));
        VbEqual([VbError(1), VbResult(false)], await VbSet(1, VbIdle, "user:502", 4, 1));
        VbEqual([VbError(1), VbResult(false)], await VbSet(1, VbIdle, "user:502", 4, 2));
        VbEqual([VbResult(true), VbOfflineList(20, 3, 4)], await VbSet(1, VbOffline, "user:503", 4, 0));

        Assert.Empty(_vbStore.GetHolders(VbUserShared));
        Assert.Null(VbStored(VbUserPermanent, WiredVariableTarget.User, VbIdle));
    }

    [Theory]
    [InlineData(1, VbOffline, "user:501")]
    [InlineData(1, VbOffline, "furni:505")]
    [InlineData(1, VbOffline, "room:508")]
    [InlineData(1, VbOffline, "@id")]
    [InlineData(1, VbOffline, "999999")]
    [InlineData(2, VbOffline, "user:502")]
    [InlineData(4, VbOffline, "user:502")]
    [InlineData(1, 999, "user:502")]
    public async Task SetFailsForAnotherVariableOrEntity(int entityType, int entityId, string id)
    {
        VbPermanentRoom();

        VbEqual([VbError(1), VbResult(false)], await VbSet(entityType, entityId, id, 3, 0));
        Assert.Equal(20, VbStored(VbUserPermanent, WiredVariableTarget.User, VbOffline)!.Value);
    }

    // A user who may see the variables but not change them gets no answer to Set and Delete, and nothing changes.
    private TestClient VbReadOnlyGuest()
    {
        var guest = VbVisitor(rights: false);
        var settings = new WiredRoomSettings(_room, new ReadOnlySettingsStore());
        typeof(WiredComponent).GetField("<Settings>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_room.GetWired(), settings);

        return guest;
    }

    [Fact]
    public async Task SetNeedsTheModifyRightAndNotJustTheInspectRight()
    {
        VbPermanentRoom();
        var guest = VbReadOnlyGuest();

        Assert.NotEmpty(await VbPermanent(1, VbOffline, guest));
        Assert.Empty(await VbSet(1, VbOffline, "user:502", 3, 0, guest));
        Assert.Empty(await VbSet(1, VbOffline, "user:502", 3, 2, guest));
        Assert.Equal(20, VbStored(VbUserPermanent, WiredVariableTarget.User, VbOffline)!.Value);
    }

    [Fact]
    public async Task DeleteNeedsTheModifyRightAndNotJustTheInspectRight()
    {
        VbPermanentRoom();
        var guest = VbReadOnlyGuest();

        Assert.NotEmpty(await VbHolders("user:502", guest));
        Assert.Empty(await VbDelete("user:502", guest));
        Assert.Equal(20, VbStored(VbUserPermanent, WiredVariableTarget.User, VbOffline)!.Value);
    }

    [Fact]
    public async Task SetNeedsModifyRights()
    {
        VbPermanentRoom();
        var guest = VbVisitor(rights: false);

        Assert.Empty(await VbSet(1, VbOffline, "user:502", 3, 0, guest));
        _room.UsersWithRights.Add(LeaverId);
        Assert.True(_room.GetWired().Settings.TrySave(_client, 2, 2, "", out _));
        VbEqual([VbResult(true)], (await VbSet(1, VbOffline, "user:502", 3, 0, guest))[..1]);
        Assert.Equal(3, VbStored(VbUserPermanent, WiredVariableTarget.User, VbOffline)!.Value);
    }

    // A value written from the Manage window is what the room's Wired reads next.
    [Fact]
    public async Task ASetValueIsReadByWiredAfterwards()
    {
        VariableButtonsRoom();
        VbSeed(VbUserPermanent, WiredVariableTarget.User, 7, 1);
        await DpShow("a=$(a)", 0);
        await DpOutput("a", DpUser, "user:" + VbUserPermanent);

        VbEqual([VbResult(true)], (await VbSet(1, 7, "user:502", 42, 0))[..1]);

        Assert.Contains("a=42", DpFire(_vbWired, _vbOwner, _vbClock, 1));
    }

    // The database page query joins the user names; the memory store only knows ids.
    private sealed class NamedStore(IWiredVariableStore inner) : IWiredVariableStore
    {
        public WiredVariableValue? Read(WiredVariableKey key) => inner.Read(key);
        public IReadOnlyDictionary<WiredVariableKey, WiredVariableValue> ReadMany(IReadOnlyCollection<WiredVariableKey> keys) => inner.ReadMany(keys);
        public IReadOnlyDictionary<WiredVariableKey, WiredVariableValue> GetHolders(uint definitionId) => inner.GetHolders(definitionId);
        public WiredVariableWrite Mutate(WiredVariableKey key, Func<WiredVariableValue?, WiredVariableValue?> update, WiredVariableAuthorization? authorization = null) =>
            inner.Mutate(key, update, authorization);
        public int DeleteDefinition(uint definitionId) => inner.DeleteDefinition(definitionId);
        public IReadOnlyDictionary<WiredVariableKey, WiredVariableValue> ClearValues(uint definitionId, WiredVariableAuthorization authorization) =>
            inner.ClearValues(definitionId, authorization);

        public WiredVariableHolderPage ReadPage(uint definitionId, WiredVariableTarget target, int page, int size, int sort,
            IReadOnlyCollection<long>? holderFilter = null, IReadOnlyDictionary<long, string>? names = null) =>
            inner.ReadPage(definitionId, target, page, size, sort, holderFilter, new ButtonsUsers().GetUserCache().ToDictionary(x => (long)x.Id, x => x.Username));
    }

    private sealed class ReadOnlySettingsStore : IWiredRoomSettingsStore
    {
        public WiredRoomSettingsSnapshot? Load(uint roomId) => new(InspectMask: (int)WiredRoomAccess.Everyone, ModifyMask: (int)WiredRoomAccess.Rights);
        public void Save(uint roomId, int actorId, bool staff, WiredRoomSettingsSnapshot? expected, WiredRoomSettingsSnapshot settings) =>
            throw new NotSupportedException();
    }

    private sealed class ButtonsDirectory(Room room) : IWiredVariableDirectory
    {
        private readonly Dictionary<uint, WiredVariableDefinition> _definitions = new()
        {
            [VbUserActive] = new(VbUserActive, RoomId, 7, "ua", WiredVariableTarget.User, WiredVariableAvailability.UserActive, true),
            [VbUserPermanent] = new(VbUserPermanent, RoomId, 7, "up", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true),
            [VbUserShared] = new(VbUserShared, RoomId, 7, "us", WiredVariableTarget.User, WiredVariableAvailability.Shared, true),
            [VbUserNoValue] = new(VbUserNoValue, RoomId, 7, "un", WiredVariableTarget.User, WiredVariableAvailability.Persistent, false),
            [VbFurniPermanent] = new(VbFurniPermanent, RoomId, 7, "fp", WiredVariableTarget.Furni, WiredVariableAvailability.Persistent, true),
            [VbFurniRoom] = new(VbFurniRoom, RoomId, 7, "fr", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, true),
            [VbFurniNoValue] = new(VbFurniNoValue, RoomId, 7, "fn", WiredVariableTarget.Furni, WiredVariableAvailability.RoomActive, false),
            [VbGlobal] = new(VbGlobal, RoomId, 7, "gr", WiredVariableTarget.Global, WiredVariableAvailability.RoomActive, true),
            [VbContext] = new(VbContext, RoomId, 7, "cx", WiredVariableTarget.Context, WiredVariableAvailability.RoomActive, true),
            [VbEcho] = new(VbEcho, RoomId, 7, "ue", WiredVariableTarget.User, WiredVariableAvailability.Persistent, true,
                Link: new(RoomId, new(WiredVariableTarget.User, $"custom:{VbUserPermanent}"), false)),
            [VbPointer] = new(VbPointer, RoomId, 7, "up2", WiredVariableTarget.User, WiredVariableAvailability.RoomActive, true, PointerAccessor: $"custom:{VbContext}")
        };

        public uint? GetRoomOwner(uint roomId) => 7;
        public WiredVariableDefinition? Find(uint id) => room.GetRoomItemHandler().GetItem(id) is null ? null : _definitions.GetValueOrDefault(id);
    }

    private sealed class ButtonsUsers : ICacheManager
    {
        private static readonly Dictionary<int, CachedUser> Users = new()
        {
            [7] = new() { Id = 7, Username = "owner", Look = "hd-7" },
            [LeaverId] = new() { Id = LeaverId, Username = "guest", Look = "hd-8" },
            [VbOffline] = new() { Id = VbOffline, Username = "offline", Look = "hd-20" },
            [VbIdle] = new() { Id = VbIdle, Username = "faraway", Look = "hd-21" }
        };

        public bool ContainsUser(int id) => Users.ContainsKey(id);
        public CachedUser? GenerateUser(int id) => Users.GetValueOrDefault(id);
        public bool TryRemoveUser(int id, [NotNullWhen(true)] out CachedUser? cachedUser) => throw new InvalidOperationException();
        public bool TryGetUser(int id, [NotNullWhen(true)] out CachedUser? cachedUser) => Users.TryGetValue(id, out cachedUser);
        public ICollection<CachedUser> GetUserCache() => Users.Values;
        public void Init() { }
    }
}
