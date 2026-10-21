using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;
using Plus.Communication.Packets.Outgoing;
using Plus.Core.FigureData;
using Plus.HabboHotel.Items.Wired;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Users.Clothing;
using Xunit;

namespace Plus.Tests;

// Editor saves answered the way Habbo answers them, as observed on AIR WIN63-202610131318-860371703 and AIR WIN63-202610151745-318700621.
public partial class ModernWiredRuntimeTests
{
    [Fact]
    public async Task BotClothesSavesAFigurePartWithAnEmptyColourInHabbosCanonicalForm()
    {
        var f = new MovementOracleSession("wf_act_bot_clothes");
        f.Client.GetHabbo().Gender = "M";
        f.Client.GetHabbo().Clothing = new ClothingComponent();
        var figures = new FigureDataManager(NullLogger<FigureDataManager>.Instance);
        figures.Init();

        await new SaveWiredEffectConfigEvent(new WiredConfigurationService(f.Store, figures, null!, TestLogging.For<WiredConfigurationService>()))
            .Parse(f.Client, Request(100, 0, "Robbie\thd-180-1.ch-210-66.lg-270-82.sh-290-80.hr-100-", 0, 0, 0, 1, 100, 0, 0));

        Assert.Contains(f.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        Assert.Equal("Robbie\thr-100.hd-180-1.ch-210-66.lg-270-82.sh-290-80", Assert.Single(f.Store.Saves).Text);
    }

    // Observed on AIR WIN63-202610201032-107964540: Bot Clothes saves an empty figure and reads it back as an empty figure.
    [Theory]
    [InlineData("Robbie\t")]
    [InlineData("\t")]
    public async Task BotClothesSavesAnEmptyFigure(string text)
    {
        var f = new MovementOracleSession("wf_act_bot_clothes");
        f.Client.GetHabbo().Gender = "M";
        f.Client.GetHabbo().Clothing = new ClothingComponent();
        var figures = new FigureDataManager(NullLogger<FigureDataManager>.Instance);
        figures.Init();

        await new SaveWiredEffectConfigEvent(new WiredConfigurationService(f.Store, figures, null!, TestLogging.For<WiredConfigurationService>()))
            .Parse(f.Client, Request(100, 0, text, 0, 0, 0, 1, 100, 0, 0));

        Assert.Contains(f.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        Assert.Equal(text, Assert.Single(f.Store.Saves).Text);
    }

    [Theory]
    [InlineData("wf_act_show_message", 11)]
    [InlineData("wf_act_furni_to_user", 10)]
    public async Task AHiddenUserSourceSavesWithoutItsTriggerInTheStack(string name, int source)
    {
        var f = new MovementOracleSession(name);
        object[] body = name == "wf_act_show_message"
            ? [100, 3, 0, 202, -1, "adv", 0, 0, 0, 1, source, 0, 0]
            : [100, 0, "", 1, 8, 0, 1, 100, 1, source, 0, 0];

        await SaveEffect(f, body);

        Assert.Contains(f.Client.Packets, packet => packet.Header == ServerPacketHeader.HideWiredConfigComposer);
        Assert.Equal([source], Assert.Single(f.Store.Saves).Origin!.Native.UserSourceTypes.ToArray());
    }

    [Fact]
    public async Task FreezeWithAnEffectOutsideZeroToFourIsRefusedWithTheFreezeEffectKey()
    {
        var f = new MovementOracleSession("wf_act_freeze");

        await SaveEffect(f, [100, 2, 5, 0, "", 0, 0, 0, 1, 0, 0, 0]);

        Assert.Empty(f.Store.Saves);
        var error = Assert.Single(f.Client.Packets, packet => packet.Header == ServerPacketHeader.WiredValidationErrorComposer).Body;
        var length = (error[0] << 8) | error[1];
        Assert.Equal("wiredfurni.error.invalid_freeze_effect", Encoding.UTF8.GetString(error, 2, length));
        Assert.Equal(new byte[4], error[(2 + length)..]);
    }

    [Fact]
    public async Task JoinTeamWithAnUnknownTeamIsDroppedWithoutAnyReply()
    {
        var f = new MovementOracleSession("wf_act_join_team");
        var before = f.Action.Configuration;

        await SaveEffect(f, [100, 2, 9, 0, "", 0, 0, 0, 1, 0, 0, 0]);

        Assert.Empty(f.Client.Packets);
        Assert.Empty(f.Store.Saves);
        Assert.Same(before, f.Action.Configuration);
    }

    [Theory]
    [InlineData("wf_act_bot_talk")]
    [InlineData("wf_act_bot_talk_to_avatar")]
    public void AFreshBotTalkFrameSendsAnEmptyText(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));

        Assert.Equal("", WiredNativeEditorProjection.DefaultRuntime(1, descriptor)!.Origin!.Native.Text);
    }

    // Rows saved before the change keep "\t" for an empty bot and message; they still load and read the same as a fresh box.
    [Theory]
    [InlineData("wf_act_bot_talk")]
    [InlineData("wf_act_bot_talk_to_avatar")]
    public void AStoredBotTalkRowWithOnlyATabStillLoads(string name)
    {
        Assert.True(WiredBoxRegistry.TryGet(name, out var descriptor));
        var stored = WiredNativeEditorProjection.DefaultNative(descriptor) with { Text = "\t" };

        Assert.True(WiredNativeEditorProjection.TryCompile(1, descriptor, stored, out var runtime, storedRow: true));
        Assert.Equal(WiredNativeEditorProjection.DefaultRuntime(1, descriptor)!.Text, runtime.Text);
    }

    private static Task SaveEffect(MovementOracleSession f, object[] body)
    {
        f.Client.Packets.Clear();

        // A filter that changes nothing, for Show Message's word-filter step.
        var filter = new ReplacingFilter("bobba", "bobba");

        return new SaveWiredEffectConfigEvent(new WiredConfigurationService(f.Store, null!, filter, TestLogging.For<WiredConfigurationService>()))
            .Parse(f.Client, Request(body));
    }
}
