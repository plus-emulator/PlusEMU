using System.Data;
using System.Data.Common;
using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// Raw saves of the variable boxes, Variable Changed and Echo through the real save packets, in one room whose variables are read back the
// way the hotel stores them. Observed on AIR WIN63-202610201032-107964540.
public partial class PlacedFurniRoomTests
{
    private const string NameLength = "wiredfurni.error.variables.name_length";
    private const string NameSyntax = "wiredfurni.error.variables.name_syntax";
    private const string NameTaken = "wiredfurni.error.variables.name_uniq";
    private const string MissingVariable = "wiredfurni.error.variables.missing_variable";
    private const string NeedsExecutionContext = "wiredfurni.error.variables.requires_execution_context";
    private const string VariableCount = "wiredfurni.error.invalid_variable_count";
    private const uint NamedUser = 801, NamedFurni = 802, NamedGlobal = 803, NamedContext = 804, NamedPointer = 805, NamedFurniPointer = 806;
    private const uint NamedChanged = 807, NamedEcho = 808;

    // The stored wired_item_configurations rows of this room, by item id: the box name and its stored native configuration.
    private readonly Dictionary<uint, (string Box, string Json)> _variableRows = [];
    private readonly Dictionary<uint, IWiredConfiguredItem> _variableBoxes = [];

    private WiredConfigurationService UseVariableRows()
    {
        var database = Proxy<Plus.Database.IDatabase>((method, _) => method == "Connection"
            ? new NoOpConnection(write: WriteVariableRow, query: ReadVariableRow) : throw new InvalidOperationException(method));
        _databaseField.SetValue(null, database);
        var wired = new WiredComponent(_room, TestLogging.Logger, TimeProvider.System, TestRoomSettings.Empty, TestWiredRoomSettingsFactory.Instance,
            new StoredRowStore(_variableRows), database, TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty,
            TestGroupManager.Empty, TestWiredDefinitions.Unused, TestWiredCommands.Unused, TestWiredAccess.Unused, TestItemRuntime.Travel);
        Set("_wiredComponent", wired);

        return new(new EmptyConfigurationStore(), null!, new FakeWordFilter(), TestLogging.For<WiredConfigurationService>());
    }

    private void WriteVariableRow(string sql, DbParameterCollection parameters)
    {
        if (sql.Contains("INSERT INTO wired_item_configurations", StringComparison.Ordinal)) {
            _variableRows[Convert.ToUInt32(Parameter(parameters, "itemId"))] = ((string)Parameter(parameters, "name")!, (string)Parameter(parameters, "configuration")!);
        }
    }

    private DataTable ReadVariableRow(string sql, DbParameterCollection parameters)
    {
        var table = new DataTable();

        if (sql.StartsWith("SELECT owner FROM rooms", StringComparison.Ordinal)) {
            table.Columns.Add("owner", typeof(string));
            table.Rows.Add(_room.OwnerId.ToString());
        }
        else if (sql.StartsWith("SELECT room_id FROM items", StringComparison.Ordinal)) {
            table.Columns.Add("room_id", typeof(uint));
            table.Rows.Add(RoomId);
        }
        else if (sql.Contains("FROM wired_item_configurations WHERE item_id=@itemId", StringComparison.Ordinal)) {
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Configuration", typeof(string));

            if (_variableRows.TryGetValue(Convert.ToUInt32(Parameter(parameters, "itemId")), out var row)) {
                table.Rows.Add(row.Box, row.Json);
            }
        }
        else if (sql.Contains("WHERE i.id=@itemId", StringComparison.Ordinal)) {
            table.Columns.Add("RoomId", typeof(uint));
            table.Columns.Add("Owner", typeof(string));
            table.Columns.Add("BoxName", typeof(string));
            table.Columns.Add("Configuration", typeof(string));

            if (_variableRows.TryGetValue(Convert.ToUInt32(Parameter(parameters, "itemId")), out var row)) {
                table.Rows.Add(RoomId, _room.OwnerId.ToString(), row.Box, row.Json);
            }
        }
        else {
            table.Columns.Add("Value", typeof(long));
        }

        return table;
    }

    private static object? Parameter(DbParameterCollection parameters, string name) =>
        parameters.Cast<DbParameter>().Single(parameter => parameter.ParameterName.TrimStart('@') == name).Value;

