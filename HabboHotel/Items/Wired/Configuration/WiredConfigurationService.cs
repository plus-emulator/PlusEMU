using System.Collections.Immutable;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.Core.FigureData;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Modern;
using Plus.HabboHotel.Items.Wired.Modern.Actions;
using Plus.HabboHotel.Permissions;
using Plus.HabboHotel.Rooms.Chat.Filter;
using Plus.HabboHotel.Subscriptions;

namespace Plus.HabboHotel.Items.Wired.Configuration;

public sealed record WiredConfigurationSaveRequest(uint ItemId, WiredBoxCategory Envelope, WiredConfiguration Configuration,
    WiredNativeEditorConfiguration? Native = null);

public interface IWiredConfigurationService
{
    void Save(GameClient session, WiredConfigurationSaveRequest request);
}

public sealed class WiredConfigurationService(
    IWiredConfigurationStore store,
    IFigureDataManager figures,
    IWordFilterManager wordFilter,
    ILogger<WiredConfigurationService> logger) : IWiredConfigurationService
{
    internal const string WiredVariableOwnerOnly = "Only the room owner can set up a reference to another room.";

    public void Save(GameClient session, WiredConfigurationSaveRequest request)
    {
        var habbo = session.GetHabbo();

        if (!habbo.InRoom || habbo.CurrentRoom is not { } room || !room.GetWired().Settings.CanModify(session)) {
            return;
        }

        try {
            var selectedItem = room.GetRoomItemHandler().GetItem(request.ItemId);

            if (selectedItem is { IsTemporary: false } && Plus.HabboHotel.Items.Wired.Chests.WiredChestFurniture.IsContract(selectedItem.Definition)) {
                if (request.Native != null) {
                    session.Send(new WiredValidationErrorComposer("This contract has no supported native editor conversion."));

                    return;
                }

                var saved = false;
                room.GetWired().WithChests(module => saved = request.Envelope == WiredBoxCategory.Action
                    && Plus.HabboHotel.Items.Wired.Chests.WiredChestContractEditor.TrySave(module, selectedItem, request.Configuration));
                session.Send(saved ? new HideWiredConfigComposer() : new WiredValidationErrorComposer("Invalid contract requirements."));

                return;
            }

            if (selectedItem == null || selectedItem.IsTemporary || !room.GetWired().TryGet(request.ItemId, out var box)) {
                return;
            }

            if (box is not IWiredConfiguredItem configured || request.Native is not { } requested
                || !WiredNativeEditorProjection.Supports(configured.Descriptor.CanonicalName)) {
                session.Send(new WiredValidationErrorComposer("This box has no supported native editor conversion."));

                return;
            }

            var descriptor = configured.Descriptor;

            if (descriptor.Envelope != request.Envelope) {
                session.Send(new WiredValidationErrorComposer("The save packet does not match this Wired box."));

                return;
            }

            if (descriptor.CanonicalName == "wf_act_give_reward" && !habbo.Access.Can(PermissionKeys.ModerationTool)) {
                session.Send(new WiredValidationErrorComposer("You do not have permission to configure Wired rewards."));

                return;
            }

            // A reference reads and writes variables of the owner's other rooms, so only the owner sets one up.
            if (descriptor.CanonicalName == "wf_var_reference" && room.OwnerId != habbo.Id) {
                session.Send(new WiredValidationErrorComposer(WiredVariableOwnerOnly));

                return;
            }

            bool CanModify() => habbo.CurrentRoom == room && room.GetWired().Settings.CanModify(session);
            var native = requested with { NativeCode = descriptor.EditorCode };

            if (descriptor.CanonicalName == "wf_act_bot_clothes") {
                native = native with { Text = ValidateBotFigure(native.Text, session) };
            }

            // The reward pop-up text of the two give-from-chest boxes is word-filtered once at save, like Show Message.
            if (descriptor.CanonicalName is "wf_act_show_message" or "wf_act_give_currency" or "wf_act_give_furni") {
                native = native with { Text = FilterMessageText(native.Text) };
            }

            // The Web API add-on saves only keys this server made for it (VariablesWebApiAddon.as:108).
            if (descriptor.CanonicalName == "wf_xtra_var_web_api" && !room.GetWired().WebApiKeysAccepted(selectedItem.Id, native.Text)) {
                session.Send(new WiredValidationErrorComposer("These Web API keys were not made for this add-on."));

                return;
            }

            var current = configured.Configuration;

            if (!WiredNativeEditorProjection.TryProject(selectedItem, descriptor, current, out var projected)) {
                session.Send(new WiredValidationErrorComposer("Unable to project the saved settings."));

                return;
            }

            var capturesSnapshots = SnapshotNames.Contains(descriptor.CanonicalName);

            native = native with
            {
                SavedState = capturesSnapshots
                    ? new() { Snapshots = CaptureSnapshots(room, descriptor.CanonicalName, native) }
                    : projected.SavedState
            };

            var error = "Invalid native Wired settings.";

            // A pick of a temporary copy is refused with the official key, in every box including Place Furni.
            if (native.PrimaryItems.Concat(native.SecondaryItems)
                .Any(pick => room.GetRoomItemHandler().GetItem(pick.ItemId)?.IsTemporary == true)) {
                session.Send(new WiredValidationErrorComposer(WiredConfigurationSave.TemporaryFurniNotAllowedKey));

                return;
            }

            // Place Furni refuses a template that cannot be cloned (observed r93a; see WiredPlaceTemplatePolicy).
            if (descriptor.CanonicalName == "wf_act_place_furni"
                && Modern.Actions.WiredPlaceTemplatePolicy.RefusalKey(room, native.SavedState.Snapshots) is { } notCloneable) {
                session.Send(new WiredValidationErrorComposer(notCloneable));

                return;
            }

            var sources = WiredSourceRules.Check(descriptor, native, WiredNativeEditorProjection.Metadata(descriptor.CanonicalName));

            if (sources.Dropped) {
                return;
            }

            if (sources.Key is { } sourceKey) {
                session.Send(new WiredValidationErrorComposer(sourceKey));

                return;
            }

            if (!WiredNativeEditorProjection.TryFillSourceSlots(descriptor, native, out native, out var countError)) {
                session.Send(new WiredValidationErrorComposer(countError ?? error));

                return;
            }

            if (WiredNativeEditorProjection.DroppedWithoutReply(descriptor, native)) {
                return;
            }

            if (WiredNativeEditorProjection.SaveRefusal(descriptor, native) is { } refusal) {
                session.Send(new WiredValidationErrorComposer(refusal));

                return;
            }

            // Sources 10 and 11 save in any stack, even when its trigger does not offer them. Show Message with 11 saves without a
            // Click User trigger (observed on AIR WIN63-202610151745-318700621), and Furni To User with 10 saves without a Bot Reaches
            // User trigger (observed on AIR WIN63-202610131318-860371703).
            var compiled = WiredNativeEditorProjection.TryCompile(selectedItem.Id, descriptor, native, out var runtime,
                WiredNativeEditorProjection.AnyTriggerContext);

            if (!compiled) {
                error = WiredNativeEditorProjection.SourceRejectionKey(descriptor, native) ?? error;
            }

            if (compiled && WiredFurniVisualRules.PickRefusal(descriptor.CanonicalName, runtime,
                id => room.GetRoomItemHandler().GetItem(id)?.IsBuildersClub == true) is { } pickRefusal) {
                session.Send(new WiredValidationErrorComposer(pickRefusal));

                return;
            }

            if (!compiled
                || !WiredConfigurationSave.TrySave(configured, runtime, store, out error,
                    id => room.GetRoomItemHandler().GetItem(id) != null,
                    (original, validated, persist) => room.GetWired().PublishConfigured(original, validated, () =>
                    {
                        if (!CanModify() || !ReferenceEquals(current, original.Configuration)) {
                            throw new InvalidOperationException("The editor admission is no longer current.");
                        }

                        persist();
                    }), isTemporaryInRoom: id => room.GetRoomItemHandler().GetItem(id)?.IsTemporary == true)) {
                session.Send(new WiredValidationErrorComposer(error));

                return;
            }

            session.Send(new HideWiredConfigComposer());
        }
        catch (Exception error) when (error is ArgumentException or IOException or OverflowException
            or InvalidOperationException or FormatException or DbException or JsonException) {
            logger.LogWarning(error, "Failed to save Wired settings in room {RoomId}", room.Id);
            session.Send(new WiredValidationErrorComposer("Unable to save these Wired settings."));
        }
    }

    // Observed on hhus: the filtered Show Message text is what is stored and returned on reopen, and the save still succeeds.
    // The official rule set is unobserved; this applies only the hotel word filter that PlusEMU already has.
    private string FilterMessageText(string text)
    {
        var filtered = wordFilter.CheckMessage(text);

        return filtered == text.TrimEnd(' ') ? text : filtered;
    }

    private static readonly HashSet<string> SnapshotNames = ["wf_act_match_to_sshot", "wf_cnd_match_snapshot",
        "wf_cnd_not_match_snap", "wf_trg_stuff_state", "wf_trg_state_changed", "wf_act_place_furni"];

    private static ImmutableArray<WiredFurniSnapshot> CaptureSnapshots(Rooms.Room room, string name, WiredNativeEditorConfiguration native)
    {
        // Place templates come from a pick list only: the picked furni (100, or 110 the snapshot the editor opens on) or the second
        // pick list (101). A live or selector source keeps no templates.
        if (name == "wf_act_place_furni" && native.FurniSourceTypes is not [100 or 101 or 110, ..]) {
            return [];
        }

        var handler = room.GetRoomItemHandler();
        var picks = name == "wf_act_place_furni" && native.FurniSourceTypes is [101, ..] ? native.SecondaryItems : native.PrimaryItems;

        var allowWall = WiredNativeEditorProjection.Metadata(name).AllowWall;

        // Only cards whose editor admits wall picks capture them, and only with a parseable wall position.
        return picks.Select(reference => reference.ItemId).Distinct().Select(handler.GetItem)
            .Where(item => item is { IsFloorItem: true } || allowWall && item is { IsWallItem: true })
            .Select(item => (Item: item!, Snapshot: WiredRoomOperations.Capture(item!)))
            .Where(pair => !pair.Item.IsWallItem || pair.Snapshot.Wall != null).Select(pair => pair.Snapshot).ToImmutableArray();
    }

    private string ValidateBotFigure(string data, GameClient session)
    {
        var fields = data.Split('\t', 2);

        if (fields.Length == 2) {
            fields[1] = fields[1].TrimEnd('.');
        }

        // Observed on AIR WIN63-202610201032-107964540: an empty figure is saved and read back as an empty figure.
        if (fields.Length == 2 && fields[1].Length == 0) {
            return data;
        }

        if (fields.Length != 2 || !WiredBotActions.FigureWellFormed(fields[1])) {
            throw new ArgumentException("Invalid bot figure.");
        }

        var habbo = session.GetHabbo();

        // Paid clothing is only checked against a loaded wardrobe, so the figure is rejected without one.
        if (habbo.Clothing is not { } wardrobe) {
            throw new ArgumentException("Bot figure cannot be validated without a loaded wardrobe.");
        }

        var validated = figures.ProcessFigureCanonical(fields[1], habbo.Gender, wardrobe.GetClothingParts, ClubAccess.LevelFor(habbo.Access));

        return fields[0] + "\t" + validated.TrimEnd('.');
    }
}
