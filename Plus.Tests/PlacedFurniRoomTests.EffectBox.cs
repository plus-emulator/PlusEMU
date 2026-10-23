using System.Buffers.Binary;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.Core.Settings;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Quests;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users.Clothing.Parts;
using Plus.HabboHotel.Users.Effects;
using Xunit;

namespace Plus.Tests;

public partial class PlacedFurniRoomTests
{
    private const uint EffectBoxId = 50;

    // Only the owner gets the dialog, and OK sends the plain use packet.
    // The Habbo dialog text says the box works once and the effect can be worn again later from the effects tab, so it is permanent.
    // A box left in state 1 by the old toggle is the same item and must still open.
    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public async Task TheOwnerOpensAnEffectBoxOnceAndWearsItsEffectForGood(string state)
    {
        var (store, box, user) = PrepareEffectBox("fxbox_fx16", state, ownerId: 7);

        await new UseFurnitureEvent(EffectBoxUseService()).Parse(_room, _client, ClientPacket((int)EffectBoxId, 0));

        Assert.Equal([(EffectBoxId, 7, RoomId, 16)], store.Consumed);
        Assert.Equal(Ints(16, 0, int.MaxValue).Append((byte)1), Body(ServerPacketHeader.AvatarEffectAddedComposer));
        Assert.Equal(Ints(16, int.MaxValue).Append((byte)1), Body(ServerPacketHeader.AvatarEffectActivatedComposer));
        Assert.Equal(Ints(user.VirtualId, 16, 0), Body(ServerPacketHeader.AvatarEffectComposer));
        var effects = Assert.IsType<EffectsComponent>(_client.GetHabbo().Effects);
        Assert.Equal(16, effects.CurrentEffect);
        Assert.True(effects.HasEffect(16, activatedOnly: true));

        // The box plays its opening animation (state 1) and then leaves the room, which frees its tile.
        Assert.Equal("1", box.LegacyDataString);
        Assert.Contains(ServerPacketHeader.ObjectDataUpdateComposer, _client.Sent);
        Assert.Same(box, _room.GetRoomItemHandler().GetItem(EffectBoxId));
        box.ProcessUpdates();
        Assert.Null(_room.GetRoomItemHandler().GetItem(EffectBoxId));
        Assert.Contains(ServerPacketHeader.ObjectRemoveComposer, _client.Sent);
        Assert.Empty(_room.GetGameMap().GetRoomItemForSquare(1, 1));
    }

    [Fact]
    public async Task SomeoneElsesEffectBoxDoesNothingEvenWithRights()
    {
        var (store, box, _) = PrepareEffectBox("fxbox_fx16", "0", ownerId: 99);

        await new UseFurnitureEvent(EffectBoxUseService()).Parse(_room, _client, ClientPacket((int)EffectBoxId, 0));

        Assert.Empty(store.Consumed);
        Assert.Equal("0", box.LegacyDataString);
        Assert.DoesNotContain(ServerPacketHeader.ObjectDataUpdateComposer, _client.Sent);
        Assert.DoesNotContain(ServerPacketHeader.AvatarEffectAddedComposer, _client.Sent);
        Assert.Equal(0, _client.GetHabbo().Effects!.CurrentEffect);
        box.ProcessUpdates();
        Assert.Same(box, _room.GetRoomItemHandler().GetItem(EffectBoxId));
    }

    [Fact]
    public async Task AnEffectBoxThatIsAlreadyOpeningIsNotUsedTwice()
    {
        var (store, _, _) = PrepareEffectBox("fxbox_fx16", "0", ownerId: 7);
        var use = new UseFurnitureEvent(EffectBoxUseService());

        await use.Parse(_room, _client, ClientPacket((int)EffectBoxId, 0));
        await use.Parse(_room, _client, ClientPacket((int)EffectBoxId, 0));

        Assert.Single(store.Consumed);
        Assert.Single(_client.Sent, header => header == ServerPacketHeader.AvatarEffectAddedComposer);
    }

    [Fact]
    public async Task AnEffectBoxWithoutAnEffectNumberIsKept()
    {
        var (store, box, _) = PrepareEffectBox("fxbox_fxdeluxe", "0", ownerId: 7);

        await new UseFurnitureEvent(EffectBoxUseService()).Parse(_room, _client, ClientPacket((int)EffectBoxId, 0));

        Assert.Empty(store.Consumed);
        Assert.Same(box, _room.GetRoomItemHandler().GetItem(EffectBoxId));
        Assert.DoesNotContain(ServerPacketHeader.AvatarEffectAddedComposer, _client.Sent);
    }

    [Fact]
    public void EffectBoxFurnitureUsesTheEffectBoxInteraction() =>
        Assert.Equal(InteractionType.EffectBox, InteractionTypes.GetTypeFromString("effect_box"));

    private (EffectBoxStore Store, Item Box, RoomUser User) PrepareEffectBox(string name, string state, uint ownerId)
    {
        var store = new EffectBoxStore();
        var redemptions = new ItemRedemptionService(store, Proxy<ISettingsManager>((_, _) => "1"));
        var interactors = new Plus.HabboHotel.Items.Interactor.ItemInteractorFactory(TestItemRuntime.Travel, TestItemRuntime.Profiles,
            TestItemRuntime.Quests, TestItemRuntime.Rewards, TestItemRuntime.Achievements, redemptions);
        Set("_roomItemHandling", new RoomItemHandling(_room, TestRoomItemStore.Instance, TestRoomItemMetadataStore.Instance,
            TestGameClientManager.Empty, TestLanguageManager.RoomItems, interactors, TestItemRuntime.Travel, TestItemRuntime.Rewards));
        var user = AdmitOwner();
        var habbo = _client.GetHabbo();
        habbo.Effects = new EffectsComponent(TimeProvider.System);
        habbo.Effects.Init(habbo);

        var box = Furni(EffectBoxId, InteractionType.EffectBox, WiredBoxType.None);
        box.OwnerId = ownerId;
        box.Definition.ItemName = name;
        box.Definition.Modes = 2;
        box.Definition.Stackable = false;
        box.ExtraData = new LegacyDataFormat { Data = state };
        Assert.True(_room.GetRoomItemHandler().SetFloorItem(null, box, 1, 1, 0, true, false, false));
        _client.Sent.Clear();
        _client.Packets.Clear();

        return (store, box, user);
    }

    private static FurnitureUseService EffectBoxUseService() => new(null!, Proxy<IQuestManager>((_, _) => null));

    private byte[] Body(uint header) => Assert.Single(_client.Packets, packet => packet.Header == header).Body;

    private static byte[] Ints(params int[] values)
    {
        var bytes = new byte[values.Length * 4];

        for (var i = 0; i < values.Length; i++) {
            BinaryPrimitives.WriteInt32BigEndian(bytes.AsSpan(i * 4), values[i]);
        }

        return bytes;
    }

    private sealed class EffectBoxStore : IItemRedemptionStore
    {
        public List<(uint ItemId, int OwnerId, uint RoomId, int SpriteId)> Consumed { get; } = [];

        public AvatarEffect ConsumeEffectBox(uint itemId, int ownerId, uint roomId, int spriteId, DateTimeOffset now)
        {
            Consumed.Add((itemId, ownerId, roomId, spriteId));

            return new(500, ownerId, spriteId, 0, true, now, 1);
        }

        public void DeleteExchange(uint itemId, int ownerId, uint roomId) => throw new NotSupportedException();

        public IReadOnlyList<ClothingParts> ConsumeClothing(uint itemId, int ownerId, uint roomId, string name, IReadOnlyCollection<int> partIds) =>
            throw new NotSupportedException();
    }
}
