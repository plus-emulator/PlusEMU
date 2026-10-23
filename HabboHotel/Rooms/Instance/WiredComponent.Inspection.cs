using Plus.HabboHotel.Items;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;

namespace Plus.HabboHotel.Rooms.Instance;

public partial class WiredComponent
{
    // Inspection captures attached entities without firing a stack or inventing trigger/event payloads.
    internal WiredVariableFrame CaptureVariableInspectionFrame() => WiredVariableRuntimeFrames.CaptureInspection(
        new WiredRuntimeContext(_room, new(WiredEventKind.Inspection), _targets, this)
        { NowMilliseconds = _engine.NowMilliseconds });

    /// <summary>
    /// The context entry of a user, furni or room variable box editor: the users or furni in the room holding its variable with their values, or the room variable's value. Null for every other box.
    /// </summary>
    public WiredVariableBoxContext? VariableBoxContext(IWiredConfiguredItem box)
    {
        if (box.Descriptor.CanonicalName is not ("wf_var_user" or "wf_var_furni" or "wf_var_room")
            || Variables.Module.DescribeDefinitions([box.Item.Id]) is not [var description]
            || !Variables.TryCaptureNativeCatalog(out var catalog)
            || catalog?.Variables.FirstOrDefault(variable => variable.Id == description.CatalogId) is not { } variable) {
            return null;
        }

        var target = description.Definition.Target;
        var reference = new WiredVariableReference(target, description.Definition.Token);
        var frame = CaptureVariableInspectionFrame();
        using var reads = Variables.Module.CaptureReads([reference], frame);

        if (target == WiredVariableTarget.Global) {
            return new(WiredVariableBoxContext.Room, variable, [], Wire(reads.Read(reference, new(target, 0, 0), frame)?.Value ?? 0));
        }

        var holders = frame.Holders.Where(holder => holder.Target == target)
            .Select(holder => (holder.EntityId, Value: reads.Read(reference, holder, frame)))
            .Where(read => read.Value is not null)
            .Select(read => KeyValuePair.Create(target == WiredVariableTarget.User ? read.EntityId : TemporaryItemIds.ToWiredEntityId(read.EntityId),
                Wire(read.Value!.Value)));

        return new(target == WiredVariableTarget.User ? WiredVariableBoxContext.User : WiredVariableBoxContext.Furni, variable, [.. holders], null);
    }

    private static int Wire(long value) => (int)Math.Clamp(value, int.MinValue, int.MaxValue);
}
