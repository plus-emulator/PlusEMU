using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Plus.HabboHotel.Items.DataFormat;
using Plus.HabboHotel.Items.Wired.Configuration;
using Plus.HabboHotel.Items.Wired.Runtime;

namespace Plus.HabboHotel.Items.Wired.Modern.Actions;

public static class WiredRoomForwarding
{
    public sealed record Destination(uint RoomId, uint TeleporterId = 0);
    public static WiredConfiguration Defaults() => new() { IntParams = [0, 100] };
    public static bool TryValidate(WiredConfiguration proposed, out WiredConfiguration validated, out string error)
    {
        validated = proposed;
        error = "Invalid room forwarding configuration.";

        if (!WiredLegacyProtocol.IsWithinLimits(proposed) || proposed.IntParams.Length != 2
            || proposed.IntParams[0] is not (0 or 10 or 11 or 200 or 201)
            || proposed.IntParams[1] is not (0 or 100 or 200 or 201)
            || proposed.Text.Length > 0 && !PositiveId(proposed.Text, out _)) {
            return false;
        }

        validated = proposed with
        {
            UserSources = ImmutableDictionary<string, int>.Empty.Add("users", proposed.IntParams[0]),
            FurniSources = ImmutableDictionary<string, int>.Empty.Add("links", proposed.IntParams[1])
        };
        error = "";

        return true;
    }

    public static Destination? Resolve(IEnumerable<Item> items, string roomText, Func<uint, uint> roomOfItem, Func<uint, uint> pairedItem, ILogger logger)
    {
        foreach (var item in items) {
            if (item.ExtraData is MapDataFormat map && map.Data.TryGetValue("internalLink", out var link) && PositiveId(link, out var linkedRoom)) {
                return new(linkedRoom);
            }

            var raw = item.ExtraData.Serialize();

            if (raw.TrimStart().StartsWith('{')) {
                try {
                    using var document = JsonDocument.Parse(raw);

                    if (document.RootElement.TryGetProperty("room_linker", out var section) && section.ValueKind == JsonValueKind.Object) {
                        if (section.TryGetProperty("RoomId", out var room) && room.ValueKind == JsonValueKind.Number && room.TryGetInt32(out var roomId) && roomId > 0) {
                            return new((uint)roomId);
                        }

                        if (section.TryGetProperty("ItemId", out var pair) && pair.ValueKind == JsonValueKind.Number && pair.TryGetInt32(out var pairId) && pairId > 0) {
                            var destination = roomOfItem((uint)pairId);

                            if (destination is > 0 and <= int.MaxValue) {
                                return new(destination, (uint)pairId);
                            }
                        }
                    }
                }
                catch (JsonException error) {
                    logger.LogWarning(error, "Invalid room_linker data on item {ItemId}", item.Id);
                }
            }

            // A teleporter or WIRED Room Linker leads to the room its other half stands in; an unplaced half leads nowhere.
            if (!InteractionTypes.IsLinkedPair(item.Definition.InteractionType) || item.IsTemporary || item.Id > int.MaxValue) {
                continue;
            }

            var paired = pairedItem(item.Id);

            if (paired == 0 || paired > int.MaxValue) {
                continue;
            }

            var destinationRoom = roomOfItem(paired);

            if (destinationRoom is > 0 and <= int.MaxValue) {
                return new(destinationRoom, paired);
            }
        }

        return PositiveId(roomText, out var fallback) ? new(fallback) : null;
    }

    private static readonly AsyncLocal<Action<Action>?> SwitchOverride = new();

    /// <summary>Runs the server-side room switch of a forward off the firing room's Wired pass.</summary>
    private static void SwitchRoom(Action action)
    {
        if (SwitchOverride.Value is { } switchRoom) {
            switchRoom(action);

            return;
        }

        Task.Run(() =>
        {
            try {
                action();
            }
            catch (Exception error) {
                Plus.Core.ExceptionLogger.LogException(error);
            }
        });
    }

