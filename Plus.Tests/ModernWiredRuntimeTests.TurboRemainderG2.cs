using System.Reflection;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Badges;
using Plus.HabboHotel.Users.Inventory.Furniture;
using Xunit;

namespace Plus.Tests;

// g2: wf_act_mute_triggerer and wf_act_give_reward through the real stack engine and reward service.
public partial class ModernWiredRuntimeTests
{
    private static void GiveUserAccess(TeleportFixture f, params string[] rights)
    {
        f.Habbo.Access = EditorTestSupport.Access(rights);
        f.Room.MutedUsers = new(); // The fixture room is built without its constructor.
    }

    [Fact]
    public void KickAppliesEffectFourOnTheNextStateUpdateAndRemovesTheUserTwoSecondsAfterTheFiring()
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        var replies = Capture(f.Client);
        f.Fire(land: false);
        f.Advance(500);
        var effect = Assert.Single(replies, reply => reply.Header == ServerPacketHeader.AvatarEffectComposer);
        var reader = new WireReader(effect.Body);
        Assert.Equal(7, reader.Int());
        Assert.Equal(4, reader.Int());
        Assert.Equal(0, reader.Int());
        Assert.Equal(4, f.Habbo.Effects!.CurrentEffect);
        f.Advance(1499);
        Assert.True(f.User.IsAttachedTo(f.Room));
        Assert.DoesNotContain(replies, reply => reply.Header == ServerPacketHeader.UserRemoveComposer);
        f.Advance(1);
        Assert.False(f.User.IsAttachedTo(f.Room));
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void KickLeavesAUserWhoAlreadyLeftTheRoomAlone()
    {
        using var f = new TeleportFixture(actionName: "wf_act_kick_user", intParams: [], text: "");
        GiveUserAccess(f);
        var replies = Capture(f.Client);
        f.Fire(land: false);
        f.Room.GetRoomUserManager().RemoveUserFromRoom(f.Client, true, false);
        replies.Clear();
        f.Advance(2000);
        Assert.DoesNotContain(replies, reply => reply.Header == ServerPacketHeader.UserRemoveComposer || reply.Header == ServerPacketHeader.AvatarEffectComposer);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void MuteTriggererMutesTheTriggeringUserForTheMinutesAndWhispersTheMessage()
    {
        using var f = new TeleportFixture(actionName: "wf_act_mute_triggerer", intParams: [3, 0], text: "Quiet please");
        GiveUserAccess(f);
        var replies = Capture(f.Client);
        var before = DateTimeOffset.UtcNow;
        f.Fire();
        var after = DateTimeOffset.UtcNow;
        Assert.True(f.Room.MutedUsers.TryGetValue(1, out var until));
        Assert.InRange(until, before.AddMinutes(3), after.AddMinutes(3));
        Assert.True(f.Room.CheckMute(f.Client, after.AddMinutes(2)));
        Assert.False(f.Room.CheckMute(f.Client, after.AddMinutes(4)));
        Assert.False(f.Room.MutedUsers.ContainsKey(1)); // The expired mute is dropped by the first check after it.
        var chat = Assert.Single(Chats(replies));
        Assert.Equal("Quiet please", chat.Text);
        Assert.Equal(ServerPacketHeader.WhisperComposer, chat.Header);
        Assert.Empty(f.Errors);
    }

    [Fact]
    public void MuteTriggererWithoutAMessageMutesQuietly()
    {
        using var f = new TeleportFixture(actionName: "wf_act_mute_triggerer", intParams: [1, 0], text: "");
        GiveUserAccess(f);
        var replies = Capture(f.Client);
        f.Fire();
        Assert.True(f.Room.MutedUsers.ContainsKey(1));
        Assert.Empty(Chats(replies));
    }

    [Fact]
    public void MuteTriggererSavesTheZeroMinuteSliderPositionButMutesNobody()
    {
        using var f = new TeleportFixture(actionName: "wf_act_mute_triggerer", intParams: [1, 0], text: "Hush");
        GiveUserAccess(f);
        Assert.True(WiredNativeTestSupport.TryValidateRuntime(f.Action, new() { IntParams = [0, 0], Text = "Hush" }, out var config, out var error), error);
        f.Action.ApplyConfiguration(config);
        var replies = Capture(f.Client);
        f.Fire();
        Assert.Empty(f.Room.MutedUsers);
        Assert.Empty(Chats(replies)); // Turbo whispers only a user it actually muted.
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    [InlineData(-1, false)]
    public void MuteTriggererMinutesFollowTheOctoberSliderRange(int minutes, bool accepted)
    {
        Assert.True(WiredBoxRegistry.TryGet("wf_act_mute_triggerer", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with { OwnedIntParams = [minutes] };
        Assert.Equal(accepted, WiredNativeEditorProjection.TryCompile(7, descriptor, native, out _));
        Assert.True(WiredNativeEditorProjection.TryCompile(7, descriptor, native with { OwnedIntParams = [1], Text = new('x', 100) }, out _));
        Assert.False(WiredNativeEditorProjection.TryCompile(7, descriptor, native with { OwnedIntParams = [1], Text = new('x', 101) }, out _));
    }

    [Fact]
    public void MuteTriggererNeverMutesTheRoomOwnerOrStaffAndSaysNothingToThem()
    {
        using var owner = new TeleportFixture(actionName: "wf_act_mute_triggerer", intParams: [5, 0], text: "No");
        GiveUserAccess(owner);
        owner.Room.OwnerId = 1;
        var replies = Capture(owner.Client);
        owner.Fire();
        Assert.Empty(owner.Room.MutedUsers);
        Assert.Empty(Chats(replies));

        using var staff = new TeleportFixture(actionName: "wf_act_mute_triggerer", intParams: [5, 0], text: "No");
        GiveUserAccess(staff, PermissionKeys.ModerationTool);
        replies = Capture(staff.Client);
        staff.Fire();
        Assert.Empty(staff.Room.MutedUsers);
        Assert.Empty(Chats(replies));
    }

    private static (Habbo Habbo, RoomUser User, List<(uint Header, byte[] Body)> Replies) RewardUser(TeleportFixture f, int id, int virtualId, bool inRoom = true, bool inventory = true)
    {
        var habbo = new Habbo { Id = id, Username = "user" + id, CurrentRoom = inRoom ? f.Room : null, Access = EditorTestSupport.Access([]) };

        if (inventory) {
            habbo.Inventory = new() { Furniture = new([], []), Badges = new(new()) };
        }

        var (client, _) = HabbiconTestSupport.Client(habbo);
        habbo.Client = client;
        var replies = Capture(client);
        var user = new RoomUser(id, 0, virtualId, f.Room, client, TestChatEmotions.Unused, TestRewardProgress.Unused);
        RoomUsers(f.Room)[virtualId] = user;

        return (habbo, user, replies);
    }

    private sealed class G2RewardStore(Func<Habbo, WiredRewardGrant> grant) : IWiredRewardStore
    {
        public readonly List<int> Claimed = [];
        public WiredRewardGrant ClaimAndGrant(Item box, uint roomId, Habbo habbo, WiredConfiguration configuration, IItemDataManager definitions, DateTimeOffset now)
        {
            Claimed.Add(habbo.Id);

            return grant(habbo);
        }
    }

    private static int[] ResultReasons(List<(uint Header, byte[] Body)> replies) => replies
        .Where(reply => reply.Header == ServerPacketHeader.WiredRewardResultComposer).Select(reply => new WireReader(reply.Body).Int()).ToArray();

    private static WiredConfiguration RewardConfig(string prizes, int[]? ints = null)
    {
        var draft = WiredRewards.Defaults() with { Text = prizes };

        if (ints != null) {
            draft = draft with { IntParams = [.. ints] };
        }

        Assert.True(WiredRewards.TryValidate(draft, out var config, out var error), error);

        return config;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(8)]
    public void RewardRefusalsTellTheUserWhyAndGrantNothing(int reason)
    {
        using var f = new TeleportFixture();
        var (habbo, user, replies) = RewardUser(f, 1, 7);
        var store = new G2RewardStore(_ => new(reason));
        var rewards = new WiredRewardService(store, DispatchProxy.Create<IItemDataManager, RecordingProxy>(), TimeProvider.System, TestLogging.Rewards);
        var context = Context(f.Room, new(WiredEventKind.Enter) { Actor = user }, [], [user]);
        context.Triggering.UserIds.Add(user.VirtualId);
        Assert.False(rewards.Execute(MakeItem(100, "wf_act_give_reward"), context, RewardConfig("0,BADGE1,100")));
        Assert.Equal([1], store.Claimed);
        Assert.Equal([reason], ResultReasons(replies));
        Assert.Empty(habbo.Inventory!.Furniture.GetItems);
        Assert.False(habbo.Inventory.Badges.HasBadge("BADGE1"));
    }

    [Fact]
    public void RewardBadgeAndProductGrantsReachTheInventoryOnceAndCountAsAGive()
    {
        using var f = new TeleportFixture();
        var (habbo, user, replies) = RewardUser(f, 1, 7);
        var item = new InventoryItem { Id = 900, OwnerId = 1, Definition = new() { Id = 5, Type = ItemType.Floor, SpriteId = 44 } };
        var grants = new Queue<WiredRewardGrant>([new(4, "BADGE1"), new(5, Furniture: item)]);
        var store = new G2RewardStore(_ => grants.Dequeue());
        var rewards = new WiredRewardService(store, DispatchProxy.Create<IItemDataManager, RecordingProxy>(), TimeProvider.System, TestLogging.Rewards);
        var context = Context(f.Room, new(WiredEventKind.Enter) { Actor = user }, [], [user]);
        context.Triggering.UserIds.Add(user.VirtualId);
        var config = RewardConfig("0,BADGE1,50;1,rubber_duck,50");
        Assert.True(rewards.Execute(MakeItem(100, "wf_act_give_reward"), context, config));
        Assert.True(habbo.Inventory!.Badges.HasBadge("BADGE1"));
        Assert.True(rewards.Execute(MakeItem(100, "wf_act_give_reward"), context, config));
        Assert.Same(item, habbo.Inventory.Furniture.GetItem(900));
        Assert.Single(habbo.Inventory.Furniture.GetItems);
        Assert.Equal([4, 5], ResultReasons(replies));
    }

    [Fact]
    public void RewardClaimsEachRealUserInTheRoomOnceAndSkipsBotsAndAbsentUsers()
    {
        using var f = new TeleportFixture();
        var (_, first, firstReplies) = RewardUser(f, 1, 7);
        var (_, second, secondReplies) = RewardUser(f, 2, 8);
        var (_, outside, outsideReplies) = RewardUser(f, 3, 9, inRoom: false);
        var (_, bare, bareReplies) = RewardUser(f, 4, 10, inventory: false);
        var bot = AddNamedBot(f, 11, "Bobby", 2, 2);
        var store = new G2RewardStore(_ => new(4, "BADGE1"));
        var rewards = new WiredRewardService(store, DispatchProxy.Create<IItemDataManager, RecordingProxy>(), TimeProvider.System, TestLogging.Rewards);
        var context = Context(f.Room, new(WiredEventKind.Enter) { Actor = first }, [], [first, second, outside, bare, bot]);

        foreach (var user in new[] { first, second, outside, bare, bot }) {
            context.Triggering.UserIds.Add(user.VirtualId);
        }

        Assert.True(rewards.Execute(MakeItem(100, "wf_act_give_reward"), context, RewardConfig("0,BADGE1,100")));
        Assert.Equal([1, 2], store.Claimed.Order().ToArray());
        Assert.Single(ResultReasons(firstReplies));
        Assert.Single(ResultReasons(secondReplies));
        Assert.Empty(ResultReasons(outsideReplies));
        Assert.Empty(ResultReasons(bareReplies));
    }

    [Fact]
    public void RewardWithoutAnyPrizeNeverReachesTheStore()
    {
        using var f = new TeleportFixture();
        var (_, user, replies) = RewardUser(f, 1, 7);
        var store = new G2RewardStore(_ => new(4, "BADGE1"));
        var rewards = new WiredRewardService(store, DispatchProxy.Create<IItemDataManager, RecordingProxy>(), TimeProvider.System, TestLogging.Rewards);
        var context = Context(f.Room, new(WiredEventKind.Enter) { Actor = user }, [], [user]);
        context.Triggering.UserIds.Add(user.VirtualId);
        Assert.False(rewards.Execute(MakeItem(100, "wf_act_give_reward"), context, RewardConfig("")));
        Assert.Empty(store.Claimed);
        Assert.Empty(replies);
    }

    [Fact]
    public void RewardPickFollowsTheProbabilityRollAndUniqueModeSpendsEachPrizeOncePerUser()
    {
        var prizes = new[] { new WiredRewardEntry(0, "A", 30), new WiredRewardEntry(1, "B", 20), new WiredRewardEntry(0, "C", 10) };
        var claim = new WiredRewardClaim();
        Assert.Equal("A", WiredRewards.Pick(prizes, false, claim, 1)!.Code);
        Assert.Equal("A", WiredRewards.Pick(prizes, false, claim, 30)!.Code);
        Assert.Equal("B", WiredRewards.Pick(prizes, false, claim, 31)!.Code);
        Assert.Equal("B", WiredRewards.Pick(prizes, false, claim, 50)!.Code);
        Assert.Equal("C", WiredRewards.Pick(prizes, false, claim, 51)!.Code);
        Assert.Equal("C", WiredRewards.Pick(prizes, false, claim, 60)!.Code);
        Assert.Null(WiredRewards.Pick(prizes, false, claim, 61)); // Probability missed: the sum is below 100.
        Assert.Null(WiredRewards.Pick(prizes, false, claim, 100));

        claim.ReceivedCodes.Add("A");
        Assert.Equal("B", WiredRewards.Pick(prizes, true, claim, 1)!.Code);
        Assert.Equal("C", WiredRewards.Pick(prizes, true, claim, 2)!.Code);
        claim.ReceivedCodes.UnionWith(["B", "C"]);
        Assert.Null(WiredRewards.Pick(prizes, true, claim, 1)); // Everything received already.
    }

    [Theory]
    [InlineData("0,BADGE1,100", true)]
    [InlineData("1,furni#5,100;0,ACH_Login1,50", true)]
    [InlineData("1,rubber_duck,1", true)]
    [InlineData("", true)]
    [InlineData("2,x,10", false)]
    [InlineData("0,,10", false)]
    [InlineData("0,AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA,10", false)]
    [InlineData("1,furni#0,10", false)]
    [InlineData("1,furni#abc,10", false)]
    [InlineData("1,duck#5,10", false)]
    [InlineData("0,BADGE1", false)]
    [InlineData("0,BADGE1,x", false)]
    [InlineData("0,BADGE1,10,extra", false)]
    public void RewardPrizeListFormatIsTypeCodeProbability(string prizes, bool accepted)
    {
        Assert.Equal(accepted, WiredRewards.TryEntries(prizes, out _));
    }

    [Fact]
    public void RewardPrizeListIsLimitedToTwentyPrizesAndProbabilitiesClampToAPercentage()
    {
        Assert.True(WiredRewards.TryEntries(string.Join(';', Enumerable.Range(0, 20).Select(index => $"0,B{index},5")), out var twenty));
        Assert.Equal(20, twenty.Count);
        Assert.False(WiredRewards.TryEntries(string.Join(';', Enumerable.Range(0, 21).Select(index => $"0,B{index},5")), out _));
        Assert.True(WiredRewards.TryEntries("0,A,250;0,B,-4", out var clamped));
        Assert.Equal([100, 0], clamped.Select(entry => entry.Probability).ToArray());
        Assert.False(WiredRewards.TryEntries(new string('1', 1) + ",x" + new string('y', 100) + ",5", out _)); // 101-character code.
    }

    [Theory]
    [InlineData(new[] { 0, 0, 0, 1, 0 }, true)]
    [InlineData(new[] { 3, 1, 1000, 1000, 11 }, true)]
    [InlineData(new[] { 4, 0, 0, 1, 0 }, false)]
    [InlineData(new[] { -1, 0, 0, 1, 0 }, false)]
    [InlineData(new[] { 0, 2, 0, 1, 0 }, false)]
    [InlineData(new[] { 0, 0, 1001, 1, 0 }, false)]
    [InlineData(new[] { 0, 0, -1, 1, 0 }, false)]
    [InlineData(new[] { 0, 0, 0, 0, 0 }, false)]
    [InlineData(new[] { 0, 0, 0, 1001, 0 }, false)]
    [InlineData(new[] { 0, 0, 0, 1, 12 }, false)]
    [InlineData(new[] { 0, 0, 0, 1 }, false)]
    public void RewardIntervalUniqueLimitAndCountFollowTheEditorRanges(int[] ints, bool accepted)
    {
        var draft = WiredRewards.Defaults() with { IntParams = [.. ints], Text = "0,BADGE1,100" };
        Assert.Equal(accepted, WiredRewards.TryValidate(draft, out _, out _));
    }

    [Fact]
    public void OnlyStaffWithTheModerationToolMaySaveARewardBox()
    {
        object[] BodyFor() => [100, 4, 0, 0, 0, 1, "0,BADGE1,100", 0, 0, 0, 1, 0, 0, 0];

        var plain = new MovementOracleSession("wf_act_give_reward");
        var original = plain.Action.Configuration;
        plain.Client.Packets.Clear();
        new SaveWiredEffectConfigEvent(new WiredConfigurationService(plain.Store, null!, null!, TestLogging.For<WiredConfigurationService>()))
            .Parse(plain.Client, Request(BodyFor())).GetAwaiter().GetResult();
        Assert.Same(original, plain.Action.Configuration);
        Assert.Empty(plain.Store.Saves);
        Assert.DoesNotContain(plain.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        Assert.Contains(plain.Client.Packets, packet => packet.Header == ServerPacketHeader.WiredValidationErrorComposer);

        var staff = new MovementOracleSession("wf_act_give_reward");
        staff.Client.GetHabbo().Access = EditorTestSupport.Access([PermissionKeys.ModerationTool]);
        staff.Client.Packets.Clear();
        new SaveWiredEffectConfigEvent(new WiredConfigurationService(staff.Store, null!, null!, TestLogging.For<WiredConfigurationService>()))
            .Parse(staff.Client, Request(BodyFor())).GetAwaiter().GetResult();
        Assert.Equal("0,BADGE1,100", Assert.Single(staff.Store.Saves).Text);
        Assert.Contains(staff.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
    }
}
