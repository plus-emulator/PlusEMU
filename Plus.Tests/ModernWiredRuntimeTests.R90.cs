using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Engine;
using Plus.Communication.Packets.Outgoing;
using Plus.Communication.Packets.Outgoing.Rooms.Engine;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Labels;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Items.Wired.Variables.Fx;
using Plus.HabboHotel.Rooms.Instance;
using Xunit;

namespace Plus.Tests;

/// <summary>Facts of the hhus live run r90 (reviews/r90-implementation-facts.md, WIN63-202610131318-860371703).</summary>
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public void PickupConfirmationCarriesTheVariableKeysInTheObservedSize()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new ObjectRemoveConfirmComposer(42, false, ObjectRemoveConfirmComposer.VariableTitle, ObjectRemoveConfirmComposer.VariableBody).Compose(packet);

        Assert.Equal(new object[] { 2, 42u, "wiredfurni.variables.pickup.title", "wiredfurni.variables.pickup.body" }, packet.Writes);
        // Observed: 83 bytes with the 6-byte frame head (length and header): 4 + 4 + (2 + 33) + (2 + 32) = 77.
        Assert.Equal(77, 4 + 4 + 2 + ObjectRemoveConfirmComposer.VariableTitle.Length + 2 + ObjectRemoveConfirmComposer.VariableBody.Length);
        Assert.Equal(3498u, new ObjectRemoveConfirmComposer(0, false, "", "").MessageId);
    }

    [Fact]
    public async Task APickupOfAnythingButAStoredVariableIsHandedOnWithItsConfirmFlag()
    {
        using var f = new TeleportFixture();
        var item = MakeItem(130, "wf_var_user");
        f.Items[item.Id] = item;
        var calls = new List<uint>();
        var service = new RecordingPickup(calls);
        var sent = Capture(f.Client);

        // The legacy two-int form and the AIR/Volt confirmed form both reach the pickup when nothing stored is at stake.
        await new PickupObjectEvent(service).Parse(f.Client, EditorTestSupport.Incoming(2, 130));
        await new PickupObjectEvent(service).Parse(f.Client, EditorTestSupport.Incoming(10, 130, true));

        Assert.Equal([130u, 130u], calls);
        Assert.Empty(sent);
        Assert.False(f.Room.GetWired().StoresVariableData(item));
    }

    [Fact]
    public void RoomEntryAlwaysCarriesTheLabelList()
    {
        using var f = new TeleportFixture();
        f.Room.GetWired().LabelStore = new EmptyLabels();
        var sent = Capture(f.Client);

        f.Room.GetWired().SendLabels(f.Client);

        // Observed on hhus (r90): an empty 2237 of 10 bytes on every room entry.
        var labels = Reply(sent, ServerPacketHeader.WiredLabelsComposer);
        Assert.Equal(0, labels.Int());
        labels.End();
    }

    [Fact]
    public void LeaderboardRowsArePushedNoSoonerThanFiveSecondsAfterAChange()
    {
        using var f = new TeleportFixture();
        var wired = f.Room.GetWired();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var subscriptions = (System.Collections.IDictionary)typeof(WiredComponent).GetField("_leaderboardSubscriptions", flags)!.GetValue(wired)!;
        var subscription = typeof(WiredComponent).GetNestedType("LeaderboardSubscription", flags)!;
        subscriptions[(1, 77)] = Activator.CreateInstance(subscription, f.Client, 1, "user:9", new[] { 0 })!;
        var before = DateTimeOffset.UtcNow;

        typeof(WiredComponent).GetMethod("LeaderboardValuesChanged", flags)!.Invoke(wired,
            [new[] { new WiredVariableChange(0, new(9, WiredVariableTarget.User, 1), WiredVariableChangeKind.Updated, null, new(9, null, null), 0, 0) }]);
        typeof(WiredComponent).GetMethod("FlushLeaderboards", flags)!.Invoke(wired, []);

        // Observed on hhus (r90): +5.3 s and +9.4 s after the writes, never in their tick. The change is still waiting.
        var due = (DateTimeOffset?)typeof(WiredComponent).GetField("_leaderboardPushDue", flags)!.GetValue(wired);
        Assert.NotNull(due);
        Assert.True(due >= before + WiredComponent.LeaderboardPushDelay);
        Assert.Contains(9u, (HashSet<uint>)typeof(WiredComponent).GetField("_dirtyLeaderboardVariables", flags)!.GetValue(wired)!);
    }

    [Fact]
    public void PlayingGamePacketIsOneBoolean()
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        new YouArePlayingGameComposer(true).Compose(packet);

        Assert.Equal(new object[] { true }, packet.Writes);
        Assert.Equal(1293u, new YouArePlayingGameComposer(false).MessageId);
    }

    [Theory]
    [InlineData("wf_xtra_var_fx_health")]
    [InlineData("wf_xtra_var_fx_number")]
    public void FreshFxEditorLeavesTheStyleSlotsUnset(string card)
    {
        // Observed on hhus (r90 varfx-a/b): 23 ints, user source first, colour/width/renderer -1 until a save normalises them.
        var native = WiredNativeEditorProjection.DefaultNative(Descriptor(card));

        Assert.Equal(23, native.OwnedIntParams.Length);
        Assert.Equal(1, native.OwnedIntParams[0]);
        Assert.Equal([-1, -1, -1], native.OwnedIntParams.Skip(7).Take(3));
        Assert.NotNull(WiredNativeEditorProjection.DefaultRuntime(1, Descriptor(card)));
    }

    [Fact]
    public void VariableFxPacketsUseTheOfficialIds()
    {
        // Old registry: 3340 class_2593, 1418 class_3245, 1943 class_2741, 2532 class_2530 (new 2205, 364, 2372, 214).
        Assert.Equal((3340u, 1418u, 1943u, 2532u), (ServerPacketHeader.WiredVariableFxConfigsComposer, ServerPacketHeader.WiredVariableFxConfigsRemovedComposer,
            ServerPacketHeader.WiredVariableFxStatusComposer, ServerPacketHeader.WiredVariableFxStatusRemovedComposer));
    }

    private sealed class RecordingPickup(List<uint> calls) : Plus.HabboHotel.Rooms.IRoomItemPickupService
    {
        public Task PickUp(Plus.HabboHotel.GameClients.GameClient session, uint itemId)
        {
            calls.Add(itemId);

            return Task.CompletedTask;
        }

        public bool TryPickUp(Plus.HabboHotel.GameClients.GameClient session, uint itemId)
        {
            calls.Add(itemId);

            return true;
        }
    }

    private sealed class EmptyLabels : IWiredLabelStore
    {
        public IReadOnlyDictionary<uint, WiredLabel> Load(IReadOnlyCollection<uint> furniIds) => new Dictionary<uint, WiredLabel>();
        public void Save(WiredLabel label) => throw new NotSupportedException();
    }
}