    private sealed class StoredRowStore(Dictionary<uint, (string Box, string Json)> rows) : IWiredConfigurationStore
    {
        public WiredConfiguration? Load(uint itemId, WiredBoxDescriptor descriptor) =>
            rows.TryGetValue(itemId, out var row) ? WiredConfigurationStore.DecodeRow(itemId, row.Box, row.Json) : null;
        public void Save(uint itemId, WiredBoxDescriptor descriptor, WiredConfiguration configuration) { }
        public void Reset(IReadOnlyCollection<uint> itemIds) { }
    }

    private IWiredConfiguredItem PlaceVariableBox(uint id, string name)
    {
        var box = WiredBox(id, name, (int)(id % 4), (int)(id / 4 % 4));
        Assert.True(_room.GetWired().AddBox((IWiredItem)box));
        _variableBoxes[id] = box;

        return box;
    }

    /// <summary>One raw variable box save: the answer ("saved" or the error key).</summary>
    private string SaveVariable(WiredConfigurationService service, uint id, int[] ints, string name, params string[] variables)
    {
        object[] body = [(int)id, ints.Length, .. ints.Cast<object>(), name, 0, 0, 0, variables.Length, .. variables.Cast<object>(), 0];
        _client.Packets.Clear();
        new SaveWiredVariableConfigEvent(service).Parse(_client, EditorTestSupport.Incoming(body)).GetAwaiter().GetResult();

        return SaveAnswer();
    }

    private string SaveAnswer()
    {
        var refusal = _client.Packets.Where(packet => packet.Header == ServerPacketHeader.WiredValidationErrorComposer).ToArray();

        if (refusal.Length == 0) {
            return _client.Packets.Any(packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer) ? "saved" : "dropped";
        }

        return System.Text.Encoding.UTF8.GetString(refusal[0].Body, 2, (refusal[0].Body[0] << 8) | refusal[0].Body[1]);
    }

    /// <summary>The name the editor opens on again: the stored text of the box.</summary>
    private string StoredName(uint id)
    {
        var box = _variableBoxes[id];

        return WiredNativeEditorProjection.HotelFrame(box.Descriptor, box.Configuration.Origin!.Native).Text;
    }

    private static int[] VariableInts(string box) => box switch
    {
        "wf_var_user" => [0, 1],
        "wf_var_furni" => [1, 1],
        "wf_var_room" => [1],
        _ => [1]
    };

    // A room with a user, a furni, a global and a context variable (with a value), a user pointer and a furni pointer, both read through
    // the context variable.
    private WiredConfigurationService NamedVariableRoom()
    {
        var service = UseVariableRows();

        foreach (var (id, box, name) in new[] { (NamedUser, "wf_var_user", "nu"), (NamedFurni, "wf_var_furni", "nf"), (NamedGlobal, "wf_var_room", "ng"),
            (NamedContext, "wf_var_context", "nc") }) {
            PlaceVariableBox(id, box);
            Assert.Equal("saved", SaveVariable(service, id, VariableInts(box), name));
        }

        PlaceVariableBox(NamedPointer, "wf_var_pointer");
        Assert.Equal("saved", SaveVariable(service, NamedPointer, [1], "pu", $"ctx:{NamedContext}"));
        PlaceVariableBox(NamedFurniPointer, "wf_var_pointer");
        Assert.Equal("saved", SaveVariable(service, NamedFurniPointer, [0], "pf", $"ctx:{NamedContext}"));

        return service;
    }

    private string SaveNamed(WiredConfigurationService service, uint id, string name) => id switch
    {
        NamedPointer => SaveVariable(service, id, [1], name, $"ctx:{NamedContext}"),
        NamedFurniPointer => SaveVariable(service, id, [0], name, $"ctx:{NamedContext}"),
        _ => SaveVariable(service, id, VariableInts(_variableBoxes[id].Descriptor.CanonicalName), name)
    };

    // wf_var_pointer.r111_name_rule_pointer, wf_var_user.r111_name_rule_user, wf_var_pointer.r111_editor (PtrUP),
    // wf_var_pointer.r111_editor_name_underscore: spaces become underscores, the name is lower case and loses its leading and trailing
    // underscores. Inner underscores and digit-only names stay.
    [Theory]
    [InlineData("_a", "a")]
    [InlineData("a_", "a")]
    [InlineData("_1", "1")]
    [InlineData("1", "1")]
    [InlineData("a__b", "a__b")]
    [InlineData("9_", "9")]
    [InlineData("a b", "a_b")]
    [InlineData("PtrUP", "ptrup")]
    [InlineData("_______________________________________a", "a")]
    public void APointerOrUserVariableNameIsStoredCleaned(string sent, string stored)
    {
        var service = NamedVariableRoom();

        foreach (var id in new[] { NamedPointer, NamedUser }) {
            Assert.Equal("saved", SaveNamed(service, id, sent));
            Assert.Equal(stored, StoredName(id));
            Assert.Equal("saved", SaveNamed(service, id, id == NamedPointer ? "pu" : "nu"));
        }
    }

