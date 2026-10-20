using System.Collections.Immutable;
using System.Globalization;
using Plus.Communication.Packets;
using Plus.Communication.Packets.Outgoing.Rooms.Furni.Wired;
using Plus.HabboHotel.GameClients;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Links;
using Plus.HabboHotel.Items.Wired.Runtime;
using Plus.HabboHotel.Items.Wired.Variables;
using Plus.HabboHotel.Rooms;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

/// <summary>
/// The October effects of the r88 dossier (WIN63-202610131318-860371703). The client only shows their editors and, for transparency
/// and highlight, the incoming visual packets; every server rule here is inferred from those editors (WiredEvidenceRegistry AirSourceOnly).
/// Runtime int layouts are the ones WiredNativeAuxiliaryEditor.October compiles.
/// </summary>
public static class WiredOctoberActions
{
    public static readonly IReadOnlySet<string> Names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "wf_act_progress_ach", "wf_act_give_effect", "wf_act_override_height", "wf_act_progress_reward_track", "wf_act_reset_reward_track",
        "wf_act_set_visibility", "wf_act_set_highlight", "wf_act_send_message", "wf_act_send_dataframe"
    };

    private static int P(WiredConfiguration config, int index) => config.IntParams.Length > index ? config.IntParams[index] : 0;

    public static bool Execute(string name, WiredModernBox box, WiredRuntimeContext context, WiredConfiguration config,
        Func<string, Item[]> furni, Func<string, RoomUser[]> users) => name switch
        {
            "wf_act_progress_ach" => ProgressAchievement(context, config, users("users")),
            "wf_act_give_effect" => GiveEffect(config, users("users")),
            "wf_act_override_height" => OverrideHeight(context, config, furni("items")),
            "wf_act_progress_reward_track" => ProgressRewardTrack(context, config, users("users")),
            // No reward track service in PlusEMU can reset a player's track; the box saves and loads but changes nothing.
            "wf_act_reset_reward_track" => false,
            "wf_act_set_visibility" => SetVisibility(context, config, furni, users),
            "wf_act_set_highlight" => SetHighlight(context, config, furni, users),
            "wf_act_send_message" => SendMessage(box, context, config, furni("endpoints")),
            "wf_act_send_dataframe" => SendDataframe(box, context, config, furni),
            _ => throw new InvalidOperationException("Action has no executor.")
        };

    /// <summary>A literal, or the referenced variable's value read on the reference's own sources (value-or-variable option 1).</summary>
    private static long? Amount(WiredRuntimeContext context, WiredConfiguration config, int option, int literal, int target)
    {
        if (option == 0) {
            return literal;
        }

        if (config.VariableIds.Length == 0 || WiredVariableAbsent.Is(config.VariableIds[0])) {
            return null;
        }

        return ReadVariable(context, config, (WiredVariableTarget)target, config.VariableIds[0],
            config.UserSources.GetValueOrDefault("reference"), config.FurniSources.GetValueOrDefault("reference"));
    }

    private static long? ReadVariable(WiredRuntimeContext context, WiredConfiguration config, WiredVariableTarget target, string id,
        int userSource, int furniSource)
    {
        var module = context.Room.GetWired().Variables.Module;
        using var queries = new WiredVariableQueries(module, WiredVariableRuntimeFrames.Create(context, context.VariableFrame));

        return queries.ReadOperand(target, id, userSource, furniSource, config);
    }

    // ProgressAchievement.as: 1 adds the amount to the player's progress, 0 sets the progress to it. Only achievements an Achievement
    // Enabler of the room lists can progress, which is what the editor's achievementsInRoom dropdown offers.
    private static bool ProgressAchievement(WiredRuntimeContext context, WiredConfiguration config, RoomUser[] recipients)
    {
        var group = config.Text;

        if (!context.Room.GetWired().AchievementEnabled(group) || !context.Room.Achievements.Achievements.ContainsKey(group)
            || Amount(context, config, P(config, 1), P(config, 2), P(config, 3)) is not { } amount) {
            return false;
        }

        var changed = false;

        foreach (var user in recipients.Where(user => !user.IsBot)) {
            if (user.GetClient() is not { } client || client.GetHabbo() is not { } habbo) {
                continue;
            }

            var progress = P(config, 0) == 1 ? amount : amount - (habbo.GetAchievementData(group)?.Progress ?? 0);

            if (progress is > 0 and <= int.MaxValue) {
                changed |= context.Room.Achievements.ProgressAchievement(client, group, (int)progress);
            }
        }

        return changed;
    }

    // class_3931.as [effect id, priority, type]: type 0 puts the effect on, type 1 takes it off. Priority has no server meaning here.
    private static bool GiveEffect(WiredConfiguration config, RoomUser[] users)
    {
        foreach (var user in users) {
            user.ApplyEffect(P(config, 2) == 0 ? P(config, 0) : 0);
        }

        return users.Length > 0;
    }

    // class_4092.as [height in hundredths, type]: type 0 makes the furni stack at that height, type 1 restores its own height.
    private static bool OverrideHeight(WiredRuntimeContext context, WiredConfiguration config, Item[] items)
    {
        var changed = false;

        foreach (var item in items.Where(item => item.IsFloorItem)) {
            double? height = P(config, 1) == 0 ? P(config, 0) / 100d : null;

            if (item.WiredHeightOverride == height) {
                continue;
            }

            item.WiredHeightOverride = height;
            context.Room.GetGameMap().UpdateMapForItem(item);
            changed = true;
        }

        return changed;
    }

    // ProgressRewardTrack.as: the task id names the reward track action progressed; the score is the amount.
    private static bool ProgressRewardTrack(WiredRuntimeContext context, WiredConfiguration config, RoomUser[] recipients)
    {
        var parts = config.Text.Split('\t');

        if (parts.Length != 2 || parts[1].Length == 0 || Amount(context, config, P(config, 1), P(config, 2), P(config, 3)) is not { } amount
            || amount is <= 0 or > int.MaxValue) {
            return false;
        }

        var changed = false;

        foreach (var user in recipients.Where(user => !user.IsBot)) {
            if (user.GetClient() is { } client) {
                user.RewardTracks.Progress(client, parts[1], (int)amount);
                changed = true;
            }
        }

        return changed;
    }

    // Audience 1 is everyone in the room; otherwise the users of the audience source see it (class_4285.as onAudienceChanged).
    private static void Show(WiredRuntimeContext context, WiredConfiguration config, Func<string, RoomUser[]> users, IServerPacket packet)
    {
        if (P(config, 0) == 1) {
            context.Room.SendPacket(packet);

            return;
        }

        foreach (var client in users("audience").Select(user => user.GetClient()).OfType<GameClient>().Distinct()) {
            client.Send(packet);
        }
    }

    // class_4285.as [audience, visibility 0/100, click through, easing, target users]; the targets are furni or, with target users, users.
    private static bool SetVisibility(WiredRuntimeContext context, WiredConfiguration config, Func<string, Item[]> furni, Func<string, RoomUser[]> users)
    {
        var targetUsers = P(config, 4) == 1;
        var targets = Targets(config, furni, users, targetUsers);

        if (targets.Length == 0) {
            return false;
        }

        var (transparency, clickThrough, easing) = (P(config, 1), P(config, 2) == 1, P(config, 3));
        Show(context, config, users, targetUsers
            ? new WiredUserTransparencyComposer(targets, transparency, clickThrough, easing)
            : new WiredFurniTransparencyComposer(targets, transparency, clickThrough, easing));

        if (P(config, 0) == 1) {
            context.Room.GetWired().RememberTransparency(targetUsers, targets, transparency, clickThrough, easing);
        }

        return true;
    }

    private static int[] Targets(WiredConfiguration config, Func<string, Item[]> furni, Func<string, RoomUser[]> users, bool targetUsers) => targetUsers
        ? [.. users("targets").Select(user => user.VirtualId).Distinct()]
        : [.. furni("items").Where(item => item.IsFloorItem).Select(item => unchecked((int)item.Id)).Distinct()];

    // class_4214.as [audience, type, strength, colour, animation, easing, target users]; users get class_3603, furni class_2680.
    private static bool SetHighlight(WiredRuntimeContext context, WiredConfiguration config, Func<string, Item[]> furni, Func<string, RoomUser[]> users)
    {
        var targetUsers = P(config, 6) == 1;
        var targets = Targets(config, furni, users, targetUsers);

        if (targets.Length == 0) {
            return false;
        }

        // Darken carries no colour and Brighten is white (class_4214.as readIntParamsFromForm); Brighten always pulses.
        var type = P(config, 1);
        var color = type == 2 ? 0 : type == 4 ? 0xFFFFFF : P(config, 3);
        var animation = type == 4 ? 1 : P(config, 4);
        // Observed on AIR WIN63-202610201032-107964540: the packet carries the saved strength (Brighten On stays 38) and the saved
        // easing, also for a pulsing highlight.
        var strength = P(config, 2);
        var easing = P(config, 5);
        Show(context, config, users, targetUsers
            ? new WiredUserHighlightComposer(targets, type, strength, color, animation, easing)
            : new WiredFurniHighlightComposer(targets, type, strength, color, animation, easing));

        if (P(config, 0) == 1) {
            context.Room.GetWired().RememberHighlight(targetUsers, targets, type, strength, color, animation, easing);
        }

        return true;
    }

    private static int Hops(WiredRuntimeContext context) => context.Event.Link is { } received ? received.Hops + 1 : 0;

    // SendLinkMessage.as [source]: 0 forwards the text that started the stack (said, or a received link message), 1 the custom message.
    private static bool SendMessage(WiredModernBox box, WiredRuntimeContext context, WiredConfiguration config, Item[] endpoints)
    {
        var message = P(config, 0) == 1 ? context.Policy.FormatText(context, config.Text)
            : context.Event.Kind is WiredEventKind.Speech or WiredEventKind.LinkMessage ? context.Event.Message : "";

        if (message.Length == 0 || WiredLinkDataframeCodec.Utf8Length(message) > WiredLinkDataframeCodec.MaxLinkMessageBytes) {
            return false;
        }

        var payload = new WiredLinkPayload(context.Room.Id, box.Item.Id, message, [], Hops(context));

        return context.Room.GetWired().SendLink([.. endpoints, .. Speakers(context, false)], payload, WiredEventKind.LinkMessage);
    }

    // LinkDataframeSendPreset rows: mode 0 a number literal, 1 a variable's number, 2 a variable's text connector text, 3 a custom message.
    private static bool SendDataframe(WiredModernBox box, WiredRuntimeContext context, WiredConfiguration config, Func<string, Item[]> furni)
    {
        if (WiredLinkDataframeCodec.Decode(config.Text, P(config, 0)) is not { } keys) {
            return false;
        }

        var rows = ImmutableArray.CreateBuilder<WiredLinkRow>(keys.Length);

        for (var row = 0; row < keys.Length; row++) {
            var (mode, literal, target) = (P(config, 1 + row * 3), P(config, 2 + row * 3), P(config, 3 + row * 3));
            var key = WiredLinkDataframeCodec.NormalizeKey(keys[row].Key);
            var id = config.VariableIds.Length > row ? config.VariableIds[row] : WiredVariableAbsent.Id;
            long? number = mode switch
            {
                WiredLinkDataframeCodec.NumberLiteral => literal,
                WiredLinkDataframeCodec.NumberVariable or WiredLinkDataframeCodec.VariableText when !WiredVariableAbsent.Is(id) =>
                    ReadVariable(context, config, (WiredVariableTarget)target, id, config.UserSources.GetValueOrDefault("row" + row),
                        config.FurniSources.GetValueOrDefault("row" + row)),
                _ => null
            };
            var text = mode switch
            {
                WiredLinkDataframeCodec.CustomMessage => context.Policy.FormatText(context, keys[row].Text),
                WiredLinkDataframeCodec.VariableText when number is { } value => TextConnector(context, id, value),
                _ => number?.ToString(CultureInfo.InvariantCulture) ?? ""
            };
            rows.Add(new(key, text, mode is WiredLinkDataframeCodec.NumberLiteral or WiredLinkDataframeCodec.NumberVariable ? number : null));
        }

        var payload = new WiredLinkPayload(context.Room.Id, box.Item.Id, "", rows.MoveToImmutable(), Hops(context));

        return context.Room.GetWired().SendLink([.. furni("endpoints"), .. Speakers(context, true)], payload, WiredEventKind.LinkDataframe);
    }

    // A variable's text connector names its values; a value without a name sends the number.
    private static string TextConnector(WiredRuntimeContext context, string id, long value) =>
        WiredVariableDescription.TryParseCatalogId(id, out _, out var token) && WiredVariableModule.TryDefinitionId(token, out var definition)
        && context.Room.GetWired().Variables.Catalog().Variables.FirstOrDefault(description => description.Definition.ItemId == definition) is { } described
        && value is >= int.MinValue and <= int.MaxValue && described.TextConnector.TryGetValue((int)value, out var name)
            ? name : value.ToString(CultureInfo.InvariantCulture);

    // Link Speaker add-ons of the stack (class_4123.as [message, dataframe]) add their picked WIRED Link furni as endpoints.
    private static IEnumerable<Item> Speakers(WiredRuntimeContext context, bool dataframe) =>
        context.Policy.Addons.LinkSpeakers.Where(speaker => dataframe ? speaker.Dataframe : speaker.Message).SelectMany(speaker => speaker.Endpoints);
}
