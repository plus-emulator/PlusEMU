using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Xunit;
using static Plus.Tests.WiredR104Save;

namespace Plus.Tests;

// Bot Reaches Furni editor saves as Habbo stores them. Observed on AIR WIN63-202610201032-107964540.
public sealed class WiredR105BotReachedFurniEditorTests
{
    private const string Name = "wf_trg_bot_reached_stf";

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Robbie")]
    [InlineData("Robbie ")]
    [InlineData("<b>x</b>")]
    [InlineData("a\tb")]
    [InlineData("a\nb")]
    [InlineData("ünï")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void TheBotNameIsStoredAsSent(string text)
    {
        using var world = new G1World();
        var result = Save(world, Name, text: text);

        Assert.True(result.Accepted, result.ToString());
        Assert.Equal(text, result.Text);
    }

    [Fact]
    public void AHundredCharacterBotNameIsStoredAsSent()
    {
        using var world = new G1World();
        var text = new string('a', 100);

        Assert.Equal(text, Save(world, Name, text: text).Text);
    }

    [Fact]
    public void ExtraIntsAreDropped()
    {
        using var world = new G1World();
        var result = Save(world, Name, [1, 2], text: "Robbie");

        Assert.True(result.Accepted, result.ToString());
        Assert.Empty(result.Ints);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void TheFurniSourcesArePicksAndSelector(int source)
    {
        using var world = new G1World();

        Assert.Equal([source], Save(world, Name, furni: [source]).Furni);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(201)]
    [InlineData(901)]
    public void OtherFurniSourcesAreRefused(int source)
    {
        using var world = new G1World();

        Assert.Equal("wiredfurni.error.invalid_furni_source", Save(world, Name, furni: [source]).Key);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    public void TheUserSourcesAreNameAndSelector(int source)
    {
        using var world = new G1World();

        Assert.Equal([source], Save(world, Name, users: [source]).Users);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    [InlineData(11)]
    [InlineData(10)]
    public void OtherUserSourcesAreRefused(int source)
    {
        using var world = new G1World();

        Assert.Equal("wiredfurni.error.invalid_user_source", Save(world, Name, users: [source]).Key);
    }

    [Fact]
    public void TwoUserSourcesAreRefusedAndNoneStoresTheDefault()
    {
        using var world = new G1World();

        Assert.Equal("wiredfurni.error.invalid_user_source_count", Save(world, Name, users: [100, 200]).Key);
        Assert.Equal([100], Save(world, Name, users: []).Users);
    }

    [Fact]
    public void NoneOneOrTwoPicksAndASecondaryPickAreStored()
    {
        using var world = new G1World();
        var first = world.AddItem("default", InteractionType.None, 1, 1).Id;
        var second = world.AddItem("default", InteractionType.None, 2, 1).Id;

        Assert.True(Save(world, Name, picks: []).Accepted);
        Assert.True(Save(world, Name, picks: [first]).Accepted);
        Assert.True(Save(world, Name, picks: [first, second]).Accepted);
        Assert.True(Save(world, Name, picks: [first], secondary: [second]).Accepted);
        Assert.True(Save(world, Name, furni: [200], picks: [first]).Accepted);
    }

    [Fact]
    public void ARowStoredBeforeKeepsLoading()
    {
        Assert.True(WiredBoxRegistry.TryGet(Name, out var descriptor));
        var stored = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            Text = "Robbie",
            PrimaryItems = [new WiredNativeItemReference(5, false)]
        };

        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, stored, out var runtime, storedRow: true));
        Assert.Equal("Robbie", runtime.Text);
        Assert.Equal([100, 100], runtime.IntParams.ToArray());
    }
}
