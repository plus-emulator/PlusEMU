using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Reflection;
using System.Text;
using Plus.Core.Settings;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.Communication.Packets.Outgoing;
using Xunit;

namespace Plus.Tests;

/// <summary>Branding saves through the real metadata service into the real placement path; only the item store is faked.</summary>
public partial class PlacedFurniRoomTests
{
    [Fact]
    public void BrandingIsWrittenBeforePublicationAndTheSnapshotIsAttachedAfterCommit()
    {
        var item = PlacedBranding(20, InteractionType.Background);
        Recipient();
        item.ExtraData = new MapDataFormat(new() { ["old"] = "value" });
        var store = new BrandingStore(() =>
        {
            Assert.Equal("old\tvalue", item.ExtraData.Serialize());
            Assert.False(MovedItems().ContainsKey(20));
            Assert.Empty(ObjectUpdates());
        });

        new RoomItemMetadataService(store, null!).SetBranding(_client, new(20, Pairs("offsetX", "1", "offsetY", "2")));

        Assert.Equal((1, 20u, RoomId, "state\t0\noffsetX\t1\noffsetY\t2"), (store.Writes, store.ItemId, store.RoomId, store.Data));
        Assert.Equal("state\t0\noffsetX\t1\noffsetY\t2", item.ExtraData.Serialize());
        var update = Assert.Single(ObjectUpdates());
        Assert.True(update.Body.AsSpan().IndexOf(Encode("state", "0", "offsetX", "1", "offsetY", "2")) >= 0);
        Assert.True(MovedItems().ContainsKey(20));
    }

    [Fact]
    public void BlockedPlacementNeverWritesOrChangesTheModel()
    {
        var item = PlacedBranding(21, InteractionType.Background);
        Recipient();
        item.ExtraData = new MapDataFormat(new() { ["old"] = "value" });
        item.SetState(99, 99, 0);
        var store = new BrandingStore();

        new RoomItemMetadataService(store, null!).SetBranding(_client, new(21, Pairs("offsetX", "1")));

        Assert.Equal(0, store.Writes);
        Assert.Equal("old\tvalue", item.ExtraData.Serialize());
        Assert.Empty(ObjectUpdates());
        Assert.Empty(_client.Sent);
        Assert.False(MovedItems().ContainsKey(21));
    }

    [Fact]
    public void FailingStoreLeavesStateGeometryAndPublicationUnchanged()
    {
        var item = PlacedBranding(22, InteractionType.Background);
        Recipient();
        item.ExtraData = new MapDataFormat(new() { ["old"] = "value" });
        var before = (item.GetX, item.GetY, item.GetZ, item.Rotation);
        var store = new BrandingStore { Fail = true };

        Assert.Throws<InvalidOperationException>(() => new RoomItemMetadataService(store, null!).SetBranding(_client, new(22, Pairs("offsetX", "1"))));

        Assert.Equal(1, store.Writes);
        Assert.Equal("old\tvalue", item.ExtraData.Serialize());
        Assert.Equal(before, (item.GetX, item.GetY, item.GetZ, item.Rotation));
        Assert.Empty(ObjectUpdates());
        Assert.Empty(_client.Sent);
        Assert.False(MovedItems().ContainsKey(22));
    }

    [Theory]
    [InlineData("right")]
    [InlineData("permission")]
    [InlineData("temporary")]
    [InlineData("image")]
    [InlineData("missing-map")]
    public void DeniedBrandingNeverReachesTheStoreOrTheModel(string denial)
    {
        var item = denial == "temporary" ? FloorTemporary(23) : PlacedBranding(23, InteractionType.Background);
        Recipient();
        item.ExtraData = new MapDataFormat(new() { ["old"] = "value" });

        if (denial == "right") {
            _room.OwnerName = "someone-else";
        }

        if (denial == "permission") {
            _client.GetHabbo().Access = UserAccess.Empty;
        }

        var values = denial switch
        {
            "image" => Pairs("url", "https://example.invalid/x.png"),
            "missing-map" => null,
            _ => Pairs("offsetX", "1"),
        };
        var store = new BrandingStore();

        new RoomItemMetadataService(store, null!).SetBranding(_client, new(23, values));

        Assert.Equal(0, store.Writes);
        Assert.Equal("old\tvalue", item.ExtraData.Serialize());
        Assert.Empty(ObjectUpdates());
    }

