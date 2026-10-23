using System.Collections.Concurrent;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Variables;

/// <summary>Concrete scalar action/condition adapter; refresh live holders while retaining the firing's context values.</summary>
public sealed class WiredVariableConfiguredBox : IWiredContextualItem
{
    private readonly WiredVariableExecutors _executors;
    public WiredVariableConfiguredBox(Room room, Item item, WiredBoxDescriptor descriptor, WiredVariableExecutors executors)
    {
        if (!WiredVariableExecutors.Supports(descriptor.CanonicalName)) {
            throw new ArgumentException("No scalar executor for this box.", nameof(descriptor));
        }

        Instance = room;
        Item = item;
        _executors = executors;
        Descriptor = descriptor with { Support = WiredBoxSupport.Implemented };
        Configuration = WiredNativeEditorProjection.DefaultRuntime(item.Id, descriptor) ?? new();
    }
    public Room Instance { get; set; }
    public Item Item { get; set; }
    public WiredBoxType Type => Item.Definition.WiredType;
    public ConcurrentDictionary<uint, Item> SetItems { get; set; } = new();
    public string StringData { get; set; } = "";
    public bool BoolData { get; set; }
    public string ItemsData { get; set; } = "";
    public WiredBoxDescriptor Descriptor { get; }
    public WiredConfiguration Configuration { get; private set; } = new();
    public bool HasPersistedConfiguration { get; private set; }
    public bool TryValidateConfiguration(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        validated = proposed;

        if (WiredNativeEditorProjection.Supports(Descriptor.CanonicalName)
            && (!WiredNativeEditorProjection.IsBound(Item.Id, Descriptor, proposed)
                || !WiredNativeEditorProjection.TryValidateRuntime(Item, Descriptor, proposed, out validated, out error))) {
            error = "Invalid native variable authority.";

            return false;
        }

        return WiredVariableExecutors.TryValidate(Descriptor.CanonicalName, proposed, out error);
    }
    public void ApplyConfiguration(WiredConfiguration validated)
    {
        Configuration = validated;
        HasPersistedConfiguration = true;
        StringData = validated.Text;
        ItemsData = string.Join(';', validated.SelectedItems);
    }
    public bool Execute(params object[] arguments) => arguments.OfType<WiredVariableFrame>().FirstOrDefault() is { } frame
        && _executors.Execute(Descriptor.CanonicalName, Configuration, frame);
    public bool Execute(WiredRuntimeContext context)
    {
        if (!ReferenceEquals(context.Room, Instance)) {
            return false;
        }

        context.VariableFrame = WiredVariableRuntimeFrames.Create(context, context.VariableFrame);
        var configuration = context.ConfigurationOf(this);

        var variables = Instance.GetWired()?.Variables;
        var changesBefore = variables?.Module.ValueChanges ?? 0;
        var decreasesBefore = variables?.Module.ValueDecreases ?? 0;

        if (!_executors.Execute(Descriptor.CanonicalName, configuration, context.VariableFrame)) {
            return false;
        }

        var changes = (variables?.Module.ValueChanges ?? 0) - changesBefore;
        var lowered = changes > 0 && (variables?.Module.ValueDecreases ?? 0) - decreasesBefore == changes;

        // A Give Variable or Change Variable Value that writes a variable flashes that variable's definition box, and the variable add-ons on its tile, with the action's own flash.
        // A delayed write through a pointer flashes the definition of the variable the pointer points at, like the same write on that variable.
        // The pointer's own box never flashes, and without a delay the write is ignored.
        if (_executors.Destination(Descriptor.CanonicalName, configuration) is { } named && variables != null
            && (configuration.Delay > 0 ? variables.Module.Unpoint(named, context.VariableFrame) : named) is { } destination
            && !variables.Module.IsPointer(destination)) {
            // Runs of one Variable Changed stack in the same tick (two Gives in one click) flip the context variable they each give once in that tick's update, so those gives count as one firing here.
            var firing = Descriptor.CanonicalName == "wf_act_give_var" && destination.Target == WiredVariableTarget.Context
                && context.Trigger is WiredVariableChangedTrigger trigger ? trigger : context.Firing ?? context;

            // A batched Change Variable Value is written when its stack's batch flushes, so the kind is picked then, in the same pass.
            if (Descriptor.CanonicalName == "wf_act_change_var_val" && context.VariableFrame.VariableChanges is { } batch && variables != null) {
                batch.AfterFlush(variables.Module, destination, (moved, down) => variables.FlashDefinition(destination, DefinitionKind(moved, down), firing));
            }
            else {
                Instance.GetWired()?.Variables.FlashDefinition(destination, DefinitionKind(changes > 0, lowered), firing);
            }
        }

        return true;
    }

    // A change that leaves the value as it was only flips the definition between 0 and 1; one that lowers the value shows 4/5 instead of 2/3.
    private int DefinitionKind(bool valueChanged, bool lowered) => Descriptor.CanonicalName switch
    {
        "wf_act_give_var" => WiredFlashStates.DefinitionGiven,
        "wf_act_remove_var" => WiredFlashStates.DefinitionRemoved,
        _ when !valueChanged => WiredFlashStates.DefinitionUnchanged,
        _ when lowered => WiredFlashStates.DefinitionDecreased,
        _ => WiredFlashStates.DefinitionChanged
    };
    public void HandleSave(IIncomingPacket packet) => throw new InvalidOperationException("Configured boxes must use validated configuration persistence.");
}