    // wf_var_furni.r111_name_rule_kinds part 1: the same cleaning on furni, global and context variables.
    [Theory]
    [InlineData("_a", "a")]
    [InlineData("a_", "a")]
    [InlineData("PtrUP", "ptrup")]
    [InlineData("a b", "a_b")]
    public void AFurniGlobalOrContextVariableNameIsStoredCleaned(string sent, string stored)
    {
        var service = NamedVariableRoom();

        foreach (var (id, own) in new[] { (NamedFurni, "nf"), (NamedGlobal, "ng"), (NamedContext, "nc") }) {
            Assert.Equal("saved", SaveNamed(service, id, sent));
            Assert.Equal(stored, StoredName(id));
            Assert.Equal("saved", SaveNamed(service, id, own));
        }
    }

    // A name that is empty once cleaned is too short, on every kind, and the stored name stays.
    [Theory]
    [InlineData("__")]
    [InlineData("___")]
    [InlineData(" _ ")]
    public void ANameOfOnlyUnderscoresAndSpacesIsTooShort(string sent)
    {
        var service = NamedVariableRoom();

        foreach (var (id, own) in new[] { (NamedPointer, "pu"), (NamedUser, "nu"), (NamedFurni, "nf"), (NamedGlobal, "ng"), (NamedContext, "nc") }) {
            Assert.Equal(NameLength, SaveNamed(service, id, sent));
            Assert.Equal(own, StoredName(id));
        }
    }

    // wf_var_furni.r111_name_rule_kinds part 2: each kind takes the name of each other kind and is refused.
    [Fact]
    public void AVariableNameIsUniqueAcrossAllKindsOfTheRoom()
    {
        var service = NamedVariableRoom();
        var own = new Dictionary<uint, string> { [NamedUser] = "nu", [NamedFurni] = "nf", [NamedGlobal] = "ng", [NamedContext] = "nc" };

        foreach (var id in own.Keys) {
            foreach (var other in own.Keys.Where(other => other != id)) {
                Assert.Equal(NameTaken, SaveNamed(service, id, own[other]));
                Assert.Equal(own[id], StoredName(id));
            }
        }
    }

    // wf_var_pointer.r111_p05_editor_names_order_lb_fx_pickup and r111_p05b_editor_retry: a user pointer named like a furni, global or
    // context variable or like a furni pointer is refused, and so is a furni pointer named like a user variable or the user pointer.
    [Theory]
    [InlineData(NamedPointer, "nf")]
    [InlineData(NamedPointer, "ng")]
    [InlineData(NamedPointer, "nc")]
    [InlineData(NamedPointer, "pf")]
    [InlineData(NamedFurniPointer, "nu")]
    [InlineData(NamedFurniPointer, "nf")]
    [InlineData(NamedFurniPointer, "ng")]
    [InlineData(NamedFurniPointer, "pu")]
    public void APointerNameIsUniqueAcrossEveryVariableOfTheRoom(uint id, string name)
    {
        var service = NamedVariableRoom();

        Assert.Equal(NameTaken, SaveNamed(service, id, name));
        Assert.Equal(id == NamedPointer ? "pu" : "pf", StoredName(id));
    }

    // wf_var_furni.r111_name_rule_kinds part 3: the cleaned name is what must be unique.
    [Theory]
    [InlineData("_nf_")]
    [InlineData("NF")]
    [InlineData("Ng")]
    public void UniquenessIsCheckedOnTheCleanedName(string sent)
    {
        var service = NamedVariableRoom();

        Assert.Equal(NameTaken, SaveNamed(service, NamedUser, sent));
        Assert.Equal("nu", StoredName(NamedUser));
    }