    [Theory]
    [InlineData("https://images.example/room.png", "")]
    [InlineData("http://images.example/room.png?v=2", "https://example.com/")]
    [InlineData("", "")]
    public void BackgroundSavesWebImageAndClickAddresses(string imageUrl, string clickUrl)
    {
        var item = PlacedBranding(25, InteractionType.Background);
        Recipient();
        var store = new BrandingStore();

        new RoomItemMetadataService(store, null!).SetBranding(_client, new(25, Pairs("imageUrl", imageUrl, "clickUrl", clickUrl, "offsetX", "-120", "offsetY", "40", "offsetZ", "3")));

        var expected = new Dictionary<string, string>
        {
            ["state"] = "0",
            ["imageUrl"] = imageUrl,
            ["clickUrl"] = clickUrl,
            ["offsetX"] = "-120",
            ["offsetY"] = "40",
            ["offsetZ"] = "3"
        };
        Assert.Equal(1, store.Writes);
        Assert.Equal(expected, Assert.IsType<MapDataFormat>(item.ExtraData).Data);
        var reloaded = FurniExtraData.Load(item.Definition, store.Data!, keepLegacy: true);
        Assert.Equal(expected, Assert.IsType<MapDataFormat>(reloaded).Data);
        var update = Assert.Single(ObjectUpdates());
        Assert.True(update.Body.AsSpan().IndexOf(Encode("imageUrl", imageUrl)) >= 0);
    }

    [Theory]
    [InlineData("imageUrl", "javascript:alert(1)")]
    [InlineData("imageUrl", "data:image/png;base64,AAAA")]
    [InlineData("imageUrl", "file:///etc/passwd")]
    [InlineData("imageUrl", "ftp://images.example/room.png")]
    [InlineData("imageUrl", "//images.example/room.png")]
    [InlineData("imageUrl", "/camera/room.png")]
    [InlineData("imageUrl", "https://images.example/a.png\nclickUrl\tjavascript:alert(1)")]
    [InlineData("imageUrl", "https://images.example/a.png\toffsetX")]
    [InlineData("imageUrl", @"https://images.example/\u0009a.png")]
    [InlineData("imageUrl", "https://images.example/a b.png")]
    [InlineData("clickUrl", "javascript:alert(1)")]
    [InlineData("offsetX", "https://images.example/a.png")]
    [InlineData("url", "https://images.example/a.png")]
    [InlineData("ImageUrl", "https://images.example/a.png")]
    public void BackgroundRejectsUnsafeAddresses(string key, string value)
    {
        var item = PlacedBranding(26, InteractionType.Background);
        Recipient();
        item.ExtraData = new MapDataFormat(new() { ["old"] = "value" });
        var store = new BrandingStore();

        new RoomItemMetadataService(store, null!).SetBranding(_client, new(26, Pairs(key, value, "offsetX", "0")));

        Assert.Equal(0, store.Writes);
        Assert.Equal("old\tvalue", item.ExtraData.Serialize());
        Assert.Empty(ObjectUpdates());
    }

