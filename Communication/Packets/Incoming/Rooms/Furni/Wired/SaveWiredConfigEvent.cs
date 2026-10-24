using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
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

            // A condition save with a quantifier outside 0..2 is answered and stored as 0.
            if (native.Quantifier is { } quantifier and not (0 or 1 or 2)) {
                native = native with { Quantifier = 0 };
            }

            // The limit of 20 ids, repeats counted, holds on every box, add-ons and variable boxes
            // included, and comes before their own refusals except the two source count ones.
            if (!WiredNativeEditorProjection.TryLimitPicks(native, out native)) {
                if (SourceCountRefusal(session, itemId, native) is { } key) {
                    session.Send(new WiredValidationErrorComposer(key));
                }

                return Task.CompletedTask;
            }

            if (WiredNativeEditorProjection.WithinBounds(visual == null ? native : WiredFurniVisualRules.WithoutNegativeDelay(native))) {
                service.Save(session, new(itemId, Envelope, WiredLegacyProtocol.WireDraft(native), native));
            }
        }

        return Task.CompletedTask;
    }

    private string? SourceCountRefusal(GameClient session, uint itemId, WiredNativeEditorConfiguration native)
    {
        if (session.GetHabbo()?.CurrentRoom is not { } room || !room.GetWired().Settings.CanModify(session)
            || !room.GetWired().TryGet(itemId, out var box) || box is not IWiredConfiguredItem configured
            || configured.Descriptor.Envelope != Envelope || !WiredNativeEditorProjection.Supports(configured.Descriptor.CanonicalName)) {
            return null;
        }

        var verdict = WiredSourceRules.Check(configured.Descriptor, native, WiredNativeEditorProjection.Metadata(configured.Descriptor.CanonicalName));

        return !verdict.Dropped && verdict.Key is WiredSourceRules.FurniSourceCountKey or WiredSourceRules.UserSourceCountKey ? verdict.Key : null;
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
