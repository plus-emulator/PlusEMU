using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plus.Communication.Flash;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// A Variable Reference whose shared source was picked up. Observed on AIR WIN63-202610201032-107964540: the reference still opens at
// once with its stored name, ints and variable id and an empty shared list. The room's variable list keeps it until the room unloads.
public partial class PlacedFurniRoomTests
{
    private const uint SourceRoomId = 8, SourceId = 200, ReferenceId = 601;
    private const string SourceWireId = "shared:8:user:200";

    [Fact]
    public async Task AReferenceWhoseSourceIsPickedUpStillOpensWithItsStoredSettings()
    {
        var rows = LinkedRooms();
        var item = await SavedReference(rows);
        Assert.Equal($"r119ref [0] [{SourceWireId}] shared 1", Open(ReferenceId));
        Assert.Contains(ReferenceId, RoomVariableIds());
        var listed = Assert.Single(NativeVariables(), variable => variable.Id == $"user:{ReferenceId}");

        PickUpSource(rows);

        Assert.Equal($"r119ref [0] [{SourceWireId}] shared 0", Open(ReferenceId));
        Assert.Contains(ReferenceId, RoomVariableIds());
        Assert.Equal(listed, Assert.Single(NativeVariables(), variable => variable.Id == $"user:{ReferenceId}"));
        Assert.True(_room.GetWired().TryGet(item.Id, out _));
    }

    // Nobody opened a variable list in this room between the save and the pickup.
    [Fact]
    public async Task AReferenceWhoseSourceIsPickedUpStaysListedWithoutAnEarlierLook()
    {
        var rows = LinkedRooms();
        await SavedReference(rows);

        PickUpSource(rows);

        Assert.Contains(ReferenceId, RoomVariableIds());
        var listed = Assert.Single(NativeVariables(), variable => variable.Id == $"user:{ReferenceId}");
        Assert.Equal(("r119ref", 20), (listed.Name, listed.Availability));
    }

    [Fact]
    public async Task AListedReferenceWhoseSourceIsPickedUpNeitherReadsNorWrites()
    {
        var rows = LinkedRooms();
        await SavedReference(rows);
        var holder = new WiredVariableHolder(WiredVariableTarget.User, 7, 1);
        var frame = new WiredVariableFrame(RoomId, [holder]);
        var module = _room.GetWired().Variables.Module;
        WiredVariableReference reference = new(WiredVariableTarget.User, $"custom:{ReferenceId}");
        Assert.Contains(ReferenceId, RoomVariableIds());
        Assert.True(module.TryResolveCatalogId($"user:{ReferenceId}", WiredVariableTarget.User, out _));

        PickUpSource(rows);

        Assert.Contains(ReferenceId, RoomVariableIds());
        Assert.False(module.TryResolveCatalogId($"user:{ReferenceId}", WiredVariableTarget.User, out _));
        Assert.Null(module.Read(reference, holder, frame));
        Assert.False(module.Mutate(reference, holder, WiredVariableMutation.Set, 6, frame));
    }

    [Fact]
    public async Task AReferenceWhoseSourceIsPickedUpLoadsAgainAfterTheRoomReloads()
    {
        var rows = LinkedRooms();
        var item = await SavedReference(rows);
        PickUpSource(rows);
        var log = new CapturingLogger();

        var wired = Linked(rows, log, new WiredConfigurationStore(rows.Database));
        var loaded = wired.LoadWiredBox(item);

        Assert.DoesNotContain(log.Lines, line => line.StartsWith("Cannot load Wired configuration", StringComparison.Ordinal));
        Assert.NotNull(loaded);
        Assert.True(wired.TryGet(ReferenceId, out _));
        Assert.Equal($"r119ref [0] [{SourceWireId}] shared 0", Open(ReferenceId));
        Assert.DoesNotContain(ReferenceId, RoomVariableIds());
        Assert.DoesNotContain(NativeVariables(), variable => variable.Id == $"user:{ReferenceId}");
    }

    // Plus behavior, not probed on Habbo: saving the reference again after its source was picked up is refused like any save that
    // names a missing source.
    [Fact]
    public async Task SavingAReferenceAgainAfterItsSourceIsPickedUpIsRefused()
    {
        var rows = LinkedRooms();
        await SavedReference(rows);
        PickUpSource(rows);

        Assert.False(await SaveReference());
        Assert.Equal($"r119ref [0] [{SourceWireId}] shared 0", Open(ReferenceId));
    }

    // Room 8 holds the owner's shared user variable r119src (has value, availability 11); this room (42) will hold the reference.
    private static LinkedRoomRows LinkedRooms()
    {
        var rows = new LinkedRoomRows();
        rows.Owners[SourceRoomId] = "7";
        rows.Owners[RoomId] = "7";
        rows.ItemRooms[SourceId] = SourceRoomId;
        rows.ItemRooms[ReferenceId] = RoomId;
        Assert.True(WiredBoxRegistry.TryGet("wf_var_user", out var user));
        var source = WiredNativeEditorProjection.DefaultNative(user) with { OwnedIntParams = [11, 1], Text = "r119src" };
        rows.Configurations[SourceId] = ("wf_var_user", source.Version, JsonSerializer.Serialize(source));

        return rows;
    }

