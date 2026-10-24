using Plus.HabboHotel.Items.Wired.Runtime;

namespace Plus.HabboHotel.Items.Wired.Variables;

public sealed partial class WiredRoomVariables
{
    private readonly Dictionary<uint, WiredVariableTextInputBox> _textInputs = [];

    /// <summary>Engine pre-predicate hook: null uses the ordinary speech predicate; false rejects without publishing captures.</summary>
    public bool? CaptureSpeech(WiredRuntimeContext context, IWiredContextualTrigger trigger)
    {
        if (trigger.Descriptor.CanonicalName != "wf_trg_says_something" || !ReferenceEquals(context.Room, _room)) {
            return null;
        }

        var boxes = _textInputs.Values.Where(box => IsAttached(box.Item) && box.Item.GetX == trigger.Item.GetX && box.Item.GetY == trigger.Item.GetY)
            .OrderBy(box => box.Item.GetZ).ThenBy(box => box.Item.Id).ToArray();

        if (boxes.Length == 0) {
            return null;
        }

        var triggerConfig = context.ConfigurationOf(trigger);

        if (!trigger.TryValidateConfiguration(triggerConfig, out triggerConfig, out _) || context.Event.Kind != WiredEventKind.Speech || context.Event.Actor is null || triggerConfig.IntParams.Length != 3
            || triggerConfig.IntParams[2] == 1 && context.Event.Actor.HabboId != _room.OwnerId) {
            return false;
        }

        var capturers = new List<WiredVariableCapturer>();
        var capturing = new List<(Item Box, uint Definition)>();

        foreach (var box in boxes) {
            if (!box.TryValidateConfiguration(context.ConfigurationOf(box), out var config, out _)) {
                continue;
            }

            if (!box.TryCapture(config, Module, out var id)) {
                continue;
            }

            // Text mode without a Text Connector on the variable captures nothing, not even a number. Only number mode reads numbers.
            capturers.Add(new(id, config.Text, config.IntParams[0] == 2
                ? MetadataOn(id, "wf_xtra_var_text_connector")?.TextConnector ?? new Dictionary<long, string>() : null));
            capturing.Add((box.Item, id));
        }

        if (capturers.Count == 0) {
            return null;
        }

        if (!WiredVariableTextCapture.TryMatch(triggerConfig.Text, context.Event.Message, triggerConfig.IntParams[0], capturers, out var values)) {
            return false;
        }

        context.VariableFrame = WiredVariableRuntimeFrames.Create(context, context.VariableFrame);

        if (!Module.CaptureContextValues(values, context.VariableFrame)) {
            return false;
        }

        // A capture flashes the capturer, gives its variable (definition state 6, then 7) and flashes the add-ons on the definition's tile, with the say.
        foreach (var (box, definition) in capturing.Where(entry => values.ContainsKey(entry.Definition))) {
            _room.GetWired()?.OnEvent(box);
            FlashDefinition(definition, WiredFlashStates.DefinitionGiven);
        }

        return true;
    }
}