    // r111_p05 and r111_p05b: with a user variable as accessor, a bad or taken name is answered for the name, before the accessor. A
    // 41-character name with a dash is too long before it is a syntax error.
    [Theory]
    [InlineData("ptr-1", NameSyntax)]
    [InlineData("", NameLength)]
    [InlineData("nf", NameTaken)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa-a", NameLength)]
    public void APointerNameIsCheckedBeforeItsAccessor(string name, string key)
    {
        var service = NamedVariableRoom();

        Assert.Equal(key, SaveVariable(service, NamedPointer, [1], name, $"user:{NamedUser}"));
        Assert.Equal(MissingVariable, SaveVariable(service, NamedPointer, [1], "ok_name", $"user:{NamedUser}"));
    }

    // wf_var_pointer.r111_editor L66-L74: a pointer save with two variables, in either order, is refused and the pointer is unchanged.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void APointerWithTwoVariablesIsRefusedWithTheCountKey(bool contextFirst)
    {
        var service = NamedVariableRoom();
        string[] variables = contextFirst ? [$"ctx:{NamedContext}", $"user:{NamedUser}"] : [$"user:{NamedUser}", $"ctx:{NamedContext}"];

        Assert.Equal(VariableCount, SaveVariable(service, NamedPointer, [1], "pu", variables));
        Assert.Equal([$"ctx:{NamedContext}"], _variableBoxes[NamedPointer].Configuration.VariableIds.ToArray());
    }

    // r111.ptr_unknown_accessor: every accessor that is no context variable of the room is a missing variable.
    [Theory]
    [InlineData("999999999")]
    [InlineData("n")]
    [InlineData("0")]
    [InlineData("")]
    [InlineData("801")]
    [InlineData("-459")]
    [InlineData("nc")]
    public void AnUnknownPointerAccessorIsAMissingVariable(string accessor)
    {
        var service = NamedVariableRoom();

        Assert.Equal(MissingVariable, SaveVariable(service, NamedPointer, [1], "pu", accessor));
        Assert.Equal([$"ctx:{NamedContext}"], _variableBoxes[NamedPointer].Configuration.VariableIds.ToArray());
    }

    // wf_var_pointer.r111_editor L167-L171: Variable Changed in variable mode refuses a pointer.
    [Fact]
    public void VariableChangedRefusesAPointerVariable()
    {
        var service = NamedVariableRoom();
        PlaceVariableBox(NamedChanged, "wf_trg_var_changed");
        _client.Packets.Clear();

        new SaveWiredTriggerConfigEvent(service).Parse(_client,
            ClientPacket((int)NamedChanged, 6, 1, 1, 0, 7, -1, 0, "", 0, 0, 0, 1, $"user:{NamedPointer}", 0)).GetAwaiter().GetResult();

        Assert.Equal(NeedsExecutionContext, SaveAnswer());
    }

    // wf_var_pointer.r111_editor L174-L177: Variable Changed in box mode refuses a picked pointer box.
    [Fact]
    public void VariableChangedRefusesAPickedPointerBox()
    {
        var service = NamedVariableRoom();
        PlaceVariableBox(NamedChanged, "wf_trg_var_changed");
        _client.Packets.Clear();

        new SaveWiredTriggerConfigEvent(service).Parse(_client,
            ClientPacket((int)NamedChanged, 6, 1, 1, 0, 7, -1, 1, "", 1, (int)NamedPointer, 0, 0, 1, "0", 0)).GetAwaiter().GetResult();

        Assert.Equal(NeedsExecutionContext, SaveAnswer());
    }

    // Controls: the same saves with a user variable succeed.
    [Fact]
    public void VariableChangedStillSavesAUserVariableAndItsBox()
    {
        var service = NamedVariableRoom();
        PlaceVariableBox(NamedChanged, "wf_trg_var_changed");

        _client.Packets.Clear();
        new SaveWiredTriggerConfigEvent(service).Parse(_client,
            ClientPacket((int)NamedChanged, 6, 1, 1, 0, 7, -1, 0, "", 0, 0, 0, 1, $"user:{NamedUser}", 0)).GetAwaiter().GetResult();
        Assert.Equal("saved", SaveAnswer());

        _client.Packets.Clear();
        new SaveWiredTriggerConfigEvent(service).Parse(_client,
            ClientPacket((int)NamedChanged, 6, 1, 1, 0, 7, -1, 1, "", 1, (int)NamedUser, 0, 0, 1, "0", 0)).GetAwaiter().GetResult();
        Assert.Equal("saved", SaveAnswer());
    }

    // wf_var_pointer.r111_editor L182-L185: an Echo of a pointer is refused; an Echo of a user variable saves.
    [Fact]
    public void EchoRefusesAPointer()
    {
        var service = NamedVariableRoom();
        PlaceVariableBox(NamedEcho, "wf_var_echo");

        Assert.Equal(NeedsExecutionContext, SaveVariable(service, NamedEcho, [], "pecho", $"user:{NamedPointer}"));
        Assert.Equal("saved", SaveVariable(service, NamedEcho, [], "pecho", $"user:{NamedUser}"));
    }

    // Not observed: whether an echo's name counts. Only the five named kinds are proven, so an echo keeps the name it has and does not
    // take a name from them.
    [Fact]
    public void AnEchoNameDoesNotBlockAVariableName()
    {
        var service = NamedVariableRoom();
        PlaceVariableBox(NamedEcho, "wf_var_echo");
        Assert.Equal("saved", SaveVariable(service, NamedEcho, [], "pecho", $"user:{NamedUser}"));

        Assert.Equal("saved", SaveNamed(service, NamedFurni, "pecho"));
        Assert.Equal("saved", SaveVariable(service, NamedEcho, [], "nu", $"user:{NamedUser}"));
        Assert.Equal("saved", SaveNamed(service, NamedUser, "nu"));
    }

    // Only ASCII capitals are lowered. A non-ASCII letter stays a syntax error, also one whose lower case is an ASCII letter (the Kelvin
    // sign), as an accented letter is on Habbo (wf_var_pointer.r111_editor).
    [Theory]
    [InlineData("\u212Aa")]
    [InlineData("ptr_\u00e9")]
    [InlineData("PTR_\u00c9")]
    public void ANonAsciiLetterInAPointerNameIsASyntaxError(string name)
    {
        var service = NamedVariableRoom();

        Assert.Equal(NameSyntax, SaveNamed(service, NamedPointer, name));
        Assert.Equal("pu", StoredName(NamedPointer));
    }

    // Not observed: names stored before the cleaning rule. A stored name is compared as it is stored, so a new save keeps working beside
    // an older name that differs only in case.
    [Fact]
    public void AStoredNameIsComparedAsStored()
    {
        var service = UseVariableRows();
        StoreRow(NamedUser, "wf_var_user", [0, 1], "Score");
        var item = Furni(NamedUser, Plus.HabboHotel.Items.InteractionType.None, WiredBoxType.None);
        item.Definition.InteractionName = item.Definition.ItemName = "wf_var_user";
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, 1, 0, 0, true, false, false));
        Assert.NotNull(_room.GetWired().LoadWiredBox(item));
        PlaceVariableBox(NamedGlobal, "wf_var_room");

