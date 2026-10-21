using System.Data;
using System.Text.Json;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

// Raw saves of Variable Reference. The room belongs to user 7. A shared variable is looked up the way the hotel reads it, from the stored
// rows. Observed on AIR WIN63-202610201032-107964540.
public partial class PlacedFurniRoomTests
{
    private const string MustOwn = "wiredfurni.error.variables.reference.must_own";
    private const string InvalidReference = "wiredfurni.error.variables.reference.valid";

    // The stored row of the one shared variable the save asks for: its room, the owner of that room and its box.
    private string? SaveReference(string shared, (uint RoomId, string Owner, string Box, int[] Ints)? stored)
    {
        var read = new Func<string, DataTable>(sql =>
        {
            var table = new DataTable();

            if (sql.StartsWith("SELECT owner FROM rooms", StringComparison.Ordinal)) {
                table.Columns.Add("owner", typeof(string));
                table.Rows.Add(_room.OwnerId.ToString());

                return table;
            }

            table.Columns.Add("RoomId", typeof(uint));
            table.Columns.Add("Owner", typeof(string));
            table.Columns.Add("BoxName", typeof(string));
            table.Columns.Add("Configuration", typeof(string));

            if (sql.StartsWith("SELECT room_id FROM items", StringComparison.Ordinal)) {
                table.Columns.Add("room_id", typeof(uint));
                table.Rows.Add(RoomId);

                return table;
            }

            if (stored is { } row && sql.Contains("WHERE i.id=@itemId", StringComparison.Ordinal)) {
                Assert.True(WiredBoxRegistry.TryGet(row.Box, out var descriptor));
                var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [.. row.Ints], Text = "coins" };
                table.Rows.Add(row.RoomId, row.Owner, row.Box, JsonSerializer.Serialize(native));
            }

            return table;
        });
        var database = Proxy<Plus.Database.IDatabase>((method, _) => method == "Connection" ? new NoOpConnection(read) : throw new InvalidOperationException(method));
        _databaseField.SetValue(null, database);
        var wired = new WiredComponent(_room, TestLogging.Logger, TimeProvider.System, TestRoomSettings.Empty, TestWiredRoomSettingsFactory.Instance,
            new EmptyConfigurationStore(), database, TestWiredRewardService.Instance, TestBotManagementStore.Instance, TestWiredClients.Empty,
            TestGroupManager.Empty, TestWiredDefinitions.Unused, TestWiredCommands.Unused, TestWiredAccess.Unused, TestItemRuntime.Travel);
        Set("_wiredComponent", wired);
        var box = WiredBox(601, "wf_var_reference", 1, 1);
        Assert.True(_room.GetWired().AddBox((IWiredItem)box));
        var fresh = WiredNativeEditorProjection.DefaultNative(box.Descriptor);
        object[] body = [601, 1, 1, "alias", 0, fresh.FurniSourceTypes.Length, .. fresh.FurniSourceTypes.Cast<object>(),
            fresh.UserSourceTypes.Length, .. fresh.UserSourceTypes.Cast<object>(), 1, shared, 0];
        _client.Packets.Clear();
        var service = new WiredConfigurationService(new EmptyConfigurationStore(), null!, null!, TestLogging.For<WiredConfigurationService>());
        new SaveWiredVariableConfigEvent(service).Parse(_client, EditorTestSupport.Incoming(body)).GetAwaiter().GetResult();

        var refusal = _client.Packets.Where(packet => packet.Header == ServerPacketHeader.WiredValidationErrorComposer).ToArray();

        if (refusal.Length == 0) {
            return _client.Packets.Any(packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer) ? "saved" : "dropped";
        }

        return System.Text.Encoding.UTF8.GetString(refusal[0].Body, 2, (refusal[0].Body[0] << 8) | refusal[0].Body[1]);
    }

    [Fact]
    public void AReferenceToASharedVariableOfTheOwnersOtherRoomIsSaved() =>
        Assert.Equal("saved", SaveReference("shared:8:room:200", (8, "7", "wf_var_room", [11])));

    [Fact]
    public void AReferenceToAnotherOwnersSharedVariableIsRefusedWithMustOwn() =>
        Assert.Equal(MustOwn, SaveReference("shared:8:room:200", (8, "99", "wf_var_room", [11])));

    [Theory]
    [InlineData("shared:8:room:999")]
    public void AReferenceToAVariableThatDoesNotExistIsRefusedAsInvalid(string shared) =>
        Assert.Equal(InvalidReference, SaveReference(shared, null));

    [Theory]
    [InlineData("shared:8:room:200", 8u, "wf_var_room", 1)]
    [InlineData("shared:8:room:200", 9u, "wf_var_room", 11)]
    [InlineData("shared:8:user:200", 8u, "wf_var_room", 11)]
    public void AReferenceToAVariableThatIsNotSharedThereIsRefusedAsInvalid(string shared, uint room, string box, int availability) =>
        Assert.Equal(InvalidReference, SaveReference(shared, (room, "99", box, [availability])));
}