    private WiredComponent Linked(LinkedRoomRows rows, ILogger log, IWiredConfigurationStore store)
    {
        _databaseField.SetValue(null, rows.Database);
        var wired = new WiredComponent(_room, log, TimeProvider.System, TestRoomSettings.Empty, TestWiredRoomSettingsFactory.Instance,
            store, rows.Database, TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty,
            TestGroupManager.Empty, TestWiredDefinitions.Unused, TestWiredCommands.Unused, TestWiredAccess.Unused, TestItemRuntime.Travel);
        Set("_wiredComponent", wired);

        return wired;
    }

    // The reference box placed in this room, then saved.
    private async Task<Item> SavedReference(LinkedRoomRows rows)
    {
        Linked(rows, TestLogging.Logger, new EmptyConfigurationStore());
        var item = Furni(ReferenceId, InteractionType.WiredVariable, WiredBoxType.None);
        item.RoomId = RoomId;
        item.Definition.InteractionName = item.Definition.ItemName = "wf_var_reference";
        _room.GetRoomItemHandler().LoadFurniture([item]);
        Assert.True(await SaveReference());

        return item;
    }

    // The raw variable save packet for the reference box: read only off, the source picked. True when the editor closed (saved).
    private async Task<bool> SaveReference()
    {
        Assert.True(_room.GetWired().TryGet(ReferenceId, out var box));
        var fresh = WiredNativeEditorProjection.DefaultNative(((IWiredConfiguredItem)box).Descriptor);
        _client.Packets.Clear();
        var service = new WiredConfigurationService(new EmptyConfigurationStore(), null!, null!, TestLogging.For<WiredConfigurationService>());

        await new SaveWiredVariableConfigEvent(service).Parse(_client, EditorTestSupport.Incoming([(int)ReferenceId, 1, 0, "r119ref", 0,
            fresh.FurniSourceTypes.Length, .. fresh.FurniSourceTypes.Cast<object>(), fresh.UserSourceTypes.Length, .. fresh.UserSourceTypes.Cast<object>(),
            1, SourceWireId, 0]));

        return _client.Packets.Any(packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
    }

    // What a confirmed pickup in room 8 leaves behind: the box's settings row is deleted and the item is no longer in a room.
    private static void PickUpSource(LinkedRoomRows rows)
    {
        new WiredConfigurationStore(rows.Database).Reset([SourceId]);
        rows.ItemRooms[SourceId] = 0;
    }

    // The list the client shows: the room's native variable catalog.
    private WiredNativeVariable[] NativeVariables()
    {
        Assert.True(_room.GetWired().Variables.TryCaptureNativeCatalog(out var catalog));

        return [.. catalog!.Variables];
    }

    private uint[] RoomVariableIds() => [.. _room.GetWired().Variables.Catalog().Variables.Select(variable => variable.Definition.ItemId)];

    // The raw WIRED_OPEN packet, then the variable editor frame read up to the count of its shared-variable list (context 4): name, ints,
    // variable ids and that count.
    private string Open(uint id)
    {
        _client.Packets.Clear();
        new OpenWiredEvent().Parse(_room, _client, ClientPacket((int)id)).GetAwaiter().GetResult();
        var frame = new FlashIncomingPacket { Buffer = Assert.Single(_client.Packets, packet => packet.Header == ServerPacketHeader.WiredVariableConfigComposer).Body };
        int[] Ints() => [.. Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadInt())];

        frame.ReadInt();
        Ints();
        Ints();
        frame.ReadInt();
        Assert.Equal(id, frame.ReadUInt());
        var name = frame.ReadString();
        var ints = Ints();
        var variableIds = Enumerable.Range(0, frame.ReadInt()).Select(_ => frame.ReadString()).ToArray();
        Ints();
        Ints();
        frame.ReadInt();
        frame.ReadBool();

        for (var groups = 0; groups < 2; groups++) {
            for (var count = frame.ReadInt(); count > 0; count--) {
                Ints();
            }
        }

        Ints();
        Ints();
        frame.ReadBool();
        Assert.Equal(1, frame.ReadInt());
        Assert.Equal(4, frame.ReadInt());

        return $"{name} [{string.Join(',', ints)}] [{string.Join(',', variableIds)}] shared {frame.ReadInt()}";
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Lines { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }

    /// <summary>The rooms, items and Wired settings rows the variable directory, the settings store and the variable save read and write.</summary>
    private sealed class LinkedRoomRows
    {
        public Dictionary<uint, string> Owners { get; } = [];
        public Dictionary<uint, uint> ItemRooms { get; } = [];
        public Dictionary<uint, (string Box, int Version, string Json)> Configurations { get; } = [];
        public Plus.Database.IDatabase Database { get; }

        public LinkedRoomRows() => Database = Proxy<Plus.Database.IDatabase>((method, _) =>
            method == "Connection" ? new NoOpConnection(write: Write, query: Query) : throw new InvalidOperationException(method));

        private static object? Param(DbParameterCollection parameters, string name) =>
            parameters.Cast<DbParameter>().FirstOrDefault(p => string.Equals(p.ParameterName.TrimStart('@'), name, StringComparison.OrdinalIgnoreCase))?.Value;

        private static DataTable Table(params (string Name, Type Type)[] columns)
        {
            var table = new DataTable();

            foreach (var (name, type) in columns) {
                table.Columns.Add(name, type);
            }

            return table;
        }

        private DataTable Query(string sql, DbParameterCollection parameters)
        {
            if (sql.Contains("WHERE i.id=@itemId", StringComparison.Ordinal)) {
                var table = Table(("RoomId", typeof(uint)), ("Owner", typeof(string)), ("BoxName", typeof(string)), ("Configuration", typeof(string)));
                var itemId = Convert.ToUInt32(Param(parameters, "itemId"));

                if (ItemRooms.TryGetValue(itemId, out var room) && Owners.TryGetValue(room, out var owner) && Configurations.TryGetValue(itemId, out var row)) {
                    table.Rows.Add(room, owner, row.Box, row.Json);
                }

                return table;
            }

            if (sql.Contains("c.box_name IN ('wf_var_user','wf_var_room')", StringComparison.Ordinal)) {
                var table = Table(("ItemId", typeof(uint)), ("RoomId", typeof(uint)), ("RoomName", typeof(string)), ("BoxName", typeof(string)), ("Configuration", typeof(string)));
                var except = Convert.ToUInt32(Param(parameters, "room"));

                foreach (var (itemId, row) in Configurations.Where(row => row.Value.Box is "wf_var_user" or "wf_var_room")) {
                    if (ItemRooms.GetValueOrDefault(itemId) is var room and > 0 && room != except && Owners.GetValueOrDefault(room) == (string?)Param(parameters, "owner")) {
                        table.Rows.Add(itemId, room, $"room {room}", row.Box, row.Json);
                    }
                }

                return table;
            }

            if (sql.Contains("c.box_name='wf_var_reference'", StringComparison.Ordinal)) {
                return Table(("ItemId", typeof(uint)), ("RoomId", typeof(uint)), ("RoomName", typeof(string)), ("BoxName", typeof(string)), ("Configuration", typeof(string)));
            }

            if (sql.StartsWith("SELECT owner FROM rooms", StringComparison.Ordinal)) {
                var table = Table(("owner", typeof(string)));

                if (Owners.TryGetValue(Convert.ToUInt32(Param(parameters, "roomId")), out var owner)) {
                    table.Rows.Add(owner);
                }

                return table;
            }

            if (sql.StartsWith("SELECT room_id FROM items", StringComparison.Ordinal)) {
                var table = Table(("room_id", typeof(uint)));

                if (ItemRooms.TryGetValue(Convert.ToUInt32(Param(parameters, "itemId")), out var room)) {
                    table.Rows.Add(room);
                }

                return table;
            }

            if (sql.StartsWith("SELECT box_name AS Name,configuration AS Configuration FROM wired_item_configurations", StringComparison.Ordinal)) {
                var table = Table(("Name", typeof(string)), ("Configuration", typeof(string)));

                if (Configurations.TryGetValue(Convert.ToUInt32(Param(parameters, "itemId")), out var row)) {
                    table.Rows.Add(row.Box, row.Json);
                }

                return table;
            }

            if (sql.StartsWith("SELECT box_name AS BoxName, schema_version AS Version, configuration AS Json", StringComparison.Ordinal)) {
                var table = Table(("BoxName", typeof(string)), ("Version", typeof(int)), ("Json", typeof(string)));

                if (Configurations.TryGetValue(Convert.ToUInt32(Param(parameters, "Id")), out var row)) {
                    table.Rows.Add(row.Box, row.Version, row.Json);
                }

                return table;
            }

            return Table(("Value", typeof(int)));
        }

        private void Write(string sql, DbParameterCollection parameters)
        {
            if (sql.Contains("INSERT INTO wired_item_configurations", StringComparison.Ordinal)) {
                Configurations[Convert.ToUInt32(Param(parameters, "itemId") ?? Param(parameters, "Id"))] =
                    ((string)Param(parameters, "name")!, Convert.ToInt32(Param(parameters, "Version")), (string)Param(parameters, "configuration")!);
            }
            else if (sql.StartsWith("DELETE FROM wired_item_configurations", StringComparison.Ordinal)) {
                foreach (var id in parameters.Cast<DbParameter>().Select(p => Convert.ToUInt32(p.Value))) {
                    Configurations.Remove(id);
                }
            }
        }
    }
}
