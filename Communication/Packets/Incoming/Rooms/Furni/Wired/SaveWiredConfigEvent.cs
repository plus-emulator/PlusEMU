using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items;

namespace Plus.Communication.Packets.Incoming.Rooms.Furni.Wired;

internal abstract class SaveWiredConfigEvent(IWiredConfigurationService service) : IPacketEvent
{
    protected abstract WiredBoxCategory Envelope { get; }

    public Task Parse(GameClient session, IIncomingPacket packet)
    {
        uint itemId;

        try {
            itemId = BuildersClubItemIds.FromWire(packet.ReadUInt());
        }
        catch (ArgumentException) {
            return Task.CompletedTask;
        }

        if (itemId is > 0 and <= int.MaxValue && WiredLegacyProtocol.TryReadNativeUnbounded(packet, Envelope, out var native)) {
            // Two boxes have their own bounds: the others drop a save that is out of bounds without a reply.
            var visual = VisualBoxName(session, itemId);

            // Observed on AIR WIN63-202610201032-107964540: a condition save with a quantifier outside 0..2 is answered and stored as 0.
            if (native.Quantifier is { } quantifier and not (0 or 1 or 2)) {
                native = native with { Quantifier = 0 };
            }

            if (visual != null) {
                native = WiredFurniVisualRules.ReadSave(visual, native);
            }

            if (WiredNativeEditorProjection.WithinBounds(visual == null ? native : WiredFurniVisualRules.WithoutNegativeDelay(native))) {
                service.Save(session, new(itemId, Envelope, WiredLegacyProtocol.WireDraft(native), native));
            }
        }

        return Task.CompletedTask;
    }

    private static string? VisualBoxName(GameClient? session, uint itemId) =>
        session?.GetHabbo()?.CurrentRoom?.GetWired()?.TryGet(itemId, out var box) == true && box is IWiredConfiguredItem configured
            && WiredFurniVisualRules.Applies(configured.Descriptor.CanonicalName) ? configured.Descriptor.CanonicalName : null;
}

internal sealed class SaveWiredSelectorConfigEvent(IWiredConfigurationService service) : SaveWiredConfigEvent(service)
{
    protected override WiredBoxCategory Envelope => WiredBoxCategory.Selector;
}

internal sealed class SaveWiredAddonConfigEvent(IWiredConfigurationService service) : SaveWiredConfigEvent(service)
{
    protected override WiredBoxCategory Envelope => WiredBoxCategory.Addon;
}

internal sealed class SaveWiredVariableConfigEvent(IWiredConfigurationService service) : SaveWiredConfigEvent(service)
{
    protected override WiredBoxCategory Envelope => WiredBoxCategory.Variable;
}