    [Fact]
    public void BackgroundClampsNumbersAndDropsUnknownKeys()
    {
        var item = PlacedBranding(27, InteractionType.Background);
        Recipient();
        var store = new BrandingStore();

        new RoomItemMetadataService(store, null!).SetBranding(_client, new(27, Pairs(
            "imageUrl", "", "offsetX", "99999", "offsetY", "-99999", "offsetZ", "5000", "scale", "9999", "alpha", "-5", "extra", "1", "state", "7")));

        Assert.Equal(new Dictionary<string, string>
        {
            ["state"] = "0",
            ["imageUrl"] = "",
            ["offsetX"] = "10000",
            ["offsetY"] = "-10000",
            ["offsetZ"] = "1000",
            ["scale"] = "500",
            ["alpha"] = "0"
        }, Assert.IsType<MapDataFormat>(item.ExtraData).Data);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public void BackgroundTreatsAnEmptyOffsetAsZero()
    {
        var item = PlacedBranding(28, InteractionType.Background);
        Recipient();

        new RoomItemMetadataService(new BrandingStore(), null!).SetBranding(_client, new(28, Pairs("offsetX", "", "offsetY", " 4 ")));

        var data = Assert.IsType<MapDataFormat>(item.ExtraData).Data;
        Assert.Equal(("0", "4"), (data["offsetX"], data["offsetY"]));
    }

    [Theory]
    [InlineData("offsetX", "abc")]
    [InlineData("offsetY", "1.5")]
    [InlineData("offsetZ", "99999999999999999999")]
    [InlineData("scale", "big")]
    [InlineData("alpha", "50%")]
    public void BackgroundRejectsNumbersThatAreNotWholeNumbersAndTellsTheUser(string key, string value)
    {
        var item = PlacedBranding(29, InteractionType.Background);
        Recipient();
        item.ExtraData = new MapDataFormat(new() { ["old"] = "value" });
        var store = new BrandingStore();

        new RoomItemMetadataService(store, null!).SetBranding(_client, new(29, Pairs(key, value)));

        Assert.Equal(0, store.Writes);
        Assert.Equal("old\tvalue", item.ExtraData.Serialize());
        Assert.Empty(ObjectUpdates());
        Assert.Contains(ServerPacketHeader.BroadcastMessageAlertComposer, _client.Sent);
    }

    [Theory]
    [InlineData("https://images.example/a.png", true)]
    [InlineData("https://IMAGES.example/a.png", true)]
    [InlineData("https://cdn.test/a.png", true)]
    [InlineData("https://eu.cdn.test/path/a.png", true)]
    [InlineData("https://evil.example/a.png", false)]
    [InlineData("https://notimages.example/a.png", false)]
    [InlineData("https://images.example.evil.test/a.png", false)]
    [InlineData("https://images.example@evil.test/a.png", false)]
    [InlineData("https://user:pw@images.example/a.png", false)]
    public void BackgroundImageHostMustBeOnTheConfiguredList(string imageUrl, bool allowed)
    {
        PlacedBranding(30, InteractionType.Background);
        Recipient();
        var store = new BrandingStore();
        var service = new RoomItemMetadataService(store, null!, new Settings(" images.example , *.cdn.test,cdn.test "));

        service.SetBranding(_client, new(30, Pairs("imageUrl", imageUrl)));

        Assert.Equal(allowed ? 1 : 0, store.Writes);
        Assert.Equal(!allowed, _client.Sent.Contains(ServerPacketHeader.BroadcastMessageAlertComposer));
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("  , ")]
    public void BackgroundImageHostIsFreeWhenTheListIsEmpty(string setting)
    {
        PlacedBranding(31, InteractionType.Background);
        Recipient();
        var store = new BrandingStore();

        new RoomItemMetadataService(store, null!, new Settings(setting)).SetBranding(_client, new(31, Pairs("imageUrl", "https://any.example/a.png")));

        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public void BackgroundClickAddressIsNotHeldToTheImageHostList()
    {
        PlacedBranding(32, InteractionType.Background);
        Recipient();
        var store = new BrandingStore();

        new RoomItemMetadataService(store, null!, new Settings("images.example")).SetBranding(_client, new(32, Pairs("clickUrl", "https://elsewhere.example/")));

        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public void BackgroundRejectsAnImageAddressThatIsTooLong()
    {
        PlacedBranding(33, InteractionType.Background);
        Recipient();
        var store = new BrandingStore();

        new RoomItemMetadataService(store, null!).SetBranding(_client, new(33, Pairs("imageUrl", "https://images.example/" + new string('a', 600))));

        Assert.Equal(0, store.Writes);
    }

    private sealed class Settings(string allowedHosts) : ISettingsManager
    {
        public string TryGetValue(string value) => value == FurniExtraData.BackgroundAllowedHostsSetting ? allowedHosts : "0";
        public string? GetOptionalValue(string key) => TryGetValue(key);
        public Task Reload() => Task.CompletedTask;
    }

    [Theory]
    [InlineData(InteractionType.Gate)]
    [InlineData(InteractionType.FxProvider)]
    public void IdOnlyAndNonBackgroundFramesKeepThePlacementOnlyRepublish(InteractionType type)
    {
        var item = PlacedBranding(24, type);
        Recipient();
        item.ExtraData = new MapDataFormat(new() { ["old"] = "value" });
        var store = new BrandingStore();
        var service = new RoomItemMetadataService(store, null!);

        service.SetBranding(_client, new(24, null));
        Assert.Single(ObjectUpdates());
        service.SetBranding(_client, new(24, Pairs("a", "1")));

        Assert.Equal(0, store.Writes);
        Assert.Equal("old\tvalue", item.ExtraData.Serialize());
        Assert.Equal(2, ObjectUpdates().Count);
        Assert.True(MovedItems().ContainsKey(24));
    }

    private Item PlacedBranding(uint id, InteractionType type)
    {
        _client.GetHabbo().Access = Branding();
        var item = Furni(id, type, WiredBoxType.None);
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, item, 1, 1, 0, true, false, false));
        MovedItems().Clear();
        _client.Sent.Clear();
        _client.Packets.Clear();

        return item;
    }

    // A registered room user bound to the fixture client receives room broadcasts like a real occupant.
    private RoomUser Recipient()
    {
        var user = new RoomUser(7, RoomId, 1, _room, _client, TestChatEmotions.Unused, TestRewardProgress.Unused);
        var users = (ConcurrentDictionary<int, RoomUser>)typeof(RoomUserManager).GetField("_users", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_room.GetRoomUserManager())!;
        users.TryAdd(1, user);

        return user;
    }

    private List<(uint Header, byte[] Body)> ObjectUpdates() =>
        _client.Packets.Where(packet => packet.Header == ServerPacketHeader.ObjectUpdateComposer).ToList();

    private static ImmutableArray<string>? Pairs(params string[] values) => ImmutableArray.Create(values);

    // Flash string framing: a big-endian UInt16 length followed by UTF-8 bytes, in key/value order.
    private static byte[] Encode(params string[] values)
    {
        using var stream = new MemoryStream();

        foreach (var value in values) {
            var bytes = Encoding.UTF8.GetBytes(value);
            var length = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(length, checked((ushort)bytes.Length));
            stream.Write(length);
            stream.Write(bytes);
        }

        return stream.ToArray();
    }

    private Item FloorTemporary(uint id)
    {
        _client.GetHabbo().Access = Branding();
        var item = new Item { Id = id, RoomId = _room.Id, OwnerId = 7, IsTemporary = true, Definition = new ItemDefinition { InteractionType = InteractionType.Background, Type = ItemType.Floor } };
        var floor = (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_floorItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_room.GetRoomItemHandler())!;
        floor[id] = item;

        return item;
    }

    private ConcurrentDictionary<uint, Item> MovedItems() =>
        (ConcurrentDictionary<uint, Item>)typeof(RoomItemHandling).GetField("_movedItems", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_room.GetRoomItemHandler())!;

    private static UserAccess Branding() =>
        UserAccess.Create(Array.Empty<RoleAssignment>(), [new UserPermissionOverride(PermissionKeys.RoomItemSaveBrandingItems, false)]);

    private sealed class BrandingStore(Action? beforeWrite = null) : IRoomItemMetadataStore
    {
        public bool Fail { get; init; }
        public int Writes { get; private set; }
        public uint ItemId { get; private set; }
        public uint RoomId { get; private set; }
        public string? Data { get; private set; }
        public void SetBrandingData(uint itemId, uint roomId, string data)
        {
            beforeWrite?.Invoke();
            Writes++;
            (ItemId, RoomId, Data) = (itemId, roomId, data);

            if (Fail) {
                throw new InvalidOperationException("forced failure");
            }
        }
        public void SetMannequinData(uint itemId, uint roomId, string data) => throw new NotSupportedException();
        public void SetToner(uint itemId, uint roomId, int hue, int saturation, int lightness) => throw new NotSupportedException();
        public Plus.HabboHotel.Items.Data.Moodlight.MoodlightRecord? LoadMoodlight(uint itemId) => throw new NotSupportedException();
        public void SetMoodlightEnabled(uint itemId, uint roomId, bool enabled) => throw new NotSupportedException();
        public void UpdateMoodlightPreset(uint itemId, uint roomId, int preset, string value) => throw new NotSupportedException();
        public Plus.HabboHotel.Items.Data.Toner.TonerRecord? LoadToner(uint itemId) => throw new NotSupportedException();
    }
}
