using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Inventory.AvatarEffect;
using Plus.HabboHotel.Users;
using Plus.HabboHotel.Users.Effects;
using Xunit;

namespace Plus.Tests;

// An effect box gives an effect for good; it is stored with a total duration of 0. The client reads the permanent flag in the effects
// list, AvatarEffectAdded and AvatarEffectActivated and shows it as permanent.
public sealed class PermanentAvatarEffectTests
{
    private static readonly DateTimeOffset Now = FixedTimeProvider.Epoch;

    [Fact]
    public void APermanentEffectNeverExpires()
    {
        var (habbo, effect) = Owner(activatedAt: Now.AddYears(-5));
        var (client, sent) = HabbiconTestSupport.Client(habbo);
        habbo.Client = client;

        Assert.IsType<EffectsComponent>(habbo.Effects).CheckEffectExpiryAt(habbo, Now);

        Assert.Same(effect, Assert.Single(habbo.Effects!.GetAllEffects));
        Assert.True(habbo.Effects.HasEffectAt(16, Now, activatedOnly: true));
        Assert.Empty(sent);
    }

    [Fact]
    public void TheEffectsListMarksAPermanentEffect()
    {
        var (habbo, _) = Owner(activatedAt: Now.AddYears(-5));

        var entry = Assert.Single(new AvatarEffectService(new FixedTimeProvider(Now)).Capture(habbo));

        Assert.Equal(new AvatarEffectEntry(16, int.MaxValue, 0, true, int.MaxValue, true), entry);
        Assert.Equal(new object[] { 1, 16, 0, int.MaxValue, 0, int.MaxValue, true }, Writes(new AvatarEffectsComposer([entry])));
    }

    [Fact]
    public void TheAddedAndActivatedPacketsCarryThePermanentFlag()
    {
        Assert.Equal(new object[] { 16, 0, int.MaxValue, true }, Writes(new AvatarEffectAddedComposer(16, int.MaxValue, true)));
        Assert.Equal(new object[] { 16, int.MaxValue, true }, Writes(new AvatarEffectActivatedComposer(new AvatarEffectActivation(16, int.MaxValue, true))));
    }

    private static (Habbo Habbo, AvatarEffect Effect) Owner(DateTimeOffset activatedAt)
    {
        var habbo = new Habbo { Id = 7, Username = "owner" };
        habbo.Effects = new EffectsComponent(new FixedTimeProvider(Now));
        habbo.Effects.Init(habbo);
        var effect = new AvatarEffect(501, 7, 16, 0, true, activatedAt, 1);
        habbo.Effects.TryAdd(effect);

        return (habbo, effect);
    }

    private static List<object> Writes(IServerPacket composer)
    {
        var packet = new HabbiconTestSupport.RecordingPacket();
        composer.Compose(packet);

        return packet.Writes;
    }
}
