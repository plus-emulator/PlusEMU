using Plus.Communication.Packets.Outgoing;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Xunit;

namespace Plus.Tests;

// Set Furni Highlight sends the saved values unchanged.
public partial class ModernWiredRuntimeTests
{
    [Theory]
    // Brighten is sent at the saved 38, not doubled.
    [InlineData(4, 38, 0xFFFFFF, 1, 0)]
    // A pulsing highlight keeps its saved easing.
    [InlineData(1, 100, 0xFFB700, 1, 2)]
    [InlineData(3, 50, 0xFF3030, 1, 4)]
    public void SetFurniHighlightSendsTheSavedStrengthAndEasing(int type, int strength, int color, int animation, int easing)
    {
        using var f = new TeleportFixture();
        Assert.True(f.Engine.Remove(100));
        OctoberBox<WiredModernAction>(f, 110, "wf_act_set_highlight", 0, 0, native => native with
        {
            OwnedIntParams = [0, type, strength, color, animation, easing, 0],
            PrimaryItems = [new(1, false)],
            FurniSourceTypes = [100],
            UserSourceTypes = [0, 0]
        });
        var sent = Capture(f.Client);

        f.Fire(land: false);

        var packet = Reply(sent, ServerPacketHeader.WiredFurniHighlightComposer);
        Assert.Equal((1, 1, type, strength, color, animation, easing),
            (packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int(), packet.Int()));
        packet.End();
    }
}