    /// <summary>Replaces the room switch for the calling test flow.</summary>
    internal static IDisposable UseRoomSwitch(Action<Action> switchRoom)
    {
        SwitchOverride.Value = switchRoom;

        return new RoomSwitchScope();
    }

    private sealed class RoomSwitchScope : IDisposable
    {
        public void Dispose() => SwitchOverride.Value = null;
    }

    /// <summary>The glow a forwarded user wears from the firing until it is taken into the next room, and for a moment after.</summary>
    public const int TeleportEffectId = 234;

    /// <summary>How long after the firing the user is taken into the linked room.</summary>
    public const int ForwardDelayMilliseconds = 950;

    /// <summary>The arriving user loses the glow on this room tick after entering.</summary>
    public const int ArrivalEffectTicks = 3;

    // The firing puts effect 234 on the user at once. The user keeps walking, and about 950 ms later (not on a room tick) the room switch starts.
    // The user enters on the other linker's tile facing its rotation, still with effect 234, which is cleared on the linked room's third tick
    // after the entry. With the other linker picked up nothing happens.
    public static bool Execute(WiredRuntimeContext context, WiredConfiguration config, ILogger logger, IItemTravelStore travelStore)
    {
        var items = context.Targets.ResolveFurni(context, config.SelectedItems, config.FurniSources["links"]);
        var target = Resolve(items, config.Text, id => ItemTeleporterFinder.GetTeleRoomId(id, context.Room, travelStore), travelStore.FindLinkedTeleporter, logger);

        if (target == null || target.RoomId == context.Room.RoomId) {
            return false;
        }

        var room = context.Room;
        var wired = room.GetWired();
        var firstTick = wired.FirstRoomTickAfter(context.NowMilliseconds);
        var tick = WiredFlashStates.StateUpdateMilliseconds;
        var forwarded = false;

        foreach (var user in context.Targets.ResolveUsers(context, [], config.UserSources["users"]).Where(user => !user.IsBot)) {
            var client = user.GetClient();
            var habbo = client?.GetHabbo();

            if (client == null || habbo == null || !ReferenceEquals(habbo.CurrentRoom, room)) {
                continue;
            }

            var original = user.CurrentEffect == TeleportEffectId ? 0 : user.CurrentEffect;

            void Forward()
            {
                if (!user.IsAttachedTo(room) || !ReferenceEquals(habbo.CurrentRoom, room)) {
                    return;
                }

                habbo.IsTeleporting = target.TeleporterId != 0;
                habbo.TeleportingRoomId = target.TeleporterId == 0 ? 0 : target.RoomId;
                habbo.TeleporterId = target.TeleporterId;
                habbo.ForwardArrivalRestoreEffect = original;
                room.GetWired().RecordRoomNetworkForward(user, target.RoomId);
                // The glow goes along to the linked room: leaving must not put the old effect back.
                WiredTemporaryEffects.For(room).Drop(user);
                // No RoomForward and no OpenConnection: the server takes the user out of the room (YouArePlayingGame) and sends the linked room's data
                // starting with RoomReady. Run outside this room's Wired pass so two rooms forwarding into each other cannot hold each other's locks.
                SwitchRoom(() => habbo.PrepareRoom(target.RoomId, "", openConnection: false));
            }

            if (!wired.ScheduleAux(context, ForwardDelayMilliseconds, Forward)) {
                Forward();
                forwarded = true;

                continue;
            }

            // The glow is dropped on the tick an in-room teleport drops it; a user still in the room (the forward was not sent) gets
            // the previous effect back, one who left keeps the glow for the arrival.
            var restore = WiredTemporaryEffects.For(room).Acquire(user, () => user.CurrentEffect, user.ApplyEffect, () => user.IsAttachedTo(room),
                TeleportEffectId);

            if (!wired.ScheduleOnRoomTick(context, firstTick + 6 * tick, restore, restore, afterAvatarUpdates: true)) {
                restore();
            }

            forwarded = true;
        }

        return forwarded;
    }

    private static bool PositiveId(string text, out uint id) => uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id is > 0 and <= int.MaxValue;
}
