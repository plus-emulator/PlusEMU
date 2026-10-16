using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;
using Xunit;

namespace Plus.Tests;

public sealed partial class WiredRemainderG3Tests
{
    // Observed r93a (AIR WIN63-202610131318-860371703, runtime receipts line 330): an owned, tradeable pura_mdl5 as the Place Furni
    // template is refused with wiredfurni.error.furni_can_not_be_cloned. The other value categories are inferred (WiredPlaceTemplatePolicy).
    [Theory]
    [InlineData("trade", false)]
    [InlineData("market", false)]
    [InlineData("recycle", false)]
    [InlineData("rare", false)]
    [InlineData("limited", false)]
    [InlineData("exchange", false)]
    [InlineData("chest", false)]
    [InlineData("plain", true)]
    [InlineData("clone", true)]
    public void PlaceFurniTemplatesThatCanCarryValueCannotBeCloned(string kind, bool cloneable)
    {
        var (room, map, items) = ModernWiredRuntimeTests.WorldForG3();
        var template = ModernWiredRuntimeTests.MakeItemForG3(10, "pura_mdl5");
        template.Definition.AllowTrade = kind == "trade";
        template.Definition.AllowMarketplaceSell = kind == "market";
        template.Definition.AllowEcotronRecycle = kind == "recycle";
        template.Definition.IsRare = kind == "rare";
        template.Definition.InteractionType = kind switch
        {
            "exchange" => InteractionType.Exchange,
            "chest" => InteractionType.WiredChestCoins,
            _ => InteractionType.None
        };

        if (kind == "limited") {
            template.UniqueNumber = 3;
        }

        if (kind == "clone") {
            template = new Item { Id = 10, IsTemporary = true, Definition = template.Definition, ExtraData = template.ExtraData };
        }

        template.SetState(1, 1, 0, Gamemap.GetAffectedTiles(1, 1, 1, 1, 0));
        items[template.Id] = template;

        Assert.Equal(cloneable, WiredPlaceTemplatePolicy.CanClone(template));
        Assert.Equal(cloneable ? null : "wiredfurni.error.furni_can_not_be_cloned",
            WiredPlaceTemplatePolicy.RefusalKey(room, [WiredRoomOperations.Capture(template)]));
    }

    // A saved row whose template definition carries value never places a clone.
    [Fact]
    public void PlaceFurniNeverClonesAValueCarryingDefinitionFromASavedRow()
    {
        var (room, _, items) = ModernWiredRuntimeTests.WorldForG3();
        var definition = ModernWiredRuntimeTests.MakeItemForG3(5, "pura_mdl5").Definition;
        definition.Id = 5;
        definition.Stackable = true;
        definition.AllowTrade = true;
        var manager = new TestWiredDefinitions(() => new() { [5] = definition });
        var action = ModernWiredRuntimeTests.ActionBoxForG3(room, "wf_act_place_furni", manager);
        Assert.True(WiredBoxRegistry.TryGet("wf_act_place_furni", out var descriptor));
        var native = WiredNativeEditorProjection.DefaultNative(descriptor) with
        {
            SavedState = new() { Snapshots = [new(900, 5, 2, 1, 0, 0, "1")] }
        };
        Assert.True(WiredNativeEditorProjection.TryCompile(action.Item.Id, descriptor, native, out var compiled));
        Assert.True(WiredConfigurationSave.TrySave(action, compiled, TestWiredConfigurationStore.Instance, out var error), error);

        Assert.False(action.Execute(new WiredRuntimeContext(room, new(WiredEventKind.Use), new(() => items.Values.ToArray(), () => []), new G3Operations())));
        Assert.DoesNotContain(items.Values, item => item.IsTemporary);
    }
}