        Assert.Equal("saved", SaveNamed(service, NamedGlobal, "score"));
        Assert.Equal("score", StoredName(NamedGlobal));
    }

    // A room saved before these rules keeps loading: stored names in upper case or with a leading underscore, and the same name on two
    // kinds, load as they were stored.
    [Fact]
    public void StoredVariablesWithOlderNamesStillLoad()
    {
        UseVariableRows();
        StoreRow(NamedUser, "wf_var_user", [0, 1], "Score");
        StoreRow(NamedFurni, "wf_var_furni", [1, 1], "_score");
        StoreRow(NamedGlobal, "wf_var_room", [1], "score");
        StoreRow(NamedContext, "wf_var_context", [1], "Ctx");
        StoreRow(NamedPointer, "wf_var_pointer", [1], "_Ptr", $"ctx:{NamedContext}");

        foreach (var (id, box, name) in new[] { (NamedUser, "wf_var_user", "Score"), (NamedFurni, "wf_var_furni", "_score"), (NamedGlobal, "wf_var_room", "score"),
            (NamedContext, "wf_var_context", "Ctx"), (NamedPointer, "wf_var_pointer", "_Ptr") }) {
            var item = Furni(id, Plus.HabboHotel.Items.InteractionType.None, WiredBoxType.None);
            item.Definition.InteractionName = item.Definition.ItemName = box;
            Assert.True(_room.GetRoomItemHandler().SetFloorItem(null!, item, (int)(id % 4), (int)(id / 4 % 4), 0, true, false, false));
            var loaded = Assert.IsType<Plus.HabboHotel.Items.Wired.Variables.WiredVariableDefinitionBox>(_room.GetWired().LoadWiredBox(item));

            Assert.True(loaded.HasPersistedConfiguration);
            Assert.Equal(name, loaded.Configuration.Text);
        }
    }

    private void StoreRow(uint id, string box, int[] ints, string name, params string[] variables)
    {
        Assert.True(WiredBoxRegistry.TryGet(box, out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            OwnedIntParams = [.. ints],
            Text = name,
            VariableIds = variables.Length == 0 ? WiredNativeEditorProjection.DefaultNative(descriptor).VariableIds : [.. variables]
        };
        _variableRows[id] = (box, JsonSerializer.Serialize(native));
    }
}
